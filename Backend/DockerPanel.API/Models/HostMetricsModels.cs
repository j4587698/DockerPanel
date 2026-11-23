namespace DockerPanel.API.Models;

/// <summary>
/// 宿主机实时指标（通过 /host/proc、/host/sys 只读挂载采集）。
/// 未启用主机监控时 <see cref="Available"/> 为 false，并通过 <see cref="UnavailableReason"/> 说明原因。
/// </summary>
public sealed class HostMetricsSnapshot
{
    public bool Available { get; set; }
    public string? UnavailableReason { get; set; }

    /// <summary>是否仅能读取容器自身的 /proc（此时不展示为宿主机指标）。</summary>
    public bool Containerized { get; set; }

    public string? HostName { get; set; }
    public string? OsName { get; set; }
    public string? OsVersion { get; set; }
    public string? KernelVersion { get; set; }
    public long UptimeSeconds { get; set; }

    public HostCpuMetrics Cpu { get; set; } = new();
    public HostMemoryMetrics Memory { get; set; } = new();
    public HostLoadMetrics Load { get; set; } = new();
    public HostDiskMetrics Disk { get; set; } = new();
    public HostNetworkMetrics Network { get; set; } = new();

    /// <summary>挂载点容量（需要 /host/root 只读挂载）。</summary>
    public bool FileSystemAvailable { get; set; }
    public string? FileSystemUnavailableReason { get; set; }
    public List<HostFileSystemMetrics> FileSystems { get; set; } = new();

    public DateTime Timestamp { get; set; }
}

public sealed class HostCpuMetrics
{
    public double UsagePercent { get; set; }
    public int CoreCount { get; set; }
    public double UserPercent { get; set; }
    public double SystemPercent { get; set; }
    public double IowaitPercent { get; set; }
}

public sealed class HostMemoryMetrics
{
    public long TotalBytes { get; set; }
    public long UsedBytes { get; set; }
    public long AvailableBytes { get; set; }
    public long SwapTotalBytes { get; set; }
    public long SwapUsedBytes { get; set; }
    public double UsagePercent { get; set; }
    public string UsedFormatted { get; set; } = "0 B";
    public string TotalFormatted { get; set; } = "0 B";
    public string AvailableFormatted { get; set; } = "0 B";
}

public sealed class HostLoadMetrics
{
    public double Load1 { get; set; }
    public double Load5 { get; set; }
    public double Load15 { get; set; }
}

public sealed class HostDiskMetrics
{
    /// <summary>块设备读写速率（需要 /host/proc/diskstats）。</summary>
    public bool Available { get; set; }
    public string? UnavailableReason { get; set; }
    public long ReadBytesPerSec { get; set; }
    public long WriteBytesPerSec { get; set; }
    public string ReadSpeedFormatted { get; set; } = "0 B/s";
    public string WriteSpeedFormatted { get; set; } = "0 B/s";
}

public sealed class HostNetworkMetrics
{
    /// <summary>
    /// 宿主机网络命名空间指标。读取 /host/proc/1/net/dev，避免把容器网卡误当宿主机网卡。
    /// </summary>
    public bool Available { get; set; }
    public string? UnavailableReason { get; set; }
    public long RxBytesPerSec { get; set; }
    public long TxBytesPerSec { get; set; }
    public string RxSpeedFormatted { get; set; } = "0 B/s";
    public string TxSpeedFormatted { get; set; } = "0 B/s";
    public long TotalRxBytes { get; set; }
    public long TotalTxBytes { get; set; }
    public long TotalRxPackets { get; set; }
    public long TotalTxPackets { get; set; }
}

public sealed class HostFileSystemMetrics
{
    public string Device { get; set; } = string.Empty;
    public string MountPoint { get; set; } = string.Empty;
    public string FileSystem { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long UsedBytes { get; set; }
    public long AvailableBytes { get; set; }
    public double UsagePercent { get; set; }
    public string TotalFormatted { get; set; } = "0 B";
    public string UsedFormatted { get; set; } = "0 B";
}
