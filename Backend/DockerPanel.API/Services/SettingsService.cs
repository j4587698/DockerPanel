using DockerPanel.API.Models;
using DockerPanel.API.Data;
using Microsoft.Extensions.Logging;
using TinyDb;

namespace DockerPanel.API.Services;

/// <summary>
/// 真实持久化的设置服务实现
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly ILogger<SettingsService> _logger;
    private readonly TinyDbContext _dbContext;
    private const string DefaultSettingsId = "default";
    private static readonly SemaphoreSlim DefaultSettingsLock = new(1, 1);

    public SettingsService(ILogger<SettingsService> logger, TinyDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    /// <summary>
    /// 获取系统设置
    /// </summary>
    public async Task<SystemSettings> GetSettingsAsync()
    {
        _logger.LogDebug("从数据库获取系统设置");
        var settings = _dbContext.Settings.FindById(DefaultSettingsId);
        
        if (settings == null)
        {
            await DefaultSettingsLock.WaitAsync();
            try
            {
                settings = _dbContext.Settings.FindById(DefaultSettingsId);
                if (settings == null)
                {
                    _logger.LogInformation("未找到系统设置，正在创建默认设置");
                    settings = CreateDefaultSettings();
                    _dbContext.Settings.Upsert(settings);
                }
            }
            finally
            {
                DefaultSettingsLock.Release();
            }
        }

        NormalizeSettings(settings);

        return await Task.FromResult(settings);
    }

    /// <summary>
    /// 更新系统设置
    /// </summary>
    public async Task<bool> UpdateSettingsAsync(SystemSettings settings)
    {
        _logger.LogInformation("更新系统设置");

        if (settings == null)
            return false;

        var validationResult = ValidateSettings(settings);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("设置验证失败: {Errors}", string.Join(", ", validationResult.Errors.Select(e => e.Message)));
            return false;
        }

        NormalizeSettings(settings);
        settings.Id = DefaultSettingsId;
        settings.UpdatedAt = DateTime.UtcNow;

        var updated = _dbContext.Settings.Update(settings) > 0;
        if (!updated)
        {
            _dbContext.Settings.Insert(settings);
            updated = true;
        }

        return await Task.FromResult(updated);
    }

    /// <summary>
    /// 重置设置为默认值
    /// </summary>
    public async Task<bool> ResetSettingsAsync()
    {
        _logger.LogInformation("重置设置为默认值");
        var defaultSettings = CreateDefaultSettings();
        var updated = _dbContext.Settings.Update(defaultSettings) > 0;
        if (!updated)
        {
            _dbContext.Settings.Insert(defaultSettings);
            updated = true;
        }

        return await Task.FromResult(updated);
    }

    /// <summary>
    /// 创建默认设置
    /// </summary>
    private static SystemSettings CreateDefaultSettings()
    {
        var now = DateTime.UtcNow;
        return new SystemSettings
        {
            Id = DefaultSettingsId,
            SiteName = "DockerPanel",
            SiteDescription = "Docker容器管理平台",
            AdminEmail = "admin@dockerpanel.com",
            TimeZone = "Asia/Shanghai",
            DefaultLanguage = "zh-CN",
            SessionTimeoutMinutes = 60,
            MaxLoginAttempts = 5,
            LockoutDurationMinutes = 15,
            Security = new SecuritySettings
            {
                JwtExpirationMinutes = 60,
                PasswordMinLength = 8,
                RequireUppercase = true,
                RequireLowercase = true,
                RequireNumbers = true,
                RequireSpecialChars = true
            },
            Monitoring = new MonitoringSettings
            {
                EnableMetrics = true,
                EnableHealthChecks = true,
                EnableAlerts = true,
                MetricsRetentionDays = 30,
                MetricsCollectionIntervalSeconds = 5,
                AlertThresholds = new AlertThresholdSettings
                {
                    Cpu = 80,
                    Memory = 80,
                    Disk = 90
                }
            },
            UI = new UISettings
            {
                Theme = "auto",
                RefreshInterval = 3000,
                DefaultPageSize = 20
            },
            ContainerEngines = new ContainerEngineConfiguration
            {
                DefaultEngine = "docker",
                Docker = new DockerHostConfiguration
                {
                    Host = "", // 留空，代码会自动根据 OS 识别本地 Docker
                    ApiVersion = "1.41"
                }
            },
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// 验证设置
    /// </summary>
    public async Task<SettingsValidationResult> ValidateSettingsAsync(SystemSettings settings)
    {
        if (settings == null)
        {
            return new SettingsValidationResult
            {
                IsValid = false,
                Errors = new List<SettingsValidationError> { new SettingsValidationError { Property = "Settings", Message = "设置不能为空" } }
            };
        }

        return await Task.FromResult(ValidateSettings(settings));
    }

    private static SettingsValidationResult ValidateSettings(SystemSettings settings)
    {
        var result = new SettingsValidationResult { IsValid = true };
        NormalizeSettings(settings);

        if (string.IsNullOrWhiteSpace(settings.SiteName))
        {
            result.IsValid = false;
            result.Errors.Add(new SettingsValidationError { Property = nameof(SystemSettings.SiteName), Message = "站点名称不能为空" });
        }

        if (!string.IsNullOrWhiteSpace(settings.AdminEmail) && !IsValidEmail(settings.AdminEmail))
        {
            result.IsValid = false;
            result.Errors.Add(new SettingsValidationError { Property = nameof(SystemSettings.AdminEmail), Message = "管理员邮箱格式无效" });
        }

        if (settings.Security.JwtExpirationMinutes < 5 || settings.Security.JwtExpirationMinutes > 1440)
        {
            result.IsValid = false;
            result.Errors.Add(new SettingsValidationError { Property = nameof(SecuritySettings.JwtExpirationMinutes), Message = "会话超时必须在 5 到 1440 分钟之间" });
        }

        if (settings.Security.PasswordMinLength < 6 || settings.Security.PasswordMinLength > 32)
        {
            result.IsValid = false;
            result.Errors.Add(new SettingsValidationError { Property = nameof(SecuritySettings.PasswordMinLength), Message = "密码最小长度必须在 6 到 32 之间" });
        }

        if (settings.MaxLoginAttempts < 1 || settings.MaxLoginAttempts > 20)
        {
            result.IsValid = false;
            result.Errors.Add(new SettingsValidationError { Property = nameof(SystemSettings.MaxLoginAttempts), Message = "最大登录失败次数必须在 1 到 20 之间" });
        }

        if (settings.LockoutDurationMinutes < 1 || settings.LockoutDurationMinutes > 1440)
        {
            result.IsValid = false;
            result.Errors.Add(new SettingsValidationError { Property = nameof(SystemSettings.LockoutDurationMinutes), Message = "锁定时长必须在 1 到 1440 分钟之间" });
        }

        if (settings.UI.RefreshInterval < 3000 || settings.UI.RefreshInterval > 60000)
        {
            result.IsValid = false;
            result.Errors.Add(new SettingsValidationError { Property = nameof(UISettings.RefreshInterval), Message = "刷新间隔必须在 3 到 60 秒之间" });
        }

        if (settings.Monitoring.MetricsCollectionIntervalSeconds < 5 || settings.Monitoring.MetricsCollectionIntervalSeconds > 3600)
        {
            result.IsValid = false;
            result.Errors.Add(new SettingsValidationError { Property = nameof(MonitoringSettings.MetricsCollectionIntervalSeconds), Message = "指标采集间隔必须在 5 到 3600 秒之间" });
        }

        if (settings.Monitoring.MetricsRetentionDays < 1 || settings.Monitoring.MetricsRetentionDays > 3650)
        {
            result.IsValid = false;
            result.Errors.Add(new SettingsValidationError { Property = nameof(MonitoringSettings.MetricsRetentionDays), Message = "指标保留天数必须在 1 到 3650 天之间" });
        }

        if (!new[] { 10, 20, 50, 100 }.Contains(settings.UI.DefaultPageSize))
        {
            result.IsValid = false;
            result.Errors.Add(new SettingsValidationError { Property = nameof(UISettings.DefaultPageSize), Message = "默认分页大小只能是 10、20、50 或 100" });
        }

        if (!IsValidLogLevel(settings.Logging.LogLevel))
        {
            result.IsValid = false;
            result.Errors.Add(new SettingsValidationError { Property = nameof(LoggingSettings.LogLevel), Message = "日志级别无效" });
        }

        if (settings.Logging.LogRetentionDays < 1 || settings.Logging.LogRetentionDays > 3650)
        {
            result.IsValid = false;
            result.Errors.Add(new SettingsValidationError { Property = nameof(LoggingSettings.LogRetentionDays), Message = "日志保留天数必须在 1 到 3650 天之间" });
        }

        return result;
    }

    private static void NormalizeSettings(SystemSettings settings)
    {
        settings.Id = string.IsNullOrWhiteSpace(settings.Id) ? DefaultSettingsId : settings.Id;
        settings.SiteName = string.IsNullOrWhiteSpace(settings.SiteName) ? "DockerPanel" : settings.SiteName.Trim();
        settings.SiteDescription ??= string.Empty;
        settings.AdminEmail ??= string.Empty;
        settings.TimeZone = string.IsNullOrWhiteSpace(settings.TimeZone) ? "Asia/Shanghai" : settings.TimeZone.Trim();
        settings.DefaultLanguage = string.IsNullOrWhiteSpace(settings.DefaultLanguage) ? "zh-CN" : settings.DefaultLanguage.Trim();
        settings.Security ??= new SecuritySettings();
        settings.Monitoring ??= new MonitoringSettings();
        settings.Monitoring.AlertThresholds ??= new AlertThresholdSettings();
        settings.Backup ??= new BackupSettings();
        settings.Logging ??= new LoggingSettings();
        settings.UI ??= new UISettings();
        settings.ContainerEngines ??= new ContainerEngineConfiguration();
        settings.CustomSettings ??= new Dictionary<string, object>();

        settings.SessionTimeoutMinutes = Math.Clamp(settings.SessionTimeoutMinutes <= 0 ? settings.Security.JwtExpirationMinutes : settings.SessionTimeoutMinutes, 5, 1440);
        settings.MaxLoginAttempts = Math.Clamp(settings.MaxLoginAttempts <= 0 ? 5 : settings.MaxLoginAttempts, 1, 20);
        settings.LockoutDurationMinutes = Math.Clamp(settings.LockoutDurationMinutes <= 0 ? 15 : settings.LockoutDurationMinutes, 1, 1440);
        settings.Security.JwtExpirationMinutes = Math.Clamp(settings.Security.JwtExpirationMinutes <= 0 ? settings.SessionTimeoutMinutes : settings.Security.JwtExpirationMinutes, 5, 1440);
        settings.Security.PasswordMinLength = Math.Clamp(settings.Security.PasswordMinLength <= 0 ? 8 : settings.Security.PasswordMinLength, 6, 32);
        settings.Monitoring.MetricsRetentionDays = Math.Clamp(settings.Monitoring.MetricsRetentionDays <= 0 ? 30 : settings.Monitoring.MetricsRetentionDays, 1, 3650);
        settings.Monitoring.MetricsCollectionIntervalSeconds = Math.Clamp(settings.Monitoring.MetricsCollectionIntervalSeconds <= 0 ? 5 : settings.Monitoring.MetricsCollectionIntervalSeconds, 5, 3600);
        settings.Monitoring.AlertThresholds.Cpu = Math.Clamp(settings.Monitoring.AlertThresholds.Cpu <= 0 ? 80 : settings.Monitoring.AlertThresholds.Cpu, 50, 100);
        settings.Monitoring.AlertThresholds.Memory = Math.Clamp(settings.Monitoring.AlertThresholds.Memory <= 0 ? 80 : settings.Monitoring.AlertThresholds.Memory, 50, 100);
        settings.Monitoring.AlertThresholds.Disk = Math.Clamp(settings.Monitoring.AlertThresholds.Disk <= 0 ? 90 : settings.Monitoring.AlertThresholds.Disk, 50, 100);
        settings.UI.Theme = settings.UI.Theme is "light" or "dark" or "auto" ? settings.UI.Theme : "auto";
        settings.UI.RefreshInterval = Math.Clamp(settings.UI.RefreshInterval <= 0 ? 3000 : settings.UI.RefreshInterval, 3000, 60000);
        settings.UI.DefaultPageSize = new[] { 10, 20, 50, 100 }.Contains(settings.UI.DefaultPageSize) ? settings.UI.DefaultPageSize : 20;
        settings.Logging.LogLevel = NormalizeLogLevel(settings.Logging.LogLevel);
        settings.Logging.LogPath = string.IsNullOrWhiteSpace(settings.Logging.LogPath) ? "./logs" : settings.Logging.LogPath.Trim();
        settings.Logging.LogRetentionDays = Math.Clamp(settings.Logging.LogRetentionDays <= 0 ? 7 : settings.Logging.LogRetentionDays, 1, 3650);
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new System.Net.Mail.MailAddress(email);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidLogLevel(string level) => NormalizeLogLevel(level) == level;

    private static string NormalizeLogLevel(string level)
    {
        return level?.Trim() switch
        {
            "Trace" => "Trace",
            "Debug" => "Debug",
            "Information" => "Information",
            "Warning" => "Warning",
            "Error" => "Error",
            "Critical" => "Critical",
            _ => "Information"
        };
    }
}