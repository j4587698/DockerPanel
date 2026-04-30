using System.ComponentModel.DataAnnotations;

namespace DockerPanel.API.Models;

/// <summary>
/// 仓库配置
/// </summary>
public class RegistryConfig
{
    public string ApiVersion { get; set; } = "v2";
    public string? Namespace { get; set; }
    public int Timeout { get; set; } = 30;
    public int MaxRetries { get; set; } = 3;
    public bool AllowInsecure { get; set; } = false;
    public bool PlainHttp { get; set; } = false;
    public string? MirrorOf { get; set; }
    public List<string> Mirrors { get; set; } = new();
    public List<string> InsecureRegistries { get; set; } = new();
    public Dictionary<string, string> CustomHeaders { get; set; } = new();
    public Dictionary<string, object> Features { get; set; } = new();
    public RateLimitConfig? RateLimit { get; set; }
    public NetworkProxyConfig? Proxy { get; set; }
}

/// <summary>
/// 速率限制配置
/// </summary>
public class RateLimitConfig
{
    public int RequestsPerSecond { get; set; } = 10;
    public int BurstSize { get; set; } = 20;
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// 网络代理配置
/// </summary>
public class NetworkProxyConfig
{
    public string? HttpProxy { get; set; }
    public string? HttpsProxy { get; set; }
    public string? NoProxy { get; set; }
    public string? ProxyUsername { get; set; }
    public string? ProxyPassword { get; set; }
}

/// <summary>
/// 仓库凭证
/// </summary>
public class RegistryCredential
{
    public string Id { get; set; } = string.Empty;
    public string RegistryId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Password { get; set; } = string.Empty;
    public string? Token { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public Dictionary<string, string> Permissions { get; set; } = new();
}

/// <summary>
/// 仓库登录请求
/// </summary>
public class RegistryLoginRequest
{
    [Required]
    public string RegistryId { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Token { get; set; }
    public bool SaveCredentials { get; set; } = false;
    public Dictionary<string, string>? AdditionalParams { get; set; }
}

/// <summary>
/// 镜像推送请求
/// </summary>
public class PushImageRequest
{
    [Required]
    public string ImageName { get; set; } = string.Empty;
    public string? Tag { get; set; } = "latest";
    [Required]
    public string RegistryId { get; set; } = string.Empty;
    public string? TargetRepository { get; set; }
    public string? TargetTag { get; set; }
    public bool Force { get; set; } = false;
    public Dictionary<string, string>? Labels { get; set; }
    public string? NodeId { get; set; }
}

/// <summary>
/// 镜像拉取请求
/// </summary>
public class PullImageRequest
{
    [Required]
    public string ImageName { get; set; } = string.Empty;
    public string? Tag { get; set; } = "latest";
    [Required]
    public string RegistryId { get; set; } = string.Empty;
    public string? LocalName { get; set; }
    public string? LocalTag { get; set; }
    public bool Force { get; set; } = false;
    public string? Platform { get; set; }
    public string? NodeId { get; set; }
}

/// <summary>
/// 仓库搜索请求
/// </summary>
public class SearchRegistryRequest
{
    [Required]
    public string RegistryId { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public int Limit { get; set; } = 50;
    public int Offset { get; set; } = 0;
    public bool IncludeOfficial { get; set; } = true;
    public bool IncludeAutomated { get; set; } = true;
    public string? SortBy { get; set; } = "name";
    public string? SortOrder { get; set; } = "desc";
}

/// <summary>
/// 仓库搜索请求（简化版，用于 POST 接口）
/// </summary>
public class RegistrySearchRequest
{
    public string Query { get; set; } = string.Empty;
    public int Limit { get; set; } = 20;
    public int Offset { get; set; } = 0;
}

/// <summary>
/// 仓库镜像标签信息
/// </summary>
public class RegistryImageTag
{
    public string Name { get; set; } = string.Empty;
    public string Digest { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdated { get; set; }
    public bool IsLatest { get; set; }
    public List<string> Platforms { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// 仓库镜像详情
/// </summary>
public class RegistryImageDetail
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<RegistryImageTag> Tags { get; set; } = new();
    public long TotalSize { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdated { get; set; }
    public string RegistryId { get; set; } = string.Empty;
    public string RegistryName { get; set; } = string.Empty;
    public bool IsOfficial { get; set; }
    public bool IsAutomated { get; set; }
    public int StarCount { get; set; }
    public int DownloadCount { get; set; }
    public string? License { get; set; }
    public List<string> Platforms { get; set; } = new();
    public Dictionary<string, string> Labels { get; set; } = new();
    public List<string> Architectures { get; set; } = new();
    public List<RegistryImageLayer> Layers { get; set; } = new();
}

/// <summary>
/// 镜像层信息
/// </summary>
public class RegistryImageLayer
{
    public string Digest { get; set; } = string.Empty;
    public long Size { get; set; }
    public string? MediaType { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Command { get; set; }
}

/// <summary>
/// 仓库操作结果
/// </summary>
public class RegistryOperationResult
{
    public string RegistryId { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
    public Dictionary<string, object> Details { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// 仓库健康检查结果
/// </summary>
public class RegistryHealthCheckResult
{
    public string RegistryId { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CheckTime { get; set; }
    public long ResponseTimeMs { get; set; }
    public string Version { get; set; } = string.Empty;
    public List<string> AvailableApis { get; set; } = new();
    public Dictionary<string, object> Metrics { get; set; } = new();
    public List<HealthCheckIssue> Issues { get; set; } = new();
}

/// <summary>
/// 健康检查问题
/// </summary>
public class HealthCheckIssue
{
    public string Type { get; set; } = string.Empty; // error, warning, info
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Component { get; set; }
    public Dictionary<string, object> Details { get; set; } = new();
}

/// <summary>
/// 仓库镜像扫描结果
/// </summary>
public class RegistryImageScanResult
{
    public string RegistryId { get; set; } = string.Empty;
    public string ImageName { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public DateTime ScanTime { get; set; }
    public ScanStatus Status { get; set; }
    public int CriticalVulnerabilities { get; set; }
    public int HighVulnerabilities { get; set; }
    public int MediumVulnerabilities { get; set; }
    public int LowVulnerabilities { get; set; }
    public List<SecurityVulnerability> Vulnerabilities { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// 扫描状态
/// </summary>
public enum ScanStatus
{
    Pending,
    Scanning,
    Completed,
    Failed,
    Skipped
}

/// <summary>
/// 安全漏洞
/// </summary>
public class SecurityVulnerability
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; // critical, high, medium, low
    public string Package { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? FixedVersion { get; set; }
    public List<string> Cves { get; set; } = new();
    public string? References { get; set; }
    public DateTime PublishedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 仓库访问日志
/// </summary>
public class RegistryAccessLog
{
    public string Id { get; set; } = string.Empty;
    public string RegistryId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // pull, push, login, search
    public string ImageName { get; set; } = string.Empty;
    public string? Tag { get; set; }
    public string? UserId { get; set; }
    public string? Username { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public long ResponseTimeMs { get; set; }
    public long TransferBytes { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// 仓库配置模板
/// </summary>
public class RegistryConfigTemplate
{
    public string RegistryType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RegistryConfig DefaultConfig { get; set; } = new();
    public List<TemplateField> Fields { get; set; } = new();
    public List<string> SupportedAuthTypes { get; set; } = new();
    public Dictionary<string, string> Examples { get; set; } = new();
}

/// <summary>
/// 模板字段
/// </summary>
public class TemplateField
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // text, password, number, boolean, select
    public bool Required { get; set; }
    public string? DefaultValue { get; set; }
    public string? Placeholder { get; set; }
    public string? Description { get; set; }
    public List<string>? Options { get; set; }
    public Dictionary<string, object>? Validation { get; set; }
}

/// <summary>
/// 批量仓库操作请求
/// </summary>
public class BatchRegistryOperationRequest
{
    [Required]
    public string RegistryId { get; set; } = string.Empty;
    [Required]
    public string Operation { get; set; } = string.Empty; // push, pull, delete, scan
    [Required]
    public List<BatchImageOperation> Images { get; set; } = new();
    public Dictionary<string, object>? Parameters { get; set; }
    public bool ContinueOnError { get; set; } = true;
    public int MaxConcurrency { get; set; } = 5;
}

/// <summary>
/// 批量镜像操作
/// </summary>
public class BatchImageOperation
{
    public string ImageName { get; set; } = string.Empty;
    public string? Tag { get; set; } = "latest";
    public string? TargetName { get; set; }
    public string? TargetTag { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// 批量仓库操作结果
/// </summary>
public class BatchRegistryOperationResult
{
    public string RegistryId { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
    public int TotalImages { get; set; }
    public int SuccessfulOperations { get; set; }
    public int FailedOperations { get; set; }
    public List<RegistryBatchOperationItem> Results { get; set; } = new();
    public List<string> GlobalErrors { get; set; } = new();
    public Dictionary<string, object> Summary { get; set; } = new();
}

/// <summary>
/// 批量仓库操作项
/// </summary>
public class RegistryBatchOperationItem
{
    public string ImageName { get; set; } = string.Empty;
    public string? Tag { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Error { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
    public Dictionary<string, object> Details { get; set; } = new();
}

/// <summary>
/// 仓库使用统计
/// </summary>
public class RegistryUsageStats
{
    public string RegistryId { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public long TotalPulls { get; set; }
    public long TotalPushes { get; set; }
    public long TotalBandwidth { get; set; }
    public long UniqueUsers { get; set; }
    public int TotalImages { get; set; }
    public long TotalSize { get; set; }
    public Dictionary<string, long> DailyPulls { get; set; } = new();
    public Dictionary<string, long> DailyPushes { get; set; } = new();
    public List<TopImage> TopPulledImages { get; set; } = new();
    public List<TopImage> TopPushedImages { get; set; } = new();
    public List<TopUser> TopUsers { get; set; } = new();
}

/// <summary>
/// 热门镜像
/// </summary>
public class TopImage
{
    public string Name { get; set; } = string.Empty;
    public long Count { get; set; }
    public long Size { get; set; }
    public double Percentage { get; set; }
}

/// <summary>
/// 热门用户
/// </summary>
public class TopUser
{
    public string Username { get; set; } = string.Empty;
    public long OperationCount { get; set; }
    public long BandwidthUsage { get; set; }
}

/// <summary>
/// 仓库清理策略
/// </summary>
public class RegistryCleanupPolicy
{
    public int RetentionDays { get; set; } = 30;
    public int MaxTagsPerImage { get; set; } = 10;
    public List<string> ProtectedTags { get; set; } = new() { "latest", "stable" };
    public List<string> ExcludeImages { get; set; } = new();
    public bool DeleteUntagged { get; set; } = true;
    public bool DryRun { get; set; } = false;
    public long MinSizeBytes { get; set; } = 0;
}

/// <summary>
/// 仓库清理结果
/// </summary>
public class RegistryCleanupResult
{
    public string RegistryId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
    public int TotalImagesScanned { get; set; }
    public int TagsDeleted { get; set; }
    public long SpaceFreed { get; set; }
    public List<CleanupItem> DeletedItems { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public bool IsDryRun { get; set; }
}

/// <summary>
/// 清理项
/// </summary>
public class CleanupItem
{
    public string ImageName { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// 仓库同步配置
/// </summary>
public class RegistrySyncConfig
{
    public string RegistryId { get; set; } = string.Empty;
    public bool Enabled { get; set; } = false;
    public string SyncMode { get; set; } = "pull"; // pull, push, bidirectional
    public string? SourceRegistryId { get; set; }
    public List<string> IncludeImages { get; set; } = new();
    public List<string> ExcludeImages { get; set; } = new();
    public string Schedule { get; set; } = "0 2 * * *"; // Cron表达式
    public bool SyncTags { get; set; } = true;
    public bool SyncMetadata { get; set; } = true;
    public int MaxConcurrency { get; set; } = 3;
    public Dictionary<string, object> Options { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastSync { get; set; }
    public string? LastSyncStatus { get; set; }
}

/// <summary>
/// 仓库扫描配置
/// </summary>
public class RegistryScanConfig
{
    public string RegistryId { get; set; } = string.Empty;
    public bool Enabled { get; set; } = false;
    public string ScannerType { get; set; } = "trivy"; // trivy, clair, snyk
    public string Schedule { get; set; } = "0 3 * * *"; // Cron表达式
    public List<string> IncludeImages { get; set; } = new();
    public List<string> ExcludeImages { get; set; } = new();
    public List<string> SeverityLevels { get; set; } = new() { "CRITICAL", "HIGH", "MEDIUM", "LOW" };
    public bool ScanOnPush { get; set; } = true;
    public bool FailOnCritical { get; set; } = false;
    public Dictionary<string, object> ScannerOptions { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastScan { get; set; }
    public string? LastScanStatus { get; set; }
}

/// <summary>
/// 仓库搜索结果
/// </summary>
public class RegistrySearchResult
{
    public string RegistryId { get; set; } = string.Empty;
    public string RegistryName { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int ReturnedCount { get; set; }
    public int Offset { get; set; }
    public List<RegistryImage> Results { get; set; } = new();
    public Dictionary<string, object> Facets { get; set; } = new();
    public DateTime SearchTime { get; set; }
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// 仓库镜像
/// </summary>
public class RegistryImage
{
    public string Name { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public long Size { get; set; }
    public int StarCount { get; set; }
    public int DownloadCount { get; set; }
    public bool IsOfficial { get; set; }
    public bool IsAutomated { get; set; }
    public DateTime LastUpdated { get; set; }
    public string? License { get; set; }
    public List<string> Architectures { get; set; } = new();
    public Dictionary<string, string> Labels { get; set; } = new();
}