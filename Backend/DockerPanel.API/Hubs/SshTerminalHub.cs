using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using DockerPanel.API.Models;
using Renci.SshNet;
using System.Collections.Concurrent;
using System.Text;

namespace DockerPanel.API.Hubs;

/// <summary>
/// SSH 终端 Hub - 使用 SignalR 进行实时 SSH 通信
/// </summary>
[Authorize(Roles = AuthRoles.Admin)]
public class SshTerminalHub : Hub
{
    private readonly ILogger<SshTerminalHub> _logger;
    private readonly IHubContext<SshTerminalHub> _hubContext;
    private static readonly ConcurrentDictionary<string, SshSession> _sessions = new();

    public SshTerminalHub(ILogger<SshTerminalHub> logger, IHubContext<SshTerminalHub> hubContext)
    {
        _logger = logger;
        _hubContext = hubContext;
    }

    /// <summary>
    /// 连接到 SSH 服务器
    /// </summary>
    public async Task Connect(SshConnectRequest request)
    {
        var connectionId = Context.ConnectionId;
        _logger.LogInformation("SSH 终端连接请求: {ConnectionId} -> {Host}:{Port}", connectionId, request.Host, request.Port);

        try
        {
            // 创建 SSH 连接
            Renci.SshNet.ConnectionInfo sshConnectionInfo;

            if (!string.IsNullOrEmpty(request.PrivateKey))
            {
                // 使用私钥认证
                using var keyStream = new MemoryStream(Encoding.UTF8.GetBytes(request.PrivateKey));
                var privateKeyFile = string.IsNullOrEmpty(request.Passphrase)
                    ? new PrivateKeyFile(keyStream)
                    : new PrivateKeyFile(keyStream, request.Passphrase);
                sshConnectionInfo = new Renci.SshNet.ConnectionInfo(request.Host, request.Port, request.Username,
                    new PrivateKeyAuthenticationMethod(request.Username, privateKeyFile));
            }
            else if (!string.IsNullOrWhiteSpace(request.PrivateKeyPath))
            {
                if (!File.Exists(request.PrivateKeyPath))
                {
                    await Clients.Caller.SendAsync("Error", new { code = "ssh.privateKey.notFound", message = "SSH private key file not found" });
                    return;
                }

                var privateKeyFile = string.IsNullOrEmpty(request.Passphrase)
                    ? new PrivateKeyFile(request.PrivateKeyPath)
                    : new PrivateKeyFile(request.PrivateKeyPath, request.Passphrase);
                sshConnectionInfo = new Renci.SshNet.ConnectionInfo(request.Host, request.Port, request.Username,
                    new PrivateKeyAuthenticationMethod(request.Username, privateKeyFile));
            }
            else if (!string.IsNullOrEmpty(request.Password))
            {
                // 使用密码认证
                sshConnectionInfo = new Renci.SshNet.ConnectionInfo(request.Host, request.Port, request.Username,
                    new PasswordAuthenticationMethod(request.Username, request.Password));
            }
            else
            {
                await Clients.Caller.SendAsync("Error", new { code = "ssh.auth.missing", message = "SSH password or private key is required" });
                return;
            }

            sshConnectionInfo.Timeout = TimeSpan.FromSeconds(request.Timeout > 0 ? request.Timeout : 30);

            var client = new SshClient(sshConnectionInfo);
            await Task.Run(() => client.Connect());

            if (!client.IsConnected)
            {
                await Clients.Caller.SendAsync("Error", new { code = "ssh.connect.failed", message = "SSH connection failed" });
                return;
            }

            // 创建 Shell 流
            var shellStream = client.CreateShellStream("xterm-256color",
                (uint)request.Cols, (uint)request.Rows,
                (uint)(request.Cols * 8), (uint)(request.Rows * 16), 1024);

            var session = new SshSession
            {
                Client = client,
                ShellStream = shellStream,
                ConnectionId = connectionId,
                Host = request.Host,
                ConnectedAt = DateTime.UtcNow
            };

            _sessions[connectionId] = session;

            // 启动读取输出的任务 - 使用 hubContext 而不是 Clients
            var hubContext = _hubContext;
            _ = Task.Run(async () => await ReadShellOutput(connectionId, shellStream, hubContext));

            await Clients.Caller.SendAsync("Connected", new
            {
                Host = request.Host,
                Port = request.Port,
                Username = request.Username,
                ConnectedAt = DateTime.UtcNow
            });

            _logger.LogInformation("SSH 终端已连接: {ConnectionId} -> {Host}", connectionId, request.Host);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSH 连接失败: {ConnectionId}", connectionId);
            await Clients.Caller.SendAsync("Error", new { code = "ssh.connect.failed", message = ex.Message });
        }
    }

    /// <summary>
    /// 发送输入到终端
    /// </summary>
    public async Task SendInput(string input)
    {
        var connectionId = Context.ConnectionId;

        if (_sessions.TryGetValue(connectionId, out var session) && session.ShellStream != null)
        {
            try
            {
                session.ShellStream.Write(input);
                session.ShellStream.Flush();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送输入失败: {ConnectionId}", connectionId);
                await Clients.Caller.SendAsync("Error", new { code = "ssh.send.failed", message = ex.Message });
            }
        }
        else
        {
            await Clients.Caller.SendAsync("Error", new { code = "ssh.session.not_found", message = "Session not found or disconnected" });
        }
    }

    /// <summary>
    /// 调整终端大小
    /// </summary>
    public async Task Resize(int cols, int rows)
    {
        var connectionId = Context.ConnectionId;

        if (_sessions.TryGetValue(connectionId, out var session) && session.ShellStream != null)
        {
            try
            {
                // 使用 SSH.NET 2025.1.0 的 ChangeWindowSize 方法
                // 这会通过 SSH 通道发送 window-change 请求 (SIGWINCH)
                session.ShellStream.ChangeWindowSize(
                    (uint)cols,
                    (uint)rows,
                    (uint)(cols * 8),   // 像素宽度
                    (uint)(rows * 16)); // 像素高度

                _logger.LogDebug("终端 resize 成功: {Cols}x{Rows}", cols, rows);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "调整终端大小失败: {ConnectionId}", connectionId);
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// 断开 SSH 连接
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
    /// 读取 Shell 输出并推送到客户端
    /// </summary>
    private async Task ReadShellOutput(string connectionId, ShellStream shellStream, IHubContext<SshTerminalHub> hubContext)
    {
        var buffer = new byte[4096];

        try
        {
            while (_sessions.ContainsKey(connectionId) && shellStream.CanRead)
            {
                var bytesRead = await shellStream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    var output = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    if (_sessions.ContainsKey(connectionId))
                    {
                        await hubContext.Clients.Client(connectionId).SendAsync("Output", output);
                    }
                }
                else
                {
                    await Task.Delay(10);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取 Shell 输出失败: {ConnectionId}", connectionId);

            if (_sessions.ContainsKey(connectionId))
            {
                try
                {
                    await hubContext.Clients.Client(connectionId).SendAsync("Error", new { code = "ssh.connection.disconnected", message = "Connection disconnected" });
                    await hubContext.Clients.Client(connectionId).SendAsync("Disconnected");
                }
                catch { /* 忽略发送错误 */ }
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
                session.ShellStream?.Close();
                session.ShellStream?.Dispose();
                session.Client?.Disconnect();
                session.Client?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理 SSH 会话失败: {ConnectionId}", connectionId);
            }

            _logger.LogInformation("SSH 会话已清理: {ConnectionId}", connectionId);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// SSH 连接请求
    /// </summary>
    public class SshConnectRequest
    {
        public string Host { get; set; } = "";
        public int Port { get; set; } = 22;
        public string Username { get; set; } = "";
        public string? Password { get; set; }
        public string? PrivateKey { get; set; }
        public string? PrivateKeyPath { get; set; }
        public string? Passphrase { get; set; }
        public int Cols { get; set; } = 80;
        public int Rows { get; set; } = 24;
        public int Timeout { get; set; } = 30;
    }

    /// <summary>
    /// SSH 会话
    /// </summary>
    private class SshSession
    {
        public SshClient Client { get; set; } = null!;
        public ShellStream? ShellStream { get; set; }
        public string ConnectionId { get; set; } = "";
        public string Host { get; set; } = "";
        public DateTime ConnectedAt { get; set; }
    }
}
