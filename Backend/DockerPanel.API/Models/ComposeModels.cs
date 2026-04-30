using System.ComponentModel.DataAnnotations;
using TinyDb.Attributes;

namespace DockerPanel.API.Models;

/// <summary>
/// Docker Compose文件信息
/// </summary>
[Entity]
public class ComposeFile
{
    [Id]
    [IdGeneration(IdGenerationStrategy.ObjectId)]
    public string Id { get; set; } = string.Empty;
    
    [Index]
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    [Index]
    public string? NodeId { get; set; }
    public string NodeName { get; set; } = string.Empty;
    public string Version { get; set; } = "3.8";
    public List<string> Services { get; set; } = new();
    public List<string> Networks { get; set; } = new();
    public List<string> Volumes { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    [Index]
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string UpdatedBy { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Hash { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    [Index]
    public ComposeStatus Status { get; set; }
    
    /// <summary>
    /// 服务详情列表（用于可视化编辑器联动）
    /// </summary>
    public List<ComposeServiceDetail>? ServiceDetails { get; set; }
}

/// <summary>
/// Compose 服务详情（用于可视化编辑器）
/// </summary>
public class ComposeServiceDetail
{
    public string Name { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public List<string> Ports { get; set; } = new();
    public Dictionary<string, string?> Environment { get; set; } = new();
    public List<string> Volumes { get; set; } = new();
    
    // 构建配置
    public string? Build { get; set; }
    public string? Dockerfile { get; set; }
    public string? Context { get; set; }
    
    // 运行配置
    public string? ContainerName { get; set; }
    public string? Command { get; set; }
    public string? Entrypoint { get; set; }
    public string? WorkingDir { get; set; }
    public string? User { get; set; }
    public string? Hostname { get; set; }
    
    // 重启策略
    public string? Restart { get; set; }
    
    // 依赖
    public List<string> DependsOn { get; set; } = new();
    
    // 网络配置
    public List<string> Networks { get; set; } = new();
    
    // 标签
    public Dictionary<string, string> Labels { get; set; } = new();
    
    // 环境文件
    public List<string> EnvFile { get; set; } = new();
    
    // 健康检查
    public ComposeHealthCheck? HealthCheck { get; set; }
    
    // 资源限制
    public long? MemLimit { get; set; }
    public long? MemReservation { get; set; }
    public double? CpuCount { get; set; }
    public long? CpuShares { get; set; }
    
    // 其他
    public bool? Privileged { get; set; }
    public List<string> CapAdd { get; set; } = new();
    public List<string> CapDrop { get; set; } = new();
    public List<string> ExtraHosts { get; set; } = new();
    public string? Pid { get; set; }
    public string? Ipc { get; set; }
    public string? NetworkMode { get; set; }
}

/// <summary>
/// 健康检查配置
/// </summary>
public class ComposeHealthCheck
{
    public List<string>? Test { get; set; }
    public int? Interval { get; set; }
    public int? Timeout { get; set; }
    public int? Retries { get; set; }
    public int? StartPeriod { get; set; }
    public bool? Disable { get; set; }
}

/// <summary>
/// Docker Compose状态枚举
/// </summary>
public enum ComposeStatus
{
    Unknown,
    Created,
    Running,
    Stopped,
    PartiallyRunning,
    Error,
    Deploying,
    Removing
}

/// <summary>
/// Docker Compose项目信息
/// </summary>
public class ComposeProject
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public string? NodeId { get; set; }
    public string NodeName { get; set; } = string.Empty;
    public ComposeStatus Status { get; set; }
    public List<ComposeServiceInfo> Services { get; set; } = new();
    public List<ComposeNetworkInfo> Networks { get; set; } = new();
    public List<ComposeVolumeInfo> Volumes { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Dictionary<string, object> Config { get; set; } = new();
}

/// <summary>
/// Compose服务信息
/// </summary>
public class ComposeServiceInfo
{
    public string Name { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ContainerId { get; set; }
    public List<string> Ports { get; set; } = new();
    public List<string> Networks { get; set; } = new();
    public Dictionary<string, string> Environment { get; set; } = new();
    public DateTime? CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public bool IsRunning { get; set; }
    public int Health { get; set; } // 0=unknown, 1=healthy, 2=unhealthy
    public string? HealthStatus { get; set; }
}

/// <summary>
/// Compose网络信息
/// </summary>
public class ComposeNetworkInfo
{
    public string Name { get; set; } = string.Empty;
    public string? NetworkId { get; set; }
    public string Driver { get; set; } = "bridge";
    public bool External { get; set; }
    public Dictionary<string, string> Labels { get; set; } = new();
    public List<string> ConnectedServices { get; set; } = new();
}

/// <summary>
/// Compose卷信息
/// </summary>
public class ComposeVolumeInfo
{
    public string Name { get; set; } = string.Empty;
    public string? VolumeId { get; set; }
    public string Driver { get; set; } = "local";
    public bool External { get; set; }
    public Dictionary<string, string> Options { get; set; } = new();
    public Dictionary<string, string> Labels { get; set; } = new();
    public List<string> UsedByServices { get; set; } = new();
}

/// <summary>
/// 创建Compose文件请求
/// </summary>
public class CreateComposeFileRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    [Required]
    public string Content { get; set; } = string.Empty;
    public string? Path { get; set; }
    public string? NodeId { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// 更新Compose文件请求
/// </summary>
public class UpdateComposeFileRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Content { get; set; }
    public string? Path { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// 部署Compose项目请求
/// </summary>
public class DeployComposeRequest
{
    [Required]
    public string ComposeFileId { get; set; } = string.Empty;
    public string? NodeId { get; set; }
    public bool Detach { get; set; } = true;
    public bool RemoveOrphans { get; set; } = true;
    public bool ForceRecreate { get; set; } = false;
    public bool NoRecreate { get; set; } = false;
    public bool NoBuild { get; set; } = true;
    public bool NoDeps { get; set; } = false;
    public List<string>? Services { get; set; }
    public Dictionary<string, string>? Environment { get; set; }
    public Dictionary<string, string>? Labels { get; set; }
    public int Timeout { get; set; } = 60;
}

/// <summary>
/// Compose操作请求
/// </summary>
public class ComposeOperationRequest
{
    [Required]
    public string ComposeFileId { get; set; } = string.Empty;
    public string? NodeId { get; set; }
    public List<string>? Services { get; set; }
    public int Timeout { get; set; } = 30;
    public bool Force { get; set; } = false;
}

/// <summary>
/// Compose日志请求
/// </summary>
public class ComposeLogsRequest
{
    [Required]
    public string ComposeFileId { get; set; } = string.Empty;
    public string? NodeId { get; set; }
    public List<string>? Services { get; set; }
    public bool Follow { get; set; } = false;
    public bool Timestamps { get; set; } = true;
    public string? Since { get; set; }
    public string? Until { get; set; }
    public int? Tail { get; set; }
}

/// <summary>
/// Compose日志响应
/// </summary>
public class ComposeLogsResponse
{
    public string ComposeFileId { get; set; } = string.Empty;
    public List<ComposeLogEntry> Logs { get; set; } = new();
    public DateTime? Since { get; set; }
    public DateTime? Until { get; set; }
    public bool HasMore { get; set; }
}

/// <summary>
/// Compose日志条目
/// </summary>
public class ComposeLogEntry
{
    public string Service { get; set; } = string.Empty;
    public string Container { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Stream { get; set; } = string.Empty; // stdout, stderr
}

/// <summary>
/// Compose操作结果
/// </summary>
public class ComposeOperationResult
{
    public string ComposeFileId { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
    public List<string> AffectedServices { get; set; } = new();
    public Dictionary<string, object> Details { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Compose项目统计信息
/// </summary>
public class ComposeProjectStats
{
    public string ComposeFileId { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public int TotalServices { get; set; }
    public int RunningServices { get; set; }
    public int StoppedServices { get; set; }
    public int UnhealthyServices { get; set; }
    public int TotalNetworks { get; set; }
    public int TotalVolumes { get; set; }
    public long TotalSize { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastDeployed { get; set; }
    public string Status { get; set; } = string.Empty;
    public double HealthPercentage => TotalServices > 0 ? (double)RunningServices / TotalServices * 100 : 0;
}

/// <summary>
/// Compose模板
/// </summary>
public class ComposeTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> Variables { get; set; } = new();
    public string? Icon { get; set; }
    public string Version { get; set; } = "1.0.0";
    public string Author { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int DownloadCount { get; set; }
    public double Rating { get; set; }
    public bool IsOfficial { get; set; }
    public bool IsPublic { get; set; } = true;
}

/// <summary>
/// Compose文件验证结果
/// </summary>
public class ComposeValidationResult
{
    public string ComposeFileId { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public List<ValidationError> Errors { get; set; } = new();
    public List<ValidationWarning> Warnings { get; set; } = new();
    public List<ValidationInfo> Infos { get; set; } = new();
    public DateTime ValidatedAt { get; set; }
    public string Version { get; set; } = string.Empty;
    public int ServiceCount { get; set; }
    public int NetworkCount { get; set; }
    public int VolumeCount { get; set; }
}

/// <summary>
/// 验证错误
/// </summary>
public class ValidationError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Service { get; set; }
    public string? Property { get; set; }
    public string? Value { get; set; }
    public string Line { get; set; } = string.Empty;
}

/// <summary>
/// 验证警告
/// </summary>
public class ValidationWarning
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Service { get; set; }
    public string? Property { get; set; }
    public string Line { get; set; } = string.Empty;
}

/// <summary>
/// 验证信息
/// </summary>
public class ValidationInfo
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Service { get; set; }
    public string Line { get; set; } = string.Empty;
}

/// <summary>
/// Compose文件版本
/// </summary>
public class ComposeFileVersion
{
    public string Id { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Compose依赖检查结果
/// </summary>
public class ComposeDependencyCheck
{
    public string ComposeFileId { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public List<string> MissingImages { get; set; } = new();
    public List<string> MissingNetworks { get; set; } = new();
    public List<string> MissingVolumes { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public Dictionary<string, object> Details { get; set; } = new();
    public DateTime CheckedAt { get; set; }
}

/// <summary>
/// 克隆Compose文件请求
/// </summary>
public class CloneComposeFileRequest
{
    [Required]
    public string NewName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? NewPath { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// 批量Compose操作请求
/// </summary>
public class BatchComposeOperationRequest
{
    [Required]
    public List<string> ComposeFileIds { get; set; } = new();
    [Required]
    public string Operation { get; set; } = string.Empty;
    public string? NodeId { get; set; }
    public Dictionary<string, object>? Options { get; set; }
}

/// <summary>
/// 从模板创建Compose请求
/// </summary>
public class CreateFromTemplateRequest
{
    [Required]
    public string TemplateId { get; set; } = string.Empty;
    [Required]
    public string Name { get; set; } = string.Empty;
    [Required]
    public Dictionary<string, object> Variables { get; set; } = new();
    public string? Description { get; set; }
    public string? NodeId { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// 导入Compose文件请求
/// </summary>
public class ImportComposeFileRequest
{
    [Required]
    public string FilePath { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? NodeId { get; set; }
    public bool Overwrite { get; set; } = false;
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// 导出Compose文件响应
/// </summary>
public class ExportComposeFileResponse
{
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Format { get; set; } = "yaml";
    public DateTime ExportedAt { get; set; }
}

/// <summary>
/// Compose文件历史记录
/// </summary>
public class ComposeFileHistory
{
    public string ComposeFileId { get; set; } = string.Empty;
    public List<ComposeFileVersion> Versions { get; set; } = new();
    public DateTime LastModified { get; set; }
    public string LastModifiedBy { get; set; } = string.Empty;
    public int TotalVersions => Versions.Count;
}

/// <summary>
/// 验证Compose内容请求
/// </summary>
public class ValidateComposeContentRequest
{
    [Required]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 解析Compose内容请求
/// </summary>
public class ParseComposeContentRequest
{
    [Required]
    public string Content { get; set; } = string.Empty;
}