namespace DockerPanel.API.Hubs;

/// <summary>
/// Docker 系统统计推送载荷（DockerStatsUpdated）。
/// 替代匿名类型：匿名类型无法在 JsonSerializerContext 中注册，AOT 下序列化会直接抛异常。
/// 字段名与既有推送保持一致（camelCase 由 SignalR 上下文策略保证）。
/// </summary>
public class DockerStatsPushMessage
{
    public DockerStatsPushDocker Docker { get; set; } = new();
    public DockerStatsPushContainers Containers { get; set; } = new();
    public DockerStatsPushResources Resources { get; set; } = new();
    public DockerStatsPushNetwork Network { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

public class DockerStatsPushDocker
{
    public string Status { get; set; } = "running";
    public int NCPU { get; set; } = 1;
}

public class DockerStatsPushContainers
{
    public int Running { get; set; }
    public int Stopped { get; set; }
    public int Total { get; set; }
}

public class DockerStatsPushResources
{
    public double CpuUsagePercent { get; set; }
    public long MemoryUsed { get; set; }
    public long MemoryLimit { get; set; }
    public double MemoryPercent { get; set; }
    public string MemoryUsedFormatted { get; set; } = "0 B";
    public string MemoryLimitFormatted { get; set; } = "0 B";
}

public class DockerStatsPushNetwork
{
    public long RxBytesPerSec { get; set; }
    public long TxBytesPerSec { get; set; }
    public string RxSpeedFormatted { get; set; } = "0 B/s";
    public string TxSpeedFormatted { get; set; } = "0 B/s";
}

/// <summary>
/// 单个容器的统计推送载荷（ContainerStatsUpdated 列表项）。
/// </summary>
public class ContainerStatsPushMessage
{
    public string ContainerId { get; set; } = "";
    public string Name { get; set; } = "";
    public ContainerStatsPushCpu CpuStats { get; set; } = new();
    public ContainerStatsPushMemory MemoryStats { get; set; } = new();
    public List<ContainerStatsPushNetworkItem> Networks { get; set; } = new();
}

public class ContainerStatsPushCpu
{
    public double PercentCpu { get; set; }
    public long CpuUsage { get; set; }
    public long SystemUsage { get; set; }
}

public class ContainerStatsPushMemory
{
    public long Usage { get; set; }
    public long Limit { get; set; }
    public double PercentMemory { get; set; }
}

public class ContainerStatsPushNetworkItem
{
    public string Name { get; set; } = "";
    public long RxBytes { get; set; }
    public long TxBytes { get; set; }
    public long RxPackets { get; set; }
    public long TxPackets { get; set; }
}
