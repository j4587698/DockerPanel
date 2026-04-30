using System.ComponentModel.DataAnnotations;

namespace DockerPanel.API.Models;

/// <summary>
/// 创建容器请求模型
/// </summary>
public class CreateContainerRequest
{
    public string? Name { get; set; }
    public string? Image { get; set; }
    public string? User { get; set; }
    public List<string>? Entrypoint { get; set; }
    public List<string>? Command { get; set; }
    public List<PortMapping>? Ports { get; set; }
    public List<VolumeMapping>? Volumes { get; set; }
    public List<DeviceMapping>? Devices { get; set; }
    public List<TmpfsMount>? Tmpfs { get; set; }
    public Dictionary<string, string>? Environment { get; set; }
    public Dictionary<string, string>? Labels { get; set; }
    public bool AutoRemove { get; set; } = false;
    public bool Interactive { get; set; } = false;
    public bool Tty { get; set; } = false;
    public bool Privileged { get; set; } = false;
    public bool ReadOnlyRootfs { get; set; } = false;
    public bool HostPid { get; set; } = false;
    public bool Init { get; set; } = false;
    public string? WorkingDir { get; set; }
    public string? Hostname { get; set; }
    public string? MacAddress { get; set; }
    public string? ShmSize { get; set; }
    public string? StopSignal { get; set; }
    public int? StopTimeout { get; set; }
    public List<string>? GroupAdd { get; set; }
    public List<string>? Dns { get; set; }
    public List<string>? DnsSearch { get; set; }
    public List<string>? ExtraHosts { get; set; }
    public string? NetworkMode { get; set; } = "bridge";
    public List<string>? CapAdd { get; set; }
    public List<string>? CapDrop { get; set; }
    public string? Runtime { get; set; }
    public LogConfig? LogConfig { get; set; }
    public ResourceLimits? Resources { get; set; }
    public RestartPolicy? RestartPolicy { get; set; }
    public ContainerNetworkConfig? Network { get; set; }
    public HealthCheckConfig? HealthCheck { get; set; }
    public DomainMappingConfig? DomainMapping { get; set; }
    public string? ConnectionId { get; set; }
}

/// <summary>
/// 端口映射
/// </summary>
public class PortMapping
{
    public string HostPort { get; set; } = string.Empty;
    public string ContainerPort { get; set; } = string.Empty;
    public string Protocol { get; set; } = "tcp";
    public string? HostIp { get; set; }
}

/// <summary>
/// 卷映射
/// </summary>
public class VolumeMapping
{
    public string HostPath { get; set; } = string.Empty;
    public string ContainerPath { get; set; } = string.Empty;
    public bool ReadOnly { get; set; } = false;
}

/// <summary>
/// Tmpfs 挂载
/// </summary>
public class TmpfsMount
{
    public string ContainerPath { get; set; } = string.Empty;
    public string? Options { get; set; }
}

/// <summary>
/// 设备映射
/// </summary>
public class DeviceMapping
{
    public string HostPath { get; set; } = string.Empty;
    public string ContainerPath { get; set; } = string.Empty;
}

/// <summary>
/// 日志配置
/// </summary>
public class LogConfig
{
    public string Driver { get; set; } = "json-file";
    public string? MaxSize { get; set; }
    public int? MaxFile { get; set; }
}

/// <summary>
/// 资源限制
/// </summary>
public class ResourceLimits
{
    public string? MemoryLimit { get; set; }
    public string? MemorySwap { get; set; }
    public string? MemoryReservation { get; set; }
    public string? CpuQuota { get; set; }
    public string? CpuPeriod { get; set; }
    public string? CpuShares { get; set; }
    public string? CpusetCpus { get; set; }
    public string? ShmSize { get; set; }
    public long? PidsLimit { get; set; }
}

/// <summary>
/// 重启策略
/// </summary>
public class RestartPolicy
{
    public string Name { get; set; } = "no";
    public int? MaximumRetryCount { get; set; }
}


/// <summary>
/// 容器网络配置
/// </summary>
public class ContainerNetworkConfig
{
    public string? NetworkId { get; set; }
    public List<string>? Aliases { get; set; }
    public string? IpAddress { get; set; }
    public List<string>? AdditionalNetworks { get; set; }
}

/// <summary>
/// 执行命令请求
/// </summary>
public class ExecCommandRequest
{
    [Required]
    public string[] Command { get; set; } = Array.Empty<string>();
    public bool Detach { get; set; } = false;
    public bool Tty { get; set; } = false;
    public bool Interactive { get; set; } = false;
    public Dictionary<string, string>? Environment { get; set; }
    public string? WorkingDir { get; set; }
}

/// <summary>
/// 更新容器请求
/// </summary>
public class UpdateContainerRequest
{
    public ResourceLimits? Resources { get; set; }
    public RestartPolicy? RestartPolicy { get; set; }
}

/// <summary>
/// 批量容器操作请求
/// </summary>
public class BatchContainerOperationRequest
{
    [Required]
    public List<string> ContainerIds { get; set; } = new();
    [Required]
    public string Operation { get; set; } = string.Empty;
    public Dictionary<string, object>? Parameters { get; set; }
    public int? Timeout { get; set; }
    public bool? Force { get; set; }
}

/// <summary>
/// 健康检查配置
/// </summary>
public class HealthCheckConfig
{
    /// <summary>
    /// 健康检查命令字符串，如 "CMD curl -f http://localhost/"
    /// </summary>
    public string? Test { get; set; }

    /// <summary>
    /// 检查间隔（秒）
    /// </summary>
    public int Interval { get; set; } = 30;

    /// <summary>
    /// 超时时间（秒）
    /// </summary>
    public int Timeout { get; set; } = 10;

    /// <summary>
    /// 重试次数
    /// </summary>
    public int Retries { get; set; } = 3;

    /// <summary>
    /// 容器启动后等待时间（秒）
    /// </summary>
    public int StartPeriod { get; set; } = 0;

    /// <summary>
    /// 连续成功次数阈值
    /// </summary>
    public int StartInterval { get; set; } = 5;
}

/// <summary>
/// 健康检查状态
/// </summary>
public class HealthCheckStatus
{
    public string Status { get; set; } = string.Empty; // "healthy", "unhealthy", "starting", "none"
    public string? FailingStreak { get; set; }
    public string? Log { get; set; }
    public DateTime? LastCheck { get; set; }
    public DateTime? StartedAt { get; set; }
}

/// <summary>
/// 健康检查日志
/// </summary>
public class HealthCheckLog
{
    public string ContainerId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Output { get; set; }
    public int ExitCode { get; set; }
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// 健康检查统计
/// </summary>
public class HealthCheckStats
{
    public string ContainerId { get; set; } = string.Empty;
    public int TotalChecks { get; set; }
    public int SuccessfulChecks { get; set; }
    public int FailedChecks { get; set; }
    public int ConsecutiveFailures { get; set; }
    public int ConsecutiveSuccesses { get; set; }
    public DateTime LastSuccess { get; set; }
    public DateTime LastFailure { get; set; }
    public double SuccessRate { get; set; }
    public TimeSpan AverageResponseTime { get; set; }
}


/// <summary>
/// 块IO设备限制
/// </summary>
public class BlkioDeviceLimit
{
    /// <summary>
    /// 设备路径
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 读速率（字节/秒）
    /// </summary>
    public long? ReadRate { get; set; }

    /// <summary>
    /// 写速率（字节/秒）
    /// </summary>
    public long? WriteRate { get; set; }

    /// <summary>
    /// 读IOPS
    /// </summary>
    public long? ReadIops { get; set; }

    /// <summary>
    /// 写IOPS
    /// </summary>
    public long? WriteIops { get; set; }
}

/// <summary>
/// 更新容器资源限制请求
/// </summary>
public class UpdateContainerResourcesRequest
{
    /// <summary>
    /// 重启策略
    /// </summary>
    public RestartPolicyRequest? RestartPolicy { get; set; }

    /// <summary>
    /// CPU限制（核心数，如 1.5）
    /// </summary>
    public double? CpuLimit { get; set; }

    /// <summary>
    /// CPU配额（微秒）
    /// </summary>
    public long? CpuQuota { get; set; }

    /// <summary>
    /// CPU周期（微秒）
    /// </summary>
    public long? CpuPeriod { get; set; }

    /// <summary>
    /// CPU权重
    /// </summary>
    public long? CpuShares { get; set; }

    /// <summary>
    /// 内存限制（字节）
    /// </summary>
    public long? Memory { get; set; }

    /// <summary>
    /// 内存限制（字节）- 别名
    /// </summary>
    public long? MemoryLimit { get => Memory; set => Memory = value; }

    /// <summary>
    /// 内存交换限制（字节）
    /// </summary>
    public long? MemorySwap { get; set; }

    /// <summary>
    /// 内存交换限制（字节）- 别名
    /// </summary>
    public long? MemorySwapLimit { get => MemorySwap; set => MemorySwap = value; }

    /// <summary>
    /// 内存预留（字节）
    /// </summary>
    public long? MemoryReservation { get; set; }

    /// <summary>
    /// CPU核心绑定（如 "0-3" 或 "0,2"）
    /// </summary>
    public string? CpusetCpus { get; set; }

    /// <summary>
    /// CPU内存节点绑定
    /// </summary>
    public string? CpusetMems { get; set; }

    /// <summary>
    /// 块IO权重
    /// </summary>
    public long? BlkioWeight { get; set; }

    /// <summary>
    /// PID限制
    /// </summary>
    public long? PidsLimit { get; set; }

    /// <summary>
    /// 是否禁用OOM Killer
    /// </summary>
    public bool? OomKillDisable { get; set; }
}

/// <summary>
/// 重启策略请求
/// </summary>
public class RestartPolicyRequest
{
    /// <summary>
    /// 策略名称: no, always, on-failure, unless-stopped
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 最大重试次数（仅 on-failure 策略有效）
    /// </summary>
    public int MaximumRetryCount { get; set; }
}

/// <summary>
/// 域名映射配置
/// </summary>
public class DomainMappingConfig
{
    /// <summary>
    /// 域名
    /// </summary>
    public string? Domain { get; set; }

    /// <summary>
    /// 容器端口
    /// </summary>
    public int ContainerPort { get; set; } = 80;

    /// <summary>
    /// 是否启用SSL
    /// </summary>
    public bool EnableSsl { get; set; } = false;

    /// <summary>
    /// SSL证书ID（可选）
    /// </summary>
    public string? CertificateId { get; set; }

    /// <summary>
    /// ACME账户ID（用于自动申请证书）
    /// </summary>
    public string? AccountId { get; set; }

    /// <summary>
    /// 是否自动申请证书
    /// </summary>
    public bool AutoRequestCertificate { get; set; } = false;

    /// <summary>
    /// 路径前缀（可选）
    /// </summary>
    public string? PathPrefix { get; set; } = "/";

    /// <summary>
    /// 额外的YARP路由配置
    /// </summary>
    public Dictionary<string, object>? YarpConfig { get; set; }
}

/// <summary>
/// 重建容器请求
/// </summary>
public class RecreateContainerRequest
{
    /// <summary>
    /// 是否拉取最新镜像
    /// </summary>
    public bool PullLatest { get; set; } = false;

    /// <summary>
    /// 是否自动启动新容器
    /// </summary>
    public bool AutoStart { get; set; } = true;

    /// <summary>
    /// 是否保留数据卷
    /// </summary>
    public bool KeepVolumes { get; set; } = true;
}

/// <summary>
/// 重命名容器请求
/// </summary>
public class RenameContainerRequest
{
    /// <summary>
    /// 新名称
    /// </summary>
    public string NewName { get; set; } = string.Empty;
}