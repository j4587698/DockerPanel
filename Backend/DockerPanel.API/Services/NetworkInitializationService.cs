using DockerPanel.API.Services;

namespace DockerPanel.API.Services;

/// <summary>
/// 网络初始化服务 - 在应用启动时确保默认网络存在
/// </summary>
public class NetworkInitializationService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NetworkInitializationService> _logger;

    public NetworkInitializationService(IServiceProvider serviceProvider, ILogger<NetworkInitializationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("开始初始化网络服务...");

        try
        {
            // 使用作用域创建服务
            using var scope = _serviceProvider.CreateScope();
            var networkService = scope.ServiceProvider.GetRequiredService<INetworkService>();

            // 确保默认网络存在
            var defaultNetwork = await networkService.EnsureDefaultNetworkAsync();
            _logger.LogInformation("默认网络初始化完成: {NetworkName} ({NetworkId})",
                defaultNetwork.Name, defaultNetwork.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化默认网络失败");
            // 不抛出异常，允许应用继续启动
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("网络初始化服务停止");
        return Task.CompletedTask;
    }
}