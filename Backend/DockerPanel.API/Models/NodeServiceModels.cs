namespace DockerPanel.API.Models;

/// <summary>
/// 节点统计信息
/// </summary>
public class NodeStats
{
    public string NodeId { get; set; } = string.Empty;
    public string NodeName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public NodeCpuStats Cpu { get; set; } = new();
    public NodeMemoryStats Memory { get; set; } = new();
    public NodeDiskStats Disk { get; set; } = new();
    public NodeNetworkStatsExtended Network { get; set; } = new();
    public NodeContainerStatsExtended Containers { get; set; } = new();
    public double SystemLoad { get; set; }
    public TimeSpan Uptime { get; set; }
    public int ProcessCount { get; set; }
    public int ThreadCount { get; set; }

    // 兼容SimpleContainerService.cs中使用的属性
    public int ContainerCount { get; set; }
    public int RunningContainerCount { get; set; }
    public int StoppedContainerCount { get; set; }
    public int ImageCount { get; set; }
    public int NetworkCount { get; set; }
    public int VolumeCount { get; set; }
    public double CpuUsage { get; set; }
    public long MemoryUsage { get; set; }
    public long MemoryTotal { get; set; }
    public long DiskUsage { get; set; }
    public long DiskTotal { get; set; }
}

/// <summary>
/// 节点CPU统计
/// </summary>
public class NodeCpuStats
{
    public int CoreCount { get; set; }
    public double UsagePercent { get; set; }
    public double UserTime { get; set; }
    public double SystemTime { get; set; }
    public double IdleTime { get; set; }
    public double IowaitTime { get; set; }
    public double StealTime { get; set; }
    public double LoadAverage1m { get; set; }
    public double LoadAverage5m { get; set; }
    public double LoadAverage15m { get; set; }
    public List<CpuCoreStats> Cores { get; set; } = new();
}

/// <summary>
/// CPU核心统计
/// </summary>
public class CpuCoreStats
{
    public int CoreId { get; set; }
    public double UsagePercent { get; set; }
    public double UserTime { get; set; }
    public double SystemTime { get; set; }
    public double IdleTime { get; set; }
    public double IowaitTime { get; set; }
}

/// <summary>
/// 节点内存统计
/// </summary>
public class NodeMemoryStats
{
    public long Total { get; set; }
    public long Available { get; set; }
    public long Used { get; set; }
    public long Free { get; set; }
    public long Buffers { get; set; }
    public long Cached { get; set; }
    public long SwapTotal { get; set; }
    public long SwapUsed { get; set; }
    public long SwapFree { get; set; }
    public double UsagePercent { get; set; }
    public double SwapUsagePercent { get; set; }
}

/// <summary>
/// 节点磁盘统计
/// </summary>
public class NodeDiskStats
{
    public long Total { get; set; }
    public long Used { get; set; }
    public long Free { get; set; }
    public double UsagePercent { get; set; }
    public long InodesTotal { get; set; }
    public long InodesUsed { get; set; }
    public long InodesFree { get; set; }
    public double InodesUsagePercent { get; set; }
    public double ReadBytesPerSecond { get; set; }
    public double WriteBytesPerSecond { get; set; }
    public double ReadOperationsPerSecond { get; set; }
    public double WriteOperationsPerSecond { get; set; }
    public List<DiskPartitionStats> Partitions { get; set; } = new();
}

/// <summary>
/// 磁盘分区统计
/// </summary>
public class DiskPartitionStats
{
    public string Device { get; set; } = string.Empty;
    public string MountPoint { get; set; } = string.Empty;
    public string FileSystem { get; set; } = string.Empty;
    public long Total { get; set; }
    public long Used { get; set; }
    public long Free { get; set; }
    public double UsagePercent { get; set; }
    public long InodesTotal { get; set; }
    public long InodesUsed { get; set; }
    public long InodesFree { get; set; }
    public double InodesUsagePercent { get; set; }
}

/// <summary>
/// 节点网络统计
/// </summary>
public class NodeNetworkStatsExtended
{
    public long BytesReceived { get; set; }
    public long BytesTransmitted { get; set; }
    public long PacketsReceived { get; set; }
    public long PacketsTransmitted { get; set; }
    public long ErrorsIn { get; set; }
    public long ErrorsOut { get; set; }
    public long DroppedIn { get; set; }
    public long DroppedOut { get; set; }
    public double ReceiveRate { get; set; }
    public double TransmitRate { get; set; }
    public List<NetworkInterfaceStats> Interfaces { get; set; } = new();
}

/// <summary>
/// 网络接口统计
/// </summary>
public class NetworkInterfaceStats
{
    public string Name { get; set; } = string.Empty;
    public bool IsUp { get; set; }
    public long BytesReceived { get; set; }
    public long BytesTransmitted { get; set; }
    public long PacketsReceived { get; set; }
    public long PacketsTransmitted { get; set; }
    public long ErrorsIn { get; set; }
    public long ErrorsOut { get; set; }
    public long DroppedIn { get; set; }
    public long DroppedOut { get; set; }
    public double ReceiveRate { get; set; }
    public double TransmitRate { get; set; }
}

/// <summary>
/// 节点容器统计
/// </summary>
public class NodeContainerStatsExtended
{
    public int Total { get; set; }
    public int Running { get; set; }
    public int Stopped { get; set; }
    public int Paused { get; set; }
    public int Restarting { get; set; }
    public int Removing { get; set; }
    public int Dead { get; set; }
    public int Created { get; set; }
    public List<ContainerResourceUsage> ContainerUsages { get; set; } = new();
}

/// <summary>
/// 容器资源使用情况
/// </summary>
public class ContainerResourceUsage
{
    public string ContainerId { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public double CpuUsagePercent { get; set; }
    public long MemoryUsage { get; set; }
    public long MemoryLimit { get; set; }
    public double MemoryUsagePercent { get; set; }
    public long NetworkRxBytes { get; set; }
    public long NetworkTxBytes { get; set; }
    public long DiskReadBytes { get; set; }
    public long DiskWriteBytes { get; set; }
}

/// <summary>
/// 节点健康状态
/// </summary>
public class NodeHealthStatus
{
    public string NodeId { get; set; } = string.Empty;
    public string NodeName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // healthy, unhealthy, unknown
    public string Message { get; set; } = string.Empty;
    public DateTime LastCheck { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public List<HealthCheckResult> HealthChecks { get; set; } = new();
    public Dictionary<string, object> Metrics { get; set; } = new();

    /// <summary>
    /// 健康检查结果字典，兼容SimpleContainerService的使用
    /// </summary>
    public Dictionary<string, bool> Checks { get; set; } = new();

    /// <summary>
    /// 节点是否健康，兼容SimpleContainerService的使用
    /// </summary>
    public bool IsHealthy { get; set; }
}

/// <summary>
/// 健康检查结果
/// </summary>
public class HealthCheckResult
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // pass, fail, warn
    public string Message { get; set; } = string.Empty;
    public DateTime ExecutedAt { get; set; }
    public TimeSpan Duration { get; set; }
    public Dictionary<string, object> Details { get; set; } = new();
}

/// <summary>
/// 节点性能指标
/// </summary>
public class NodePerformanceMetrics
{
    public string NodeId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double CpuScore { get; set; } // 0-100
    public double MemoryScore { get; set; } // 0-100
    public double DiskScore { get; set; } // 0-100
    public double NetworkScore { get; set; } // 0-100
    public double OverallScore { get; set; } // 0-100
    public List<string> Bottlenecks { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public PerformanceTrend Trend { get; set; } = new();
}

/// <summary>
/// 性能趋势
/// </summary>
public class PerformanceTrend
{
    public string Direction { get; set; } = string.Empty; // improving, declining, stable
    public double ChangePercent { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public List<DataPoint> DataPoints { get; set; } = new();
}

/// <summary>
/// 数据点
/// </summary>
public class DataPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
}

/// <summary>
/// 节点事件
/// </summary>
public class NodeEvent
{
    public string Id { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty; // startup, shutdown, error, warning, info
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
    public EventSeverity Severity { get; set; } = EventSeverity.Info;
}

/// <summary>
/// 事件严重程度
/// </summary>
public enum EventSeverity
{
    Info,
    Warning,
    Error,
    Critical
}