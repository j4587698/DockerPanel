using Serilog.Core;
using Serilog.Events;

namespace DockerPanel.API.Services;

/// <summary>
/// 将系统设置中的日志级别应用到 Serilog 动态级别开关。
/// </summary>
public class LoggingSettingsApplyService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LoggingLevelSwitch _levelSwitch;
    private readonly ILogger<LoggingSettingsApplyService> _logger;
    private LogEventLevel? _currentLevel;

    public LoggingSettingsApplyService(
        IServiceScopeFactory scopeFactory,
        LoggingLevelSwitch levelSwitch,
        ILogger<LoggingSettingsApplyService> logger)
    {
        _scopeFactory = scopeFactory;
        _levelSwitch = levelSwitch;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ApplyOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "应用日志级别设置失败");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task ApplyOnceAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var settings = await settingsService.GetSettingsAsync();
        stoppingToken.ThrowIfCancellationRequested();

        var level = ParseLevel(settings.Logging.LogLevel);
        if (_currentLevel == level)
        {
            return;
        }

        _levelSwitch.MinimumLevel = level;
        _currentLevel = level;
        _logger.LogInformation("日志级别已更新为 {LogLevel}", level);
    }

    private static LogEventLevel ParseLevel(string? level)
    {
        return level?.Trim() switch
        {
            "Trace" => LogEventLevel.Verbose,
            "Debug" => LogEventLevel.Debug,
            "Information" => LogEventLevel.Information,
            "Warning" => LogEventLevel.Warning,
            "Error" => LogEventLevel.Error,
            "Critical" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }
}
