namespace DockerPanel.API.Services;

/// <summary>
/// 操作审计日志保留清理服务
/// </summary>
public class OperationAuditRetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OperationAuditRetentionService> _logger;

    public OperationAuditRetentionService(IServiceScopeFactory scopeFactory, ILogger<OperationAuditRetentionService> logger)
    {
        _scopeFactory = scopeFactory;
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
                _logger.LogError(ex, "清理操作审计日志失败");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task CleanupOnceAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var auditService = scope.ServiceProvider.GetRequiredService<IOperationAuditService>();

        var settings = await settingsService.GetSettingsAsync();
        stoppingToken.ThrowIfCancellationRequested();

        var retentionDays = settings.Logging.LogRetentionDays <= 0 ? 7 : settings.Logging.LogRetentionDays;
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var deleted = await auditService.DeleteOlderThanAsync(cutoff);

        if (deleted > 0)
        {
            _logger.LogInformation("已清理 {Count} 条 {RetentionDays} 天前的操作审计日志", deleted, retentionDays);
        }
    }
}