using DockerPanel.API.Models;

namespace DockerPanel.API.Services;

/// <summary>
/// 节点资源监控服务接口
/// </summary>
public interface INodeResourceService
{
    /// <summary>
    /// 获取所有节点的资源概览
    /// </summary>
    Task<IEnumerable<NodeResourceOverview>> GetNodesResourceOverviewAsync();

    /// <summary>
    /// 获取指定节点的资源概览
    /// </summary>
    Task<NodeResourceOverview?> GetNodeResourceOverviewAsync(string nodeId);

    /// <summary>
    /// 获取指定节点的详细资源信息
    /// </summary>
    Task<NodeResourceDetails?> GetNodeResourceDetailsAsync(string nodeId);

    /// <summary>
    /// 获取节点的历史资源使用趋势
    /// </summary>
    Task<NodeResourceTrend?> GetNodeResourceTrendAsync(string nodeId, TimeSpan timeRange);

    /// <summary>
    /// 获取集群资源统计
    /// </summary>
    Task<ClusterResourceStats> GetClusterResourceStatsAsync();

    /// <summary>
    /// 获取资源告警信息
    /// </summary>
    Task<IEnumerable<DockerPanel.API.Models.ResourceAlert>> GetResourceAlertsAsync();

    /// <summary>
    /// 创建资源告警规则
    /// </summary>
    Task<DockerPanel.API.Models.ResourceAlertRule> CreateAlertRuleAsync(DockerPanel.API.Models.CreateResourceAlertRuleRequest request);

    /// <summary>
    /// 更新资源告警规则
    /// </summary>
    Task<DockerPanel.API.Models.ResourceAlertRule> UpdateAlertRuleAsync(string id, DockerPanel.API.Models.UpdateResourceAlertRuleRequest request);

    /// <summary>
    /// 删除资源告警规则
    /// </summary>
    Task<bool> DeleteAlertRuleAsync(string id);

    /// <summary>
    /// 获取资源告警规则列表
    /// </summary>
    Task<IEnumerable<DockerPanel.API.Models.ResourceAlertRule>> GetAlertRulesAsync();
}

/// <summary>
/// 节点资源概览
/// </summary>
public class NodeResourceOverview
{
    public string NodeId { get; set; } = string.Empty;
    public string NodeName { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public NodeResourceStatus Status { get; set; }
    public ResourceUsage CpuUsage { get; set; } = new();
    public ResourceUsage MemoryUsage { get; set; } = new();
    public ResourceUsage DiskUsage { get; set; } = new();
    public NetworkUsage NetworkUsage { get; set; } = new();
    public ContainerUsage ContainerUsage { get; set; } = new();
    public DateTime LastUpdated { get; set; }
    public List<string> Alerts { get; set; } = new();
}

/// <summary>
/// 资源使用情况
/// </summary>
public class ResourceUsage
{
    public double Used { get; set; }
    public double Total { get; set; }
    public double Percentage { get; set; }
    public string Unit { get; set; } = string.Empty;
    public Trend Trend { get; set; } = Trend.Stable;
}

/// <summary>
/// 资源趋势
/// </summary>
public enum Trend
{
    Stable,
    Increasing,
    Decreasing,
    Fluctuating
}


/// <summary>
/// 网络使用情况
/// </summary>
public class NetworkUsage
{
    public double BandwidthUsed { get; set; }
    public double BandwidthTotal { get; set; }
    public int ConnectionsCount { get; set; }
    public double PacketsIn { get; set; }
    public double PacketsOut { get; set; }
    public double ErrorsIn { get; set; }
    public double ErrorsOut { get; set; }
}

/// <summary>
/// 容器使用情况
/// </summary>
public class ContainerUsage
{
    public int TotalCount { get; set; }
    public int RunningCount { get; set; }
    public int StoppedCount { get; set; }
    public int PausedCount { get; set; }
    public ResourceUsage CpuUsage { get; set; } = new();
    public ResourceUsage MemoryUsage { get; set; } = new();
    public double ResourceUtilizationScore { get; set; }
}

/// <summary>
/// 节点资源详情
/// </summary>
public class NodeResourceDetails
{
    public NodeResourceOverview Overview { get; set; } = new();
    public List<ContainerResourceInfo> Containers { get; set; } = new();
    public List<NetworkResourceInfo> Networks { get; set; } = new();
    public List<VolumeInfo> Volumes { get; set; } = new();
    public SystemInfo SystemInfo { get; set; } = new();
    public DockerEngineInfo DockerInfo { get; set; } = new();
    public PerformanceMetrics PerformanceMetrics { get; set; } = new();
}

/// <summary>
/// 容器资源信息
/// </summary>
public class ContainerResourceInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public ResourceUsage CpuUsage { get; set; } = new();
    public ResourceUsage MemoryUsage { get; set; } = new();
    public ResourceUsage DiskUsage { get; set; } = new();
    public NetworkUsage NetworkUsage { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public List<string> Labels { get; set; } = new();
}

/// <summary>
/// 网络资源信息
/// </summary>
public class NetworkResourceInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Driver { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public bool Internal { get; set; }
    public int ContainersCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime Created { get; set; }
}


/// <summary>
/// 系统信息
/// </summary>
public class SystemInfo
{
    public string OsType { get; set; } = string.Empty;
    public string KernelVersion { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public int CpuCores { get; set; }
    public long TotalMemory { get; set; }
    public long TotalDisk { get; set; }
    public DateTime BootTime { get; set; }
    public double Uptime { get; set; }
}

/// <summary>
/// Docker引擎信息
/// </summary>
public class DockerEngineInfo
{
    public string Version { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = string.Empty;
    public string GoVersion { get; set; } = string.Empty;
    public int Containers { get; set; }
    public int Images { get; set; }
    public int Networks { get; set; }
    public int Volumes { get; set; }
    public string ServerVersion { get; set; } = string.Empty;
    public DateTime SystemTime { get; set; }
}

/// <summary>
/// 性能指标
/// </summary>
public class PerformanceMetrics
{
    public double CpuLoadAverage { get; set; }
    public double MemoryPressure { get; set; }
    public double DiskIoWait { get; set; }
    public double NetworkLatency { get; set; }
    public int ProcessCount { get; set; }
    public double ThreadCount { get; set; }
    public double ContextSwitches { get; set; }
}

/// <summary>
/// 节点资源趋势
/// </summary>
public class NodeResourceTrend
{
    public string NodeId { get; set; } = string.Empty;
    public List<ResourceDataPoint> CpuTrend { get; set; } = new();
    public List<ResourceDataPoint> MemoryTrend { get; set; } = new();
    public List<ResourceDataPoint> DiskTrend { get; set; } = new();
    public List<ResourceDataPoint> NetworkTrend { get; set; } = new();
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Interval { get; set; }
}

/// <summary>
/// 资源数据点
/// </summary>
public class ResourceDataPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
}

/// <summary>
/// 集群资源统计
/// </summary>
public class ClusterResourceStats
{
    public int TotalNodes { get; set; }
    public int OnlineNodes { get; set; }
    public int OfflineNodes { get; }
    public int WarningNodes { get; set; }
    public int ErrorNodes { get; set; }
    public ClusterCpuUsage ClusterCpuUsage { get; set; } = new();
    public ClusterMemoryUsage ClusterMemoryUsage { get; set; } = new();
    public ClusterDiskUsage ClusterDiskUsage { get; set; } = new();
    public int TotalContainers { get; set; }
    public int RunningContainers { get; set; }
    public int StoppedContainers { get; set; }
    public double ClusterUtilizationScore { get; set; }
    public DateTime LastUpdated { get; set; }
    public List<string> CriticalAlerts { get; set; } = new();
}

/// <summary>
/// 集群资源使用情况
/// </summary>
public class ClusterCpuUsage
{
    public double Used { get; set; }
    public double Total { get; set; }
    public double Percentage { get; set; }
    public double AverageUsage { get; set; }
    public double MaxUsage { get; set; }
    public double MinUsage { get; set; }
}

/// <summary>
/// 集群内存使用情况
/// </summary>
public class ClusterMemoryUsage
{
    public long Used { get; set; }
    public long Total { get; set; }
    public double Percentage { get; set; }
    public double AverageUsage { get; set; }
    public double MaxUsage { get; set; }
    public double MinUsage { get; set; }
}

/// <summary>
/// 集群磁盘使用情况
/// </summary>
public class ClusterDiskUsage
{
    public long Used { get; set; }
    public long Total { get; set; }
    public double Percentage { get; set; }
    public double AverageUsage { get; set; }
    public double MaxUsage { get; set; }
    public double MinUsage { get; set; }
}

/// <summary>
/// 资源告警
/// </summary>
public class ResourceAlert
{
    public string Id { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public AlertType AlertType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public double Threshold { get; set; }
    public double CurrentValue { get; set; }
    public string Severity { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
}

/// <summary>
/// 告警类型
/// </summary>
public enum AlertType
{
    Threshold,
    Anomaly,
    ConnectionLost,
    ServiceDown,
    Capacity,
    Performance
}

/// <summary>
/// 告警严重程度
/// </summary>
public enum AlertSeverity
{
    Info,
    Warning,
    Critical,
    Emergency
}

/// <summary>
/// 创建资源告警规则请求
/// </summary>
public class CreateResourceAlertRule
{
    public string Name { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public AlertType AlertType { get; set; }
    public double ThresholdValue { get; set; }
    public string ComparisonOperator { get; set; } = ">";
    public string Severity { get; set; } = "Warning";
    public string? NodeId { get; set; }
    public bool IsEnabled { get; set; } = true;
    public List<string> NotificationChannels { get; set; } = new();
    public string? Description { get; set; }
}

/// <summary>
/// 更新资源告警规则请求
/// </summary>
public class UpdateResourceAlertRuleRequest
{
    public string? Name { get; set; }
    public string? ResourceType { get; set; }
    public AlertType? AlertType { get; set; }
    public double? ThresholdValue { get; set; }
    public string? ComparisonOperator { get; set; }
    public string? Severity { get; set; }
    public bool? IsEnabled { get; set; }
    public List<string>? NotificationChannels { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// 资源告警规则
/// </summary>
public class ResourceAlertRule
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public AlertType AlertType { get; set; }
    public double ThresholdValue { get; set; }
    public string ComparisonOperator { get; set; } = ">";
    public string Severity { get; set; } = "Warning";
    public string? NodeId { get; set; }
    public bool IsEnabled { get; set; } = true;
    public List<string> NotificationChannels { get; set; } = new();
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int TriggeredCount { get; set; }
    public DateTime LastTriggeredAt { get; set; }
}

/// <summary>
/// 比较操作符
/// </summary>
public enum ComparisonOperator
{
    GreaterThan,
    LessThan,
    Equals,
    NotEquals,
    GreaterThanOrEqual,
    LessThanOrEqual
}

