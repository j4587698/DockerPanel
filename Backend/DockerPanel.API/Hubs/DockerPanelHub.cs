using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using DockerPanel.API.Services;
using DockerPanel.API.Models;
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
    private readonly IImageService _imageService;
    private readonly INodeResourceService _nodeResourceService;
    private readonly LogStreamingService _logStreamingService;
    private static readonly ConcurrentDictionary<string, HashSet<string>> _subscriptions = new();
    private static int _connectionCount = 0;

    /// <summary>
    /// 检查是否有任何活动的连接
    /// </summary>
    public static bool HasConnections => _connectionCount > 0;

    /// <summary>
    /// 规范化订阅的节点 ID（未指定 = 默认节点，与后端 nodeId=null 的语义一致）
    /// </summary>
    private static string NormalizeNodeId(string? nodeId) =>
        string.IsNullOrWhiteSpace(nodeId) ? "default" : nodeId.Trim();

    /// <summary>订阅键："{类型}:{节点ID}"</summary>
    private static string SubKey(string kind, string? nodeId) => $"{kind}:{NormalizeNodeId(nodeId)}";

    /// <summary>
    /// 检查是否有任何连接订阅了指定类型
    /// </summary>
    public static bool HasSubscription(string subscriptionType)
    {
        var prefix = subscriptionType + ":";
        foreach (var kvp in _subscriptions)
        {
            foreach (var key in kvp.Value)
            {
                if (key.Equals(subscriptionType, StringComparison.Ordinal) ||
                    key.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 获取订阅了指定类型的所有节点 ID（去重，"default" 表示默认节点）
    /// </summary>
    public static List<string> GetSubscribedNodeIds(string kind)
    {
        var prefix = kind + ":";
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in _subscriptions)
        {
            foreach (var key in kvp.Value)
            {
                if (key.Equals(kind, StringComparison.Ordinal))
                    result.Add("default");
                else if (key.StartsWith(prefix, StringComparison.Ordinal))
                    result.Add(key[prefix.Length..]);
            }
        }
        return result.ToList();
    }

    /// <summary>
    /// 获取订阅了某个精确键的所有连接 ID
    /// </summary>
    public static List<string> GetConnectionsFor(string key)
    {
        return _subscriptions.Where(kvp => kvp.Value.Contains(key)).Select(kvp => kvp.Key).ToList();
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
        IImageService imageService,
        INodeResourceService nodeResourceService,
        LogStreamingService logStreamingService)
    {
        _logger = logger;
        _containerService = containerService;
        _imageService = imageService;
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

        await Clients.Caller.SendAsync("Welcome", new WelcomeMessage
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
    /// 订阅容器状态更新（可指定节点，默认节点传 null）
    /// </summary>
    public async Task SubscribeToContainers(string? nodeId = null)
    {
        var connectionId = Context.ConnectionId;

        if (!_subscriptions.ContainsKey(connectionId))
        {
            _subscriptions[connectionId] = new HashSet<string>();
        }

        _subscriptions[connectionId].Add(SubKey("containers", nodeId));

        _logger.LogInformation("客户端 {ConnectionId} 订阅了容器状态更新, 节点: {NodeId}", connectionId, NormalizeNodeId(nodeId));

        // 发送当前容器状态
        try
        {
            var containers = await _containerService.GetContainersAsync(nodeId, all: true);
            await Clients.Caller.SendAsync("ContainersUpdated", containers);
        }
        catch (Exception ex)
        {
            var language = GetConnectionLanguage(connectionId);
            var errorMessage = LocalizationService.GetTranslatedMessage("signalr.error.containerList", language, "Failed to get container list");
            _logger.LogError(ex, "获取容器列表失败");
            await Clients.Caller.SendAsync("Error", new HubErrorMessage { Message = errorMessage });
        }
    }

    /// <summary>
    /// 取消订阅容器状态更新
    /// </summary>
    public Task UnsubscribeFromContainers(string? nodeId = null)
    {
        var connectionId = Context.ConnectionId;

        if (_subscriptions.TryGetValue(connectionId, out var subs))
        {
            subs.Remove(SubKey("containers", nodeId));
            _logger.LogInformation("客户端 {ConnectionId} 取消订阅容器状态更新, 节点: {NodeId}", connectionId, NormalizeNodeId(nodeId));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 订阅系统资源监控（可指定节点，默认节点传 null）
    /// </summary>
    public async Task SubscribeToSystemStats(string? nodeId = null)
    {
        var connectionId = Context.ConnectionId;

        if (!_subscriptions.ContainsKey(connectionId))
        {
            _subscriptions[connectionId] = new HashSet<string>();
        }

        _subscriptions[connectionId].Add(SubKey("systemstats", nodeId));

        _logger.LogInformation("客户端 {ConnectionId} 订阅了系统资源监控, 节点: {NodeId}", connectionId, NormalizeNodeId(nodeId));

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
            await Clients.Caller.SendAsync("Error", new HubErrorMessage { Message = errorMessage });
        }
    }

    /// <summary>
    /// 取消订阅系统资源监控
    /// </summary>
    public Task UnsubscribeFromSystemStats(string? nodeId = null)
    {
        var connectionId = Context.ConnectionId;

        if (_subscriptions.TryGetValue(connectionId, out var subs))
        {
            subs.Remove(SubKey("systemstats", nodeId));
            _logger.LogInformation("客户端 {ConnectionId} 取消订阅系统资源监控, 节点: {NodeId}", connectionId, NormalizeNodeId(nodeId));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 订阅镜像列表更新（可指定节点，默认节点传 null）
    /// </summary>
    public async Task SubscribeToImages(string? nodeId = null)
    {
        var connectionId = Context.ConnectionId;

        if (!_subscriptions.ContainsKey(connectionId))
        {
            _subscriptions[connectionId] = new HashSet<string>();
        }

        _subscriptions[connectionId].Add(SubKey("images", nodeId));

        _logger.LogInformation("客户端 {ConnectionId} 订阅了镜像列表更新, 节点: {NodeId}", connectionId, NormalizeNodeId(nodeId));

        // 发送当前镜像列表
        try
        {
            var images = await _imageService.GetImagesAsync(nodeId);
            await Clients.Caller.SendAsync("ImagesUpdated", images);
        }
        catch (Exception ex)
        {
            var language = GetConnectionLanguage(connectionId);
            var errorMessage = LocalizationService.GetTranslatedMessage("signalr.error.imageList", language, "Failed to get image list");
            _logger.LogError(ex, "获取镜像列表失败");
            await Clients.Caller.SendAsync("Error", new HubErrorMessage { Message = errorMessage });
        }
    }

    /// <summary>
    /// 取消订阅镜像列表更新
    /// </summary>
    public Task UnsubscribeFromImages(string? nodeId = null)
    {
        var connectionId = Context.ConnectionId;

        if (_subscriptions.TryGetValue(connectionId, out var subs))
        {
            subs.Remove(SubKey("images", nodeId));
            _logger.LogInformation("客户端 {ConnectionId} 取消订阅镜像列表更新, 节点: {NodeId}", connectionId, NormalizeNodeId(nodeId));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 订阅容器统计信息（可指定节点，默认节点传 null）
    /// </summary>
    public async Task SubscribeToContainerStats(string? nodeId = null)
    {
        var connectionId = Context.ConnectionId;

        if (!_subscriptions.ContainsKey(connectionId))
        {
            _subscriptions[connectionId] = new HashSet<string>();
        }

        _subscriptions[connectionId].Add(SubKey("containerstats", nodeId));

        _logger.LogInformation("客户端 {ConnectionId} 订阅了容器统计信息, 节点: {NodeId}", connectionId, NormalizeNodeId(nodeId));

        // 发送当前容器状态
        try
        {
            var containers = await _containerService.GetContainersAsync(nodeId, all: true);
            await Clients.Caller.SendAsync("ContainersUpdated", containers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取容器列表失败");
            var language = GetConnectionLanguage(connectionId);
            var errorMessage = LocalizationService.GetTranslatedMessage("signalr.error.containerList", language, "Failed to get container list");
            await Clients.Caller.SendAsync("Error", new HubErrorMessage { Message = errorMessage });
        }
    }

    /// <summary>
    /// 取消订阅容器统计信息
    /// </summary>
    public Task UnsubscribeFromContainerStats(string? nodeId = null)
    {
        var connectionId = Context.ConnectionId;

        if (_subscriptions.TryGetValue(connectionId, out var subs))
        {
            subs.Remove(SubKey("containerstats", nodeId));
            _logger.LogInformation("客户端 {ConnectionId} 取消订阅容器统计信息, 节点: {NodeId}", connectionId, NormalizeNodeId(nodeId));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 订阅实时日志（可指定节点，默认节点传 null）
    /// </summary>
    public async Task SubscribeToLogs(string containerId, int tailLines = 100, string? nodeId = null)
    {
        var connectionId = Context.ConnectionId;
        var nodeKey = NormalizeNodeId(nodeId);

        if (!_subscriptions.ContainsKey(connectionId))
        {
            _subscriptions[connectionId] = new HashSet<string>();
        }

        var subscriptionKey = $"logs:{nodeKey}:{containerId}";
        _subscriptions[connectionId].Add(subscriptionKey);

        // 将连接加入 SignalR 组，便于组播
        await Groups.AddToGroupAsync(connectionId, subscriptionKey);

        _logger.LogInformation("客户端 {ConnectionId} 订阅了容器 {ContainerId} 的日志, 节点: {NodeId}", connectionId, containerId, nodeKey);

        // 启动日志流推送
        await _logStreamingService.SubscribeToLogsAsync(connectionId, containerId, tailLines, nodeKey);

        // 发送订阅确认
        await Clients.Caller.SendAsync("LogsSubscribed", new LogsSubscribedMessage { ContainerId = containerId, TailLines = tailLines });
    }

    /// <summary>
    /// 取消订阅实时日志
    /// </summary>
    public async Task UnsubscribeFromLogs(string containerId, string? nodeId = null)
    {
        var connectionId = Context.ConnectionId;
        var nodeKey = NormalizeNodeId(nodeId);

        if (_subscriptions.TryGetValue(connectionId, out var subs))
        {
            subs.Remove($"logs:{nodeKey}:{containerId}");
        }

        // 从 SignalR 组移除
        await Groups.RemoveFromGroupAsync(connectionId, $"logs:{nodeKey}:{containerId}");

        // 清理日志流订阅
        _logStreamingService.UnsubscribeFromLogs(connectionId, containerId, nodeKey);

        _logger.LogInformation("客户端 {ConnectionId} 取消订阅容器 {ContainerId} 的日志, 节点: {NodeId}", connectionId, containerId, nodeKey);
    }

    /// <summary>
    /// 发送心跳响应
    /// </summary>
    public async Task Ping()
    {
        await Clients.Caller.SendAsync("Pong", new PongMessage
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
        await Clients.Caller.SendAsync("ConnectionStatus", new ConnectionStatusMessage
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
    /// 广播容器状态更新给订阅了该节点的客户端
    /// </summary>
    public static async Task BroadcastContainerUpdate(IHubContext<DockerPanelHub> hubContext, object containers, string? nodeId = null)
    {
        foreach (var connectionId in GetConnectionsFor(SubKey("containers", nodeId)))
        {
            await hubContext.Clients.Client(connectionId).SendAsync("ContainersUpdated", containers);
        }
    }

    /// <summary>
    /// 广播镜像列表更新给订阅了该节点的客户端
    /// </summary>
    public static async Task BroadcastImageUpdate(IHubContext<DockerPanelHub> hubContext, object images, string? nodeId = null)
    {
        foreach (var connectionId in GetConnectionsFor(SubKey("images", nodeId)))
        {
            await hubContext.Clients.Client(connectionId).SendAsync("ImagesUpdated", images);
        }
    }

    /// <summary>
    /// 广播系统状态更新给所有订阅的客户端
    /// </summary>
    public static async Task BroadcastSystemStatsUpdate(IHubContext<DockerPanelHub> hubContext, Services.ClusterResourceStats stats)
    {
        var connections = _subscriptions.Where(kvp => kvp.Value.Any(k => k == "systemstats" || k.StartsWith("systemstats:", StringComparison.Ordinal))).Select(kvp => kvp.Key);

        foreach (var connectionId in connections)
        {
            await hubContext.Clients.Client(connectionId).SendAsync("SystemStatsUpdated", stats);
        }
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
        await hubContext.Clients.All.SendAsync("ComposeDeployProgress", new ComposeDeployProgressMessage
        {
            ProjectId = projectId,
            Step = step,
            StepKey = step,
            Status = GetStatusFromStep(step),
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
        await hubContext.Clients.All.SendAsync("ComposeOperationProgress", new ComposeOperationProgressMessage
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
        await hubContext.Clients.All.SendAsync("VolumeArchiveProgress", new VolumeArchiveProgressMessage
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
        await hubContext.Clients.All.SendAsync("ImagePullProgress", new ImagePullProgressMessage
        {
            PullId = pullId,
            ImageName = imageName,
            Step = step,
            StepKey = step,
            Status = GetStatusFromStep(step),
            Progress = progress,
            Detail = detail,
            Layer = layer == null ? null : new ImagePullLayerMessage
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
    /// 整体进度 = 各层完成度平均值，且只增不减（单调），保证 UI 进度条不会倒退。
    /// </summary>
    public class ImagePullProgressAggregator
    {
        private readonly Dictionary<string, PullLayerInfo> _layers = new();
        private readonly object _lock = new();
        private int _lastOverall;

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
                    var computed = Math.Min(100, sum / _layers.Count);
                    if (computed > _lastOverall)
                        _lastOverall = computed;
                    return _lastOverall;
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
    /// 创建「按层聚合 + 单调递增」的镜像拉取进度广播器。
    /// 统一所有需要重新拉取镜像的入口（镜像拉取/重建/自动更新/回滚/compose 部署）的进度输出格式，
    /// 前端订阅 image-pull-progress 即可得到一致的进度表现。
    /// </summary>
    public static IProgress<ImagePullProgress> CreatePullProgressBroadcaster(
        IHubContext<DockerPanelHub> hubContext,
        string pullId,
        string imageName)
    {
        var aggregator = new ImagePullProgressAggregator();
        return new Progress<ImagePullProgress>(p =>
        {
            var layerId = p.Id ?? "layer";
            var status = p.Status ?? "";
            aggregator.Update(layerId, status, p.Current, p.Total);

            var layer = aggregator.Layers.FirstOrDefault(l => l.LayerId == layerId);
            BroadcastImagePullProgress(
                hubContext,
                pullId,
                imageName,
                "拉取中",
                aggregator.OverallProgress,
                $"{aggregator.Layers.Count} 个层 · {status}",
                layer).Wait();
        });
    }

    /// <summary>
    /// 广播镜像推送进度给所有客户端
    /// </summary>
    public static async Task BroadcastImagePushProgress(IHubContext<DockerPanelHub> hubContext, string pushId, string imageName, string step, int progress, string? detail = null)
    {
        await hubContext.Clients.All.SendAsync("ImagePushProgress", new ImagePushProgressMessage
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
        await hubContext.Clients.All.SendAsync("ImageBuildProgress", new ImageBuildProgressMessage
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