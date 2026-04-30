namespace DockerPanel.API.Services;

/// <summary>
/// 后台任务清理服务
/// 定期清理过期已完成/失败的任务
/// </summary>
public class BackgroundTaskCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackgroundTaskCleanupService> _logger;

    public BackgroundTaskCleanupService(
        IServiceProvider serviceProvider,
        ILogger<BackgroundTaskCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("后台任务清理服务已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 每小时清理一次
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);

                using var scope = _serviceProvider.CreateScope();
                var taskService = scope.ServiceProvider.GetRequiredService<BackgroundTaskService>();
                
                taskService.CleanupOldTasks();
            }
            catch (OperationCanceledException)
            {
                // 正常停止
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理后台任务时发生错误");
            }
        }

        _logger.LogInformation("后台任务清理服务已停止");
    }
}
