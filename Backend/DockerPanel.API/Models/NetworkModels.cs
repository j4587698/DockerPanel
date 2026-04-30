using System.ComponentModel.DataAnnotations;
using TinyDb.Attributes;

namespace DockerPanel.API.Models;

/// <summary>
/// 网络详细信息
/// </summary>
public class NetworkDetailInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Driver { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty; // local, swarm, global
    public bool Internal { get; set; }
    public bool EnableIPv6 { get; set; }
    public bool Attachable { get; set; }
    public bool Ingress { get; set; }
    public Dictionary<string, string> Labels { get; set; } = new();
    public Dictionary<string, string> Options { get; set; } = new();
    public NetworkIpamConfig IPAM { get; set; } = new();
    public List<NetworkContainer> Containers { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public NetworkStatistics Statistics { get; set; } = new();
}

/// <summary>
/// 网络IPAM配置
/// </summary>
public class NetworkIpamConfig
{
    public string Driver { get; set; } = string.Empty;
    public List<NetworkIpamConfigEntry> Config { get; set; } = new();
    public List<string> Options { get; set; } = new();
}

/// <summary>
/// 网络IPAM配置条目
/// </summary>
public class NetworkIpamConfigEntry
{
    public string Subnet { get; set; } = string.Empty;
    public string? IPRange { get; set; }
    public string? Gateway { get; set; }
    public List<string> AuxiliaryAddresses { get; set; } = new();
}

/// <summary>
/// 网络容器信息
/// </summary>
public class NetworkContainer
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public List<string> IpAddresses { get; set; } = new();
    public List<string> Aliases { get; set; } = new();
    public NetworkEndpointInfo EndpointId { get; set; } = new();
}

/// <summary>
/// 网络端点信息
/// </summary>
public class NetworkEndpointInfo
{
    public string EndpointId { get; set; } = string.Empty;
    public string? NetworkId { get; set; }
    public string? IpAddress { get; set; }
    public string? IPPrefixLen { get; set; }
    public string? Gateway { get; set; }
    public string? MacAddress { get; set; }
    public List<string> Aliases { get; set; } = new();
    public Dictionary<string, string> DNS { get; set; } = new();
}

/// <summary>
/// 网络连接配置
/// </summary>
public class NetworkConnectionConfig
{
    public string ContainerId { get; set; } = string.Empty;
    public string NetworkId { get; set; } = string.Empty;
    public List<string> Aliases { get; set; } = new();
    public Dictionary<string, string> EndpointConfig { get; set; } = new();
    public Dictionary<string, string> IPAMConfig { get; set; } = new();
    public bool Force { get; set; } = false;
    public string? IPAddress { get; set; }
    public string? IPv4Address { get; set; }
    public string? IPv6Address { get; set; }
    public List<string>? Links { get; set; }
}

/// <summary>
/// 网络容器信息（详细）
/// </summary>
public class NetworkContainerInfo
{
    public string ContainerId { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string NetworkId { get; set; } = string.Empty;
    public string NetworkName { get; set; } = string.Empty;
    public string EndpointId { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string IPPrefixLen { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public List<string> Aliases { get; set; } = new();
    public Dictionary<string, string> DNS { get; set; } = new();
    public DateTime ConnectedAt { get; set; }
}

/// <summary>
/// 网络清理选项
/// </summary>
public class NetworkPruneOptions
{
    public bool Filters { get; set; } = false;
    public string? LabelFilter { get; set; }
    public Dictionary<string, string>? Labels { get; set; }
    public bool Until { get; set; } = false;
    public DateTime? UntilDate { get; set; }
}

/// <summary>
/// 网络清理结果
/// </summary>
public class NetworkPruneResult
{
    public int NetworksDeleted { get; set; }
    public long SpaceReclaimed { get; set; }
    public List<string> DeletedNetworkIds { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public bool Success { get; set; } = true;
}

/// <summary>
/// 网络统计信息
/// </summary>
public class NetworkStatistics
{
    public string NetworkId { get; set; } = string.Empty;
    public string NetworkName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public int ConnectedContainers { get; set; }
    public long BytesReceived { get; set; }
    public long BytesTransmitted { get; set; }
    public long PacketsReceived { get; set; }
    public long PacketsTransmitted { get; set; }
    public long ErrorsReceived { get; set; }
    public long ErrorsTransmitted { get; set; }
    public long PacketsDropped { get; set; }
    public NetworkBandwidthUsage BandwidthUsage { get; set; } = new();
}

/// <summary>
/// 网络带宽使用情况
/// </summary>
public class NetworkBandwidthUsage
{
    public double CurrentRxMbps { get; set; }
    public double CurrentTxMbps { get; set; }
    public double AverageRxMbps { get; set; }
    public double AverageTxMbps { get; set; }
    public double PeakRxMbps { get; set; }
    public double PeakTxMbps { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// 网络IPAM信息
/// </summary>
public class NetworkIpamInfo
{
    public string NetworkId { get; set; } = string.Empty;
    public string NetworkName { get; set; } = string.Empty;
    public NetworkIpamConfig Config { get; set; } = new();
    public List<NetworkIpamPool> Pools { get; set; } = new();
    public NetworkIpamUsage Usage { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 网络IPAM池
/// </summary>
public class NetworkIpamPool
{
    public string Subnet { get; set; } = string.Empty;
    public string? IPRange { get; set; }
    public string? Gateway { get; set; }
    public long AvailableIPs { get; set; }
    public long TotalIPs { get; set; }
    public double UsagePercent { get; set; }
    public List<string> UsedIPs { get; set; } = new();
}

/// <summary>
/// 网络IPAM使用情况
/// </summary>
public class NetworkIpamUsage
{
    public long TotalIPs { get; set; }
    public long UsedIPs { get; set; }
    public long AvailableIPs { get; set; }
    public double UsagePercent { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// 更新网络请求
/// </summary>
public class UpdateNetworkRequest
{
    public Dictionary<string, string>? Labels { get; set; }
    public Dictionary<string, string>? Options { get; set; }
    public bool? EnableIPv6 { get; set; }
    public bool? Internal { get; set; }
    public bool? Attachable { get; set; }
    public NetworkIpamConfig? IPAM { get; set; }
}

/// <summary>
/// 连接容器到网络请求
/// </summary>
public class ConnectContainerToNetworkRequest
{
    [Required]
    public string ContainerId { get; set; } = string.Empty;
    [Required]
    public string NetworkId { get; set; } = string.Empty;
    public List<string>? Aliases { get; set; }
    public Dictionary<string, string>? EndpointConfig { get; set; }
    public Dictionary<string, string>? IPAMConfig { get; set; }
    public bool Force { get; set; } = false;
}

/// <summary>
/// 断开容器网络连接请求
/// </summary>
public class DisconnectContainerFromNetworkRequest
{
    [Required]
    public string ContainerId { get; set; } = string.Empty;
    [Required]
    public string NetworkId { get; set; } = string.Empty;
    public bool Force { get; set; } = false;
}

/// <summary>
/// 网络服务发现
/// </summary>
public class NetworkServiceDiscovery
{
    public string NetworkId { get; set; } = string.Empty;
    public string NetworkName { get; set; } = string.Empty;
    public List<DiscoveredService> Services { get; set; } = new();
    public DateTime LastDiscovered { get; set; }
}

/// <summary>
/// 发现的服务
/// </summary>
public class DiscoveredService
{
    public string ServiceName { get; set; } = string.Empty;
    public string ContainerId { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public List<int> Ports { get; set; } = new();
    public Dictionary<string, string> Labels { get; set; } = new();
    public DateTime DiscoveredAt { get; set; }
}


/// <summary>
/// 网络健康状态
/// </summary>
public class NetworkHealthStatus
{
    public string NetworkId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public DateTime LastCheck { get; set; }
}

/// <summary>
/// 网络事件
/// </summary>
public class NetworkEvent
{
    public string Type { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = new();
}


/// <summary>
/// 网络检查信息
/// </summary>
public class NetworkInspect
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime Created { get; set; }
    public string Scope { get; set; } = string.Empty;
    public string Driver { get; set; } = string.Empty;
    public bool EnableIPv6 { get; set; }
    public bool Internal { get; set; }
    public Dictionary<string, object> IPAM { get; set; } = new();
    public Dictionary<string, object> Options { get; set; } = new();
    public Dictionary<string, object> Labels { get; set; } = new();
}

/// <summary>
/// 网络配置
/// </summary>
public class NetworkConfig
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Driver { get; set; } = string.Empty;
    public bool Internal { get; set; }
    public bool Attachable { get; set; }
    public bool Ingress { get; set; }
    public Dictionary<string, string> Labels { get; set; } = new();
    public Dictionary<string, object> Options { get; set; } = new();
    public NetworkIPAMConfig IPAM { get; set; } = new();
    public List<string>? Aliases { get; set; }
    public string? IPv4Address { get; set; }
    public string? IPv6Address { get; set; }
    public List<string>? Links { get; set; }
}

/// <summary>
/// 网络IPAM配置
/// </summary>
public class NetworkIPAMConfig
{
    public string Driver { get; set; } = string.Empty;
    public List<IPAMConfigEntry> Config { get; set; } = new();
}

/// <summary>
/// IPAM配置条目
/// </summary>
public class IPAMConfigEntry
{
    public string Subnet { get; set; } = string.Empty;
    public string? IPRange { get; set; }
    public string? Gateway { get; set; }
    public List<string>? AuxiliaryAddresses { get; set; }
}


/// <summary>
/// 网络创建结果
/// </summary>
public class NetworkCreateResult
{
    public string Id { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// 网络验证结果
/// </summary>
public class NetworkValidateResult
{
    public bool Valid { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// 网络备份结果
/// </summary>
public class NetworkBackupResult
{
    public bool Success { get; set; }
    public string BackupPath { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 网络恢复结果
/// </summary>
public class NetworkRestoreResult
{
    public bool Success { get; set; }
    public string NetworkId { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 网络克隆结果
/// </summary>
public class NetworkCloneResult
{
    public bool Success { get; set; }
    public string NewNetworkId { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 网络导出结果
/// </summary>
public class NetworkExportResult
{
    public bool Success { get; set; }
    public string ExportPath { get; set; } = string.Empty;
    public long Size { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 网络导入结果
/// </summary>
public class NetworkImportResult
{
    public bool Success { get; set; }
    public string NetworkId { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 网络安全报告
/// </summary>
public class NetworkSecurityReport
{
    public string NetworkId { get; set; } = string.Empty;
    public int SecurityScore { get; set; }
    public List<string> Vulnerabilities { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// 网络性能指标
/// </summary>
public class NetworkPerformanceMetrics
{
    public string NetworkId { get; set; } = string.Empty;
    public double ThroughputMbps { get; set; }
    public double LatencyMs { get; set; }
    public int PacketLossRate { get; set; }
    public int ActiveConnections { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// 网络优化选项
/// </summary>
public class NetworkOptimizationOptions
{
    public bool OptimizeMTU { get; set; } = true;
    public bool OptimizeDNS { get; set; } = true;
    public bool OptimizeRouting { get; set; } = false;
    public Dictionary<string, string> CustomOptions { get; set; } = new();
}

/// <summary>
/// 网络优化结果
/// </summary>
public class NetworkOptimizationResult
{
    public bool Success { get; set; }
    public List<string> Optimizations { get; set; } = new();
    public double PerformanceImprovement { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 创建网络请求
/// </summary>
public class CreateNetworkRequest
{
    public string Name { get; set; } = string.Empty;
    public string Driver { get; set; } = "bridge";
    public string? Scope { get; set; }
    public bool Internal { get; set; }
    public bool EnableIPv6 { get; set; }
    public bool Ingress { get; set; }
    public bool Attachable { get; set; }
    public Dictionary<string, string> Labels { get; set; } = new();
    public Dictionary<string, string> Options { get; set; } = new();
    public NetworkIPAMConfig? IPAM { get; set; }
    public string? NodeId { get; set; }
}

/// <summary>
/// 网络信息（简化版）
/// </summary>
[Entity]
public class NetworkInfo
{
    [Id]
    [IdGeneration(IdGenerationStrategy.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [Index]
    public string Name { get; set; } = string.Empty;
    public string Driver { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public bool Internal { get; set; }
    public bool EnableIPv6 { get; set; }
    public bool Attachable { get; set; }
    public bool Ingress { get; set; }
    public Dictionary<string, string> Labels { get; set; } = new();
    public List<NetworkContainer> Containers { get; set; } = new();
    [Index]
    public string CreatedAt { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public string? IPAM { get; set; }
    public Dictionary<string, string> Options { get; set; } = new();
}

