using Microsoft.Extensions.Logging;

namespace DockerPanel.API.Services;

/// <summary>
/// 单个容器的资源采样值。抽成与 Docker 客户端无关的轻量结构，
/// 便于在不启动 Docker 的情况下验证聚合逻辑。
/// </summary>
internal readonly record struct ContainerStatsSample(
    double CpuPercent,
    long MemoryUsed,
    long NetworkRxBytes,
    long NetworkTxBytes);

/// <summary>
/// 所有运行中容器的聚合结果。
/// </summary>
internal readonly record struct ContainerStatsAggregate(
    double CpuPercent,
    long MemoryUsed,
    long NetworkRxBytes,
    long NetworkTxBytes)
{
    public static ContainerStatsAggregate Empty => new(0, 0, 0, 0);
}

/// <summary>
/// 概览页的容器资源聚合。
///
/// 关键行为：includeStats=false 时**绝不能**枚举容器或调用 stats 采样，
/// 因为每个容器的双采样都有秒级开销。这条约定由单元测试直接校验。
/// </summary>
internal static class DockerStatsAggregator
{
    public static async Task<ContainerStatsAggregate> CollectAsync(
        bool includeStats,
        Func<Task<IReadOnlyList<string>>> listRunningContainerIdsAsync,
        Func<string, Task<ContainerStatsSample?>> getSampleAsync,
        double maxCpuPercent,
        ILogger logger)
    {
        // 提前返回：连容器列表都不应该获取（列表本身也很轻，但没必要）
        if (!includeStats)
        {
            return ContainerStatsAggregate.Empty;
        }

        double totalCpuPercent = 0;
        long totalMemoryUsed = 0;
        long networkRxBytes = 0;
        long networkTxBytes = 0;

        try
        {
            var containerIds = await listRunningContainerIdsAsync();
            var samples = await Task.WhenAll(containerIds.Select(getSampleAsync));

            foreach (var sample in samples)
            {
                if (sample is null) continue;

                totalCpuPercent += sample.Value.CpuPercent;
                totalMemoryUsed += sample.Value.MemoryUsed;
                networkRxBytes += sample.Value.NetworkRxBytes;
                networkTxBytes += sample.Value.NetworkTxBytes;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "获取容器统计信息失败，使用默认值");
            return ContainerStatsAggregate.Empty;
        }

        // CPU 百分比可能超过 100%（多核），按逻辑核心数上限收敛
        if (totalCpuPercent > maxCpuPercent)
        {
            totalCpuPercent = maxCpuPercent;
        }

        return new ContainerStatsAggregate(totalCpuPercent, totalMemoryUsed, networkRxBytes, networkTxBytes);
    }
}
