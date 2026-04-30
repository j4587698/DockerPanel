using TinyDb.Attributes;
using DockerPanel.API.Services;

namespace DockerPanel.API.Models;

/// <summary>
/// 系统设置
/// </summary>
[Entity]
public class SystemSettings
{
    [Id]
    public string Id { get; set; } = "default";
    public string SiteName { get; set; } = "DockerPanel";
    public string SiteDescription { get; set; } = string.Empty;
    public string AdminEmail { get; set; } = string.Empty;
    public string TimeZone { get; set; } = "UTC";
    public string DefaultLanguage { get; set; } = "zh-CN";
    public bool EnableRegistration { get; set; } = false;
    public bool EnableEmailNotifications { get; set; } = true;
    public int SessionTimeoutMinutes { get; set; } = 30;
    public int MaxLoginAttempts { get; set; } = 5;
    public int LockoutDurationMinutes { get; set; } = 15;
    public bool EnableTwoFactorAuth { get; set; } = false;
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public bool SmtpEnableSsl { get; set; } = true;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public DatabaseSettings Database { get; set; } = new();
    public SecuritySettings Security { get; set; } = new();
    public MonitoringSettings Monitoring { get; set; } = new();
    public BackupSettings Backup { get; set; } = new();
    public LoggingSettings Logging { get; set; } = new();
    public UISettings UI { get; set; } = new();
    public ContainerEngineConfiguration ContainerEngines { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Dictionary<string, object> CustomSettings { get; set; } = new();
}

/// <summary>
/// 数据库设置
/// </summary>
public class DatabaseSettings
{
    public string Provider { get; set; } = "LiteDB";
    public string ConnectionString { get; set; } = string.Empty;
    public int ConnectionTimeoutSeconds { get; set; } = 30;
    public int CommandTimeoutSeconds { get; set; } = 60;
    public bool EnablePooling { get; set; } = true;
    public int MaxPoolSize { get; set; } = 100;
    public int MinPoolSize { get; set; } = 5;
    public bool EnableRetryOnFailure { get; set; } = true;
    public int MaxRetryCount { get; set; } = 3;
}

/// <summary>
/// 安全设置
/// </summary>
public class SecuritySettings
{
    public string JwtSecret { get; set; } = string.Empty;
    public int JwtExpirationMinutes { get; set; } = 60;
    public int RefreshTokenExpirationDays { get; set; } = 7;
    public string PasswordPolicy { get; set; } = "MinimumLength8";
    public int PasswordMinLength { get; set; } = 8;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireNumbers { get; set; } = true;
    public bool RequireSpecialChars { get; set; } = true;
    public int PasswordHistoryCount { get; set; } = 5;
    public bool EnableCaptcha { get; set; } = false;
    public string CaptchaProvider { get; set; } = string.Empty;
    public Dictionary<string, string> CaptchaSettings { get; set; } = new();
}

/// <summary>
/// 监控设置
/// </summary>
public class MonitoringSettings
{
    public bool EnableMetrics { get; set; } = true;
    public bool EnableHealthChecks { get; set; } = true;
    public int MetricsRetentionDays { get; set; } = 30;
    public int MetricsCollectionIntervalSeconds { get; set; } = 5;
    public bool EnableAlerts { get; set; } = true;
    public List<string> AlertEmails { get; set; } = new();
    public string AlertWebhookUrl { get; set; } = string.Empty;
    public AlertThresholdSettings AlertThresholds { get; set; } = new();
    public Dictionary<string, object> AlertRules { get; set; } = new();
}

/// <summary>
/// 告警阈值设置
/// </summary>
public class AlertThresholdSettings
{
    public int Cpu { get; set; } = 80;
    public int Memory { get; set; } = 80;
    public int Disk { get; set; } = 90;
}

/// <summary>
/// 界面设置
/// </summary>
public class UISettings
{
    public string Theme { get; set; } = "auto";
    public int RefreshInterval { get; set; } = 3000;
    public int DefaultPageSize { get; set; } = 20;
}

/// <summary>
/// 备份设置
/// </summary>
public class BackupSettings
{
    public bool EnableAutoBackup { get; set; } = false;
    public string BackupSchedule { get; set; } = "0 2 * * *"; // 每天凌晨2点
    public string BackupPath { get; set; } = "./backups";
    public int BackupRetentionDays { get; set; } = 30;
    public bool CompressBackups { get; set; } = true;
    public string BackupProvider { get; set; } = "local";
    public Dictionary<string, string> BackupProviderSettings { get; set; } = new();
    public List<string> BackupIncludes { get; set; } = new();
    public List<string> BackupExcludes { get; set; } = new();
}

/// <summary>
/// 日志设置
/// </summary>
public class LoggingSettings
{
    public string LogLevel { get; set; } = "Information";
    public bool EnableFileLogging { get; set; } = true;
    public string LogPath { get; set; } = "./logs";
    public int LogRetentionDays { get; set; } = 7;
    public bool EnableConsoleLogging { get; set; } = true;
    public bool EnableStructuredLogging { get; set; } = true;
    public Dictionary<string, string> LogOverrides { get; set; } = new();
}

/// <summary>
/// 设置验证结果
/// </summary>
public class SettingsValidationResult
{
    public bool IsValid { get; set; }
    public List<SettingsValidationError> Errors { get; set; } = new();
    public List<SettingsValidationWarning> Warnings { get; set; } = new();
    public Dictionary<string, object> ValidatedSettings { get; set; } = new();
}

/// <summary>
/// 设置验证错误
/// </summary>
public class SettingsValidationError
{
    public string Property { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string AttemptedValue { get; set; } = string.Empty;
}

/// <summary>
/// 设置验证警告
/// </summary>
public class SettingsValidationWarning
{
    public string Property { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string AttemptedValue { get; set; } = string.Empty;
}

/// <summary>
/// 更新系统设置请求
/// </summary>
public class UpdateSystemSettingsRequest
{
    public string? SiteName { get; set; }
    public string? SiteDescription { get; set; }
    public string? AdminEmail { get; set; }
    public string? TimeZone { get; set; }
    public string? DefaultLanguage { get; set; }
    public bool? EnableRegistration { get; set; }
    public bool? EnableEmailNotifications { get; set; }
    public int? SessionTimeoutMinutes { get; set; }
    public int? MaxLoginAttempts { get; set; }
    public int? LockoutDurationMinutes { get; set; }
    public bool? EnableTwoFactorAuth { get; set; }
    public string? SmtpHost { get; set; }
    public int? SmtpPort { get; set; }
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public bool? SmtpEnableSsl { get; set; }
    public string? FromEmail { get; set; }
    public string? FromName { get; set; }
    public DatabaseSettings? Database { get; set; }
    public SecuritySettings? Security { get; set; }
    public MonitoringSettings? Monitoring { get; set; }
    public BackupSettings? Backup { get; set; }
    public LoggingSettings? Logging { get; set; }
    public UISettings? UI { get; set; }
    public ContainerEngineConfiguration? ContainerEngines { get; set; }
    public Dictionary<string, object>? CustomSettings { get; set; }
}

/// <summary>
/// 系统设置页面使用的聚合 DTO
/// </summary>
public class SystemSettingsDto
{
    public string Id { get; set; } = "default";
    public SystemSettingsGeneralDto General { get; set; } = new();
    public SystemSettingsSecurityDto Security { get; set; } = new();
    public SystemSettingsMonitoringDto Monitoring { get; set; } = new();
    public SystemSettingsUiDto UI { get; set; } = new();
    public SystemSettingsLoggingDto Logging { get; set; } = new();
    public DateTime UpdatedAt { get; set; }
}

public class PublicSystemSettingsDto
{
    public string SystemName { get; set; } = "DockerPanel";
    public string SystemDescription { get; set; } = string.Empty;
    public string DefaultLanguage { get; set; } = "zh-CN";
    public string DefaultTimezone { get; set; } = "Asia/Shanghai";
    public string Theme { get; set; } = "auto";
    public int RefreshInterval { get; set; } = 3000;
    public int DefaultPageSize { get; set; } = 20;
}

public class SystemSettingsGeneralDto
{
    public string SystemName { get; set; } = "DockerPanel";
    public string SystemDescription { get; set; } = string.Empty;
    public string AdminEmail { get; set; } = string.Empty;
    public string DefaultLanguage { get; set; } = "zh-CN";
    public string DefaultTimezone { get; set; } = "Asia/Shanghai";
}

public class SystemSettingsSecurityDto
{
    public int SessionTimeout { get; set; } = 3600;
    public int SessionTimeoutMinutes { get; set; } = 60;
    public int MaxLoginAttempts { get; set; } = 5;
    public int LockoutDurationMinutes { get; set; } = 15;
    public int PasswordMinLength { get; set; } = 8;
    public bool PasswordRequireUppercase { get; set; } = true;
    public bool PasswordRequireLowercase { get; set; } = true;
    public bool PasswordRequireNumbers { get; set; } = true;
    public bool PasswordRequireSpecialChars { get; set; } = true;
    public bool EnableTwoFactorAuth { get; set; } = false;
}

public class SystemSettingsMonitoringDto
{
    public bool MetricsEnabled { get; set; } = true;
    public bool HealthChecksEnabled { get; set; } = true;
    public bool AlertsEnabled { get; set; } = true;
    public int MetricsRetentionDays { get; set; } = 30;
    public int MetricsCollectionIntervalSeconds { get; set; } = 5;
    public AlertThresholdSettings AlertThresholds { get; set; } = new();
}

public class SystemSettingsUiDto
{
    public string Theme { get; set; } = "auto";
    public int RefreshInterval { get; set; } = 3000;
    public int DefaultPageSize { get; set; } = 20;
}

public class SystemSettingsLoggingDto
{
    public string LogLevel { get; set; } = "Information";
    public int LogRetentionDays { get; set; } = 7;
}