using DockerPanel.API.Services;
using DockerPanel.API.Models;
using Docker.DotNet;
using Docker.DotNet.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace DockerPanel.API.Services;

/// <summary>
/// 网络初始化服务 - 在应用启动时确保默认网络存在，并自动将 DockerPanel 自身容器加入该网络
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
        _logger.LogInformation("开始初始化网络服务与自身容器网络连接...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var networkService = scope.ServiceProvider.GetRequiredService<INetworkService>();
            var containerEngine = scope.ServiceProvider.GetService<IContainerEngine>() as DockerEngine;

            // 1. 确保默认网络存在 (dockerpanel-network)
            var defaultNetwork = await networkService.EnsureDefaultNetworkAsync();
            _logger.LogInformation("默认网络初始化完成: {NetworkName} ({NetworkId})",
                defaultNetwork.Name, defaultNetwork.Id);

            // 2. 检查 DockerPanel 自身是否在容器中运行，若是，确保自身已接入 dockerpanel-network
            if (containerEngine != null && await containerEngine.IsAvailableAsync())
            {
                await EnsureSelfContainerConnectedAsync(containerEngine, networkService, defaultNetwork.Id, defaultNetwork.Name, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化默认网络或接入自身容器失败");
            // 不抛出异常，允许应用继续启动
        }
    }

    private async Task EnsureSelfContainerConnectedAsync(
        DockerEngine engine,
        INetworkService networkService,
        string networkId,
        string networkName,
        CancellationToken ct)
    {
        try
        {
            var client = await engine.GetClientAsync();
            var selfContainer = await FindSelfContainerAsync(client, ct);
            if (selfContainer == null)
            {
                _logger.LogDebug("未检测到运行中的 DockerPanel 容器（可能为本地开发环境或非容器化部署），跳过自身网络接入");
                return;
            }

            var inspect = await client.Containers.InspectContainerAsync(selfContainer.ID, ct);

            // 如果容器处于 host 网络模式，无需接入 bridge 网络
            if (string.Equals(inspect.HostConfig?.NetworkMode, "host", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("DockerPanel 运行在 host 网络模式，无需接入桥接网络");
                return;
            }

            // 检查自身是否已经在 dockerpanel-network 中
            var isConnected = inspect.NetworkSettings?.Networks != null &&
                (inspect.NetworkSettings.Networks.ContainsKey(networkName) ||
                 inspect.NetworkSettings.Networks.ContainsKey(networkId));

            if (!isConnected)
            {
                _logger.LogInformation("检测到 DockerPanel 容器 ({Id}) 未接入 {NetworkName}，正在自动连接...", selfContainer.ID, networkName);

                var containerName = inspect.Name?.TrimStart('/') ?? "dockerpanel";
                var aliases = new List<string> { containerName };
                if (!containerName.Equals("dockerpanel", StringComparison.OrdinalIgnoreCase))
                {
                    aliases.Add("dockerpanel");
                }

                var connected = await networkService.ConnectContainerToNetworkAsync(
                    networkId,
                    selfContainer.ID,
                    new NetworkConfig
                    {
                        Aliases = aliases
                    });

                if (connected)
                {
                    _logger.LogInformation("✅ DockerPanel 容器 ({Id}) 已成功自动接入 {NetworkName}（别名: {Aliases}）",
                        selfContainer.ID, networkName, string.Join(", ", aliases));
                }
                else
                {
                    _logger.LogWarning("⚠️ 自动接入 DockerPanel 容器到 {NetworkName} 失败", networkName);
                }
            }
            else
            {
                _logger.LogInformation("DockerPanel 容器已处于 {NetworkName} 网络中", networkName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "检查或接入自身容器网络时发生异常，忽略并继续启动");
        }
    }

    private static async Task<ContainerListResponse?> FindSelfContainerAsync(DockerClient client, CancellationToken ct)
    {
        try
        {
            var containers = await client.Containers.ListContainersAsync(new ContainersListParameters { All = true }, ct);

            // 1. 通过环境变量 DOCKERPANEL_CONTAINER_ID 匹配
            var envId = Environment.GetEnvironmentVariable("DOCKERPANEL_CONTAINER_ID");
            if (!string.IsNullOrEmpty(envId))
            {
                var match = containers.FirstOrDefault(c => c.ID.StartsWith(envId, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            // 2. 通过主机名 (Hostname) 匹配
            var hostname = Environment.MachineName;
            if (!string.IsNullOrEmpty(hostname))
            {
                var match = containers.FirstOrDefault(c => c.ID.StartsWith(hostname, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            // 3. 通过容器名包含 dockerpanel 匹配
            var byName = containers.FirstOrDefault(c => c.Names != null && c.Names.Any(n =>
                n.TrimStart('/').Equals("dockerpanel", StringComparison.OrdinalIgnoreCase) ||
                n.TrimStart('/').Contains("dockerpanel", StringComparison.OrdinalIgnoreCase)));
            if (byName != null) return byName;

            return null;
        }
        catch
        {
            return null;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("网络初始化服务停止");
        return Task.CompletedTask;
    }
}
