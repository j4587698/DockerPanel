using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace DockerPanel.API.Services;

/// <summary>
/// 日志流推送服务 - 管理容器日志的实时订阅和推送
/// </summary>
public class LogStreamingService : IHostedService
{
    private readonly ILogger<LogStreamingService> _logger;
    private readonly IHubContext<Hubs.DockerPanelHub> _hubContext;
    private readonly IServiceProvider _serviceProvider;
    
    // 存储每个容器的日志流任务和取消令牌
    private readonly ConcurrentDictionary<string, LogStreamContext> _activeStreams = new();
    
    // 存储容器ID到连接ID的映射（一个容器可能有多个订阅者）
    private readonly ConcurrentDictionary<string, HashSet<string>> _containerSubscribers = new();

    public LogStreamingService(
        ILogger<LogStreamingService> logger,
        IHubContext<Hubs.DockerPanelHub> hubContext,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _hubContext = hubContext;
        _serviceProvider = serviceProvider;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("日志流推送服务已启动");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("日志流推送服务正在停止...");
        
        // 停止所有活跃的日志流
        foreach (var stream in _activeStreams.Values)
        {
            stream.CancellationTokenSource.Cancel();
        }
        
        _activeStreams.Clear();
        _containerSubscribers.Clear();
        
        _logger.LogInformation("日志流推送服务已停止");
    }

    /// <summary>
    /// 订阅容器日志
    /// </summary>
    public async Task SubscribeToLogsAsync(string connectionId, string containerId, int tailLines = 100)
    {
        // 添加订阅者
        if (!_containerSubscribers.ContainsKey(containerId))
        {
            _containerSubscribers[containerId] = new HashSet<string>();
        }
        
        _containerSubscribers[containerId].Add(connectionId);
        
        _logger.LogInformation("连接 {ConnectionId} 订阅容器 {ContainerId} 的日志", connectionId, containerId);
        
        // 如果该容器还没有活跃的日志流，启动一个新的
        if (!_activeStreams.ContainsKey(containerId))
        {
            await StartLogStreamAsync(containerId, tailLines);
        }
    }

    /// <summary>
    /// 取消订阅容器日志
    /// </summary>
    public void UnsubscribeFromLogs(string connectionId, string containerId)
    {
        if (_containerSubscribers.TryGetValue(containerId, out var subscribers))
        {
            subscribers.Remove(connectionId);
            
            // 如果没有订阅者了，停止日志流
            if (subscribers.Count == 0)
            {
                _containerSubscribers.TryRemove(containerId, out _);
                StopLogStream(containerId);
            }
        }
    }

    /// <summary>
    /// 连接断开时清理所有订阅
    /// </summary>
    public void ClearConnectionSubscriptions(string connectionId)
    {
        foreach (var kvp in _containerSubscribers.ToList())
        {
            kvp.Value.Remove(connectionId);
            
            if (kvp.Value.Count == 0)
            {
                _containerSubscribers.TryRemove(kvp.Key, out _);
                StopLogStream(kvp.Key);
            }
        }
    }

    /// <summary>
    /// 启动容器的日志流
    /// </summary>
    private async Task StartLogStreamAsync(string containerId, int tailLines)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dockerEngine = scope.ServiceProvider.GetService<IContainerEngine>() as DockerEngine;
            
            if (dockerEngine == null)
            {
                _logger.LogWarning("无法获取 Docker 引擎实例");
                return;
            }

            var client = await dockerEngine.GetClientAsync();
            var cts = new CancellationTokenSource();
            
            var context = new LogStreamContext
            {
                CancellationTokenSource = cts
            };
            
            _activeStreams[containerId] = context;
            
            _logger.LogInformation("启动容器 {ContainerId} 的日志流", containerId);
            
            // 在后台任务中处理日志流
            _ = Task.Run(async () =>
            {
                try
                {
                    var parameters = new ContainerLogsParameters
                    {
                        ShowStdout = true,
                        ShowStderr = true,
                        Follow = true,
                        Tail = tailLines.ToString(),
                        Timestamps = true
                    };

                    using var multiplexedStream = await client.Containers.GetContainerLogsAsync(
                        containerId,
                        parameters,
                        cts.Token);

                    var buffer = new byte[4096];
                    
                    while (!cts.Token.IsCancellationRequested)
                    {
                        var result = await multiplexedStream.ReadOutputAsync(buffer, 0, buffer.Length, cts.Token);
                        if (result.EOF || result.Count == 0)
                        {
                            // 流结束，等待一下再重试
                            await Task.Delay(1000, cts.Token);
                            continue;
                        }

                        var line = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count).TrimEnd('\n', '\r');
                        if (string.IsNullOrEmpty(line)) continue;

                        // 解析日志行
                        var logEntry = ParseLogLine(line);
                        
                        // 推送给订阅者
                        await BroadcastLogAsync(containerId, logEntry);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("容器 {ContainerId} 的日志流已取消", containerId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "容器 {ContainerId} 的日志流发生错误", containerId);
                }
                finally
                {
                    _activeStreams.TryRemove(containerId, out _);
                }
            }, cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动容器 {ContainerId} 的日志流失败", containerId);
        }
    }

    /// <summary>
    /// 停止容器的日志流
    /// </summary>
    private void StopLogStream(string containerId)
    {
        if (_activeStreams.TryRemove(containerId, out var context))
        {
            context.CancellationTokenSource.Cancel();
            _logger.LogInformation("已停止容器 {ContainerId} 的日志流", containerId);
        }
    }

    /// <summary>
    /// 解析日志行
    /// </summary>
    private LogEntry ParseLogLine(string line)
    {
        // Docker 日志格式: "timestamp message" 或原始消息
        var parts = line.Split(new[] { ' ' }, 2);
        
        DateTime timestamp;
        string message;
        
        if (parts.Length == 2 && DateTime.TryParse(parts[0], out timestamp))
        {
            message = parts[1];
        }
        else
        {
            timestamp = DateTime.UtcNow;
            message = line;
        }

        return new LogEntry
        {
            Timestamp = timestamp,
            Message = message,
            Level = DetermineLogLevel(message)
        };
    }

    /// <summary>
    /// 确定日志级别
    /// </summary>
    private string DetermineLogLevel(string message)
    {
        var lowerMessage = message.ToLowerInvariant();
        
        if (lowerMessage.Contains("error") || lowerMessage.Contains("exception") || lowerMessage.Contains("fatal"))
            return "error";
        if (lowerMessage.Contains("warn") || lowerMessage.Contains("warning"))
            return "warning";
        if (lowerMessage.Contains("debug") || lowerMessage.Contains("trace"))
            return "debug";
        
        return "info";
    }

    /// <summary>
    /// 广播日志给订阅者
    /// </summary>
    private async Task BroadcastLogAsync(string containerId, LogEntry logEntry)
    {
        if (_containerSubscribers.TryGetValue(containerId, out var subscribers) && subscribers.Count > 0)
        {
            await _hubContext.Clients.Group($"logs:{containerId}").SendAsync("logs", new
            {
                containerId,
                message = logEntry.Message,
                timestamp = logEntry.Timestamp.ToString("o"),
                level = logEntry.Level
            });
        }
    }
}

/// <summary>
/// 日志流上下文
/// </summary>
internal class LogStreamContext
{
    public CancellationTokenSource CancellationTokenSource { get; set; } = null!;
    // 注意：不存储 DockerClient，因为它是共享的，不应该被 dispose
}

/// <summary>
/// 日志条目
/// </summary>
internal class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Level { get; set; } = "info";
}
