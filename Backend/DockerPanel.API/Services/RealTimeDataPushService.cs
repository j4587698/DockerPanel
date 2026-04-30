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

    // 缓存系统信息（不频繁变化）
    private int _systemNCpu = 1;
    private long _systemMemTotal;
    private DateTime _lastSystemInfoUpdate = DateTime.MinValue;

    // 网络统计缓存
    private readonly ConcurrentDictionary<string, long> _lastNetworkStats = new();

    // 防止重入和停止控制
    private int _isRunning = 0;
    private int _isStopping = 0;

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
        
        // 启动时立即获取一次系统信息
        await RefreshSystemInfoAsync();
        
        // 每 30 秒刷新一次系统信息
        _systemInfoTimer = new Timer(async _ => await RefreshSystemInfoAsync(), null, 
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        
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

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dockerEngine = scope.ServiceProvider.GetService<IContainerEngine>() as DockerEngine;

            if (dockerEngine == null || !await dockerEngine.IsAvailableAsync()) return;

            var dockerClient = await dockerEngine.GetClientAsync();
            if (dockerClient == null) return;

            var systemInfo = await dockerClient.System.GetSystemInfoAsync();
            _systemNCpu = (int)(systemInfo.NCPU > 0 ? systemInfo.NCPU : 1);
            _systemMemTotal = (long)systemInfo.MemTotal;
            _lastSystemInfoUpdate = DateTime.UtcNow;

            _logger.LogDebug("系统信息已刷新: CPU={Cpu}, 内存={Mem}", _systemNCpu, FormatBytes(_systemMemTotal));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "刷新系统信息失败");
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
            // 只有当有客户端订阅了系统统计或容器统计时才推送
            bool hasSystemStatsSub = DockerPanel.API.Hubs.DockerPanelHub.HasSubscription("systemstats");
            bool hasContainerStatsSub = DockerPanel.API.Hubs.DockerPanelHub.HasSubscription("containerstats");
            int subCount = DockerPanel.API.Hubs.DockerPanelHub.GetSubscriptionCount();

            _logger.LogDebug("推送检查 - 订阅数: {SubCount}, systemstats: {SysSub}, containerstats: {ContSub}",
                subCount, hasSystemStatsSub, hasContainerStatsSub);

            if (!hasSystemStatsSub && !hasContainerStatsSub)
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

            await PushContainerStats();
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

    private async Task PushContainerStats()
    {
        // 如果正在停止，直接返回
        if (Interlocked.CompareExchange(ref _isStopping, 0, 0) == 1)
        {
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var dockerEngine = scope.ServiceProvider.GetService<IContainerEngine>() as DockerEngine;

        if (dockerEngine == null || !await dockerEngine.IsAvailableAsync()) return;

        var dockerClient = await dockerEngine.GetClientAsync();
        if (dockerClient == null) return;
        
        try
        {
            // 获取容器列表
            var containers = await dockerClient.Containers.ListContainersAsync(
                new ContainersListParameters { All = true });
            
            var runningContainers = containers.Where(c => c.State == "running").ToList();
            
            if (runningContainers.Count == 0)
            {
                // 没有运行中的容器，推送基础数据
                await PushEmptyStats(containers.Count);
                return;
            }

            // 系统级别统计
            double totalCpuPercent = 0;
            long totalMemUsed = 0;
            long networkRxSnapshot = 0;
            long networkTxSnapshot = 0;
            
            var containerStatsList = new List<object>();

            // 串行获取容器统计（避免并行给 Docker daemon 造成压力）
            foreach (var container in runningContainers)
            {
                // 如果正在停止，提前退出循环
                if (Interlocked.CompareExchange(ref _isStopping, 0, 0) == 1)
                {
                    return;
                }

                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var tcs = new TaskCompletionSource<ContainerStatsResponse>();
                    
                    var progress = new Progress<ContainerStatsResponse>(stats =>
                    {
                        tcs.TrySetResult(stats);
                    });
                    
                    var statsTask = dockerClient.Containers.GetContainerStatsAsync(
                        container.ID, 
                        new ContainerStatsParameters { Stream = false }, 
                        progress, 
                        cts.Token);
                    
                    // 等待任务完成，同时等待结果
                    await Task.WhenAll(statsTask, tcs.Task);
                    
                    var stats = await tcs.Task;
                    if (stats == null) continue;
                    double containerCpu = 0;
                    long containerMemory = 0;

                    // CPU 计算
                    if (stats.CPUStats != null && stats.PreCPUStats != null)
                    {
                        var cpuDelta = stats.CPUStats.CPUUsage.TotalUsage - stats.PreCPUStats.CPUUsage.TotalUsage;
                        var systemDelta = stats.CPUStats.SystemUsage.GetValueOrDefault() - stats.PreCPUStats.SystemUsage.GetValueOrDefault();

                        if (systemDelta > 0 && cpuDelta > 0)
                        {
                            var onlineCpus = stats.CPUStats.OnlineCPUs.GetValueOrDefault(1);
                            containerCpu = Math.Min((double)cpuDelta / (double)systemDelta * onlineCpus * 100.0, 100);
                            totalCpuPercent += containerCpu;
                        }
                    }

                    // 内存计算
                    if (stats.MemoryStats != null && stats.MemoryStats.Usage.GetValueOrDefault() > 0)
                    {
                        var cache = 0UL;
                        if (stats.MemoryStats.Stats != null)
                        {
                            if (stats.MemoryStats.Stats.TryGetValue("cache", out var cacheVal))
                                cache = cacheVal;
                            else if (stats.MemoryStats.Stats.TryGetValue("inactive_file", out var inactiveFile))
                                cache = inactiveFile;
                        }
                        
                        containerMemory = (long)Math.Max(0, (long)stats.MemoryStats.Usage.GetValueOrDefault() - (long)cache);
                        totalMemUsed += containerMemory;
                    }

                    // 网络统计
                    if (stats.Networks != null)
                    {
                        foreach (var network in stats.Networks)
                        {
                            networkRxSnapshot += (long)network.Value.RxBytes;
                            networkTxSnapshot += (long)network.Value.TxBytes;
                        }
                    }

                    // 网络统计列表
                    var networkList = new List<object>();
                    if (stats.Networks != null)
                    {
                        foreach (var network in stats.Networks)
                        {
                            networkList.Add(new
                            {
                                name = network.Key,
                                rxBytes = (long)network.Value.RxBytes,
                                txBytes = (long)network.Value.TxBytes,
                                rxPackets = (long)network.Value.RxPackets,
                                txPackets = (long)network.Value.TxPackets
                            });
                        }
                    }

                    containerStatsList.Add(new
                    {
                        containerId = container.ID,
                        name = container.Names?.FirstOrDefault()?.TrimStart('/') ?? "unknown",
                        cpuStats = new
                        {
                            percentCpu = Math.Round(containerCpu, 2),
                            cpuUsage = (long)(stats.CPUStats?.CPUUsage?.TotalUsage ?? 0),
                            systemUsage = (long)(stats.CPUStats?.SystemUsage ?? 0)
                        },
                        memoryStats = new
                        {
                            usage = containerMemory,
                            limit = (long)(stats.MemoryStats?.Limit ?? 0),
                            percentMemory = stats.MemoryStats != null && stats.MemoryStats.Limit > 0 
                                ? Math.Round((double)containerMemory / (long)stats.MemoryStats.Limit * 100, 2) 
                                : 0
                        },
                        networks = networkList
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("获取容器 {Id} 统计失败: {Msg}", container.ID[..12], ex.Message);
                }
            }

            // 计算网络速度：仍需要本轮累计快照与上一轮快照做差，但不再对外展示“总流量”
            var (rxSpeed, txSpeed) = CalculateNetworkSpeed(networkRxSnapshot, networkTxSnapshot);

            // 推送系统统计
            var systemStats = new
            {
                Docker = new
                {
                    Status = "running",
                    NCPU = _systemNCpu
                },
                Containers = new
                {
                    Running = runningContainers.Count,
                    Stopped = containers.Count - runningContainers.Count,
                    Total = containers.Count
                },
                Resources = new
                {
                    CpuUsagePercent = Math.Round(SafeDouble(totalCpuPercent), 2),
                    MemoryUsed = totalMemUsed,
                    MemoryLimit = _systemMemTotal,
                    MemoryPercent = _systemMemTotal > 0 
                        ? Math.Round(SafeDouble((double)totalMemUsed / _systemMemTotal * 100), 2) 
                        : 0,
                    MemoryUsedFormatted = FormatBytes(totalMemUsed),
                    MemoryLimitFormatted = FormatBytes(_systemMemTotal)
                },
                Network = new
                {
                    RxBytesPerSec = rxSpeed,
                    TxBytesPerSec = txSpeed,
                    RxSpeedFormatted = FormatBytesPerSec(rxSpeed),
                    TxSpeedFormatted = FormatBytesPerSec(txSpeed)
                },
                Timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients.All.SendAsync("DockerStatsUpdated", systemStats);
            
            if (containerStatsList.Count > 0)
            {
                await _hubContext.Clients.All.SendAsync("ContainerStatsUpdated", containerStatsList);
                _logger.LogDebug("推送容器统计: {Count} 个容器", containerStatsList.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "推送统计数据失败");
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

    private async Task PushEmptyStats(int totalContainers)
    {
        var systemStats = new
        {
            Docker = new { Status = "running", NCPU = _systemNCpu },
            Containers = new { Running = 0, Stopped = totalContainers, Total = totalContainers },
            Resources = new
            {
                CpuUsagePercent = 0.0,
                MemoryUsed = 0L,
                MemoryLimit = _systemMemTotal,
                MemoryPercent = 0.0,
                MemoryUsedFormatted = "0 B",
                MemoryLimitFormatted = FormatBytes(_systemMemTotal)
            },
            Network = new
            {
                RxBytesPerSec = 0L,
                TxBytesPerSec = 0L,
                RxSpeedFormatted = "0 B/s",
                TxSpeedFormatted = "0 B/s"
            },
            Timestamp = DateTime.UtcNow
        };

        await _hubContext.Clients.All.SendAsync("DockerStatsUpdated", systemStats);
    }

    private (long rxSpeed, long txSpeed) CalculateNetworkSpeed(long totalRx, long totalTx)
    {
        var currentTicks = DateTime.UtcNow.Ticks;
        long rxSpeed = 0, txSpeed = 0;
        
        if (_lastNetworkStats.TryGetValue("rx", out var lastRx) && 
            _lastNetworkStats.TryGetValue("tx", out var lastTx) && 
            _lastNetworkStats.TryGetValue("time", out var lastTime))
        {
            var timeDiffSeconds = TimeSpan.FromTicks(currentTicks - lastTime).TotalSeconds;
            if (timeDiffSeconds > 0)
            {
                if (totalRx >= lastRx) rxSpeed = (long)((totalRx - lastRx) / timeDiffSeconds);
                if (totalTx >= lastTx) txSpeed = (long)((totalTx - lastTx) / timeDiffSeconds);
            }
        }
        
        _lastNetworkStats["rx"] = totalRx;
        _lastNetworkStats["tx"] = totalTx;
        _lastNetworkStats["time"] = currentTicks;
        
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