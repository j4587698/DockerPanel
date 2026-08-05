using Docker.DotNet;
using Docker.DotNet.Models;
using DockerPanel.API.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DockerPanel.API.Services;

/// <summary>
/// Docker 事件监听服务 - 订阅 Docker daemon 的容器/镜像事件流，
/// 变化时通过 SignalR 主动推送列表更新，替代前端定时轮询全量刷新。
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

    private readonly ILogger<DockerEventService> _logger;
    private readonly IHubContext<DockerPanelHub> _hubContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly CancellationTokenSource _stopCts = new();
    private Task? _workerTask;

    // 脏标记：事件到来时置位，节流窗口结束后统一推送一次
    private int _containersDirty;
    private int _imagesDirty;
    private int _flushScheduled;
    private readonly SemaphoreSlim _flushLock = new(1, 1);

    private static readonly TimeSpan DebounceDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxReconnectDelay = TimeSpan.FromSeconds(30);
    private TimeSpan _reconnectDelay = TimeSpan.FromSeconds(2);

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
        _workerTask = Task.Run(() => RunEventLoopAsync(_stopCts.Token), CancellationToken.None);
        _logger.LogInformation("Docker 事件监听服务已启动");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("正在停止 Docker 事件监听服务...");
        _stopCts.Cancel();
        if (_workerTask != null)
        {
            try
            {
                await _workerTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "等待 Docker 事件监听服务停止超时或失败");
            }
        }
        _logger.LogInformation("Docker 事件监听服务已停止");
    }

    private async Task RunEventLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var engine = scope.ServiceProvider.GetService<IContainerEngine>() as DockerEngine;

                if (engine == null || !await engine.IsAvailableAsync())
                {
                    _logger.LogDebug("Docker 引擎不可用，{Delay} 后重试监听", _reconnectDelay);
                    await DelayReconnect(token);
                    continue;
                }

                var client = await engine.GetClientAsync();
                if (client == null) continue;

                var filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["type"] = new Dictionary<string, bool>
                    {
                        ["container"] = true,
                        ["image"] = true
                    }
                };

                // 连接成功立即同步一次，补齐事件流建立前的状态变化
                await FlushAsync(token);

                var progress = new Progress<Message>(OnDockerEvent);
                await client.System.MonitorEventsAsync(
                    new ContainerEventsParameters { Filters = filters },
                    progress,
                    token);

                // 事件流正常结束（正常情况下不会走到这里），重置退避并重连
                _reconnectDelay = TimeSpan.FromSeconds(2);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Docker 事件流中断，{Delay} 后重连", _reconnectDelay);
                await DelayReconnect(token);
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
                await Task.Delay(DebounceDelay, _stopCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            Interlocked.Exchange(ref _flushScheduled, 0);
            await FlushAsync(_stopCts.Token);
        });
    }

    /// <summary>
    /// 将脏标记对应的列表重新拉取并通过 SignalR 推送给订阅者
    /// </summary>
    private async Task FlushAsync(CancellationToken token)
    {
        await _flushLock.WaitAsync(token);
        try
        {
            var containersDirty = Interlocked.Exchange(ref _containersDirty, 0);
            var imagesDirty = Interlocked.Exchange(ref _imagesDirty, 0);

            if (containersDirty == 0 && imagesDirty == 0)
            {
                return;
            }

            using var scope = _serviceProvider.CreateScope();

            if (containersDirty == 1 && DockerPanelHub.HasSubscription("containers"))
            {
                var containerService = scope.ServiceProvider.GetService<IContainerService>();
                if (containerService != null)
                {
                    var containers = (await containerService.GetContainersAsync(all: true)).ToList();
                    await DockerPanelHub.BroadcastContainerUpdate(_hubContext, containers);
                    _logger.LogDebug("已推送容器列表更新: {Count} 个容器", containers.Count);
                }
            }

            if (imagesDirty == 1 && DockerPanelHub.HasSubscription("images"))
            {
                var imageService = scope.ServiceProvider.GetService<IImageService>();
                if (imageService != null)
                {
                    var images = (await imageService.GetImagesAsync()).ToList();
                    await DockerPanelHub.BroadcastImageUpdate(_hubContext, images);
                    _logger.LogDebug("已推送镜像列表更新: {Count} 个镜像", images.Count);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // 服务停止，忽略
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "推送容器/镜像列表更新失败");
        }
        finally
        {
            _flushLock.Release();
        }
    }

    private async Task DelayReconnect(CancellationToken token)
    {
        try
        {
            await Task.Delay(_reconnectDelay, token);
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
}
