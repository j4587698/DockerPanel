using System.ComponentModel.DataAnnotations;
using TinyDb.Attributes;

namespace DockerPanel.API.Models;

/// <summary>
/// 节点状态枚举
/// </summary>
public enum NodeResourceStatus
{
    Online,
    Offline,
    Warning,
    Error,
    Unknown
}

/// <summary>
/// Docker 连接类型
/// </summary>
public enum DockerConnectionType
{
    /// <summary>
    /// 本地连接（Unix Socket 或 Windows Named Pipe）
    /// </summary>
    Local = 0,
    
    /// <summary>
    /// TCP 直连（不安全，仅用于内网）
    /// </summary>
    Tcp = 1,
    
    /// <summary>
    /// HTTPS/TLS 连接
    /// </summary>
    Tls = 2,
    
    /// <summary>
    /// SSH 隧道连接
    /// </summary>
    SshTunnel = 3
}

/// <summary>
/// 节点信息
/// </summary>
[Entity]
public class NodeInfo
{
    [Id]
    [IdGeneration(IdGenerationStrategy.ObjectId)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 节点ID别名，兼容性属性
    /// </summary>
    public string NodeId => Id;

    [Index]
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string EngineType { get; set; } = string.Empty; // docker, podman
    public string Version { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = string.Empty;
    public string Os { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string KernelVersion { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public DateTime? LastConnected { get; set; }
    public DateTime? LastSeen { get; set; }
    [Index]
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Dictionary<string, string> Labels { get; set; } = new();
    public NodeResources Resources { get; set; } = new();
    [Index]
    public NodeResourceStatus Status { get; set; } = NodeResourceStatus.Unknown;
    public List<string> Roles { get; set; } = new(); // manager, worker
    public string? Availability { get; set; } // active, pause, drain
    public string? Platform { get; set; }
    public string? Address { get; set; }
    public string? DataPath { get; set; }
    public string? EngineUrl { get; set; }
    public bool UseSsh { get; set; }
    public string? SshUsername { get; set; }
    public string? SshPrivateKeyPath { get; set; }
    public int? SshPort { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? PrivateKeyPath { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();

    #region 新增：多节点远程管理字段

    /// <summary>
    /// 连接类型：Local, Tcp, Tls, SshTunnel
    /// </summary>
    public DockerConnectionType ConnectionType { get; set; } = DockerConnectionType.Local;

    /// <summary>
    /// Docker API 端点地址（完整URI）
    /// </summary>
    public string? DockerEndpoint { get; set; }

    /// <summary>
    /// 所属分组ID
    /// </summary>
    [Index]
    public string? GroupId { get; set; }

    /// <summary>
    /// 所属分组名称（冗余字段，便于显示）
    /// </summary>
    public string? GroupName { get; set; }

    /// <summary>
    /// 标签列表（用于分类和筛选）
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// 是否为默认节点
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// 节点排序权重
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 节点描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// TLS 配置
    /// </summary>
    public NodeTlsConfig? TlsConfig { get; set; }

    /// <summary>
    /// SSH 隧道配置
    /// </summary>
    public NodeSshTunnelConfig? SshTunnelConfig { get; set; }

    /// <summary>
    /// 连接超时时间（秒）
    /// </summary>
    public int ConnectionTimeout { get; set; } = 30;

    /// <summary>
    /// 是否启用健康检查
    /// </summary>
    public bool EnableHealthCheck { get; set; } = true;

    /// <summary>
    /// 健康检查间隔（秒）
    /// </summary>
    public int HealthCheckInterval { get; set; } = 60;

    /// <summary>
    /// 上次健康检查时间
    /// </summary>
    public DateTime? LastHealthCheck { get; set; }

    /// <summary>
    /// 健康检查结果消息
    /// </summary>
    public string? HealthCheckMessage { get; set; }

    #endregion
}

/// <summary>
/// 节点 TLS 配置
/// </summary>
public class NodeTlsConfig
{
    /// <summary>
    /// 是否启用 TLS
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// CA 证书路径
    /// </summary>
    public string? CaCertPath { get; set; }

    /// <summary>
    /// 客户端证书路径
    /// </summary>
    public string? ClientCertPath { get; set; }

    /// <summary>
    /// 客户端密钥路径
    /// </summary>
    public string? ClientKeyPath { get; set; }

    /// <summary>
    /// 是否跳过证书验证（不安全）
    /// </summary>
    public bool SkipVerify { get; set; }

    /// <summary>
    /// 服务器名称（用于 SNI）
    /// </summary>
    public string? ServerName { get; set; }
}

/// <summary>
/// 节点 SSH 隧道配置
/// </summary>
public class NodeSshTunnelConfig
{
    /// <summary>
    /// SSH 主机地址
    /// </summary>
    public string SshHost { get; set; } = string.Empty;

    /// <summary>
    /// SSH 端口
    /// </summary>
    public int SshPort { get; set; } = 22;

    /// <summary>
    /// SSH 用户名
    /// </summary>
    public string SshUsername { get; set; } = string.Empty;

    /// <summary>
    /// SSH 密码（加密存储）
    /// </summary>
    public string? SshPassword { get; set; }

    /// <summary>
    /// SSH 私钥路径
    /// </summary>
    public string? SshPrivateKeyPath { get; set; }

    /// <summary>
    /// SSH 私钥密码
    /// </summary>
    public string? SshPrivateKeyPassphrase { get; set; }

    /// <summary>
    /// 远程 Docker Socket 路径
    /// </summary>
    public string RemoteDockerSocket { get; set; } = "/var/run/docker.sock";

    /// <summary>
    /// 本地转发端口（0表示自动分配）
    /// </summary>
    public int LocalForwardPort { get; set; }

    /// <summary>
    /// SSH 连接ID（关联 SshConnectionConfigEntity）
    /// </summary>
    public string? SshConnectionId { get; set; }
}

/// <summary>
/// 节点
/// </summary>
public class Node
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 2375;
    public string EngineType { get; set; } = "docker";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? PrivateKeyPath { get; set; }
    public bool UseSsh { get; set; } = false;
    public int SshPort { get; set; } = 22;
    public string SshUsername { get; set; } = string.Empty;
    public string? SshPassword { get; set; }
    public string SshPrivateKeyPath { get; set; } = string.Empty;
    public Dictionary<string, string> Labels { get; set; } = new();
    public bool IsOnline { get; set; } = false;
    public DateTime? LastConnected { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public NodeResources Resources { get; set; } = new();
    public NodeResourceStatus Status { get; set; } = NodeResourceStatus.Unknown;

    #region 新增：多节点远程管理字段

    /// <summary>
    /// 连接类型
    /// </summary>
    public DockerConnectionType ConnectionType { get; set; } = DockerConnectionType.Local;

    /// <summary>
    /// Docker API 端点地址
    /// </summary>
    public string? DockerEndpoint { get; set; }

    /// <summary>
    /// 所属分组ID
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>
    /// 所属分组名称
    /// </summary>
    public string? GroupName { get; set; }

    /// <summary>
    /// 标签列表
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// 是否为默认节点
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// 节点排序权重
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 节点描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// TLS 配置
    /// </summary>
    public NodeTlsConfig? TlsConfig { get; set; }

    /// <summary>
    /// SSH 隧道配置
    /// </summary>
    public NodeSshTunnelConfig? SshTunnelConfig { get; set; }

    /// <summary>
    /// 连接超时时间（秒）
    /// </summary>
    public int ConnectionTimeout { get; set; } = 30;

    #endregion
}

/// <summary>
/// 节点资源信息
/// </summary>
public class NodeResources
{
    public long TotalCpu { get; set; }
    public long AvailableCpu { get; set; }
    public long TotalMemory { get; set; }
    public long AvailableMemory { get; set; }
    public long TotalDisk { get; set; }
    public long AvailableDisk { get; set; }
    public double CpuUsagePercent { get; set; }
    public double MemoryUsagePercent { get; set; }
    public double DiskUsagePercent { get; set; }
    public int ContainerCount { get; set; }
    public int RunningContainerCount { get; set; }
    public int StoppedContainerCount { get; set; }
    public int ImageCount { get; set; }
    public int NetworkCount { get; set; }
    public int VolumeCount { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// 节点状态
/// </summary>
public class NodeStatus
{
    public string State { get; set; } = string.Empty; // ready, down, unknown
    public string Message { get; set; } = string.Empty;
    public string Addr { get; set; } = string.Empty;
    public DateTime? LastSeen { get; set; }
    public bool IsHealthy { get; set; }
    public List<NodeHealthCheck> HealthChecks { get; set; } = new();
}

/// <summary>
/// 节点健康检查
/// </summary>
public class NodeHealthCheck
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // passing, warning, critical
    public string Message { get; set; } = string.Empty;
    public DateTime LastCheck { get; set; }
    public TimeSpan ResponseTime { get; set; }
}

/// <summary>
/// 节点组
/// </summary>
public class NodeGroup
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> NodeIds { get; set; } = new();
    public Dictionary<string, string> Labels { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public int NodeCount { get; set; }
    public int OnlineNodeCount { get; set; }
    public GroupSettings Settings { get; set; } = new();

    #region 新增字段

    /// <summary>
    /// 分组颜色（用于UI显示）
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// 分组图标
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// 排序权重
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 父分组ID（支持分组层级）
    /// </summary>
    public string? ParentGroupId { get; set; }

    #endregion
}

/// <summary>
/// 组设置
/// </summary>
public class GroupSettings
{
    public bool LoadBalancing { get; set; } = false;
    public string LoadBalancingStrategy { get; set; } = "round_robin"; // round_robin, least_connections, random
    public bool AutoFailover { get; set; } = false;
    public int MaxRetries { get; set; } = 3;
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan HealthCheckTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public Dictionary<string, object> CustomSettings { get; set; } = new();
}

/// <summary>
/// 创建节点组请求
/// </summary>
public class CreateNodeGroupRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> NodeIds { get; set; } = new();
    public Dictionary<string, string> Labels { get; set; } = new();
    public GroupSettings? Settings { get; set; }
}

/// <summary>
/// 更新节点组请求
/// </summary>
public class UpdateNodeGroupRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public List<string>? NodeIds { get; set; }
    public Dictionary<string, string>? Labels { get; set; }
    public GroupSettings? Settings { get; set; }
    public bool? IsActive { get; set; }
}

/// <summary>
/// 批量节点操作请求
/// </summary>
public class BatchNodeOperationRequest
{
    [Required]
    public List<string> NodeIds { get; set; } = new();
    [Required]
    public string Operation { get; set; } = string.Empty;
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// 节点统计信息
/// </summary>
public class NodeStatistics
{
    public string NodeId { get; set; } = string.Empty;
    public string NodeName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public NodeResourceUsage ResourceUsage { get; set; } = new();
    public NodeContainerStats ContainerStats { get; set; } = new();
    public NodeNetworkStats NetworkStats { get; set; } = new();
    public NodeSystemStats SystemStats { get; set; } = new();
}

/// <summary>
/// 节点资源使用情况
/// </summary>
public class NodeResourceUsage
{
    public double CpuUsagePercent { get; set; }
    public double MemoryUsagePercent { get; set; }
    public double DiskUsagePercent { get; set; }
    public double NetworkInBytes { get; set; }
    public double NetworkOutBytes { get; set; }
    public long DiskReadBytes { get; set; }
    public long DiskWriteBytes { get; set; }
}

/// <summary>
/// 节点容器统计
/// </summary>
public class NodeContainerStats
{
    public int TotalContainers { get; set; }
    public int RunningContainers { get; set; }
    public int StoppedContainers { get; set; }
    public int PausedContainers { get; set; }
    public int RestartingContainers { get; set; }
    public int RemovingContainers { get; set; }
    public int DeadContainers { get; set; }
    public int CreatedContainers { get; set; }
}

/// <summary>
/// 节点网络统计
/// </summary>
public class NodeNetworkStats
{
    public int TotalNetworks { get; set; }
    public int ConnectedNetworks { get; set; }
    public long TotalBytesReceived { get; set; }
    public long TotalBytesTransmitted { get; set; }
    public long TotalPacketsReceived { get; set; }
    public long TotalPacketsTransmitted { get; set; }
}

/// <summary>
/// 节点系统统计
/// </summary>
public class NodeSystemStats
{
    public double LoadAverage1m { get; set; }
    public double LoadAverage5m { get; set; }
    public double LoadAverage15m { get; set; }
    public TimeSpan Uptime { get; set; }
    public int ProcessCount { get; set; }
    public int ThreadCount { get; set; }
    public long FileDescriptors { get; set; }
    public long MaxFileDescriptors { get; set; }
}

/// <summary>
/// 添加节点请求（扩展版）
/// </summary>
public class AddNodeRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 主机地址
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Docker API 端口
    /// </summary>
    public int Port { get; set; } = 2375;

    /// <summary>
    /// 引擎类型：docker, podman
    /// </summary>
    public string EngineType { get; set; } = "docker";

    /// <summary>
    /// 连接类型
    /// </summary>
    public DockerConnectionType ConnectionType { get; set; } = DockerConnectionType.Local;

    /// <summary>
    /// 完整的 Docker 端点 URI（可选，优先于 Host:Port）
    /// </summary>
    public string? DockerEndpoint { get; set; }

    /// <summary>
    /// 所属分组ID
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>
    /// 标签列表
    /// </summary>
    public List<string>? Tags { get; set; }

    /// <summary>
    /// 标签字典（兼容旧版）
    /// </summary>
    public Dictionary<string, string>? Labels { get; set; }

    /// <summary>
    /// 是否为默认节点
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// 节点描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 连接超时时间（秒）
    /// </summary>
    public int ConnectionTimeout { get; set; } = 30;

    /// <summary>
    /// 是否启用健康检查
    /// </summary>
    public bool EnableHealthCheck { get; set; } = true;

    /// <summary>
    /// 健康检查间隔（秒）
    /// </summary>
    public int HealthCheckInterval { get; set; } = 60;

    #region 认证配置

    /// <summary>
    /// 用户名（TCP 基础认证）
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// 密码（TCP 基础认证）
    /// </summary>
    public string? Password { get; set; }

    #endregion

    #region TLS 配置

    /// <summary>
    /// TLS 配置
    /// </summary>
    public NodeTlsConfig? TlsConfig { get; set; }

    #endregion

    #region SSH 隧道配置

    /// <summary>
    /// 是否使用 SSH
    /// </summary>
    public bool UseSsh { get; set; }

    /// <summary>
    /// SSH 端口
    /// </summary>
    public int SshPort { get; set; } = 22;

    /// <summary>
    /// SSH 用户名
    /// </summary>
    public string? SshUsername { get; set; }

    /// <summary>
    /// SSH 密码
    /// </summary>
    public string? SshPassword { get; set; }

    /// <summary>
    /// SSH 私钥路径
    /// </summary>
    public string? SshPrivateKeyPath { get; set; }

    /// <summary>
    /// SSH 私钥密码
    /// </summary>
    public string? SshPrivateKeyPassphrase { get; set; }

    /// <summary>
    /// 远程 Docker Socket 路径
    /// </summary>
    public string RemoteDockerSocket { get; set; } = "/var/run/docker.sock";

    /// <summary>
    /// 关联的 SSH 连接ID
    /// </summary>
    public string? SshConnectionId { get; set; }

    #endregion
}

/// <summary>
/// 更新节点请求（扩展版）
/// </summary>
public class UpdateNodeRequest
{
    public string? Name { get; set; }
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? EngineType { get; set; }
    public DockerConnectionType? ConnectionType { get; set; }
    public string? DockerEndpoint { get; set; }
    public string? GroupId { get; set; }
    public List<string>? Tags { get; set; }
    public Dictionary<string, string>? Labels { get; set; }
    public bool? IsDefault { get; set; }
    public string? Description { get; set; }
    public int? ConnectionTimeout { get; set; }
    public bool? EnableHealthCheck { get; set; }
    public int? HealthCheckInterval { get; set; }

    #region 认证配置
    public string? Username { get; set; }
    public string? Password { get; set; }
    #endregion

    #region TLS 配置
    public NodeTlsConfig? TlsConfig { get; set; }
    #endregion

    #region SSH 隧道配置
    public bool? UseSsh { get; set; }
    public int? SshPort { get; set; }
    public string? SshUsername { get; set; }
    public string? SshPassword { get; set; }
    public string? SshPrivateKeyPath { get; set; }
    public string? SshPrivateKeyPassphrase { get; set; }
    public string? RemoteDockerSocket { get; set; }
    public string? SshConnectionId { get; set; }
    #endregion
}

/// <summary>
/// 测试节点连接请求
/// </summary>
public class TestNodeConnectionRequest
{
    public string? Host { get; set; }
    public int? Port { get; set; }
    public DockerConnectionType? ConnectionType { get; set; }
    public string? DockerEndpoint { get; set; }
    public int? ConnectionTimeout { get; set; }

    // 认证
    public string? Username { get; set; }
    public string? Password { get; set; }

    // TLS
    public NodeTlsConfig? TlsConfig { get; set; }

    // SSH
    public bool? UseSsh { get; set; }
    public int? SshPort { get; set; }
    public string? SshUsername { get; set; }
    public string? SshPassword { get; set; }
    public string? SshPrivateKeyPath { get; set; }
    public string? RemoteDockerSocket { get; set; }
}

/// <summary>
/// 测试节点连接结果
/// </summary>
public class TestNodeConnectionResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? DockerVersion { get; set; }
    public string? ApiVersion { get; set; }
    public string? Os { get; set; }
    public string? Architecture { get; set; }
    public long? ResponseTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 节点连接测试结果
/// </summary>
public class NodeConnectionTestResult
{
    public string NodeId { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public string? ErrorMessage { get; set; }
    public long ResponseTimeMs { get; set; }
    public DateTime TestedAt { get; set; } = DateTime.UtcNow;
}
