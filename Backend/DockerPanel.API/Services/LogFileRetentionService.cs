using Microsoft.AspNetCore.Hosting;

namespace DockerPanel.API.Services;

/// <summary>
/// 按系统设置清理过期文件日志。
/// </summary>
public class LogFileRetentionService : BackgroundService
{
    private static readonly HashSet<string> LogExtensions = new(StringComparer.OrdinalIgnoreCase) { ".log", ".txt" };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<LogFileRetentionService> _logger;

    public LogFileRetentionService(
        IServiceScopeFactory scopeFactory,
        IWebHostEnvironment environment,
        ILogger<LogFileRetentionService> logger)
    {
        _scopeFactory = scopeFactory;
        _environment = environment;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "清理文件日志失败");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task CleanupOnceAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var settings = await settingsService.GetSettingsAsync();
        stoppingToken.ThrowIfCancellationRequested();

        if (!settings.Logging.EnableFileLogging)
        {
            return;
        }

        var retentionDays = settings.Logging.LogRetentionDays <= 0 ? 7 : settings.Logging.LogRetentionDays;
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var logDirectory = ResolveLogDirectory(settings.Logging.LogPath);

        if (!Directory.Exists(logDirectory))
        {
            return;
        }

        var deletedCount = 0;
        foreach (var file in Directory.EnumerateFiles(logDirectory, "*.*", SearchOption.TopDirectoryOnly))
        {
            stoppingToken.ThrowIfCancellationRequested();

            var extension = Path.GetExtension(file);
            if (!LogExtensions.Contains(extension))
            {
                continue;
            }

            try
            {
                var lastWriteTimeUtc = File.GetLastWriteTimeUtc(file);
                if (lastWriteTimeUtc >= cutoff)
                {
                    continue;
                }

                File.Delete(file);
                deletedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "删除过期日志文件失败: {File}", file);
            }
        }

        if (deletedCount > 0)
        {
            _logger.LogInformation("已清理 {Count} 个 {RetentionDays} 天前的日志文件", deletedCount, retentionDays);
        }
    }

    private string ResolveLogDirectory(string? logPath)
    {
        var path = string.IsNullOrWhiteSpace(logPath) ? "./logs" : logPath.Trim();
        return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, path));
    }
}
