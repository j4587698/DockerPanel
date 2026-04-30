using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Docker.DotNet;
using Docker.DotNet.Models;
using DockerPanel.API.Models;
using System.Collections.Concurrent;
using System.Text;

namespace DockerPanel.API.Hubs;

/// <summary>
/// 容器终端 Hub - 通过 SignalR 提供容器终端访问
/// </summary>
[Authorize(Roles = AuthRoles.AdminOrOperator)]
public class ContainerTerminalHub : Hub
{
    private readonly ILogger<ContainerTerminalHub> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<ContainerTerminalHub> _hubContext;
    private static readonly ConcurrentDictionary<string, TerminalSession> _sessions = new();

    public ContainerTerminalHub(
        ILogger<ContainerTerminalHub> logger,
        IServiceProvider serviceProvider,
        IHubContext<ContainerTerminalHub> hubContext)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
    }

    /// <summary>
    /// 连接到容器终端
    /// </summary>
    public async Task Connect(TerminalConnectRequest request)
    {
        var connectionId = Context.ConnectionId;
        _logger.LogInformation("容器终端连接请求: {ConnectionId} -> 容器 {ContainerId}", connectionId, request.ContainerId);

        try
        {
            // 获取 Docker 客户端
            using var scope = _serviceProvider.CreateScope();
            var dockerEngine = scope.ServiceProvider.GetService<Services.DockerEngine>();
            
            if (dockerEngine == null)
            {
                await Clients.Caller.SendAsync("Error", new { code = "container.docker.unavailable", message = "Docker engine unavailable" });
                return;
            }

            var client = await dockerEngine.GetClientAsync();

            // 确定要使用的 shell
            var shell = await DetermineShellAsync(client, request.ContainerId, request.Shell);
            _logger.LogInformation("使用 Shell: {Shell}", shell);

            // 创建 exec 实例
            var execCreateParameters = new ContainerExecCreateParameters
            {
                AttachStdin = true,
                AttachStdout = true,
                AttachStderr = true,
                TTY = true,
                Cmd = new List<string> { shell },
                Env = new List<string> { "TERM=xterm-256color" }
            };

            var execCreateResponse = await client.Exec.CreateContainerExecAsync(request.ContainerId, execCreateParameters);
            
            if (execCreateResponse == null || string.IsNullOrEmpty(execCreateResponse.ID))
            {
                await Clients.Caller.SendAsync("Error", new { code = "container.exec.create_failed", message = "Failed to create exec instance" });
                return;
            }

            var execId = execCreateResponse.ID;
            _logger.LogInformation("创建 Exec 实例: {ExecId}", execId);

            // 启动 exec 并获取流
            var cts = new CancellationTokenSource();
            
            // 使用 StartContainerExecAsync 启动 exec
            // 注意：TTY 模式下流不是多路复用的，tty 参数必须与 ExecCreateParameters.Tty 一致
            var stream = await client.Exec.StartContainerExecAsync(execId, new ContainerExecStartParameters
            {
                TTY = true,
                Detach = false
            });

            // exec 启动后通过 Docker API 设置正确的终端尺寸
            try
            {
                await client.Exec.ResizeExecTtyAsync(execId, new ContainerResizeParameters
                {
                    Height = request.Rows,
                    Width = request.Cols
                }, CancellationToken.None);
                _logger.LogInformation("初始终端尺寸设置: {Cols}x{Rows}", request.Cols, request.Rows);
            }
            catch (Exception resizeEx)
            {
                _logger.LogWarning(resizeEx, "设置初始终端尺寸失败，将使用默认 80x24");
            }
            
            var session = new TerminalSession
            {
                DockerClient = client,
                Stream = stream,
                ExecId = execId,
                ContainerId = request.ContainerId,
                Shell = shell,
                ConnectionId = connectionId,
                ConnectedAt = DateTime.UtcNow,
                CancellationTokenSource = cts
            };

            _sessions[connectionId] = session;

            // 启动读取输出的任务
            _ = Task.Run(async () => await ReadTerminalOutput(connectionId, stream, cts.Token));

            await Clients.Caller.SendAsync("Connected", new
            {
                ContainerId = request.ContainerId,
                Shell = shell,
                ConnectedAt = DateTime.UtcNow
            });

            _logger.LogInformation("容器终端已连接: {ConnectionId} -> 容器 {ContainerId}, Shell: {Shell}", 
                connectionId, request.ContainerId, shell);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "容器终端连接失败: {ConnectionId}", connectionId);
            await Clients.Caller.SendAsync("Error", new { code = "terminal.connect.failed", message = ex.Message });
        }
    }

    /// <summary>
    /// 发送输入到终端
    /// </summary>
    public async Task SendInput(string input)
    {
        var connectionId = Context.ConnectionId;

        if (_sessions.TryGetValue(connectionId, out var session))
        {
            try
            {
                if (session.Stream != null && !string.IsNullOrEmpty(input))
                {
                    var payload = Encoding.UTF8.GetBytes(input);
                    await session.Stream.WriteAsync(payload, 0, payload.Length, session.CancellationTokenSource.Token);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送输入失败: {ConnectionId}", connectionId);
                await Clients.Caller.SendAsync("Error", new { code = "terminal.send.failed", message = ex.Message });
            }
        }
        else
        {
            await Clients.Caller.SendAsync("Error", new { code = "terminal.session.not_found", message = "Session not found or disconnected" });
        }
    }

    /// <summary>
    /// 调整终端大小
    /// </summary>
    public async Task Resize(int cols, int rows)
    {
        var connectionId = Context.ConnectionId;
        _logger.LogDebug("终端 resize 请求: {Cols}x{Rows}", cols, rows);

        if (_sessions.TryGetValue(connectionId, out var session))
        {
            try
            {
                if (!string.IsNullOrEmpty(session.ExecId) && session.DockerClient != null)
                {
                    await session.DockerClient.Exec.ResizeExecTtyAsync(
                        session.ExecId,
                        new ContainerResizeParameters
                        {
                            Height = rows,
                            Width = cols
                        },
                        session.CancellationTokenSource.Token);
                    _logger.LogDebug("终端 resize 成功: {Cols}x{Rows}", cols, rows);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "调整终端大小失败: {ConnectionId}", connectionId);
            }
        }
    }

    /// <summary>
    /// 断开终端连接
    /// </summary>
    public async Task Disconnect()
    {
        var connectionId = Context.ConnectionId;
        await CleanupSession(connectionId);
        await Clients.Caller.SendAsync("Disconnected");
    }

    /// <summary>
    /// 客户端断开时清理
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await CleanupSession(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// 确定要使用的 shell
    /// </summary>
    private async Task<string> DetermineShellAsync(DockerClient client, string containerId, string? preferredShell)
    {
        if (!string.IsNullOrEmpty(preferredShell))
        {
            return preferredShell;
        }

        var shells = new[] { "/bin/bash", "/bin/sh", "/bin/ash" };
        
        foreach (var shell in shells)
        {
            try
            {
                var execParams = new ContainerExecCreateParameters
                {
                    Cmd = new List<string> { "test", "-x", shell },
                    AttachStdout = false,
                    AttachStderr = false
                };
                
                var exec = await client.Exec.CreateContainerExecAsync(containerId, execParams);
                await client.Exec.StartContainerExecAsync(exec.ID, new ContainerExecStartParameters { Detach = true });
                return shell;
            }
            catch
            {
                // 继续尝试下一个
            }
        }

        return "/bin/sh";
    }

    /// <summary>
    /// 读取终端输出并推送到客户端
    /// </summary>
    private async Task ReadTerminalOutput(string connectionId, MultiplexedStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];

        try
        {
            _logger.LogInformation("开始读取终端输出: {ConnectionId}", connectionId);
            
            while (!cancellationToken.IsCancellationRequested && _sessions.ContainsKey(connectionId))
            {
                // TTY 模式下 ReadOutputAsync 直接读取原始数据（无多路复用帧头）
                var result = await stream.ReadOutputAsync(buffer, 0, buffer.Length, cancellationToken);
                
                if (result.Count > 0)
                {
                    var output = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    
                    if (_sessions.ContainsKey(connectionId))
                    {
                        await _hubContext.Clients.Client(connectionId).SendAsync("Output", output, cancellationToken);
                    }
                }
                else if (result.EOF)
                {
                    _logger.LogInformation("终端输出流结束: {ConnectionId}", connectionId);
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("终端读取被取消: {ConnectionId}", connectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取终端输出失败: {ConnectionId}", connectionId);
        }
        finally
        {
            if (_sessions.ContainsKey(connectionId))
            {
                try
                {
                    await _hubContext.Clients.Client(connectionId).SendAsync("Disconnected", CancellationToken.None);
                }
                catch { }
            }
        }
    }

    /// <summary>
    /// 清理会话
    /// </summary>
    private async Task CleanupSession(string connectionId)
    {
        if (_sessions.TryRemove(connectionId, out var session))
        {
            try
            {
                session.CancellationTokenSource?.Cancel();
                session.Stream?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理终端会话失败: {ConnectionId}", connectionId);
            }

            _logger.LogInformation("终端会话已清理: {ConnectionId}", connectionId);
        }

        await Task.CompletedTask;
    }

    public class TerminalConnectRequest
    {
        public string ContainerId { get; set; } = "";
        public string? Shell { get; set; }
        public int Cols { get; set; } = 80;
        public int Rows { get; set; } = 24;
    }

    private class TerminalSession
    {
        public DockerClient? DockerClient { get; set; }
        public MultiplexedStream? Stream { get; set; }
        public string ExecId { get; set; } = "";
        public string ContainerId { get; set; } = "";
        public string Shell { get; set; } = "";
        public string ConnectionId { get; set; } = "";
        public DateTime ConnectedAt { get; set; }
        public CancellationTokenSource CancellationTokenSource { get; set; } = new();
    }
}
