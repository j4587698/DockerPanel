using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using DockerPanel.API.Models;
using DockerPanel.API.Serialization;
using DockerPanel.API.Services;
using Microsoft.AspNetCore.Authorization;

namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// 系统设置端点（SettingsController 的样板迁移）。路由与原控制器保持一致（含兼容路由 api/settings/system/*）。
    /// </summary>
    public static class SettingsEndpoints
    {
        /// <summary>
        /// 日志类别标记（静态类不能作泛型参数）。
        /// </summary>
        public sealed class LoggingTag
        {
        }

        public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/settings")
                .RequireAuthorization(new AuthorizeAttribute { Roles = AuthRoles.Admin });

            group.MapGet("health", GetHealthAsync);
            group.MapGet("public", GetPublicSettingsAsync).AllowAnonymous();
            group.MapGet("", GetSettingsAsync);
            group.MapGet("system", GetSettingsAsync);
            group.MapPut("", UpdateSettingsAsync);
            group.MapPut("system", UpdateSettingsAsync);
            group.MapPost("reset", ResetSettingsAsync);
            group.MapPost("system/reset", ResetSettingsAsync);
            group.MapGet("export", ExportSettingsAsync);
            group.MapGet("system/export", ExportSettingsAsync);
            group.MapPost("import", ImportSettingsAsync);
            group.MapPost("system/import", ImportSettingsAsync);
            group.MapPost("health/check", GetHealthAsync);

            return app;
        }

        private static async ValueTask<IResult> GetHealthAsync(ISettingsService settingsService, ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("执行系统健康检查");
                var settingsValue = await settingsService.GetSettingsAsync();
                if (!settingsValue.Monitoring.EnableHealthChecks)
                {
                    return TypedResults.Ok(new SettingsHealthResponse
                    {
                        Status = "Disabled",
                        Timestamp = DateTime.UtcNow,
                        Message = "系统健康检查已在设置中关闭"
                    });
                }

                using var process = Process.GetCurrentProcess();
                var uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime();

                return TypedResults.Ok(new SettingsHealthResponse
                {
                    Status = "Healthy",
                    Timestamp = DateTime.UtcNow,
                    Version = GetApplicationVersion(),
                    Uptime = uptime,
                    Memory = new SettingsHealthMemory
                    {
                        UsedBytes = GC.GetTotalMemory(false),
                        HeapSizeBytes = GC.GetGCMemoryInfo().HeapSizeBytes,
                        HighMemoryLoadThresholdBytes = GC.GetGCMemoryInfo().HighMemoryLoadThresholdBytes,
                        MemoryLoadBytes = GC.GetGCMemoryInfo().MemoryLoadBytes
                    },
                    Cpu = new SettingsHealthCpu
                    {
                        Cores = Environment.ProcessorCount,
                        TotalProcessorTime = process.TotalProcessorTime
                    },
                    Services = new[]
                    {
                        new SettingsHealthService { Name = "Application", Status = "Running" },
                        new SettingsHealthService { Name = "Database", Status = "Running" }
                    }
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "健康检查失败");
                return TypedResults.Json(new ApiErrorResponse { Error = "健康检查失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async ValueTask<IResult> GetPublicSettingsAsync(ISettingsService settingsService, ILogger<LoggingTag> logger)
        {
            try
            {
                var settingsValue = await settingsService.GetSettingsAsync();
                return TypedResults.Ok(ToPublicDto(settingsValue));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取公开系统设置失败");
                return TypedResults.Json(new ApiErrorResponse { Error = "获取公开系统设置失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async ValueTask<IResult> GetSettingsAsync(ISettingsService settingsService, ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("获取系统设置");
                var settingsValue = await settingsService.GetSettingsAsync();
                return TypedResults.Ok(ToDto(settingsValue));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取系统设置失败");
                return TypedResults.Json(new ApiErrorResponse { Error = "获取系统设置失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async ValueTask<IResult> UpdateSettingsAsync(SystemSettingsDto request, ISettingsService settingsService, ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("更新系统设置");

                var settingsValue = await settingsService.GetSettingsAsync();
                ApplyDto(settingsValue, request);

                var validation = await settingsService.ValidateSettingsAsync(settingsValue);
                if (!validation.IsValid)
                {
                    return TypedResults.BadRequest(new ApiErrorResponse
                    {
                        Message = string.Join("；", validation.Errors.Select(e => e.Message))
                    });
                }

                var updated = await settingsService.UpdateSettingsAsync(settingsValue);
                if (!updated)
                {
                    return TypedResults.Json(new ApiErrorResponse { Message = "系统设置保存失败" }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
                }

                return TypedResults.Ok(ToDto(settingsValue));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "更新系统设置失败");
                return TypedResults.Json(new ApiErrorResponse { Error = "更新系统设置失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async ValueTask<IResult> ResetSettingsAsync(ISettingsService settingsService, ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("重置系统设置为默认值");

                var reset = await settingsService.ResetSettingsAsync();
                if (!reset)
                {
                    return TypedResults.Json(new ApiErrorResponse { Message = "系统设置重置失败" }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
                }

                var settingsValue = await settingsService.GetSettingsAsync();
                return TypedResults.Ok(ToDto(settingsValue));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "重置系统设置失败");
                return TypedResults.Json(new ApiErrorResponse { Error = "重置系统设置失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async ValueTask<IResult> ExportSettingsAsync(ISettingsService settingsService, ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("导出系统设置");

                var settingsValue = await settingsService.GetSettingsAsync();
                var json = JsonSerializer.Serialize(ToDto(settingsValue), WebJsonContext.Default.SystemSettingsDto);
                var bytes = Encoding.UTF8.GetBytes(json);
                var fileName = $"dockerpanel-settings-{DateTime.UtcNow:yyyyMMddHHmmss}.json";
                return Results.File(bytes, "application/json", fileName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "导出系统设置失败");
                return TypedResults.Json(new ApiErrorResponse { Error = "导出系统设置失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async ValueTask<IResult> ImportSettingsAsync(IFormFile? file, ISettingsService settingsService, ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("导入系统设置");

                if (file == null || file.Length == 0)
                {
                    return TypedResults.BadRequest(new ApiErrorResponse { Message = "请选择要导入的设置文件" });
                }

                await using var stream = file.OpenReadStream();
                var imported = await JsonSerializer.DeserializeAsync(stream, WebJsonContext.Default.SystemSettingsDto);
                if (imported == null)
                {
                    return TypedResults.BadRequest(new ApiErrorResponse { Message = "设置文件格式无效" });
                }

                var settingsValue = await settingsService.GetSettingsAsync();
                ApplyDto(settingsValue, imported);

                var validation = await settingsService.ValidateSettingsAsync(settingsValue);
                if (!validation.IsValid)
                {
                    return TypedResults.BadRequest(new ApiErrorResponse
                    {
                        Message = string.Join("；", validation.Errors.Select(e => e.Message))
                    });
                }

                await settingsService.UpdateSettingsAsync(settingsValue);
                return TypedResults.Ok(ToDto(settingsValue));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "导入系统设置失败");
                return TypedResults.Json(new ApiErrorResponse { Error = "导入系统设置失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static string GetApplicationVersion()
        {
            var assembly = typeof(SettingsEndpoints).Assembly;
            return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                   ?? assembly.GetName().Version?.ToString()
                   ?? "unknown";
        }

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
}