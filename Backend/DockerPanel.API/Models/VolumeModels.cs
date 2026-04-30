using System.ComponentModel.DataAnnotations;

namespace DockerPanel.API.Models;

/// <summary>
/// 卷使用信息
/// </summary>
public class VolumeUsageInfo
{
    public string VolumeName { get; set; } = string.Empty;
    public string VolumeId { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public long Size { get; set; }
    public long UsedBytes { get; set; }
    public long AvailableBytes { get; set; }
    public double UsagePercent { get; set; }
    public int FileCount { get; set; }
    public int UsageCount { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// 卷备份请求
/// </summary>
public class VolumeBackupRequest
{
    [Required]
    public string VolumeName { get; set; } = string.Empty;
    public string? VolumeId { get; set; }
    public string? BackupLocation { get; set; }
    public string? NodeId { get; set; }
    public bool Compress { get; set; } = true;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// 卷备份结果
/// </summary>
public class VolumeBackupResult
{
    public bool Success { get; set; }
    public string VolumeName { get; set; } = string.Empty;
    public string? BackupId { get; set; }
    public string? BackupPath { get; set; }
    public long BackupSize { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// 卷恢复请求
/// </summary>
public class VolumeRestoreRequest
{
    [Required]
    public string BackupId { get; set; } = string.Empty;
    public string? VolumeId { get; set; }
    public string? TargetVolumeName { get; set; }
    public bool OverwriteExisting { get; set; } = false;
}

/// <summary>
/// 卷恢复结果
/// </summary>
public class VolumeRestoreResult
{
    public bool Success { get; set; }
    public string BackupId { get; set; } = string.Empty;
    public string? RestoredVolumeName { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime RestoredAt { get; set; }
    public long RestoredSize { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// 卷备份信息
/// </summary>
public class VolumeBackupInfo
{
    public string BackupId { get; set; } = string.Empty;
    public string VolumeName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long Size { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Location { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// 卷详细信息
/// </summary>
public class VolumeDetailInfo
{
    public string Name { get; set; } = string.Empty;
    public string Driver { get; set; } = string.Empty;
    public string Mountpoint { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Dictionary<string, string> Labels { get; set; } = new();
    public Dictionary<string, string> Options { get; set; } = new();
    public string Scope { get; set; } = string.Empty; // local, global
    public VolumeUsage Usage { get; set; } = new();
    public List<VolumeMount> Mounts { get; set; } = new();
    public VolumeStatus Status { get; set; } = new();
    public string NodeId { get; set; } = string.Empty;
    public VolumeClusterInfo? ClusterInfo { get; set; }
}

/// <summary>
/// 卷使用情况
/// </summary>
public class VolumeUsage
{
    public long Size { get; set; }
    public long RefCount { get; set; }
    public List<VolumeUsageData> UsageData { get; set; } = new();
}

/// <summary>
/// 卷使用数据
/// </summary>
public class VolumeUsageData
{
    public string Path { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
}

/// <summary>
/// 卷挂载信息
/// </summary>
public class VolumeMount
{
    public string ContainerId { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty; // rw, ro
    public string? Driver { get; set; }
    public bool IsAnonymous { get; set; }
    public DateTime MountedAt { get; set; }
}

/// <summary>
/// 卷状态
/// </summary>
public class VolumeStatus
{
    public string State { get; set; } = string.Empty; // available, in-use, error
    public string? Message { get; set; }
    public DateTime? LastChecked { get; set; }
    public bool IsHealthy { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// 卷集群信息
/// </summary>
public class VolumeClusterInfo
{
    public string? ClusterVolumeId { get; set; }
    public string? ClusterVolumeName { get; set; }
    public string? SpecAvailability { get; set; } // active, pause, drain
    public string? CurrentState { get; set; }
    public Dictionary<string, string>? PublishContext { get; set; }
    public Dictionary<string, string>? SecretReferences { get; set; }
}

/// <summary>
/// 更新卷请求
/// </summary>
public class UpdateVolumeRequest
{
    public Dictionary<string, string>? Labels { get; set; }
    public Dictionary<string, string>? Options { get; set; }
}

/// <summary>
/// 卷清理结果
/// </summary>
public class VolumePruneResult
{
    public int VolumesDeleted { get; set; }
    public long SpaceReclaimed { get; set; }
    public List<string> DeletedVolumeNames { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// 卷统计信息
/// </summary>
public class VolumeStatistics
{
    public string VolumeName { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public long SizeBytes { get; set; }
    public long TotalSize { get; set; }
    public long FileCount { get; set; }
    public int MountCount { get; set; }
    public int TotalVolumes { get; set; }
    public int ActiveVolumes { get; set; }
    public double ReadIops { get; set; }
    public double WriteIops { get; set; }
    public double ReadThroughput { get; set; }
    public double WriteThroughput { get; set; }
    public double AverageReadLatency { get; set; }
    public double AverageWriteLatency { get; set; }
}

/// <summary>
/// 卷性能指标
/// </summary>
public class VolumePerformanceMetrics
{
    public string VolumeName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public List<VolumeIoMetric> IoMetrics { get; set; } = new();
    public List<VolumeSpaceMetric> SpaceMetrics { get; set; } = new();
}

/// <summary>
/// 卷IO指标
/// </summary>
public class VolumeIoMetric
{
    public string ContainerId { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public double ReadIops { get; set; }
    public double WriteIops { get; set; }
    public double ReadThroughput { get; set; }
    public double WriteThroughput { get; set; }
    public double ReadLatency { get; set; }
    public double WriteLatency { get; set; }
}

/// <summary>
/// 卷空间指标
/// </summary>
public class VolumeSpaceMetric
{
    public string Path { get; set; } = string.Empty;
    public long UsedBytes { get; set; }
    public long AvailableBytes { get; set; }
    public long TotalBytes { get; set; }
    public double UsagePercent { get; set; }
    public long FileCount { get; set; }
    public DateTime LastModified { get; set; }
}

/// <summary>
/// 创建卷请求
/// </summary>
public class CreateVolumeRequest
{
    /// <summary>
    /// 卷名称，为空时 Docker 自动生成随机名称
    /// </summary>
    public string? Name { get; set; }
    public string? Driver { get; set; } = "local";
    public Dictionary<string, string>? Labels { get; set; }
    public Dictionary<string, string>? Options { get; set; }
    public string? Scope { get; set; }
    public VolumeClusterSpec? ClusterSpec { get; set; }
    public string? NodeId { get; set; }
}

/// <summary>
/// 卷集群规格
/// </summary>
public class VolumeClusterSpec
{
    public string? Group { get; set; }
    public string? AccessMode { get; set; } // mountpoint, block
    public string? AccessibilityRequirements { get; set; }
    public string? CapacityRange { get; set; }
    public List<VolumeSecret>? Secrets { get; set; }
    public bool? PublishContext { get; set; }
}

/// <summary>
/// 卷密钥
/// </summary>
public class VolumeSecret
{
    public string Key { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
}

/// <summary>
/// 卷备份信息
/// </summary>
public class VolumeBackup
{
    public string BackupId { get; set; } = string.Empty;
    public string VolumeName { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public long SizeBytes { get; set; }
    public string Status { get; set; } = string.Empty; // creating, ready, failed, expired
    public string? Location { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public List<string> Checksums { get; set; } = new();
}

/// <summary>
/// 创建卷备份请求
/// </summary>
public class CreateVolumeBackupRequest
{
    [Required]
    public string VolumeName { get; set; } = string.Empty;
    public string? BackupLocation { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool Compress { get; set; } = true;
    public bool IncludeMetadata { get; set; } = true;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// 恢复卷请求
/// </summary>
public class RestoreVolumeRequest
{
    [Required]
    public string BackupId { get; set; } = string.Empty;
    public string? TargetVolumeName { get; set; }
    public bool OverwriteExisting { get; set; } = false;
    public bool VerifyChecksum { get; set; } = true;
}

/// <summary>
/// 卷快照
/// </summary>
public class VolumeSnapshot
{
    public string SnapshotId { get; set; } = string.Empty;
    public string VolumeName { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long SizeBytes { get; set; }
    public string Status { get; set; } = string.Empty; // creating, ready, failed
    public string? ParentSnapshotId { get; set; }
    public Dictionary<string, string> Labels { get; set; } = new();
    public bool IsScheduled { get; set; }
}

/// <summary>
/// 创建卷快照请求
/// </summary>
public class CreateVolumeSnapshotRequest
{
    [Required]
    public string VolumeName { get; set; } = string.Empty;
    public string? ParentSnapshotId { get; set; }
    public Dictionary<string, string> Labels { get; set; } = new();
    public bool IsScheduled { get; set; }
    public string? Schedule { get; set; }
}

/// <summary>
/// 卷清理选项
/// </summary>
public class VolumePruneOptions
{
    /// <summary>
    /// 是否使用过滤器
    /// </summary>
    public bool Filters { get; set; } = false;

    /// <summary>
    /// 标签过滤器
    /// </summary>
    public string? LabelFilter { get; set; }

    /// <summary>
    /// 是否清理所有卷
    /// </summary>
    public bool All { get; set; } = false;

    /// <summary>
    /// 卷名称过滤器
    /// </summary>
    public List<string>? NameFilters { get; set; }

    /// <summary>
    /// 驱动程序过滤器
    /// </summary>
    public List<string>? DriverFilters { get; set; }

    /// <summary>
    /// 是否强制清理
    /// </summary>
    public bool Force { get; set; } = false;

    /// <summary>
    /// 最大清理数量
    /// </summary>
    public int? MaxCount { get; set; }

    /// <summary>
    /// 最小卷大小（字节）
    /// </summary>
    public long? MinSizeBytes { get; set; }

    /// <summary>
    /// 最大卷大小（字节）
    /// </summary>
    public long? MaxSizeBytes { get; set; }

    /// <summary>
    /// 最后使用时间（在此时间之前未使用的卷将被清理）
    /// </summary>
    public DateTime? Until { get; set; }
}