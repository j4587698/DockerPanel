using TinyDb.Attributes;

namespace DockerPanel.API.Models;

/// <summary>
/// 容器信息
/// </summary>
[Entity]
public class ContainerInfo
{
    [IdGeneration(IdGenerationStrategy.ObjectId)]
    public string Id { get; set; } = string.Empty;
    
    [Index]
    public string? Name { get; set; }
    [Index]
    public string? Image { get; set; }
    public string? ImageId { get; set; }
    [Index]
    public string? Status { get; set; }
    public string? State { get; set; }
    [Index]
    public DateTime Created { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public List<string>? Command { get; set; }
    public List<string>? Entrypoint { get; set; }
    public List<string>? Environment { get; set; }
    public string? WorkingDir { get; set; }
    public List<ContainerPortMapping> Ports { get; set; } = new();
    public NetworkSettings? NetworkSettings { get; set; }
    public HealthConfig? Health { get; set; }
    public HealthCheckStatus? HealthStatus { get; set; }
    public RestartPolicy? RestartPolicy { get; set; }
    public ResourceLimits? Resources { get; set; }
    public Dictionary<string, string> Labels { get; set; } = new();
    public List<ContainerMount> Mounts { get; set; } = new();
    public string NodeId { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public ContainerHostConfig? HostConfig { get; set; }
    
    /// <summary>
    /// 域名映射（反向代理配置），非持久化属性
    /// </summary>
    public List<ContainerDomainMapping>? DomainMappings { get; set; }
}

/// <summary>
/// 容器域名映射信息（用于API返回）
/// </summary>
public class ContainerDomainMapping
{
    public string Id { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public int ContainerPort { get; set; }
    public string? PathPrefix { get; set; }
    public bool EnableSsl { get; set; }
    public bool Enabled { get; set; }
}

/// <summary>
/// 主机配置
/// </summary>
public class ContainerHostConfig
{
    public string? NetworkMode { get; set; }
    public RestartPolicy? RestartPolicy { get; set; }
    public bool Privileged { get; set; }
    public bool AutoRemove { get; set; }
}

/// <summary>
/// 容器挂载点
/// </summary>
public class ContainerMount
{
    public string? Type { get; set; }
    public string? Name { get; set; }
    public string? Source { get; set; }
    public string? Destination { get; set; }
    public string? Mode { get; set; }
    public bool Rw { get; set; }
    public string? Driver { get; set; }
    public string? Propagation { get; set; }
}

/// <summary>
/// 容器端口映射
/// </summary>
public class ContainerPortMapping
{
    public string? Ip { get; set; }
    public ushort PrivatePort { get; set; }
    public ushort PublicPort { get; set; }
    public string? Type { get; set; }
}

/// <summary>
/// 网络设置
/// </summary>
public class NetworkSettings
{
    public Dictionary<string, NetworkEndpoint> Networks { get; set; } = new();
}

/// <summary>
/// 网络端点
/// </summary>
public class NetworkEndpoint
{
    public string? IpAddress { get; set; }
    public string? IPPrefixLen { get; set; }
    public string? Gateway { get; set; }
    public string? MacAddress { get; set; }
    public List<string> Aliases { get; set; } = new();
}

/// <summary>
/// 存储卷信息
/// </summary>
[Entity]
public class VolumeInfo
{
    [IdGeneration(IdGenerationStrategy.ObjectId)]
    public string Id { get; set; } = string.Empty;
    
    [Index]
    public string Name { get; set; } = string.Empty;
    public string? Driver { get; set; }
    public string? Mountpoint { get; set; }
    public string? Status { get; set; }
    public string? Scope { get; set; }
    
    [Index]
    public DateTime Created { get; set; }
    // 兼容性别名
    public DateTime CreatedAt { get => Created; set => Created = value; }
    
    public long Size { get; set; }
    public int UsageCount { get; set; }
    public Dictionary<string, string> Labels { get; set; } = new();
    public Dictionary<string, string> Options { get; set; } = new();
    public string NodeId { get; set; } = string.Empty;
}

/// <summary>
/// 健康配置
/// </summary>
public class HealthConfig
{
    public List<string>? Test { get; set; }
    public long Interval { get; set; }
    public long Timeout { get; set; }
    public int Retries { get; set; }
    public long StartPeriod { get; set; }
}

/// <summary>
/// 镜像拉取进度
/// </summary>
public class ImagePullProgress
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ProgressDetail { get; set; } = string.Empty;
    public long Current { get; set; }
    public long Total { get; set; }
}

/// <summary>
/// 镜像推送进度
/// </summary>
public class ImagePushProgress
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ProgressDetail { get; set; } = string.Empty;
    public long Current { get; set; }
    public long Total { get; set; }
}

/// <summary>
/// 镜像构建进度
/// </summary>
public class ImageBuildProgress
{
    public string Stream { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public string ImageId { get; set; } = string.Empty;
    public string Aux { get; set; } = string.Empty;
}

/// <summary>
/// 构建镜像参数
/// </summary>
public class BuildImageParams
{
    public string Tag { get; set; } = string.Empty;
    public string Dockerfile { get; set; } = "Dockerfile";
    public Dictionary<string, string> BuildArgs { get; set; } = new();
    public bool NoCache { get; set; } = false;
    public bool Remove { get; set; } = true;
}

/// <summary>
/// 引擎版本信息
/// </summary>
public class EngineVersionInfo
{
    public string Version { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = string.Empty;
    public string Arch { get; set; } = string.Empty;
    public string Os { get; set; } = string.Empty;
    public string KernelVersion { get; set; } = string.Empty;
    public string BuildTime { get; set; } = string.Empty;
}

/// <summary>
/// 引擎系统信息
/// </summary>
public class EngineSystemInfo
{
    public string OSType { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string KernelVersion { get; set; } = string.Empty;
    public int NCPU { get; set; }
    public long MemTotal { get; set; }
    public string DockerRootDir { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}