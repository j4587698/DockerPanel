using DockerPanel.API.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DockerPanel.API.Tests;

/// <summary>
/// HostMetricsCollector 解析逻辑测试。
/// 通过 HOST_PROC/HOST_ROOT 指向临时目录模拟只读挂载，避免依赖真实宿主机。
/// </summary>
public sealed class HostMetricsCollectorTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _procDir;

    public HostMetricsCollectorTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "dockerpanel-hostmetrics-" + Guid.NewGuid().ToString("N"));
        _procDir = Path.Combine(_tempRoot, "proc");
        Directory.CreateDirectory(Path.Combine(_procDir, "1", "net"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "sys"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "root"));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
            // 测试清理失败不影响结论
        }
    }

    private HostMetricsCollector CreateCollector(string? procPath = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HOST_PROC"] = procPath ?? _procDir,
                ["HOST_SYS"] = Path.Combine(_tempRoot, "sys"),
                ["HOST_ROOT"] = Path.Combine(_tempRoot, "root")
            })
            .Build();

        return new HostMetricsCollector(NullLogger<HostMetricsCollector>.Instance, configuration);
    }

    private void WriteProcFiles(string cpuUser, string cpuIdle, long rxBytes, long readSectors)
    {
        File.WriteAllText(Path.Combine(_procDir, "stat"),
            $"cpu  {cpuUser} 0 0 {cpuIdle} 0 0 0 0 0 0\n" +
            "cpu0 1 0 0 1 0 0 0 0 0 0\n" +
            "cpu1 1 0 0 1 0 0 0 0 0 0\n");

        File.WriteAllText(Path.Combine(_procDir, "meminfo"),
            "MemTotal:        1024 kB\n" +
            "MemFree:          128 kB\n" +
            "MemAvailable:     256 kB\n" +
            "Buffers:            0 kB\n" +
            "Cached:             0 kB\n" +
            "SwapTotal:        512 kB\n" +
            "SwapFree:         384 kB\n");

        File.WriteAllText(Path.Combine(_procDir, "loadavg"), "1.50 1.00 0.50 1/100 1234\n");
        File.WriteAllText(Path.Combine(_procDir, "uptime"), "12345.67 1000.00\n");

        File.WriteAllText(Path.Combine(_procDir, "diskstats"),
            $"   8       0 sda 100 0 {readSectors} 0 50 0 {readSectors} 0 0 0 0\n" +
            "   8       1 sda1 100 0 999999 0 50 0 999999 0 0 0 0\n" +
            "   7       0 loop0 1 0 1 0 1 0 1 0 0 0 0\n");

        File.WriteAllText(Path.Combine(_procDir, "mounts"),
            "/dev/sda1 / ext4 rw,relatime 0 0\n" +
            "proc /proc proc rw,nosuid 0 0\n" +
            "tmpfs /run tmpfs rw 0 0\n");

        File.WriteAllText(Path.Combine(_procDir, "1", "net", "dev"),
            "Inter-|   Receive                                                |  Transmit\n" +
            " face |bytes    packets errs drop fifo frame compressed multicast|bytes    packets errs drop fifo colls carrier compressed\n" +
            $"  eth0: {rxBytes} 10 0 0 0 0 0 0 2000 20 0 0 0 0 0 0\n" +
            "    lo: 999999 1 0 0 0 0 0 0 999999 1 0 0 0 0 0 0\n");
    }

    [Fact]
    public void Collect_WithMissingProcPath_ReportsUnavailable()
    {
        // 显式指定的 HOST_PROC 无效时必须直接判不可用，不能回退到真实 /proc。
        // Linux runner 上 /proc 是存在的，回退会把本机进程视图当成宿主机数据，
        // 所以这条在 CI（Linux）上也是该约束的回归测试。
        var collector = CreateCollector(Path.Combine(_tempRoot, "does-not-exist"));

        var snapshot = collector.Collect();

        Assert.False(collector.IsAvailable);
        Assert.False(snapshot.Available);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.UnavailableReason));
        Assert.Contains("HOST_PROC", snapshot.UnavailableReason);
    }

    [Fact]
    public void Collect_WithExplicitProcPathMissingStat_ReportsUnavailable()
    {
        // 目录存在但缺少 stat（挂载不完整）时同样不可用，避免把空目录当成有效宿主机。
        var emptyProc = Path.Combine(_tempRoot, "empty-proc");
        Directory.CreateDirectory(emptyProc);
        var collector = CreateCollector(emptyProc);

        var snapshot = collector.Collect();

        Assert.False(collector.IsAvailable);
        Assert.False(snapshot.Available);
        Assert.Contains("HOST_PROC", snapshot.UnavailableReason);
    }

    [Fact]
    public void Collect_ParsesHostProcAndComputesRates()
    {
        WriteProcFiles(cpuUser: "100", cpuIdle: "800", rxBytes: 1000, readSectors: 2000);
        var collector = CreateCollector();

        var first = collector.Collect();
        Assert.True(first.Available, first.UnavailableReason);
        Assert.Equal(2, first.Cpu.CoreCount);
        // 首次采样没有历史快照，速率必须是 0 而不是随机值
        Assert.Equal(0, first.Network.RxBytesPerSec);
        Assert.Equal(0, first.Disk.ReadBytesPerSec);

        // 第二次采样：总 jiffies +200、idle +100 → CPU 50%
        WriteProcFiles(cpuUser: "200", cpuIdle: "900", rxBytes: 2000, readSectors: 2200);
        Thread.Sleep(60);
        var second = collector.Collect();

        Assert.True(second.Available, second.UnavailableReason);
        Assert.Equal(50, second.Cpu.UsagePercent, 1);
        Assert.Equal(1024L * 1024, second.Memory.TotalBytes);
        Assert.Equal(75, second.Memory.UsagePercent, 1);
        Assert.Equal(128L * 1024, second.Memory.SwapUsedBytes);
        Assert.Equal(1.5, second.Load.Load1, 2);
        Assert.Equal(12345, second.UptimeSeconds);

        // 网络：pt1 与 pt2 的 eth0 接收字节差 1000，速率必须 > 0
        Assert.True(second.Network.Available, second.Network.UnavailableReason);
        Assert.True(second.Network.RxBytesPerSec > 0);
        Assert.Equal(2000L, second.Network.TotalRxBytes);

        // 磁盘：只统计整盘 sda，不重复统计 sda1，也不统计 loop 设备
        Assert.True(second.Disk.Available, second.Disk.UnavailableReason);
        Assert.True(second.Disk.ReadBytesPerSec > 0);
    }
}
