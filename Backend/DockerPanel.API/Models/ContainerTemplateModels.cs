using TinyDb.Attributes;

namespace DockerPanel.API.Models;

/// <summary>
/// 容器模板
/// </summary>
[Entity]
public class ContainerTemplate
{
    [Id]
    [IdGeneration(IdGenerationStrategy.GuidV7)]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Index]
    public string Name { get; set; } = string.Empty;
    
    [Index]
    public string Type { get; set; } = "custom"; // web, database, cache, queue, monitoring, development, custom
    public string? Description { get; set; }
    public string Image { get; set; } = string.Empty;
    public List<string>? Command { get; set; }
    public string? WorkingDir { get; set; }
    public string? User { get; set; }
    public List<TemplatePortMapping>? Ports { get; set; }
    public List<TemplateVolumeMapping>? Volumes { get; set; }
    public Dictionary<string, string>? Environment { get; set; }
    public Dictionary<string, string>? Labels { get; set; }
    public TemplateRestartPolicy? RestartPolicy { get; set; }
    public string? NetworkMode { get; set; }
    public List<TemplateNetworkConfig>? Networks { get; set; }
    [Index]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 模板端口映射
/// </summary>
public class TemplatePortMapping
{
    public string? HostIp { get; set; }
    public ushort? HostPort { get; set; }
    public ushort ContainerPort { get; set; }
    public string Protocol { get; set; } = "tcp";
}

/// <summary>
/// 模板卷映射
/// </summary>
public class TemplateVolumeMapping
{
    public string? HostPath { get; set; }
    public string ContainerPath { get; set; } = string.Empty;
    public bool ReadOnly { get; set; }
}

/// <summary>
/// 模板重启策略
/// </summary>
public class TemplateRestartPolicy
{
    public string Name { get; set; } = "no"; // no, always, on-failure, unless-stopped
    public int MaximumRetryCount { get; set; }
}

/// <summary>
/// 模板网络配置
/// </summary>
public class TemplateNetworkConfig
{
    public string NetworkId { get; set; } = string.Empty;
    public string? NetworkName { get; set; }
    public List<string>? Aliases { get; set; }
    public string? IpAddress { get; set; }
}

/// <summary>
/// 创建模板请求
/// </summary>
public class CreateTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "custom";
    public string? Description { get; set; }
    public string Image { get; set; } = string.Empty;
    public List<string>? Command { get; set; }
    public string? WorkingDir { get; set; }
    public string? User { get; set; }
    public List<TemplatePortMapping>? Ports { get; set; }
    public List<TemplateVolumeMapping>? Volumes { get; set; }
    public Dictionary<string, string>? Environment { get; set; }
    public Dictionary<string, string>? Labels { get; set; }
    public TemplateRestartPolicy? RestartPolicy { get; set; }
    public string? NetworkMode { get; set; }
    public List<TemplateNetworkConfig>? Networks { get; set; }
}

/// <summary>
/// 更新模板请求
/// </summary>
public class UpdateTemplateRequest : CreateTemplateRequest
{
    public string Id { get; set; } = string.Empty;
}
