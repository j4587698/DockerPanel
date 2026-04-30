using System;
using System.Collections.Generic;
using TinyDb.Attributes;

namespace DockerPanel.API.Models;

/// <summary>
/// 容器自动升级配置
/// </summary>
[Entity]
public class ContainerAutoUpdateConfig
{
    [Id]
    [IdGeneration(IdGenerationStrategy.GuidV7)]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Index]
    public string ContainerId { get; set; } = string.Empty;
    
    [Index]
    public string ContainerName { get; set; } = string.Empty;
    
    /// <summary>
    /// 是否启用自动检测更新
    /// </summary>
    public bool EnableUpdateCheck { get; set; } = true;
    
    /// <summary>
    /// 是否启用自动拉取（默认 false，只提醒不自动拉取）
    /// </summary>
    public bool EnableAutoPull { get; set; } = false;
    
    /// <summary>
    /// 是否启用自动重启容器（默认 false）
    /// </summary>
    public bool EnableAutoRestart { get; set; } = false;
    
    /// <summary>
    /// 检测间隔（小时）
    /// </summary>
    public int CheckIntervalHours { get; set; } = 6;
    
    /// <summary>
    /// 上次检测时间
    /// </summary>
    public DateTime? LastCheckTime { get; set; }
    
    /// <summary>
    /// 上次检测到的远程镜像摘要
    /// </summary>
    public string? LastRemoteDigest { get; set; }
    
    /// <summary>
    /// 当前本地镜像摘要
    /// </summary>
    public string? CurrentLocalDigest { get; set; }
    
    /// <summary>
    /// 是否有可用更新
    /// </summary>
    [Index]
    public bool HasUpdateAvailable { get; set; } = false;
    
    /// <summary>
    /// 更新状态
    /// </summary>
    [Index]
    public AutoUpdateStatus Status { get; set; } = AutoUpdateStatus.Unknown;
    
    /// <summary>
    /// 状态消息
    /// </summary>
    public string? StatusMessage { get; set; }
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 升级历史记录
    /// </summary>
    public List<ContainerUpdateRecord> UpdateHistory { get; set; } = new();
}

/// <summary>
/// 自动升级状态
/// </summary>
public enum AutoUpdateStatus
{
    /// <summary>
    /// 未知
    /// </summary>
    Unknown,
    
    /// <summary>
    /// 检测中
    /// </summary>
    Checking,
    
    /// <summary>
    /// 已是最新
    /// </summary>
    UpToDate,
    
    /// <summary>
    /// 有可用更新
    /// </summary>
    UpdateAvailable,
    
    /// <summary>
    /// 正在拉取
    /// </summary>
    Pulling,
    
    /// <summary>
    /// 正在重启
    /// </summary>
    Restarting,
    
    /// <summary>
    /// 升级成功
    /// </summary>
    UpdateSuccess,
    
    /// <summary>
    /// 升级失败
    /// </summary>
    UpdateFailed,
    
    /// <summary>
    /// 已禁用
    /// </summary>
    Disabled
}

/// <summary>
/// 容器升级记录
/// </summary>
public class ContainerUpdateRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Time { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 操作类型
    /// </summary>
    public UpdateAction Action { get; set; }
    
    /// <summary>
    /// 旧镜像摘要
    /// </summary>
    public string? OldDigest { get; set; }
    
    /// <summary>
    /// 新镜像摘要
    /// </summary>
    public string? NewDigest { get; set; }
    
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 耗时（毫秒）
    /// </summary>
    public long DurationMs { get; set; }
}

/// <summary>
/// 升级操作类型
/// </summary>
public enum UpdateAction
{
    Check,
    Pull,
    Restart,
    FullUpdate
}

/// <summary>
/// 全局自动升级设置
/// </summary>
[Entity]
public class GlobalAutoUpdateSettings
{
    [Id]
    public string Id { get; set; } = "global";
    
    /// <summary>
    /// 是否启用全局自动更新检测
    /// </summary>
    public bool EnableGlobalCheck { get; set; } = true;
    
    /// <summary>
    /// 默认检测间隔（小时）
    /// </summary>
    public int DefaultCheckIntervalHours { get; set; } = 6;
    
    /// <summary>
    /// 是否允许自动拉取（全局开关）
    /// </summary>
    public bool AllowAutoPull { get; set; } = false;
    
    /// <summary>
    /// 是否允许自动重启（全局开关）
    /// </summary>
    public bool AllowAutoRestart { get; set; } = false;
    
    /// <summary>
    /// 更新检测时间（cron 表达式或固定时间）
    /// </summary>
    public string CheckSchedule { get; set; } = "0 */6 * * *"; // 每6小时
    
    /// <summary>
    /// 排除的镜像（不检测更新）
    /// </summary>
    public List<string> ExcludedImages { get; set; } = new();
    
    /// <summary>
    /// 通知设置
    /// </summary>
    public UpdateNotificationSettings Notifications { get; set; } = new();
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 更新通知设置
/// </summary>
public class UpdateNotificationSettings
{
    /// <summary>
    /// 检测到更新时通知
    /// </summary>
    public bool NotifyOnUpdateAvailable { get; set; } = true;
    
    /// <summary>
    /// 升级成功时通知
    /// </summary>
    public bool NotifyOnUpdateSuccess { get; set; } = true;
    
    /// <summary>
    /// 升级失败时通知
    /// </summary>
    public bool NotifyOnUpdateFailed { get; set; } = true;
    
    /// <summary>
    /// 通知方式（webhook, email 等）
    /// </summary>
    public string? WebhookUrl { get; set; }
}
