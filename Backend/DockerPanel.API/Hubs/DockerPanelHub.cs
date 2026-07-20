using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using DockerPanel.API.Services;
using System.Collections.Concurrent;

namespace DockerPanel.API.Hubs;

/// <summary>
/// DockerPanel SignalR Hub - 用于实时通信
/// </summary>
[Authorize]
public class DockerPanelHub : Hub
{
    private readonly ILogger<DockerPanelHub> _logger;
    private readonly IContainerService _containerService;
    private readonly INodeResourceService _nodeResourceService;
    private readonly LogStreamingService _logStreamingService;
    private static readonly ConcurrentDictionary<string, HashSet<string>> _subscriptions = new();
    private static int _connectionCount = 0;

    /// <summary>
    /// 检查是否有任何活动的连接
    /// </summary>
    public static bool HasConnections => _connectionCount > 0;

    /// <summary>
    /// 检查是否有任何连接订阅了指定类型
    /// </summary>
    public static bool HasSubscription(string subscriptionType)
    {
        foreach (var kvp in _subscriptions)
        {
            if (kvp.Value.Contains(subscriptionType))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 获取当前订阅数量（用于调试）
    /// </summary>
    public static int GetSubscriptionCount()
    {
        return _subscriptions.Count;
    }

    // 连接语言偏好存储
    private static readonly ConcurrentDictionary<string, string> _connectionLanguages = new();

    /// <summary>
    /// 获取连接的语言偏好
    /// </summary>
    public static string GetConnectionLanguage(string connectionId)
    {
        return _connectionLanguages.TryGetValue(connectionId, out var lang) ? lang : "zh-CN";
    }

    /// <summary>
    /// 获取所有连接的语言偏好（用于推送时按语言分组）
    /// </summary>
    public static IReadOnlyDictionary<string, string> GetAllConnectionLanguages()
    {
        return new Dictionary<string, string>(_connectionLanguages);
    }

    public DockerPanelHub(
        ILogger<DockerPanelHub> logger,
        IContainerService containerService,
        INodeResourceService nodeResourceService,
        LogStreamingService logStreamingService)
    {
        _logger = logger;
        _containerService = containerService;
        _nodeResourceService = nodeResourceService;
        _logStreamingService = logStreamingService;
    }

    /// <summary>
    /// 客户端连接时调用
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var connectionId = Context.ConnectionId;
        Interlocked.Increment(ref _connectionCount);
        _logger.LogInformation("客户端已连接: {ConnectionId}, 当前连接数: {Count}", connectionId, _connectionCount);

        // 获取连接语言并发送欢迎消息
        var language = GetConnectionLanguage(connectionId);
        var welcomeMessage = LocalizationService.GetTranslatedMessage("signalr.welcome", language, "Welcome to DockerPanel real-time service");

        await Clients.Caller.SendAsync("Welcome", new
        {
            Message = welcomeMessage,
            ConnectionId = connectionId,
            Timestamp = DateTime.UtcNow
        });

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// 客户端断开连接时调用
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        Interlocked.Decrement(ref _connectionCount);

        // 清理该连接的所有订阅
        _subscriptions.TryRemove(connectionId, out _);

        // 清理语言设置
        _connectionLanguages.TryRemove(connectionId, out _);

        // 清理日志流订阅
        _logStreamingService.ClearConnectionSubscriptions(connectionId);

        _logger.LogInformation("客户端已断开: {ConnectionId}, 当前连接数: {Count}, 异常: {Exception}",
            connectionId, _connectionCount, exception?.Message);

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// 订阅容器状态更新
    /// </summary>
    public async Task SubscribeToContainers()
    {
        var connectionId = Context.ConnectionId;

        if (!_subscriptions.ContainsKey(connectionId))
        {
            _subscriptions[connectionId] = new HashSet<string>();
        }

        _subscriptions[connectionId].Add("containers");

        _logger.LogInformation("客户端 {ConnectionId} 订阅了容器状态更新", connectionId);

        // 发送当前容器状态
        try
        {
            var containers = await _containerService.GetContainersAsync();
            await Clients.Caller.SendAsync("ContainersUpdated", containers);
        }
        catch (Exception ex)
        {
            var language = GetConnectionLanguage(connectionId);
            var errorMessage = LocalizationService.GetTranslatedMessage("signalr.error.containerList", language, "Failed to get container list");
            _logger.LogError(ex, "获取容器列表失败");
            await Clients.Caller.SendAsync("Error", new { Message = errorMessage });
        }
    }

    /// <summary>
    /// 订阅系统资源监控
    /// </summary>
    public async Task SubscribeToSystemStats()
    {
        var connectionId = Context.ConnectionId;

        if (!_subscriptions.ContainsKey(connectionId))
        {
            _subscriptions[connectionId] = new HashSet<string>();
        }

        _subscriptions[connectionId].Add("systemstats");

        _logger.LogInformation("客户端 {ConnectionId} 订阅了系统资源监控", connectionId);

        // 发送当前系统状态
        try
        {
            var stats = await _nodeResourceService.GetClusterResourceStatsAsync();
            await Clients.Caller.SendAsync("SystemStatsUpdated", stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取系统统计失败");
            var language = GetConnectionLanguage(connectionId);
            var errorMessage = LocalizationService.GetTranslatedMessage("signalr.error.systemStats", language, "Failed to get system statistics");
            await Clients.Caller.SendAsync("Error", new { Message = errorMessage });
        }
    }

    /// <summary>
    /// 取消订阅系统资源监控
    /// </summary>
    public Task UnsubscribeFromSystemStats()
    {
        var connectionId = Context.ConnectionId;

        if (_subscriptions.TryGetValue(connectionId, out var subs))
        {
            subs.Remove("systemstats");
            _logger.LogInformation("客户端 {ConnectionId} 取消订阅系统资源监控", connectionId);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 订阅容器统计信息
    /// </summary>
    public async Task SubscribeToContainerStats()
    {
        var connectionId = Context.ConnectionId;

        if (!_subscriptions.ContainsKey(connectionId))
        {
            _subscriptions[connectionId] = new HashSet<string>();
        }

        _subscriptions[connectionId].Add("containerstats");

        _logger.LogInformation("客户端 {ConnectionId} 订阅了容器统计信息", connectionId);

        // 发送当前容器状态
        try
        {
            var containers = await _containerService.GetContainersAsync();
            await Clients.Caller.SendAsync("ContainersUpdated", containers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取容器列表失败");
            var language = GetConnectionLanguage(connectionId);
            var errorMessage = LocalizationService.GetTranslatedMessage("signalr.error.containerList", language, "Failed to get container list");
            await Clients.Caller.SendAsync("Error", new { Message = errorMessage });
        }
    }

    /// <summary>
    /// 取消订阅容器统计信息
    /// </summary>
    public Task UnsubscribeFromContainerStats()
    {
        var connectionId = Context.ConnectionId;

        if (_subscriptions.TryGetValue(connectionId, out var subs))
        {
            subs.Remove("containerstats");
            _logger.LogInformation("客户端 {ConnectionId} 取消订阅容器统计信息", connectionId);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 订阅实时日志
    /// </summary>
    public async Task SubscribeToLogs(string containerId, int tailLines = 100)
    {
        var connectionId = Context.ConnectionId;

        if (!_subscriptions.ContainsKey(connectionId))
        {
            _subscriptions[connectionId] = new HashSet<string>();
        }

        var subscriptionKey = $"logs:{containerId}";
        _subscriptions[connectionId].Add(subscriptionKey);

        // 将连接加入 SignalR 组，便于组播
        await Groups.AddToGroupAsync(connectionId, $"logs:{containerId}");

        _logger.LogInformation("客户端 {ConnectionId} 订阅了容器 {ContainerId} 的日志", connectionId, containerId);

        // 启动日志流推送
        await _logStreamingService.SubscribeToLogsAsync(connectionId, containerId, tailLines);

        // 发送订阅确认
        await Clients.Caller.SendAsync("LogsSubscribed", new { ContainerId = containerId, TailLines = tailLines });
    }
    
    /// <summary>
    /// 取消订阅实时日志
    /// </summary>
    public async Task UnsubscribeFromLogs(string containerId)
    {
        var connectionId = Context.ConnectionId;
        
        if (_subscriptions.TryGetValue(connectionId, out var subs))
        {
            subs.Remove($"logs:{containerId}");
        }
        
        // 从 SignalR 组移除
        await Groups.RemoveFromGroupAsync(connectionId, $"logs:{containerId}");
        
        // 清理日志流订阅
        _logStreamingService.UnsubscribeFromLogs(connectionId, containerId);
        
        _logger.LogInformation("客户端 {ConnectionId} 取消订阅容器 {ContainerId} 的日志", connectionId, containerId);
    }

    /// <summary>
    /// 发送心跳响应
    /// </summary>
    public async Task Ping()
    {
        await Clients.Caller.SendAsync("Pong", new
        {
            Timestamp = DateTime.UtcNow,
            ServerTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        });
    }

    /// <summary>
    /// 获取连接状态
    /// </summary>
    public async Task GetConnectionStatus()
    {
        await Clients.Caller.SendAsync("ConnectionStatus", new
        {
            ConnectionId = Context.ConnectionId,
            IsConnected = true,
            ConnectedAt = DateTime.UtcNow,
            Subscriptions = _subscriptions.GetValueOrDefault(Context.ConnectionId, new HashSet<string>()).ToList()
        });
    }

    /// <summary>
    /// 设置客户端语言偏好
    /// </summary>
    public Task SetLanguage(string language)
    {
        var connectionId = Context.ConnectionId;

        // 规范化语言代码
        var normalizedLang = NormalizeLanguage(language);
        _connectionLanguages[connectionId] = normalizedLang;

        _logger.LogInformation("客户端 {ConnectionId} 设置语言为: {Language}", connectionId, normalizedLang);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 规范化语言代码
    /// </summary>
    private static string NormalizeLanguage(string language)
    {
        if (string.IsNullOrEmpty(language))
            return "zh-CN";

        var lang = language.ToLowerInvariant();

        if (lang.StartsWith("zh"))
            return "zh-CN";

        if (lang.StartsWith("en"))
            return "en-US";

        return language;
    }

    /// <summary>
    /// 广播容器状态更新给所有订阅的客户端
    /// </summary>
    public static async Task BroadcastContainerUpdate(IHubContext<DockerPanelHub> hubContext, object containers)
    {
        var connections = _subscriptions.Where(kvp => kvp.Value.Contains("containers")).Select(kvp => kvp.Key);

        foreach (var connectionId in connections)
        {
            await hubContext.Clients.Client(connectionId).SendAsync("ContainersUpdated", containers);
        }
    }

    /// <summary>
    /// 广播系统状态更新给所有订阅的客户端
    /// </summary>
    public static async Task BroadcastSystemStatsUpdate(IHubContext<DockerPanelHub> hubContext, object stats)
    {
        var connections = _subscriptions.Where(kvp => kvp.Value.Contains("systemstats")).Select(kvp => kvp.Key);

        foreach (var connectionId in connections)
        {
            await hubContext.Clients.Client(connectionId).SendAsync("SystemStatsUpdated", stats);
        }
    }

    /// <summary>
    /// 广播日志给特定容器的订阅者
    /// </summary>
    public static async Task BroadcastLogUpdate(IHubContext<DockerPanelHub> hubContext, string containerId, object logEntry)
    {
        var subscriptionKey = $"logs:{containerId}";
        var connections = _subscriptions.Where(kvp => kvp.Value.Contains(subscriptionKey)).Select(kvp => kvp.Key);

        foreach (var connectionId in connections)
        {
            await hubContext.Clients.Client(connectionId).SendAsync("LogUpdated", new { ContainerId = containerId, Log = logEntry });
        }
    }

    /// <summary>
    /// 广播日志给所有连接的客户端（用于实时日志页面）
    /// </summary>
    public static async Task BroadcastLogToAll(IHubContext<DockerPanelHub> hubContext, object logEntry)
    {
        await hubContext.Clients.All.SendAsync("logs", logEntry);
    }

    /// <summary>
    /// 广播通知给所有连接的客户端
    /// </summary>
    public static async Task BroadcastNotification(IHubContext<DockerPanelHub> hubContext, object notification)
    {
        await hubContext.Clients.All.SendAsync("Notification", notification);
    }

    /// <summary>
    /// 广播部署进度给所有客户端
    /// </summary>
    public static async Task BroadcastDeployProgress(IHubContext<DockerPanelHub> hubContext, string projectId, string step, int progress, string? detail = null)
    {
        await hubContext.Clients.All.SendAsync("ComposeDeployProgress", new
        {
            ProjectId = projectId,
            Step = step,
            StepKey = step, // 消息键，前端用于翻译
            Status = GetStatusFromStep(step), // 状态：preparing, running, completed, failed
            Progress = progress,
            Detail = detail,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// 广播操作进度给所有客户端（启动/停止等）
    /// </summary>
    public static async Task BroadcastOperationProgress(IHubContext<DockerPanelHub> hubContext, string projectName, string step, int progress, string? detail = null)
    {
        await hubContext.Clients.All.SendAsync("ComposeOperationProgress", new
        {
            ProjectName = projectName,
            Step = step,
            StepKey = step,
            Status = GetStatusFromStep(step),
            Progress = progress,
            Detail = detail,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// 广播卷打包进度给所有客户端
    /// </summary>
    public static async Task BroadcastVolumeArchiveProgress(IHubContext<DockerPanelHub> hubContext, string volumeId, string step, int progress, string? detail = null)
    {
        await hubContext.Clients.All.SendAsync("VolumeArchiveProgress", new
        {
            VolumeId = volumeId,
            Step = step,
            StepKey = step,
            Status = GetStatusFromStep(step),
            Progress = progress,
            Detail = detail,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// 广播镜像拉取进度给所有客户端
    /// </summary>
    public static async Task BroadcastImagePullProgress(IHubContext<DockerPanelHub> hubContext, string pullId, string imageName, string step, int progress, string? detail = null, PullLayerInfo? layer = null)
    {
        await hubContext.Clients.All.SendAsync("ImagePullProgress", new
        {
            PullId = pullId,
            ImageName = imageName,
            Step = step,
            StepKey = step,
            Status = GetStatusFromStep(step),
            Progress = progress,
            Detail = detail,
            Layer = layer == null ? null : new
            {
                LayerId = layer.LayerId,
                Status = layer.Status,
                Current = layer.Current,
                Total = layer.Total,
                Progress = layer.Total > 0 ? (int)((double)layer.Current / layer.Total * 100) : 0
            },
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// 单个镜像层的拉取信息
    /// </summary>
    public class PullLayerInfo
    {
        public string LayerId { get; set; } = "";
        public string Status { get; set; } = "";
        public long Current { get; set; }
        public long Total { get; set; }
    }

    /// <summary>
    /// 按层聚合镜像拉取进度，避免多个层并发推送导致整体进度来回跳动。
    /// 整体进度 = 各层完成度平均值；已完成的层记为 100。
    /// </summary>
    public class ImagePullProgressAggregator
    {
        private readonly Dictionary<string, PullLayerInfo> _layers = new();
        private readonly object _lock = new();

        public void Update(string layerId, string status, long current, long total)
        {
            lock (_lock)
            {
                _layers[layerId] = new PullLayerInfo
                {
                    LayerId = layerId,
                    Status = status,
                    Current = current,
                    Total = total
                };
            }
        }

        public int OverallProgress
        {
            get
            {
                lock (_lock)
                {
                    if (_layers.Count == 0)
                        return 0;
                    var sum = _layers.Values.Sum(l => l.Total > 0 ? (int)((double)l.Current / l.Total * 100) : (l.Status.Contains("Pull complete", StringComparison.OrdinalIgnoreCase) ? 100 : 0));
                    return Math.Min(100, sum / _layers.Count);
                }
            }
        }

        public IReadOnlyCollection<PullLayerInfo> Layers
        {
            get
            {
                lock (_lock)
                {
                    return _layers.Values.ToList();
                }
            }
        }
    }

    /// <summary>
    /// 广播镜像推送进度给所有客户端
    /// </summary>
    public static async Task BroadcastImagePushProgress(IHubContext<DockerPanelHub> hubContext, string pushId, string imageName, string step, int progress, string? detail = null)
    {
        await hubContext.Clients.All.SendAsync("ImagePushProgress", new
        {
            PushId = pushId,
            ImageName = imageName,
            Step = step,
            StepKey = step,
            Status = GetStatusFromStep(step),
            Progress = progress,
            Detail = detail,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// 订阅特定项目的部署进度
    /// </summary>
    public async Task SubscribeToDeployProgress(string projectId)
    {
        var connectionId = Context.ConnectionId;

        if (!_subscriptions.ContainsKey(connectionId))
        {
            _subscriptions[connectionId] = new HashSet<string>();
        }

        _subscriptions[connectionId].Add($"deploy:{projectId}");
        await Groups.AddToGroupAsync(connectionId, $"deploy:{projectId}");

        _logger.LogInformation("客户端 {ConnectionId} 订阅了项目 {ProjectId} 的部署进度", connectionId, projectId);
    }

    /// <summary>
    /// 取消订阅部署进度
    /// </summary>
    public async Task UnsubscribeFromDeployProgress(string projectId)
    {
        var connectionId = Context.ConnectionId;

        if (_subscriptions.TryGetValue(connectionId, out var subs))
        {
            subs.Remove($"deploy:{projectId}");
        }

        await Groups.RemoveFromGroupAsync(connectionId, $"deploy:{projectId}");
        _logger.LogInformation("客户端 {ConnectionId} 取消订阅项目 {ProjectId} 的部署进度", connectionId, projectId);
    }

    /// <summary>
    /// 广播镜像构建进度给所有客户端
    /// </summary>
    public static async Task BroadcastImageBuildProgress(IHubContext<DockerPanelHub> hubContext, string buildId, string step, int progress, string? detail = null, string? stream = null, bool isError = false)
    {
        await hubContext.Clients.All.SendAsync("ImageBuildProgress", new
        {
            BuildId = buildId,
            Step = step,
            StepKey = step,
            Status = GetStatusFromStep(step),
            Progress = progress,
            Detail = detail,
            Stream = stream,
            IsError = isError,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// 根据步骤名称获取状态
    /// </summary>
    private static string GetStatusFromStep(string step)
    {
        if (string.IsNullOrEmpty(step)) return "running";

        return step.ToLowerInvariant() switch
        {
            // 旧格式（中文）
            "完成" or "completed" or "成功" or "success" => "completed",
            "失败" or "failed" or "error" or "错误" => "failed",
            "准备中" or "准备" or "preparing" => "preparing",
            // 新格式（翻译键）
            var s when s.EndsWith(".completed") || s.EndsWith(".success") => "completed",
            var s when s.EndsWith(".failed") || s.EndsWith(".error") => "failed",
            var s when s.EndsWith(".preparing") => "preparing",
            _ => "running"
        };
    }

    /// <summary>
    /// 订阅镜像构建进度
    /// </summary>
    public async Task SubscribeToImageBuildProgress(string buildId)
    {
        var connectionId = Context.ConnectionId;

        if (!_subscriptions.ContainsKey(connectionId))
        {
            _subscriptions[connectionId] = new HashSet<string>();
        }

        _subscriptions[connectionId].Add($"imagebuild:{buildId}");
        await Groups.AddToGroupAsync(connectionId, $"imagebuild:{buildId}");

        _logger.LogInformation("客户端 {ConnectionId} 订阅了镜像构建 {BuildId} 的进度", connectionId, buildId);
    }

    /// <summary>
    /// 取消订阅镜像构建进度
    /// </summary>
    public async Task UnsubscribeFromImageBuildProgress(string buildId)
    {
        var connectionId = Context.ConnectionId;

        if (_subscriptions.TryGetValue(connectionId, out var subs))
        {
            subs.Remove($"imagebuild:{buildId}");
        }

        await Groups.RemoveFromGroupAsync(connectionId, $"imagebuild:{buildId}");
        _logger.LogInformation("客户端 {ConnectionId} 取消订阅镜像构建 {BuildId} 的进度", connectionId, buildId);
    }
}