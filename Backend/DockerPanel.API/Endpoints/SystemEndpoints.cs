using System.Reflection;
using Docker.DotNet;
using Docker.DotNet.Models;
using DockerPanel.API.Serialization;
using DockerPanel.API.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// 系统信息 Minimal API 端点（原 SystemController）。
    /// </summary>
    public static class SystemEndpoints
    {
        /// <summary>
        /// 映射系统信息相关路由。
        /// </summary>
        public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/system").RequireAuthorization();

            group.MapGet("info", GetSystemInfo);
            group.MapGet("docker-stats", GetDockerStats);
            group.MapGet("host-metrics", GetHostMetrics);

            return app;
        }

        // 用于计算网络速率的静态变量
        private static long _lastRxBytes = 0;
        private static long _lastTxBytes = 0;
        private static DateTime _lastSampleTime = DateTime.MinValue;
        private static readonly object _sampleLock = new();

        private static IResult GetSystemInfo(ILogger<SettingsEndpoints.LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("获取系统信息");

                var systemInfo = new SystemOsInfoResponse
                {
                    System = new SystemOsInfo
                    {
                        OS = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                        MachineName = Environment.MachineName,
                        ProcessorCount = Environment.ProcessorCount,
                        WorkingDirectory = Environment.CurrentDirectory,
                        FrameworkVersion = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
                    },
                    Runtime = new SystemRuntimeInfo
                    {
                        Version = GetApplicationVersion(),
                        StartTime = System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime()
                    },
                    Timestamp = DateTime.UtcNow
                };

                return TypedResults.Ok(systemInfo);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取系统信息失败");
                return TypedResults.Json(new ApiErrorResponse { Error = "获取系统信息失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static string GetApplicationVersion()
        {
            var assembly = typeof(SystemEndpoints).Assembly;
            return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                   ?? assembly.GetName().Version?.ToString()
                   ?? "unknown";
        }

        private static async Task<IResult> GetDockerStats(DockerEngine dockerEngine, IContainerService containerService, ILocalizationService localization, ILogger<SettingsEndpoints.LoggingTag> logger, string? nodeId = null, bool includeStats = true)
        {
            try
            {
                logger.LogInformation("正在获取 Docker 统计信息...（includeStats={IncludeStats}）", includeStats);

                // 检查 Docker 是否可用
                if (!await dockerEngine.IsAvailableAsync(nodeId))
                {
                    return TypedResults.Ok(new DockerStatsResponse
                    {
                        Status = "Disconnected",
                        Message = localization.GetMessage("system.dockerDisconnected")
                    });
                }

                var dockerClient = await dockerEngine.GetClientAsync(nodeId);
                var versionTask = dockerClient.System.GetVersionAsync();
                var systemInfoTask = dockerClient.System.GetSystemInfoAsync();
                var imagesTask = dockerClient.Images.ListImagesAsync(new ImagesListParameters());

                await Task.WhenAll(versionTask, systemInfoTask, imagesTask);

                var version = versionTask.Result;
                var systemInfo = systemInfoTask.Result;
                var images = imagesTask.Result;

                var runningCount = systemInfo.ContainersRunning;
                var stoppedCount = systemInfo.ContainersStopped;
                var totalCount = systemInfo.Containers;

                var imagesCount = images.Count;
                var totalSize = images.Sum(i => i.Size);

                // 容器资源聚合是较重的采集（每个运行中容器都需要双采样 stats）。
                // 概览页只需要引擎信息/容器数/镜像数时传 includeStats=false，跳过这部分开销。
                var aggregate = await DockerStatsAggregator.CollectAsync(
                    includeStats,
                    async () =>
                    {
                        var containers = await containerService.GetContainersAsync(nodeId, false);
                        return containers
                            .Where(c => c.State == "running")
                            .Select(c => c.Id)
                            .ToList();
                    },
                    async containerId =>
                    {
                        try
                        {
                            var stats = await containerService.GetContainerStatsAsync(containerId, nodeId);
                            if (stats == null) return null;

                            return new ContainerStatsSample(
                                stats.CpuStats?.PercentCpu ?? 0,
                                stats.MemoryStats?.Usage ?? 0,
                                stats.Networks?.Sum(n => n.RxBytes) ?? 0,
                                stats.Networks?.Sum(n => n.TxBytes) ?? 0);
                        }
                        catch
                        {
                            return null;
                        }
                    },
                    100 * systemInfo.NCPU,
                    logger);

                double totalCpuPercent = aggregate.CpuPercent;
                long totalContainerMemoryUsed = aggregate.MemoryUsed;
                long networkRxSnapshot = aggregate.NetworkRxBytes;
                long networkTxSnapshot = aggregate.NetworkTxBytes;

                // 宿主机总内存（从 Docker 系统信息获取）
                long hostMemoryTotal = systemInfo.MemTotal > 0 ? systemInfo.MemTotal : 0;

                // 计算容器内存占用宿主机内存的百分比
                double memoryPercent = hostMemoryTotal > 0 ? (double)totalContainerMemoryUsed / hostMemoryTotal * 100 : 0;

                // 计算网络速率（仅在实际采集容器统计时更新采样基线，
                // 否则 includeStats=false 的请求会污染后续真实速率）
                double rxBytesPerSec = 0;
                double txBytesPerSec = 0;

                if (includeStats)
                {
                    lock (_sampleLock)
                    {
                        var now = DateTime.UtcNow;
                        if (_lastSampleTime != DateTime.MinValue)
                        {
                            var elapsed = (now - _lastSampleTime).TotalSeconds;
                            if (elapsed > 0)
                            {
                                rxBytesPerSec = (networkRxSnapshot - _lastRxBytes) / elapsed;
                                txBytesPerSec = (networkTxSnapshot - _lastTxBytes) / elapsed;

                                // 防止负数（容器重启等情况）
                                if (rxBytesPerSec < 0) rxBytesPerSec = 0;
                                if (txBytesPerSec < 0) txBytesPerSec = 0;
                            }
                        }

                        // 更新上次采样数据
                        _lastRxBytes = networkRxSnapshot;
                        _lastTxBytes = networkTxSnapshot;
                        _lastSampleTime = now;
                    }
                }

                return TypedResults.Ok(new DockerStatsResponse
                {
                    Docker = new DockerVersionInfo
                    {
                        Version = version.Version,
                        ApiVersion = version.APIVersion,
                        Status = "running",
                        Os = systemInfo.OperatingSystem,
                        Arch = systemInfo.Architecture,
                        KernelVersion = systemInfo.KernelVersion,
                        NCPU = systemInfo.NCPU
                    },
                    Containers = new DockerContainerCountInfo
                    {
                        Running = runningCount,
                        Stopped = stoppedCount,
                        Total = totalCount
                    },
                    Images = new DockerImageInfo
                    {
                        Count = imagesCount,
                        TotalSize = totalSize,
                        TotalSizeFormatted = FormatBytes(totalSize)
                    },
                    Resources = includeStats
                        ? new DockerResourceInfo
                        {
                            CpuUsagePercent = Math.Round(totalCpuPercent, 2),
                            MemoryUsed = totalContainerMemoryUsed,
                            MemoryLimit = hostMemoryTotal,
                            MemoryPercent = Math.Round(memoryPercent, 2),
                            MemoryUsedFormatted = FormatBytes(totalContainerMemoryUsed),
                            MemoryLimitFormatted = FormatBytes(hostMemoryTotal)
                        }
                        : null,
                    Network = includeStats
                        ? new DockerNetworkRateInfo
                        {
                            RxBytesPerSec = Math.Round(rxBytesPerSec, 2),
                            TxBytesPerSec = Math.Round(txBytesPerSec, 2),
                            RxSpeedFormatted = FormatBytesPerSec(rxBytesPerSec),
                            TxSpeedFormatted = FormatBytesPerSec(txBytesPerSec)
                        }
                        : null,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取 Docker 统计信息时发生异常");
                return TypedResults.Ok(new DockerStatsResponse
                {
                    Status = "Error",
                    Message = "Docker 连接异常: " + ex.Message
                });
            }
        }

        private static string FormatBytes(long bytes)
        {
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

        /// <summary>
        /// 获取宿主机实时指标（需要可选的 /host/proc 只读挂载）。
        /// 未启用时返回 Available=false 和原因，前端据此显示挂载提示，而不是伪造数据。
        /// </summary>
        private static IResult GetHostMetrics(HostMetricsCollector hostMetrics)
        {
            return TypedResults.Ok(hostMetrics.Collect());
        }
    }
}