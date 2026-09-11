using Docker.DotNet;
using Docker.DotNet.Models;
using DockerPanel.API.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DockerPanel.API.Services;

/// <summary>
/// Docker 事件监听服务 - 按节点订阅 Docker daemon 的容器/镜像事件流，
/// 变化时通过 SignalR 推送给订阅了对应节点的客户端，替代前端定时轮询全量刷新。
/// 只有存在订阅的节点才会建立事件监听，订阅消失后监听自动停止。
/// </summary>
public class DockerEventService : IHostedService
{
    /// <summary>
    /// 会导致容器列表变化的容器事件
    /// </summary>
    private static readonly HashSet<string> ContainerActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "create", "start", "stop", "die", "restart", "kill",
        "pause", "unpause", "rename", "destroy", "remove", "oom"
    };

    /// <summary>
    /// 会导致镜像列表变化的镜像事件
    /// </summary>
    private static readonly HashSet<string> ImageActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "pull", "delete", "tag", "untag", "save", "load", "import", "prune", "push"
    };

    private static readonly TimeSpan ReconcileInterval = TimeSpan.FromSeconds(2);

    private readonly ILogger<DockerEventService> _logger;
    private readonly IHubContext<DockerPanelHub> _hubContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly CancellationTokenSource _stopCts = new();
    private Task? _managerTask;

    // 每个有订阅的节点一个事件监听器
    private readonly ConcurrentDictionary<string, NodeEventListener> _listeners = new(StringComparer.OrdinalIgnoreCase);

    public DockerEventService(
        ILogger<DockerEventService> logger,
        IHubContext<DockerPanelHub> hubContext,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _hubContext = hubContext;
        _serviceProvider = serviceProvider;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _managerTask = Task.Run(() => RunManagerAsync(_stopCts.Token), CancellationToken.None);
        _logger.LogInformation("Docker 事件监听服务已启动（按节点按需监听）");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("正在停止 Docker 事件监听服务...");
        _stopCts.Cancel();

        foreach (var listener in _listeners.Values)
        {
            listener.Dispose();
        }
        _listeners.Clear();

        if (_managerTask != null)
        {
            try
            {
                await _managerTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "等待 Docker 事件监听服务停止超时或失败");
            }
        }
        _logger.LogInformation("Docker 事件监听服务已停止");
    }

    /// <summary>
    /// 管理循环：按订阅情况启动/停止各节点的事件监听
    /// </summary>
    private async Task RunManagerAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                ReconcileListeners();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "协调 Docker 事件监听器失败");
            }

            try
            {
                await Task.Delay(ReconcileInterval, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void ReconcileListeners()
    {
        // 有容器或镜像列表订阅的节点才需要事件监听
        var wanted = DockerPanelHub.GetSubscribedNodeIds("containers")
            .Union(DockerPanelHub.GetSubscribedNodeIds("images"), StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 停止已无订阅的节点监听器
        foreach (var key in _listeners.Keys)
        {
            if (!wanted.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                if (_listeners.TryRemove(key, out var listener))
                {
                    listener.Dispose();
                    _logger.LogInformation("节点 {NodeId} 已无相关订阅，停止 Docker 事件监听", key);
                }
            }
        }

        // 启动新订阅节点的监听器
        foreach (var nodeId in wanted)
        {
            _listeners.GetOrAdd(nodeId, id =>
            {
                _logger.LogInformation("开始监听节点 {NodeId} 的 Docker 事件", id);
                return new NodeEventListener(id, _serviceProvider, _hubContext, _logger);
            });
        }
    }

    /// <summary>
    /// 单个节点的 Docker 事件监听器：独立的事件流、脏标记、节流推送与断线重连
    /// </summary>
    private sealed class NodeEventListener : IDisposable
    {
        private static readonly TimeSpan DebounceDelay = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan MaxReconnectDelay = TimeSpan.FromSeconds(30);

        private readonly string _nodeId;
        private readonly IServiceProvider _serviceProvider;
        private readonly IHubContext<DockerPanelHub> _hubContext;
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _task;

        // 脏标记：事件到来时置位，节流窗口结束后统一推送一次
        private int _containersDirty;
        private int _imagesDirty;
        private int _flushScheduled;
        private readonly SemaphoreSlim _flushLock = new(1, 1);
        private TimeSpan _reconnectDelay = TimeSpan.FromSeconds(2);

        public NodeEventListener(string nodeId, IServiceProvider serviceProvider, IHubContext<DockerPanelHub> hubContext, ILogger logger)
        {
            _nodeId = nodeId;
            _serviceProvider = serviceProvider;
            _hubContext = hubContext;
            _logger = logger;
            _task = Task.Run(RunAsync);
            // 观察任务异常，避免未观察异常
            _ = _task.ContinueWith(t => _logger.LogDebug(t.Exception, "节点 {NodeId} 事件监听任务异常结束", _nodeId),
                TaskContinuationOptions.OnlyOnFaulted);
        }

        /// <summary>"default" 交给 DockerEngine 解析默认节点</summary>
        private string? EngineNodeId => string.Equals(_nodeId, "default", StringComparison.OrdinalIgnoreCase) ? null : _nodeId;

        private async Task RunAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var engine = scope.ServiceProvider.GetService<IContainerEngine>() as DockerEngine;

                    if (engine == null || !await engine.IsAvailableAsync(EngineNodeId))
                    {
                        _logger.LogDebug("节点 {NodeId} Docker 引擎不可用，{Delay} 后重试监听", _nodeId, _reconnectDelay);
                        await DelayReconnect();
                        continue;
                    }

                    var client = await engine.GetClientAsync(EngineNodeId);
                    if (client == null)
                    {
                        await DelayReconnect();
                        continue;
                    }

                    var filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        ["type"] = new Dictionary<string, bool>
                        {
                            ["container"] = true,
                            ["image"] = true
                        }
                    };

                    // 连接成功立即同步一次，补齐事件流建立前的状态变化
                    await FlushAsync();

                    var progress = new Progress<Message>(OnDockerEvent);
                    await client.System.MonitorEventsAsync(
                        new ContainerEventsParameters { Filters = filters },
                        progress,
                        _cts.Token);

                    // 事件流正常结束（正常情况下不会走到这里），重置退避并重连
                    _reconnectDelay = TimeSpan.FromSeconds(2);
                }
                catch (OperationCanceledException) when (_cts.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "节点 {NodeId} Docker 事件流中断，{Delay} 后重连", _nodeId, _reconnectDelay);
                    await DelayReconnect();
                }
            }
        }

        private void OnDockerEvent(Message message)
        {
            if (message == null || string.IsNullOrEmpty(message.Type) || string.IsNullOrEmpty(message.Action))
            {
                return;
            }

            var containersChanged = message.Type == "container" && ContainerActions.Contains(message.Action);
            var imagesChanged = message.Type == "image" && ImageActions.Contains(message.Action);

            if (!containersChanged && !imagesChanged)
            {
                return;
            }

            if (containersChanged) Interlocked.Exchange(ref _containersDirty, 1);
            if (imagesChanged) Interlocked.Exchange(ref _imagesDirty, 1);

            ScheduleFlush();
        }

        /// <summary>
        /// 节流调度：1 秒窗口内最多安排一次刷新，避免 compose 等批量事件反复推送
        /// </summary>
        private void ScheduleFlush()
        {
            if (Interlocked.Exchange(ref _flushScheduled, 1) == 1)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(DebounceDelay, _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                Interlocked.Exchange(ref _flushScheduled, 0);
                await FlushAsync();
            });
        }

        /// <summary>
        /// 将脏标记对应的列表重新拉取并推送给订阅了该节点的客户端
        /// </summary>
        private async Task FlushAsync()
        {
            await _flushLock.WaitAsync();
            try
            {
                var containersDirty = Interlocked.Exchange(ref _containersDirty, 0);
                var imagesDirty = Interlocked.Exchange(ref _imagesDirty, 0);

                if (containersDirty == 0 && imagesDirty == 0)
                {
                    return;
                }

                using var scope = _serviceProvider.CreateScope();

                if (containersDirty == 1 && DockerPanelHub.HasSubscription($"containers:{_nodeId}"))
                {
                    var containerService = scope.ServiceProvider.GetService<IContainerService>();
                    if (containerService != null)
                    {
                        var containers = (await containerService.GetContainersAsync(EngineNodeId, all: true)).ToList();
                        await DockerPanelHub.BroadcastContainerUpdate(_hubContext, containers, _nodeId);
                        _logger.LogDebug("已推送节点 {NodeId} 容器列表更新: {Count} 个容器", _nodeId, containers.Count);
                    }
                }

                if (imagesDirty == 1 && DockerPanelHub.HasSubscription($"images:{_nodeId}"))
                {
                    var imageService = scope.ServiceProvider.GetService<IImageService>();
                    if (imageService != null)
                    {
                        var images = (await imageService.GetImagesAsync(EngineNodeId)).ToList();
                        await DockerPanelHub.BroadcastImageUpdate(_hubContext, images, _nodeId);
                        _logger.LogDebug("已推送节点 {NodeId} 镜像列表更新: {Count} 个镜像", _nodeId, images.Count);
                    }
                }
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                // 监听器停止，忽略
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "推送节点 {NodeId} 容器/镜像列表更新失败", _nodeId);
            }
            finally
            {
                _flushLock.Release();
            }
        }

        private async Task DelayReconnect()
        {
            try
            {
                await Task.Delay(_reconnectDelay, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // 指数退避，上限 30 秒
            _reconnectDelay = _reconnectDelay < MaxReconnectDelay
                ? _reconnectDelay * 2
                : MaxReconnectDelay;
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
