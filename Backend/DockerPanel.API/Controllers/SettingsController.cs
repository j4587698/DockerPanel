using Microsoft.AspNetCore.Mvc;
using DockerPanel.API.Services;
using DockerPanel.API.Models;
using Microsoft.AspNetCore.Authorization;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace DockerPanel.API.Controllers;

/// <summary>
/// 系统设置控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AuthRoles.Admin)]
public class SettingsController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly ISettingsService _settingsService;
    private readonly ILogger<SettingsController> _logger;
    private readonly ILocalizationService _localization;

    public SettingsController(ISettingsService settingsService, ILogger<SettingsController> logger, ILocalizationService localization)
    {
        _settingsService = settingsService;
        _logger = logger;
        _localization = localization;
    }

    /// <summary>
    /// 健康检查
    /// </summary>
    [HttpGet("health")]
    public async Task<ActionResult> GetHealth()
    {
        try
        {
            _logger.LogInformation("执行系统健康检查");
            var settings = await _settingsService.GetSettingsAsync();
            if (!settings.Monitoring.EnableHealthChecks)
            {
                return Ok(new
                {
                    Status = "Disabled",
                    Timestamp = DateTime.UtcNow,
                    Message = "系统健康检查已在设置中关闭"
                });
            }

            using var process = Process.GetCurrentProcess();
            var uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime();
            var managedMemory = GC.GetTotalMemory(false);
            var gcInfo = GC.GetGCMemoryInfo();

            var healthStatus = new
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Version = GetApplicationVersion(),
                Uptime = uptime,
                Memory = new
                {
                    UsedBytes = managedMemory,
                    HeapSizeBytes = gcInfo.HeapSizeBytes,
                    HighMemoryLoadThresholdBytes = gcInfo.HighMemoryLoadThresholdBytes,
                    MemoryLoadBytes = gcInfo.MemoryLoadBytes
                },
                Cpu = new
                {
                    Cores = Environment.ProcessorCount,
                    TotalProcessorTime = process.TotalProcessorTime
                },
                Services = new[]
                {
                    new { Name = "Application", Status = "Running" },
                    new { Name = "Database", Status = "Running" }
                }
            };

            return Ok(healthStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "健康检查失败");
            return StatusCode(500, new { error = "健康检查失败", message = ex.Message });
        }
    }

    private static string GetApplicationVersion()
    {
        var assembly = typeof(SettingsController).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? "unknown";
    }

    /// <summary>
    /// 获取公开系统设置
    /// </summary>
    [AllowAnonymous]
    [HttpGet("public")]
    public async Task<ActionResult<PublicSystemSettingsDto>> GetPublicSettings()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            return Ok(ToPublicDto(settings));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取公开系统设置失败");
            return StatusCode(500, new { error = "获取公开系统设置失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 获取系统设置
    /// </summary>
    [HttpGet]
    [HttpGet("system")]
    public async Task<ActionResult<SystemSettingsDto>> GetSettings()
    {
        try
        {
            _logger.LogInformation("获取系统设置");
            var settings = await _settingsService.GetSettingsAsync();
            return Ok(ToDto(settings));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取系统设置失败");
            return StatusCode(500, new { error = "获取系统设置失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 更新系统设置
    /// </summary>
    [HttpPut]
    [HttpPut("system")]
    public async Task<ActionResult<SystemSettingsDto>> UpdateSettings([FromBody] SystemSettingsDto request)
    {
        try
        {
            _logger.LogInformation("更新系统设置");

            var settings = await _settingsService.GetSettingsAsync();
            ApplyDto(settings, request);

            var validation = await _settingsService.ValidateSettingsAsync(settings);
            if (!validation.IsValid)
            {
                return BadRequest(new
                {
                    message = string.Join("；", validation.Errors.Select(e => e.Message)),
                    errors = validation.Errors
                });
            }

            var updated = await _settingsService.UpdateSettingsAsync(settings);
            if (!updated)
            {
                return StatusCode(500, new { message = "系统设置保存失败" });
            }

            return Ok(ToDto(settings));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新系统设置失败");
            return StatusCode(500, new { error = "更新系统设置失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 重置设置为默认值
    /// </summary>
    [HttpPost("reset")]
    [HttpPost("system/reset")]
    public async Task<ActionResult<SystemSettingsDto>> ResetSettings()
    {
        try
        {
            _logger.LogInformation("重置系统设置为默认值");

            var reset = await _settingsService.ResetSettingsAsync();
            if (!reset)
            {
                return StatusCode(500, new { message = "系统设置重置失败" });
            }

            var settings = await _settingsService.GetSettingsAsync();
            return Ok(ToDto(settings));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重置系统设置失败");
            return StatusCode(500, new { error = "重置系统设置失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 导出设置
    /// </summary>
    [HttpGet("export")]
    [HttpGet("system/export")]
    public async Task<IActionResult> ExportSettings()
    {
        try
        {
            _logger.LogInformation("导出系统设置");

            var settings = await _settingsService.GetSettingsAsync();
            var json = JsonSerializer.Serialize(ToDto(settings), JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);
            var fileName = $"dockerpanel-settings-{DateTime.UtcNow:yyyyMMddHHmmss}.json";
            return File(bytes, "application/json", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出系统设置失败");
            return StatusCode(500, new { error = "导出系统设置失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 导入设置
    /// </summary>
    [HttpPost("import")]
    [HttpPost("system/import")]
    public async Task<ActionResult<SystemSettingsDto>> ImportSettings([FromForm] IFormFile? file)
    {
        try
        {
            _logger.LogInformation("导入系统设置");

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "请选择要导入的设置文件" });
            }

            await using var stream = file.OpenReadStream();
            var imported = await JsonSerializer.DeserializeAsync<SystemSettingsDto>(stream, JsonOptions);
            if (imported == null)
            {
                return BadRequest(new { message = "设置文件格式无效" });
            }

            var settings = await _settingsService.GetSettingsAsync();
            ApplyDto(settings, imported);

            var validation = await _settingsService.ValidateSettingsAsync(settings);
            if (!validation.IsValid)
            {
                return BadRequest(new
                {
                    message = string.Join("；", validation.Errors.Select(e => e.Message)),
                    errors = validation.Errors
                });
            }

            await _settingsService.UpdateSettingsAsync(settings);
            return Ok(ToDto(settings));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入系统设置失败");
            return StatusCode(500, new { error = "导入系统设置失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 执行健康检查
    /// </summary>
    [HttpPost("health/check")]
    public Task<ActionResult> RunHealthCheck() => GetHealth();

    private static PublicSystemSettingsDto ToPublicDto(SystemSettings settings)
    {
        return new PublicSystemSettingsDto
        {
            SystemName = settings.SiteName,
            SystemDescription = settings.SiteDescription,
            DefaultLanguage = settings.DefaultLanguage,
            DefaultTimezone = settings.TimeZone,
            Theme = settings.UI.Theme,
            RefreshInterval = settings.UI.RefreshInterval,
            DefaultPageSize = settings.UI.DefaultPageSize
        };
    }

    private static SystemSettingsDto ToDto(SystemSettings settings)
    {
        var sessionMinutes = Math.Clamp(settings.Security.JwtExpirationMinutes <= 0 ? settings.SessionTimeoutMinutes : settings.Security.JwtExpirationMinutes, 5, 1440);

        return new SystemSettingsDto
        {
            Id = settings.Id,
            General = new SystemSettingsGeneralDto
            {
                SystemName = settings.SiteName,
                SystemDescription = settings.SiteDescription,
                AdminEmail = settings.AdminEmail,
                DefaultLanguage = settings.DefaultLanguage,
                DefaultTimezone = settings.TimeZone
            },
            Security = new SystemSettingsSecurityDto
            {
                SessionTimeoutMinutes = sessionMinutes,
                SessionTimeout = sessionMinutes * 60,
                MaxLoginAttempts = settings.MaxLoginAttempts,
                LockoutDurationMinutes = settings.LockoutDurationMinutes,
                PasswordMinLength = settings.Security.PasswordMinLength,
                PasswordRequireUppercase = settings.Security.RequireUppercase,
                PasswordRequireLowercase = settings.Security.RequireLowercase,
                PasswordRequireNumbers = settings.Security.RequireNumbers,
                PasswordRequireSpecialChars = settings.Security.RequireSpecialChars,
                EnableTwoFactorAuth = settings.EnableTwoFactorAuth
            },
            Monitoring = new SystemSettingsMonitoringDto
            {
                MetricsEnabled = settings.Monitoring.EnableMetrics,
                HealthChecksEnabled = settings.Monitoring.EnableHealthChecks,
                AlertsEnabled = settings.Monitoring.EnableAlerts,
                MetricsRetentionDays = settings.Monitoring.MetricsRetentionDays,
                MetricsCollectionIntervalSeconds = settings.Monitoring.MetricsCollectionIntervalSeconds,
                AlertThresholds = new AlertThresholdSettings
                {
                    Cpu = settings.Monitoring.AlertThresholds.Cpu,
                    Memory = settings.Monitoring.AlertThresholds.Memory,
                    Disk = settings.Monitoring.AlertThresholds.Disk
                }
            },
            UI = new SystemSettingsUiDto
            {
                Theme = settings.UI.Theme,
                RefreshInterval = settings.UI.RefreshInterval,
                DefaultPageSize = settings.UI.DefaultPageSize
            },
            Logging = new SystemSettingsLoggingDto
            {
                LogLevel = settings.Logging.LogLevel,
                LogRetentionDays = settings.Logging.LogRetentionDays
            },
            UpdatedAt = settings.UpdatedAt
        };
    }

    private static void ApplyDto(SystemSettings settings, SystemSettingsDto request)
    {
        if (request.General != null)
        {
            settings.SiteName = request.General.SystemName;
            settings.SiteDescription = request.General.SystemDescription;
            settings.AdminEmail = request.General.AdminEmail;
            settings.DefaultLanguage = request.General.DefaultLanguage;
            settings.TimeZone = request.General.DefaultTimezone;
        }

        if (request.Security != null)
        {
            var sessionMinutes = request.Security.SessionTimeoutMinutes > 0
                ? request.Security.SessionTimeoutMinutes
                : (int)Math.Ceiling(Math.Max(request.Security.SessionTimeout, 300) / 60.0);

            settings.SessionTimeoutMinutes = sessionMinutes;
            settings.MaxLoginAttempts = request.Security.MaxLoginAttempts;
            settings.LockoutDurationMinutes = request.Security.LockoutDurationMinutes;
            settings.EnableTwoFactorAuth = request.Security.EnableTwoFactorAuth;
            settings.Security.JwtExpirationMinutes = sessionMinutes;
            settings.Security.PasswordMinLength = request.Security.PasswordMinLength;
            settings.Security.RequireUppercase = request.Security.PasswordRequireUppercase;
            settings.Security.RequireLowercase = request.Security.PasswordRequireLowercase;
            settings.Security.RequireNumbers = request.Security.PasswordRequireNumbers;
            settings.Security.RequireSpecialChars = request.Security.PasswordRequireSpecialChars;
        }

        if (request.Monitoring != null)
        {
            settings.Monitoring.EnableMetrics = request.Monitoring.MetricsEnabled;
            settings.Monitoring.EnableHealthChecks = request.Monitoring.HealthChecksEnabled;
            settings.Monitoring.EnableAlerts = request.Monitoring.AlertsEnabled;
            settings.Monitoring.MetricsRetentionDays = request.Monitoring.MetricsRetentionDays;
            settings.Monitoring.MetricsCollectionIntervalSeconds = request.Monitoring.MetricsCollectionIntervalSeconds;
            settings.Monitoring.AlertThresholds = new AlertThresholdSettings
            {
                Cpu = request.Monitoring.AlertThresholds?.Cpu ?? settings.Monitoring.AlertThresholds.Cpu,
                Memory = request.Monitoring.AlertThresholds?.Memory ?? settings.Monitoring.AlertThresholds.Memory,
                Disk = request.Monitoring.AlertThresholds?.Disk ?? settings.Monitoring.AlertThresholds.Disk
            };
        }

        if (request.UI != null)
        {
            settings.UI.Theme = request.UI.Theme;
            settings.UI.RefreshInterval = request.UI.RefreshInterval;
            settings.UI.DefaultPageSize = request.UI.DefaultPageSize;
        }

        if (request.Logging != null)
        {
            settings.Logging.LogLevel = request.Logging.LogLevel;
            settings.Logging.LogRetentionDays = request.Logging.LogRetentionDays;
        }
    }
}