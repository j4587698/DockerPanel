using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet.Models;

namespace DockerPanel.API.Services;

/// <summary>
/// 实时数据推送服务 - 优化版本，减少 Docker API 压力
/// </summary>
public class RealTimeDataPushService : IHostedService
{
    private readonly ILogger<RealTimeDataPushService> _logger;
    private readonly IHubContext<DockerPanel.API.Hubs.DockerPanelHub> _hubContext;
    private readonly IServiceProvider _serviceProvider;
    private Timer? _timer;
    private Timer? _systemInfoTimer;
    private DateTime _lastMetricsPushAt = DateTime.MinValue;

    // 缓存系统信息（不频繁变化），按节点缓存
    private sealed class NodeSystemInfo
    {
        public int NCpu = 1;
        public long MemTotal;
        public DateTime UpdatedAt = DateTime.MinValue;
    }
    private readonly ConcurrentDictionary<string, NodeSystemInfo> _systemInfoByNode = new(StringComparer.OrdinalIgnoreCase);

    // 网络统计缓存，按节点隔离
    private readonly ConcurrentDictionary<string, long> _lastNetworkStats = new();

    // 防止重入和停止控制
    private int _isRunning = 0;
    private int _isStopping = 0;

    /// <summary>订阅键中的节点 ID 规范化（"default" → 交给 DockerEngine 解析默认节点）</summary>
    private static string? EngineNodeId(string nodeId) =>
        string.Equals(nodeId, "default", StringComparison.OrdinalIgnoreCase) ? null : nodeId;

    public RealTimeDataPushService(
        ILogger<RealTimeDataPushService> logger,
        IHubContext<DockerPanel.API.Hubs.DockerPanelHub> hubContext,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _hubContext = hubContext;
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("实时数据推送服务已启动");

        // 每 30 秒刷新一次各已订阅节点的系统信息
        _systemInfoTimer = new Timer(async _ => await RefreshSystemInfoAsync(), null,
            TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));

        // 每 5 秒检查一次订阅和系统设置，实际推送频率由监控采集间隔控制
        _timer = new Timer(PushData, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5));

        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("正在停止实时数据推送服务...");

        // 标记正在停止
        Interlocked.Exchange(ref _isStopping, 1);

        // 释放定时器，不再触发新的推送
        _timer?.Dispose();
        _systemInfoTimer?.Dispose();

        // 等待当前正在执行的推送完成（最多等待5秒）
        int waitCount = 0;
        while (Interlocked.CompareExchange(ref _isRunning, 0, 0) == 1 && waitCount < 50)
        {
            await Task.Delay(100, cancellationToken);
            waitCount++;
        }

        _logger.LogInformation("实时数据推送服务已停止");
    }

    private async Task RefreshSystemInfoAsync()
    {
        // 如果正在停止，直接返回
        if (Interlocked.CompareExchange(ref _isStopping, 0, 0) == 1)
        {
            return;
        }

        // 只刷新有订阅的节点
        var nodeIds = DockerPanel.API.Hubs.DockerPanelHub.GetSubscribedNodeIds("systemstats")
            .Union(DockerPanel.API.Hubs.DockerPanelHub.GetSubscribedNodeIds("containerstats"), StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (nodeIds.Count == 0) return;

        foreach (var nodeId in nodeIds)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dockerEngine = scope.ServiceProvider.GetService<IContainerEngine>() as DockerEngine;

                if (dockerEngine == null || !await dockerEngine.IsAvailableAsync(EngineNodeId(nodeId))) continue;

                var dockerClient = await dockerEngine.GetClientAsync(EngineNodeId(nodeId));
                if (dockerClient == null) continue;

                var systemInfo = await dockerClient.System.GetSystemInfoAsync();
                var info = _systemInfoByNode.GetOrAdd(nodeId, _ => new NodeSystemInfo());
                info.NCpu = (int)(systemInfo.NCPU > 0 ? systemInfo.NCPU : 1);
                info.MemTotal = (long)systemInfo.MemTotal;
                info.UpdatedAt = DateTime.UtcNow;

                _logger.LogDebug("系统信息已刷新: 节点={NodeId}, CPU={Cpu}, 内存={Mem}", nodeId, info.NCpu, FormatBytes(info.MemTotal));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "刷新系统信息失败: 节点 {NodeId}", nodeId);
            }
        }
    }

    private async void PushData(object? state)
    {
        // 如果正在停止，直接返回
        if (Interlocked.CompareExchange(ref _isStopping, 0, 0) == 1)
        {
            return;
        }

        // 防止重入 - 如果上一次还没执行完，跳过本次
        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
        {
            return;
        }

        try
        {
            // 收集当前有订阅的节点（systemstats 或 containerstats）
            var nodeIds = DockerPanel.API.Hubs.DockerPanelHub.GetSubscribedNodeIds("systemstats")
                .Union(DockerPanel.API.Hubs.DockerPanelHub.GetSubscribedNodeIds("containerstats"), StringComparer.OrdinalIgnoreCase)
                .ToList();

            _logger.LogDebug("推送检查 - 订阅节点数: {Count}", nodeIds.Count);

            if (nodeIds.Count == 0)
            {
                return;
            }

            var pushSettings = await GetRealtimePushSettingsAsync();
            if (!pushSettings.EnableMetrics)
            {
                return;
            }

            if (DateTime.UtcNow - _lastMetricsPushAt < pushSettings.PushInterval)
            {
                return;
            }

            _lastMetricsPushAt = DateTime.UtcNow;

            foreach (var nodeId in nodeIds)
            {
                await PushContainerStats(nodeId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "推送实时数据时发生错误");
        }
        finally
        {
            Interlocked.Exchange(ref _isRunning, 0);
        }
    }

    private async Task PushContainerStats(string nodeId)
    {
        // 如果正在停止，直接返回
        if (Interlocked.CompareExchange(ref _isStopping, 0, 0) == 1)
        {
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var dockerEngine = scope.ServiceProvider.GetService<IContainerEngine>() as DockerEngine;

        var engineNodeId = EngineNodeId(nodeId);

        if (dockerEngine == null || !await dockerEngine.IsAvailableAsync(engineNodeId)) return;

        var dockerClient = await dockerEngine.GetClientAsync(engineNodeId);
        if (dockerClient == null) return;

        // 节点级系统信息缓存（NCpu/内存总量）
        var nodeInfo = _systemInfoByNode.GetOrAdd(nodeId, _ => new NodeSystemInfo());

        try
        {
            // 获取容器列表
            var containers = await dockerClient.Containers.ListContainersAsync(
                new ContainersListParameters { All = true });
            
            var runningContainers = containers.Where(c => c.State == "running").ToList();
            
            if (runningContainers.Count == 0)
            {
                // 没有运行中的容器，推送基础数据
                await PushEmptyStats(nodeId, containers.Count, nodeInfo);
                return;
            }

            // 系统级别统计
            double totalCpuPercent = 0;
            long totalMemUsed = 0;
            long networkRxSnapshot = 0;
            long networkTxSnapshot = 0;

            var containerStatsList = new List<Hubs.ContainerStatsPushMessage>();

            // 并行获取容器统计：双采样统计每个容器约需 1-1.5s，
            // 串行时推送间隔会被拖长为 容器数 × 采样耗时（远大于配置的 5s），
            // 用信号量限制并发避免压垮 Docker daemon。
            const int maxConcurrent = 5;
            using var throttle = new SemaphoreSlim(maxConcurrent);

            var statsTasks = runningContainers.Select(CollectContainerStatsAsync);

            async Task<(Hubs.ContainerStatsPushMessage? Message, double Cpu, long Mem, long Rx, long Tx)> CollectContainerStatsAsync(Docker.DotNet.Models.ContainerListResponse container)
            {
                await throttle.WaitAsync();
                try
                {
                    // 如果正在停止，提前返回
                    if (Interlocked.CompareExchange(ref _isStopping, 0, 0) == 1)
                    {
                        return (Message: null, Cpu: 0d, Mem: 0L, Rx: 0L, Tx: 0L);
                    }

                    // 复用 DockerEngine 已实现的双采样统计（Stream=true，拿到两次采样即取消），
                    // 避免直接调用 Enhanced 版 stats 流（等待流关闭导致超时全部失败）
                    var stats = await dockerEngine.GetContainerStatsAsync(container.ID, engineNodeId);
                    if (stats == null)
                    {
                        return (Message: null, Cpu: 0d, Mem: 0L, Rx: 0L, Tx: 0L);
                    }

                    double containerCpu = Math.Min(stats.CpuStats?.PercentCpu ?? 0, 100);

                    // 网络统计列表
                    var networkList = new List<Hubs.ContainerStatsPushNetworkItem>();
                    long rx = 0, tx = 0;
                    if (stats.Networks != null)
                    {
                        foreach (var network in stats.Networks)
                        {
                            rx += network.RxBytes;
                            tx += network.TxBytes;
                            networkList.Add(new Hubs.ContainerStatsPushNetworkItem
                            {
                                Name = network.Name,
                                RxBytes = network.RxBytes,
                                TxBytes = network.TxBytes,
                                RxPackets = network.RxPackets,
                                TxPackets = network.TxPackets
                            });
                        }
                    }

                    return (Message: new Hubs.ContainerStatsPushMessage
                    {
                        ContainerId = container.ID,
                        Name = container.Names?.FirstOrDefault()?.TrimStart('/') ?? "unknown",
                        CpuStats = new Hubs.ContainerStatsPushCpu
                        {
                            PercentCpu = Math.Round(containerCpu, 2),
                            CpuUsage = stats.CpuStats?.CpuUsage ?? 0,
                            SystemUsage = stats.CpuStats?.SystemUsage ?? 0
                        },
                        MemoryStats = new Hubs.ContainerStatsPushMemory
                        {
                            Usage = stats.MemoryStats?.Usage ?? 0,
                            Limit = stats.MemoryStats?.Limit ?? 0,
                            PercentMemory = stats.MemoryStats?.PercentMemory ?? 0
                        },
                        Networks = networkList
                    }, Cpu: containerCpu, Mem: stats.MemoryStats?.Usage ?? 0, Rx: rx, Tx: tx);
                }
                catch (Exception ex)
                {
                    // 这里一旦批量失败，前端所有容器的 CPU/内存/网络都会显示为 0，
                    // 必须至少以 Warning 级别输出，否则生产环境完全无法定位。
                    _logger.LogWarning(ex, "获取容器 {Id} 统计失败", container.ID[..12]);
                    return (Message: null, Cpu: 0d, Mem: 0L, Rx: 0L, Tx: 0L);
                }
                finally
                {
                    throttle.Release();
                }
            }

            foreach (var result in await Task.WhenAll(statsTasks))
            {
                if (result.Message == null) continue;

                totalCpuPercent += result.Cpu;
                totalMemUsed += result.Mem;
                networkRxSnapshot += result.Rx;
                networkTxSnapshot += result.Tx;
                containerStatsList.Add(result.Message);
            }

            // 计算网络速度：仍需要本轮累计快照与上一轮快照做差，但不再对外展示“总流量”
            var (rxSpeed, txSpeed) = CalculateNetworkSpeed(nodeId, networkRxSnapshot, networkTxSnapshot);

            // 推送系统统计（只推给订阅了该节点的连接）
            var systemStats = new Hubs.DockerStatsPushMessage
            {
                Docker = new Hubs.DockerStatsPushDocker
                {
                    Status = "running",
                    NCPU = nodeInfo.NCpu
                },
                Containers = new Hubs.DockerStatsPushContainers
                {
                    Running = runningContainers.Count,
                    Stopped = containers.Count - runningContainers.Count,
                    Total = containers.Count
                },
                Resources = new Hubs.DockerStatsPushResources
                {
                    CpuUsagePercent = Math.Round(SafeDouble(totalCpuPercent), 2),
                    MemoryUsed = totalMemUsed,
                    MemoryLimit = nodeInfo.MemTotal,
                    MemoryPercent = nodeInfo.MemTotal > 0
                        ? Math.Round(SafeDouble((double)totalMemUsed / nodeInfo.MemTotal * 100), 2)
                        : 0,
                    MemoryUsedFormatted = FormatBytes(totalMemUsed),
                    MemoryLimitFormatted = FormatBytes(nodeInfo.MemTotal)
                },
                Network = new Hubs.DockerStatsPushNetwork
                {
                    RxBytesPerSec = rxSpeed,
                    TxBytesPerSec = txSpeed,
                    RxSpeedFormatted = FormatBytesPerSec(rxSpeed),
                    TxSpeedFormatted = FormatBytesPerSec(txSpeed)
                },
                Timestamp = DateTime.UtcNow
            };

            foreach (var connectionId in DockerPanel.API.Hubs.DockerPanelHub.GetConnectionsFor($"systemstats:{nodeId}"))
            {
                await _hubContext.Clients.Client(connectionId).SendAsync("DockerStatsUpdated", systemStats);
            }

            if (containerStatsList.Count > 0)
            {
                foreach (var connectionId in DockerPanel.API.Hubs.DockerPanelHub.GetConnectionsFor($"containerstats:{nodeId}"))
                {
                    await _hubContext.Clients.Client(connectionId).SendAsync("ContainerStatsUpdated", containerStatsList);
                }
                _logger.LogDebug("推送容器统计: 节点 {NodeId}, {Count} 个容器", nodeId, containerStatsList.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "推送统计数据失败: 节点 {NodeId}", nodeId);
        }
    }

    private async Task<RealtimePushSettings> GetRealtimePushSettingsAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var settingsService = scope.ServiceProvider.GetService<ISettingsService>();
            var settings = settingsService == null ? null : await settingsService.GetSettingsAsync();
            if (settings == null)
            {
                return new RealtimePushSettings(true, TimeSpan.FromSeconds(5));
            }

            var seconds = Math.Clamp(settings.Monitoring.MetricsCollectionIntervalSeconds <= 0
                ? 5
                : settings.Monitoring.MetricsCollectionIntervalSeconds, 5, 3600);

            return new RealtimePushSettings(settings.Monitoring.EnableMetrics, TimeSpan.FromSeconds(seconds));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取实时推送设置失败，使用默认 5 秒");
            return new RealtimePushSettings(true, TimeSpan.FromSeconds(5));
        }
    }

    private sealed record RealtimePushSettings(bool EnableMetrics, TimeSpan PushInterval);

    private async Task PushEmptyStats(string nodeId, int totalContainers, NodeSystemInfo nodeInfo)
    {
        var systemStats = new Hubs.DockerStatsPushMessage
        {
            Docker = new Hubs.DockerStatsPushDocker { Status = "running", NCPU = nodeInfo.NCpu },
            Containers = new Hubs.DockerStatsPushContainers { Running = 0, Stopped = totalContainers, Total = totalContainers },
            Resources = new Hubs.DockerStatsPushResources
            {
                CpuUsagePercent = 0.0,
                MemoryUsed = 0L,
                MemoryLimit = nodeInfo.MemTotal,
                MemoryPercent = 0.0,
                MemoryUsedFormatted = "0 B",
                MemoryLimitFormatted = FormatBytes(nodeInfo.MemTotal)
            },
            Network = new Hubs.DockerStatsPushNetwork
            {
                RxBytesPerSec = 0L,
                TxBytesPerSec = 0L,
                RxSpeedFormatted = "0 B/s",
                TxSpeedFormatted = "0 B/s"
            },
            Timestamp = DateTime.UtcNow
        };

        foreach (var connectionId in DockerPanel.API.Hubs.DockerPanelHub.GetConnectionsFor($"systemstats:{nodeId}"))
        {
            await _hubContext.Clients.Client(connectionId).SendAsync("DockerStatsUpdated", systemStats);
        }
    }

    private (long rxSpeed, long txSpeed) CalculateNetworkSpeed(string nodeId, long totalRx, long totalTx)
    {
        var currentTicks = DateTime.UtcNow.Ticks;
        long rxSpeed = 0, txSpeed = 0;

        if (_lastNetworkStats.TryGetValue($"{nodeId}:rx", out var lastRx) &&
            _lastNetworkStats.TryGetValue($"{nodeId}:tx", out var lastTx) &&
            _lastNetworkStats.TryGetValue($"{nodeId}:time", out var lastTime))
        {
            var timeDiffSeconds = TimeSpan.FromTicks(currentTicks - lastTime).TotalSeconds;
            if (timeDiffSeconds > 0)
            {
                if (totalRx >= lastRx) rxSpeed = (long)((totalRx - lastRx) / timeDiffSeconds);
                if (totalTx >= lastTx) txSpeed = (long)((totalTx - lastTx) / timeDiffSeconds);
            }
        }

        _lastNetworkStats[$"{nodeId}:rx"] = totalRx;
        _lastNetworkStats[$"{nodeId}:tx"] = totalTx;
        _lastNetworkStats[$"{nodeId}:time"] = currentTicks;

        return (rxSpeed, txSpeed);
    }

    private static double SafeDouble(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return 0;
        return Math.Min(value, 100);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private static string FormatBytesPerSec(double bytesPerSec)
    {
        if (bytesPerSec <= 0) return "0 B/s";
        string[] sizes = { "B/s", "KB/s", "MB/s", "GB/s" };
        double len = bytesPerSec;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}