using DockerPanel.API.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DockerPanel.API.Tests;

/// <summary>
/// DockerStatsAggregator 的采样开关测试。
/// 重点验证 includeStats=false 时不会再去枚举/采样容器（这是概览页省掉
/// 逐容器双采样开销的前提），不需要启动 Docker 也能跑。
/// </summary>
public sealed class DockerStatsAggregatorTests
{
    [Fact]
    public async Task CollectAsync_WhenStatsDisabled_DoesNotEnumerateOrSampleContainers()
    {
        var listCalls = 0;
        var sampleCalls = 0;

        var result = await DockerStatsAggregator.CollectAsync(
            includeStats: false,
            () =>
            {
                listCalls++;
                throw new InvalidOperationException("includeStats=false 时不应获取容器列表");
            },
            _ =>
            {
                sampleCalls++;
                throw new InvalidOperationException("includeStats=false 时不应采样容器 stats");
            },
            maxCpuPercent: 800,
            NullLogger.Instance);

        Assert.Equal(0, listCalls);
        Assert.Equal(0, sampleCalls);
        Assert.Equal(ContainerStatsAggregate.Empty, result);
    }

    [Fact]
    public async Task CollectAsync_WhenStatsEnabled_AggregatesAndClampsCpu()
    {
        var samples = new Dictionary<string, ContainerStatsSample?>
        {
            ["a"] = new ContainerStatsSample(CpuPercent: 120, MemoryUsed: 100, NetworkRxBytes: 1000, NetworkTxBytes: 2000),
            ["b"] = new ContainerStatsSample(CpuPercent: 80, MemoryUsed: 50, NetworkRxBytes: 500, NetworkTxBytes: 500),
            ["failed"] = null
        };

        var result = await DockerStatsAggregator.CollectAsync(
            includeStats: true,
            () => Task.FromResult<IReadOnlyList<string>>(new List<string> { "a", "b", "failed" }),
            id => Task.FromResult(samples.TryGetValue(id, out var sample) ? sample : null),
            maxCpuPercent: 150,
            NullLogger.Instance);

        // 200% 超过 150% 上限，必须被收敛
        Assert.Equal(150, result.CpuPercent, 3);
        Assert.Equal(150, result.MemoryUsed);
        Assert.Equal(1500, result.NetworkRxBytes);
        Assert.Equal(2500, result.NetworkTxBytes);
    }

    [Fact]
    public async Task CollectAsync_WhenListingFails_ReturnsEmptyWithoutThrowing()
    {
        var result = await DockerStatsAggregator.CollectAsync(
            includeStats: true,
            () => Task.FromException<IReadOnlyList<string>>(new InvalidOperationException("docker unavailable")),
            _ => Task.FromResult<ContainerStatsSample?>(null),
            maxCpuPercent: 800,
            NullLogger.Instance);

        Assert.Equal(ContainerStatsAggregate.Empty, result);
    }
}
