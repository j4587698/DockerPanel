using System.Globalization;
using Microsoft.Extensions.Logging;
using DockerPanel.API.Models;
using DockerPanel.API.Hubs;
using Microsoft.AspNetCore.SignalR;
using ComposeProject = Compose.NET.Types.Project;
using ComposeServiceConfig = Compose.NET.Types.ServiceConfig;
using ComposeNetworkConfig = Compose.NET.Types.NetworkConfig;
using ComposeVolumeConfig = Compose.NET.Types.VolumeConfig;
using ComposeServices = Compose.NET.Types.Services;
using ComposeLoader = Compose.NET.Loader.ComposeLoader;
using LoadOptions = Compose.NET.Loader.LoadOptions;

namespace DockerPanel.API.Services;

/// <summary>
/// Compose 部署服务实现 - 使用 Compose.NET 解析，Docker.DotNet 部署
/// </summary>
public class ComposeDeployService : IComposeDeployService
{
    private readonly ILogger<ComposeDeployService> _logger;
    private readonly DockerEngine _dockerEngine;
    private readonly IHubContext<DockerPanelHub> _hubContext;

    public ComposeDeployService(
        ILogger<ComposeDeployService> logger,
        DockerEngine dockerEngine,
        IHubContext<DockerPanelHub> hubContext)
    {
        _logger = logger;
        _dockerEngine = dockerEngine;
        _hubContext = hubContext;
    }

    /// <summary>
    /// 推送部署进度
    /// </summary>
    private async Task PushDeployProgress(string projectName, string step, int progress, string? detail = null)
    {
        _logger.LogInformation("[Deploy] {ProjectName}: {Step} - {Progress}% - {Detail}", projectName, step, progress, detail);
        await DockerPanelHub.BroadcastDeployProgress(_hubContext, projectName, step, progress, detail);
    }

    #region 解析

    /// <summary>
    /// 解析 compose 文件内容
    /// </summary>
    public async Task<ComposeParseResult> ParseAsync(string content, string? projectName = null, string? workingDir = null)
    {
        var result = new ComposeParseResult();

        try
        {
            var options = new LoadOptions
            {
                ProjectName = projectName
            };

            var loadResult = ComposeLoader.LoadFromContent(content, workingDir, options);

            result.Project = loadResult.Project;
            result.Warnings.AddRange(loadResult.Warnings);

            _logger.LogInformation("解析 compose 内容成功: 项目 {Name}, {ServiceCount} 个服务",
                loadResult.Project.Name, loadResult.Project.Services.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析 compose 内容失败");
            result.Errors.Add(ex.Message);
        }

        return await Task.FromResult(result);
    }

    /// <summary>
    /// 解析 compose 文件
    /// </summary>
    public async Task<ComposeParseResult> ParseFileAsync(string filePath, string? projectName = null)
    {
        var result = new ComposeParseResult();

        try
        {
            var options = new LoadOptions
            {
                ProjectName = projectName
            };

            var loadResult = ComposeLoader.LoadFromFile(filePath, options);

            result.Project = loadResult.Project;
            result.Warnings.AddRange(loadResult.Warnings);

            _logger.LogInformation("解析 compose 文件成功: {File}, 项目 {Name}, {ServiceCount} 个服务",
                filePath, loadResult.Project.Name, loadResult.Project.Services.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析 compose 文件失败: {File}", filePath);
            result.Errors.Add(ex.Message);
        }

        return await Task.FromResult(result);
    }

    #endregion

    #region 部署

    /// <summary>
    /// 部署 compose 项目
    /// </summary>
    public async Task<ComposeDeployResult> DeployAsync(ComposeProject project, ComposeDeployOptions? options = null, CancellationToken cancellationToken = default)
    {
        var result = new ComposeDeployResult();
        options ??= new ComposeDeployOptions();

        try
        {
            _logger.LogInformation("开始部署 compose 项目: {Name}", project.Name);
            await PushDeployProgress(project.Name, "deploy.start", 0, "Initializing...");

            // 1. 创建网络
            await PushDeployProgress(project.Name, "deploy.network", 10, "Creating networks...");
            await CreateNetworksAsync(project, result, cancellationToken);
            if (result.CreatedNetworks.Count > 0)
            {
                await PushDeployProgress(project.Name, "deploy.network", 20, $"Created networks: {string.Join(", ", result.CreatedNetworks)}");
            }

            // 2. 创建卷
            await PushDeployProgress(project.Name, "deploy.volume", 25, "Creating volumes...");
            await CreateVolumesAsync(project, result, cancellationToken);
            if (result.CreatedVolumes.Count > 0)
            {
                await PushDeployProgress(project.Name, "deploy.volume", 30, $"Created volumes: {string.Join(", ", result.CreatedVolumes)}");
            }

            // 3. 拉取镜像
            if (options.Pull)
            {
                await PushDeployProgress(project.Name, "deploy.pull", 35, "Pulling images...");
                await PullImagesAsync(project, options, cancellationToken);
                await PushDeployProgress(project.Name, "deploy.pull", 50, "Images pulled");
            }

            // 4. 按依赖顺序创建并启动容器
            await PushDeployProgress(project.Name, "deploy.container", 55, "Creating containers...");
            await CreateServicesAsync(project, options, result, cancellationToken);

            result.Success = result.Errors.Count == 0;
            result.Message = result.Success
                ? $"Project {project.Name} deployed successfully"
                : $"Project {project.Name} deployment failed: {string.Join(", ", result.Errors)}";

            if (result.Success)
            {
                await PushDeployProgress(project.Name, "deploy.completed", 100, $"Successfully deployed {result.CreatedContainers.Count} containers");
            }
            else
            {
                await PushDeployProgress(project.Name, "deploy.failed", 100, result.Message);
            }

            _logger.LogInformation("部署完成: {Name}, 成功: {Success}", project.Name, result.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "部署 compose 项目失败: {Name}", project.Name);
            result.Errors.Add(ex.Message);
            result.Success = false;
            result.Message = ex.Message;
            await PushDeployProgress(project.Name, "deploy.failed", 100, ex.Message);
        }

        return result;
    }

    /// <summary>
    /// 从内容直接部署
    /// </summary>
    public async Task<ComposeDeployResult> DeployFromContentAsync(string content, string projectName, ComposeDeployOptions? options = null, CancellationToken cancellationToken = default)
    {
        var parseResult = await ParseAsync(content, projectName);
        if (!parseResult.Success)
        {
            return new ComposeDeployResult
            {
                Success = false,
                Message = "解析失败",
                Errors = parseResult.Errors
            };
        }

        return await DeployAsync(parseResult.Project!, options, cancellationToken);
    }

    #endregion

    #region 网络管理

    private async Task CreateNetworksAsync(ComposeProject project, ComposeDeployResult result, CancellationToken cancellationToken)
    {
        var client = await GetDockerClientAsync();

        // 如果没有显式定义网络，创建 Compose 语义的项目默认网络：{project}_default
        if (project.Networks == null || project.Networks.Count == 0)
        {
            await CreateSingleNetworkAsync(client, project.Name, "default", GetDefaultNetworkName(project.Name), new ComposeNetworkConfig(), result, cancellationToken);
            return;
        }

        foreach (var (name, networkConfig) in project.Networks)
        {
            var networkName = GetNetworkName(project.Name, name, networkConfig);
            await CreateSingleNetworkAsync(client, project.Name, name, networkName, networkConfig, result, cancellationToken);
        }

        // 服务未显式指定 networks 时仍应接入隐式 default 网络。
        if (!project.Networks.ContainsKey("default") && NeedsImplicitDefaultNetwork(project))
        {
            await CreateSingleNetworkAsync(client, project.Name, "default", GetDefaultNetworkName(project.Name), new ComposeNetworkConfig(), result, cancellationToken);
        }
    }

    private bool NeedsImplicitDefaultNetwork(ComposeProject project)
    {
        return project.Services.Any(service => string.IsNullOrEmpty(service.Value.NetworkMode) &&
            (service.Value.Networks == null || service.Value.Networks.Count == 0));
    }

    private string GetDefaultNetworkName(string projectName) => $"{projectName}_default";

    private async Task CreateSingleNetworkAsync(
        Docker.DotNet.DockerClient client, 
        string projectName, 
        string networkKey,
        string networkName, 
        ComposeNetworkConfig networkConfig, 
        ComposeDeployResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            await PushDeployProgress(projectName, "deploy.network", 10, $"Processing network: {networkName}");

            // 检查网络是否已存在
            var existingNetworks = await client.Networks.ListNetworksAsync(
                new Docker.DotNet.Models.NetworksListParameters
                {
                    Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        ["name"] = new Dictionary<string, bool> { [networkName] = true }
                    }
                }, cancellationToken);

            if (existingNetworks.Any(n => n.Name == networkName))
            {
                _logger.LogInformation("网络已存在: {Name}", networkName);
                result.CreatedNetworks.Add(networkName);
                return;
            }

            // 外部网络不创建
            if (networkConfig.External == true)
            {
                _logger.LogInformation("跳过外部网络: {Name}", networkName);
                return;
            }

            var parameters = new Docker.DotNet.Models.NetworksCreateParameters
            {
                Name = networkName,
                Driver = networkConfig.Driver ?? "bridge",
                Options = networkConfig.DriverOpts?.ToDictionary(k => k.Key, v => v.Value?.ToString() ?? "")!,
                Labels = BuildNetworkLabels(projectName, networkKey)
            };

            // IPAM 配置
            if (networkConfig.Ipam != null)
            {
                parameters.IPAM = new Docker.DotNet.Models.IPAM
                {
                    Driver = networkConfig.Ipam.Driver ?? "default",
                    Config = networkConfig.Ipam.Config?.Select(c => new Docker.DotNet.Models.IPAMConfig
                    {
                        Subnet = c.Subnet!,
                        Gateway = c.Gateway!
                    }).ToList()!
                };
            }

            // 内部网络
            if (networkConfig.Internal == true)
            {
                parameters.Internal = true;
            }

            // 可附加
            if (networkConfig.Attachable == true)
            {
                parameters.Attachable = true;
            }

            await client.Networks.CreateNetworkAsync(parameters, cancellationToken);
            result.CreatedNetworks.Add(networkName);
            _logger.LogInformation("创建网络成功: {Name}", networkName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建网络失败: {Name}", networkName);
            result.Warnings.Add($"网络 {networkName}: {ex.Message}");
        }
    }

    private string GetNetworkName(string projectName, string networkName, ComposeNetworkConfig config)
    {
        // 如果定义了 name 属性，使用它
        if (!string.IsNullOrEmpty(config.Name))
            return config.Name;

        // 外部网络使用原名
        if (config.External == true)
            return networkName;

        // 否则使用 project_network 格式
        return $"{projectName}_{networkName}";
    }

    private Dictionary<string, string> BuildNetworkLabels(string projectName, string networkName)
    {
        return new Dictionary<string, string>
        {
            ["com.docker.compose.project"] = projectName,
            ["com.docker.compose.network"] = networkName,
            ["com.docker.compose.version"] = "3.8"
        };
    }

    #endregion

    #region 卷管理

    private async Task CreateVolumesAsync(ComposeProject project, ComposeDeployResult result, CancellationToken cancellationToken)
    {
        var client = await GetDockerClientAsync();

        foreach (var (name, volumeConfig) in project.Volumes)
        {
            try
            {
                var volumeName = GetVolumeName(project.Name, name, volumeConfig);

                await PushDeployProgress(project.Name, "deploy.volume", 25, $"Processing volume: {volumeName}");

                // 检查卷是否已存在
                var existingVolumes = await client.Volumes.ListAsync(
                    new Docker.DotNet.Models.VolumesListParameters
                    {
                        Filters = new Dictionary<string, IDictionary<string, bool>>
                        {
                            ["name"] = new Dictionary<string, bool> { [volumeName] = true }
                        }
                    }, cancellationToken);

                if (existingVolumes.Volumes?.Any(v => v.Name == volumeName) == true)
                {
                    _logger.LogInformation("卷已存在: {Name}", volumeName);
                    result.CreatedVolumes.Add(volumeName);
                    continue;
                }

                // 外部卷不创建
                if (volumeConfig.External == true)
                {
                    _logger.LogInformation("跳过外部卷: {Name}", volumeName);
                    continue;
                }

                var parameters = new Docker.DotNet.Models.VolumesCreateParameters
                {
                    Name = volumeName,
                    Driver = volumeConfig.Driver ?? "local",
                    DriverOpts = volumeConfig.DriverOpts?.ToDictionary(k => k.Key, v => v.Value?.ToString() ?? "")!,
                    Labels = BuildVolumeLabels(project.Name, name)
                };

                await client.Volumes.CreateAsync(parameters, cancellationToken);
                result.CreatedVolumes.Add(volumeName);
                _logger.LogInformation("创建卷成功: {Name}", volumeName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建卷失败: {Name}", name);
                result.Warnings.Add($"卷 {name}: {ex.Message}");
            }
        }
    }

    private string GetVolumeName(string projectName, string volumeName, ComposeVolumeConfig config)
    {
        if (!string.IsNullOrEmpty(config.Name))
            return config.Name;

        if (config.External == true)
            return volumeName;

        return $"{projectName}_{volumeName}";
    }

    private Dictionary<string, string> BuildVolumeLabels(string projectName, string volumeName)
    {
        return new Dictionary<string, string>
        {
            ["com.docker.compose.project"] = projectName,
            ["com.docker.compose.volume"] = volumeName,
            ["com.docker.compose.version"] = "3.8"
        };
    }

    #endregion

    #region 镜像管理

    private async Task PullImagesAsync(ComposeProject project, ComposeDeployOptions options, CancellationToken cancellationToken)
    {
        var images = new HashSet<string>();

        foreach (var (name, service) in project.Services)
        {
            if (!string.IsNullOrEmpty(service.Image))
            {
                images.Add(service.Image);
            }
        }

        foreach (var image in images)
        {
            try
            {
                _logger.LogInformation("拉取镜像: {Image}", image);
                await _dockerEngine.PullImageAsync(image, null, options.PullProgress);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "拉取镜像失败: {Image}", image);
            }
        }
    }

    #endregion

    #region 服务管理

    private async Task CreateServicesAsync(ComposeProject project, ComposeDeployOptions options, ComposeDeployResult result, CancellationToken cancellationToken)
    {
        // 获取服务依赖顺序
        var sortedServices = SortServicesByDependencies(project.Services);
        var totalServices = sortedServices.Count;
        var currentServiceIndex = 0;

        foreach (var serviceName in sortedServices)
        {
            currentServiceIndex++;

            if (options.Services != null && !options.Services.Contains(serviceName))
                continue;

            if (!project.Services.TryGetValue(serviceName, out var serviceConfig))
                continue;

            try
            {
                if (string.IsNullOrWhiteSpace(serviceConfig.Image))
                {
                    result.Errors.Add($"服务 {serviceName}: 未指定 image，当前内置部署器暂不支持 build-only 服务");
                    continue;
                }

                // 计算进度百分比 (55-95之间)
                var progressPercent = 55 + (int)((currentServiceIndex / (double)totalServices) * 40);
                await PushDeployProgress(project.Name, "deploy.container", progressPercent, $"Creating service: {serviceName}");

                var serviceStatus = await CreateServiceContainerAsync(project, serviceName, serviceConfig, options, cancellationToken);
                result.CreatedContainers.Add(serviceStatus);

                await PushDeployProgress(project.Name, "deploy.container", progressPercent, $"Service {serviceName} {serviceStatus.Status}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建服务容器失败: {Service}", serviceName);
                result.Errors.Add($"服务 {serviceName}: {ex.Message}");
                await PushDeployProgress(project.Name, "deploy.failed", 0, $"Service {serviceName}: {ex.Message}");
            }
        }
    }

    private List<string> SortServicesByDependencies(ComposeServices services)
    {
        var result = new List<string>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();

        void Visit(string name)
        {
            if (visited.Contains(name)) return;
            if (visiting.Contains(name))
                throw new Exception($"检测到循环依赖: {name}");

            visiting.Add(name);

            if (services.TryGetValue(name, out var service))
            {
                foreach (var dep in service.GetDependencies())
                {
                    if (services.ContainsKey(dep))
                        Visit(dep);
                }
            }

            visiting.Remove(name);
            visited.Add(name);
            result.Add(name);
        }

        foreach (var name in services.Keys)
        {
            Visit(name);
        }

        return result;
    }

    private async Task<ComposeServiceStatus> CreateServiceContainerAsync(ComposeProject project, string serviceName, ComposeServiceConfig service, ComposeDeployOptions options, CancellationToken cancellationToken)
    {
        var client = await GetDockerClientAsync();
        var containerName = GetContainerName(project.Name, serviceName, service, 1);

        // 检查容器是否已存在
        var existingContainers = await client.Containers.ListContainersAsync(
            new Docker.DotNet.Models.ContainersListParameters
            {
                All = true,
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["name"] = new Dictionary<string, bool> { [containerName] = true }
                }
            }, cancellationToken);

        if (existingContainers.Any(c => c.Names.Any(n => n.TrimStart('/') == containerName)))
        {
            var existing = existingContainers.First(c => c.Names.Any(n => n.TrimStart('/') == containerName));
            _logger.LogInformation("容器已存在: {Name}, 状态: {Status}", containerName, existing.State);

            // 如果需要强制重建
            if (options.ForceRecreate)
            {
                await client.Containers.RemoveContainerAsync(existing.ID, new Docker.DotNet.Models.ContainerRemoveParameters { Force = true }, cancellationToken);
            }
            else
            {
                // 如果容器已停止，启动它
                if (existing.State == "created" || existing.State == "exited")
                {
                    await client.Containers.StartContainerAsync(existing.ID, new Docker.DotNet.Models.ContainerStartParameters(), cancellationToken);
                }
                await ConnectAdditionalNetworksAsync(client, project, serviceName, service, existing.ID, cancellationToken);
                return await BuildComposeServiceStatusAsync(client, serviceName, service, existing.ID, containerName);
            }
        }

        // 构建容器创建参数
        var parameters = BuildContainerParameters(project, serviceName, service, containerName, options);

        var response = await client.Containers.CreateContainerAsync(parameters, cancellationToken);
        _logger.LogInformation("创建容器成功: {Name} -> {Id}", containerName, response.ID);

        // 启动容器
        if (options.Detach)
        {
            await client.Containers.StartContainerAsync(response.ID, new Docker.DotNet.Models.ContainerStartParameters(), cancellationToken);
            _logger.LogInformation("启动容器成功: {Name}", containerName);
        }

        await ConnectAdditionalNetworksAsync(client, project, serviceName, service, response.ID, cancellationToken);
        return await BuildComposeServiceStatusAsync(client, serviceName, service, response.ID, containerName);
    }

    private async Task<ComposeServiceStatus> BuildComposeServiceStatusAsync(
        Docker.DotNet.DockerClient client,
        string serviceName,
        ComposeServiceConfig service,
        string containerId,
        string containerName)
    {
        try
        {
            var inspect = await client.Containers.InspectContainerAsync(containerId);
            return new ComposeServiceStatus
            {
                ServiceName = serviceName,
                ContainerId = containerId,
                ContainerName = containerName,
                Image = inspect.Config?.Image ?? service.Image,
                Status = inspect.State?.Status ?? "unknown",
                IsRunning = inspect.State?.Running ?? false,
                Health = inspect.State?.Health?.Status
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取 Compose 服务容器状态失败: {ContainerId}", containerId);
            return new ComposeServiceStatus
            {
                ServiceName = serviceName,
                ContainerId = containerId,
                ContainerName = containerName,
                Image = service.Image,
                Status = "unknown",
                IsRunning = false
            };
        }
    }

    private async Task ConnectAdditionalNetworksAsync(
        Docker.DotNet.DockerClient client,
        ComposeProject project,
        string serviceName,
        ComposeServiceConfig service,
        string containerId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(service.NetworkMode) || service.Networks == null || service.Networks.Count <= 1)
            return;

        var orderedNetworks = service.NetworksByPriority()
            .Where(network => !string.IsNullOrWhiteSpace(network))
            .ToList();

        if (orderedNetworks.Count <= 1)
            return;

        var inspect = await client.Containers.InspectContainerAsync(containerId);
        var connectedNetworks = inspect.NetworkSettings?.Networks?.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var networkKey in orderedNetworks.Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var networkName = ResolveNetworkName(project, networkKey);
            if (connectedNetworks.Contains(networkName))
                continue;

            try
            {
                await client.Networks.ConnectNetworkAsync(networkName, new Docker.DotNet.Models.NetworkConnectParameters
                {
                    Container = containerId,
                    EndpointConfig = BuildEndpointSettings(service, networkKey)
                });
                _logger.LogInformation("Compose 服务 {Service} 已连接到额外网络 {Network}", serviceName, networkName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Compose 服务 {Service} 连接额外网络失败: {Network}", serviceName, networkName);
            }
        }
    }

    private Docker.DotNet.Models.CreateContainerParameters BuildContainerParameters(ComposeProject project, string serviceName, ComposeServiceConfig service, string containerName, ComposeDeployOptions options)
    {
        var labels = service.Labels?.ToDictionary(k => k.Key, v => v.Value?.ToString() ?? "") ?? new Dictionary<string, string>();

        // 添加 compose 标签
        labels["com.docker.compose.project"] = project.Name;
        labels["com.docker.compose.service"] = serviceName;
        labels["com.docker.compose.version"] = "3.8";

        var parameters = new Docker.DotNet.Models.CreateContainerParameters
        {
            Name = containerName,
            Image = service.Image!,
            Hostname = service.Hostname!,
            User = service.User!,
            Tty = service.Tty,
            OpenStdin = service.StdinOpen,
            StopSignal = service.StopSignal!,
            WorkingDir = service.WorkingDir!,
            Labels = labels,
            Env = BuildEnvironmentVariables(service, options)!,
            Cmd = service.Command?.Parts?.ToList()!,
            Entrypoint = service.Entrypoint?.Parts?.ToList()!,
            ExposedPorts = BuildExposedPorts(service)!,
            HostConfig = BuildHostConfig(project, service),
            NetworkingConfig = BuildNetworkingConfig(project, serviceName, service)!
        };

        // 健康检查
        if (service.HealthCheck != null && service.HealthCheck.Disable != true)
        {
            var healthcheck = new Docker.DotNet.Models.HealthcheckConfig
            {
                Test = service.HealthCheck.Test?.ToList()
            };

            if (service.HealthCheck.Interval.HasValue)
                healthcheck.Interval = (TimeSpan)service.HealthCheck.Interval.Value;
            if (service.HealthCheck.Timeout.HasValue)
                healthcheck.Timeout = (TimeSpan)service.HealthCheck.Timeout.Value;
            if (service.HealthCheck.Retries.HasValue)
                healthcheck.Retries = (long)service.HealthCheck.Retries.Value;
            if (service.HealthCheck.StartPeriod.HasValue)
                healthcheck.StartPeriod = (TimeSpan)service.HealthCheck.StartPeriod.Value;

            parameters.Healthcheck = healthcheck;
        }

        // 停止超时
        if (service.StopGracePeriod.HasValue)
        {
            parameters.StopTimeout = (TimeSpan)service.StopGracePeriod.Value;
        }

        return parameters;
    }

    private string GetContainerName(string projectName, string serviceName, ComposeServiceConfig service, int instance)
    {
        // 如果指定了容器名，直接使用
        if (!string.IsNullOrEmpty(service.ContainerName))
            return service.ContainerName;

        // 否则使用 project_service_instance 格式
        return instance == 1
            ? $"{projectName}_{serviceName}"
            : $"{projectName}_{serviceName}_{instance}";
    }

    private List<string>? BuildEnvironmentVariables(ComposeServiceConfig service, ComposeDeployOptions options)
    {
        var envVars = new List<string>();

        // 从服务定义
        if (service.Environment != null)
        {
            foreach (var (key, value) in service.Environment)
            {
                envVars.Add(value != null ? $"{key}={value}" : $"{key}");
            }
        }

        // 从选项覆盖
        if (options.Environment != null)
        {
            foreach (var (key, value) in options.Environment)
            {
                var existing = envVars.FindIndex(e => e.StartsWith($"{key}="));
                if (existing >= 0)
                    envVars[existing] = $"{key}={value}";
                else
                    envVars.Add($"{key}={value}");
            }
        }

        return envVars.Count > 0 ? envVars : null;
    }

    private Dictionary<string, Docker.DotNet.Models.EmptyStruct>? BuildExposedPorts(ComposeServiceConfig service)
    {
        var exposedPorts = new Dictionary<string, Docker.DotNet.Models.EmptyStruct>();

        // 从 ports 配置
        if (service.Ports != null)
        {
            foreach (var port in service.Ports)
            {
                var key = $"{port.Target}/{port.Protocol ?? "tcp"}";
                exposedPorts[key] = default;
            }
        }

        // 从 expose 配置
        if (service.Expose != null)
        {
            foreach (var expose in service.Expose)
            {
                var portStr = expose.ToString();
                var key = portStr.Contains("/") ? portStr : $"{portStr}/tcp";
                exposedPorts[key] = default;
            }
        }

        return exposedPorts.Count > 0 ? exposedPorts : null;
    }

    private Docker.DotNet.Models.HostConfig BuildHostConfig(ComposeProject project, ComposeServiceConfig service)
    {
        var hostConfig = new Docker.DotNet.Models.HostConfig
        {
            // 重启策略
            RestartPolicy = BuildRestartPolicy(service.Restart),

            // 资源限制 (UnitBytes 有隐式转换为 long)
            Memory = (long)service.MemLimit,
            MemorySwap = (long)service.MemSwapLimit,
            MemoryReservation = (long)service.MemReservation,
            CPUShares = service.CPUShares,
            CPUPeriod = service.CPUPeriod,
            CPUQuota = service.CPUQuota,
            CpusetCpus = service.CPUSet!,
            PidsLimit = service.PidsLimit,
            ShmSize = (long)service.ShmSize,

            // 特权模式
            Privileged = service.Privileged,
            ReadonlyRootfs = service.ReadOnly,
            Init = service.Init,

            // 能力
            CapAdd = service.CapAdd?.Count > 0 ? service.CapAdd.ToList() : null!,
            CapDrop = service.CapDrop?.Count > 0 ? service.CapDrop.ToList() : null!,

            // 安全选项
            SecurityOpt = service.SecurityOpt?.Count > 0 ? service.SecurityOpt.ToList() : null!,

            // DNS
            DNS = service.DNS?.Count > 0 ? service.DNS.ToList() : null!,
            DNSSearch = service.DNSSearch?.Count > 0 ? service.DNSSearch.ToList() : null!,

            // 额外 hosts (HostsList -> List<string>)
            ExtraHosts = service.ExtraHosts?.Count > 0 ? service.ExtraHosts.Select(h => $"{h.Hostname}:{h.IP}").ToList() : null!,

            // 设备 (DeviceMapping 列表)
            Devices = service.Devices?.Count > 0 ? service.Devices.Select(d =>
                new Docker.DotNet.Models.DeviceMapping
                {
                    PathOnHost = d.Source,
                    PathInContainer = d.Target,
                    CgroupPermissions = d.Permissions ?? "rwm"
                }).ToList() : null!,

            // 组
            GroupAdd = service.GroupAdd?.Count > 0 ? service.GroupAdd.ToList() : null!
        };

        // 网络模式
        if (!string.IsNullOrEmpty(service.NetworkMode))
        {
            hostConfig.NetworkMode = service.NetworkMode;
        }
        else
        {
            var primaryNetwork = GetPrimaryNetworkKey(service);
            if (!string.IsNullOrEmpty(primaryNetwork))
            {
                hostConfig.NetworkMode = ResolveNetworkName(project, primaryNetwork);
            }
        }

        // 端口绑定
        hostConfig.PortBindings = BuildPortBindings(service)!;

        // 卷绑定
        hostConfig.Binds = BuildVolumeBinds(project, service)!;

        // tmpfs
        if (service.Tmpfs?.Count > 0)
        {
            hostConfig.Tmpfs = new Dictionary<string, string>();
            foreach (var tmpfs in service.Tmpfs)
            {
                hostConfig.Tmpfs[tmpfs.ToString()] = "";
            }
        }

        // 日志配置
        if (service.Logging != null)
        {
            hostConfig.LogConfig = new Docker.DotNet.Models.LogConfig
            {
                Type = service.Logging.Driver ?? "json-file",
                Config = service.Logging.Options?.ToDictionary(k => k.Key, v => v.Value?.ToString() ?? "") ?? new Dictionary<string, string>()
            };
        }

        // Ulimits
        if (service.Ulimits?.Count > 0)
        {
            hostConfig.Ulimits = new List<Docker.DotNet.Models.Ulimit>();
            foreach (var (name, ulimit) in service.Ulimits)
            {
                hostConfig.Ulimits.Add(new Docker.DotNet.Models.Ulimit
                {
                    Name = name,
                    Soft = ulimit.Soft != 0 ? ulimit.Soft : (ulimit.Single != 0 ? ulimit.Single : 0),
                    Hard = ulimit.Hard != 0 ? ulimit.Hard : (ulimit.Single != 0 ? ulimit.Single : 0)
                });
            }
        }

        // Sysctls
        if (service.Sysctls?.Count > 0)
        {
            hostConfig.Sysctls = service.Sysctls.ToDictionary(k => k.Key, v => v.Value?.ToString() ?? "");
        }

        return hostConfig;
    }

    private Docker.DotNet.Models.RestartPolicy BuildRestartPolicy(string? restart)
    {
        if (string.IsNullOrEmpty(restart) || restart == "no")
        {
            return new Docker.DotNet.Models.RestartPolicy { Name = Docker.DotNet.Models.RestartPolicyKind.No };
        }

        if (restart.StartsWith("on-failure"))
        {
            var maxRetries = 0;
            var match = System.Text.RegularExpressions.Regex.Match(restart, @"on-failure:(\d+)");
            if (match.Success)
            {
                maxRetries = int.Parse(match.Groups[1].Value);
            }
            return new Docker.DotNet.Models.RestartPolicy { Name = Docker.DotNet.Models.RestartPolicyKind.OnFailure, MaximumRetryCount = maxRetries };
        }

        return restart switch
        {
            "always" => new Docker.DotNet.Models.RestartPolicy { Name = Docker.DotNet.Models.RestartPolicyKind.Always },
            "unless-stopped" => new Docker.DotNet.Models.RestartPolicy { Name = Docker.DotNet.Models.RestartPolicyKind.UnlessStopped },
            _ => new Docker.DotNet.Models.RestartPolicy { Name = Docker.DotNet.Models.RestartPolicyKind.No }
        };
    }

    private Dictionary<string, IList<Docker.DotNet.Models.PortBinding>>? BuildPortBindings(ComposeServiceConfig service)
    {
        if (service.Ports == null || service.Ports.Count == 0)
            return null;

        var bindings = new Dictionary<string, IList<Docker.DotNet.Models.PortBinding>>();

        foreach (var port in service.Ports)
        {
            var key = $"{port.Target}/{port.Protocol ?? "tcp"}";
            var hostPort = ConvertPortBindingValue(port.Published);
            var hostIp = ConvertPortBindingValue(port.HostIP);
            var binding = new Docker.DotNet.Models.PortBinding
            {
                HostPort = hostPort,
                HostIP = hostIp
            };

            if (!bindings.ContainsKey(key))
            {
                bindings[key] = new List<Docker.DotNet.Models.PortBinding>();
            }

            bindings[key].Add(binding);
        }

        return bindings;
    }

    private static string ConvertPortBindingValue(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            string stringValue => stringValue,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };

        return string.IsNullOrWhiteSpace(text) ? string.Empty : text;
    }

    private List<string>? BuildVolumeBinds(ComposeProject project, ComposeServiceConfig service)
    {
        if (service.Volumes == null || service.Volumes.Count == 0)
            return null;

        var binds = new List<string>();

        foreach (var volume in service.Volumes)
        {
            string source;
            var target = volume.Target;
            if (string.IsNullOrWhiteSpace(target))
                continue;

            switch (volume.Type)
            {
                case "volume":
                    // 命名卷
                    source = string.IsNullOrEmpty(volume.Source)
                        ? ""
                        : GetVolumeName(project.Name, volume.Source, project.Volumes.GetValueOrDefault(volume.Source) ?? new ComposeVolumeConfig());
                    break;

                case "bind":
                    // 绑定挂载
                    source = volume.Source ?? string.Empty;
                    break;

                case "tmpfs":
                    // tmpfs 不在 Binds 中处理
                    continue;

                default:
                    source = volume.Source ?? string.Empty;
                    break;
            }

                    var bind = string.IsNullOrEmpty(source) ? target : $"{source}:{target}";
            if (volume.ReadOnly)
                bind += ":ro";

            binds.Add(bind);
        }

        return binds.Count > 0 ? binds : null;
    }

    private Docker.DotNet.Models.NetworkingConfig? BuildNetworkingConfig(ComposeProject project, string serviceName, ComposeServiceConfig service)
    {
        if (!string.IsNullOrEmpty(service.NetworkMode))
            return null;

        // 使用优先级最高的网络作为创建时主网络；额外网络在容器创建后连接。
        var primaryNetwork = GetPrimaryNetworkKey(service);
        if (string.IsNullOrEmpty(primaryNetwork))
            return null;

        var networkName = ResolveNetworkName(project, primaryNetwork);
        var endpointSettings = BuildEndpointSettings(service, primaryNetwork);

        return new Docker.DotNet.Models.NetworkingConfig
        {
            EndpointsConfig = new Dictionary<string, Docker.DotNet.Models.EndpointSettings>
            {
                [networkName] = endpointSettings
            }
        };
    }

    private string? GetPrimaryNetworkKey(ComposeServiceConfig service)
    {
        if (service.Networks?.Count > 0)
            return service.NetworksByPriority().FirstOrDefault();

        return string.IsNullOrEmpty(service.NetworkMode) ? "default" : null;
    }

    private string ResolveNetworkName(ComposeProject project, string networkKey)
    {
        if (networkKey == "default" && (project.Networks == null || !project.Networks.ContainsKey("default")))
            return GetDefaultNetworkName(project.Name);

        return GetNetworkName(project.Name, networkKey, GetProjectNetworkConfig(project, networkKey));
    }

    private ComposeNetworkConfig GetProjectNetworkConfig(ComposeProject project, string networkKey)
    {
        return project.Networks != null && project.Networks.TryGetValue(networkKey, out var networkConfig)
            ? networkConfig
            : new ComposeNetworkConfig();
    }

    private Docker.DotNet.Models.EndpointSettings BuildEndpointSettings(ComposeServiceConfig service, string networkKey)
    {
        var endpointSettings = new Docker.DotNet.Models.EndpointSettings();

        if (service.Networks == null || !service.Networks.TryGetValue(networkKey, out var networkConfig) || networkConfig == null)
            return endpointSettings;

        var ipv4Address = networkConfig.Ipv4Address;
        if (!string.IsNullOrEmpty(ipv4Address))
        {
            endpointSettings.IPAMConfig = new Docker.DotNet.Models.EndpointIPAMConfig
            {
                IPv4Address = ipv4Address
            };
        }

        if (networkConfig.Aliases?.Count > 0)
        {
            endpointSettings.Aliases = networkConfig.Aliases.ToList();
        }

        return endpointSettings;
    }

    #endregion

    #region 停止/启动/删除

    /// <summary>
    /// 停止 compose 项目
    /// </summary>
    public async Task<Models.ComposeOperationResult> StopAsync(string projectName, List<string>? services = null, int timeout = 30, CancellationToken cancellationToken = default)
    {
        var result = new Models.ComposeOperationResult();

        try
        {
            await PushOperationProgress(projectName, "operation.start", 0, "Starting stop operation");

            var client = await GetDockerClientAsync();

            var containers = await client.Containers.ListContainersAsync(
                new Docker.DotNet.Models.ContainersListParameters
                {
                    All = true,
                    Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        ["label"] = new Dictionary<string, bool> { [$"com.docker.compose.project={projectName}"] = true }
                    }
                }, cancellationToken);

            var runningContainers = containers.Where(c => c.State == "running").ToList();
            var totalContainers = runningContainers.Count;
            var currentIndex = 0;

            foreach (var container in runningContainers)
            {
                var serviceName = container.Labels?.GetValueOrDefault("com.docker.compose.service");
                if (services != null && !string.IsNullOrEmpty(serviceName) && !services.Contains(serviceName))
                    continue;

                currentIndex++;
                var progress = (int)((double)currentIndex / totalContainers * 80) + 10;
                await PushOperationProgress(projectName, "operation.stopping", progress, $"Stopping service {serviceName ?? container.ID[..12]}");

                await client.Containers.StopContainerAsync(container.ID, new Docker.DotNet.Models.ContainerStopParameters
                {
                    WaitBeforeKillSeconds = (uint)timeout
                }, cancellationToken);

                result.AffectedServices.Add(serviceName ?? container.ID);
                _logger.LogInformation("停止容器: {Id}", container.ID);
            }

            await PushOperationProgress(projectName, "operation.completed", 100, $"Successfully stopped {result.AffectedServices.Count} containers");
            result.Success = true;
            result.Message = $"Project {projectName} stopped";
        }
        catch (Exception ex)
        {
            await PushOperationProgress(projectName, "operation.failed", -1, ex.Message);
            _logger.LogError(ex, "停止项目失败: {Name}", projectName);
            result.Success = false;
            result.Message = ex.Message;
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    /// <summary>
    /// 启动 compose 项目
    /// </summary>
    public async Task<Models.ComposeOperationResult> StartAsync(string projectName, List<string>? services = null, CancellationToken cancellationToken = default)
    {
        var result = new Models.ComposeOperationResult();

        try
        {
            await PushOperationProgress(projectName, "operation.start", 0, "Starting project");

            var client = await GetDockerClientAsync();

            var containers = await client.Containers.ListContainersAsync(
                new Docker.DotNet.Models.ContainersListParameters
                {
                    All = true,
                    Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        ["label"] = new Dictionary<string, bool> { [$"com.docker.compose.project={projectName}"] = true }
                    }
                }, cancellationToken);

            var stoppedContainers = containers.Where(c => c.State != "running").ToList();
            var totalContainers = stoppedContainers.Count;
            var currentIndex = 0;

            foreach (var container in stoppedContainers)
            {
                var serviceName = container.Labels?.GetValueOrDefault("com.docker.compose.service");
                if (services != null && !string.IsNullOrEmpty(serviceName) && !services.Contains(serviceName))
                    continue;

                currentIndex++;
                var progress = (int)((double)currentIndex / totalContainers * 80) + 10;
                await PushOperationProgress(projectName, "operation.starting", progress, $"Starting service {serviceName ?? container.ID[..12]}");

                await client.Containers.StartContainerAsync(container.ID, new Docker.DotNet.Models.ContainerStartParameters(), cancellationToken);
                result.AffectedServices.Add(serviceName ?? container.ID);
                _logger.LogInformation("启动容器: {Id}", container.ID);
            }

            await PushOperationProgress(projectName, "operation.completed", 100, $"Successfully started {result.AffectedServices.Count} containers");
            result.Success = true;
            result.Message = $"Project {projectName} started";
        }
        catch (Exception ex)
        {
            await PushOperationProgress(projectName, "operation.failed", -1, ex.Message);
            _logger.LogError(ex, "启动项目失败: {Name}", projectName);
            result.Success = false;
            result.Message = ex.Message;
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    /// <summary>
    /// 删除 compose 项目
    /// </summary>
    public async Task<Models.ComposeOperationResult> RemoveAsync(string projectName, bool removeVolumes = false, bool removeImages = false, CancellationToken cancellationToken = default)
    {
        var result = new Models.ComposeOperationResult();

        try
        {
            var client = await GetDockerClientAsync();

            // 停止并删除容器
            var containers = await client.Containers.ListContainersAsync(
                new Docker.DotNet.Models.ContainersListParameters
                {
                    All = true,
                    Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        ["label"] = new Dictionary<string, bool> { [$"com.docker.compose.project={projectName}"] = true }
                    }
                }, cancellationToken);

            foreach (var container in containers)
            {
                await client.Containers.RemoveContainerAsync(container.ID, new Docker.DotNet.Models.ContainerRemoveParameters
                {
                    Force = true,
                    RemoveVolumes = removeVolumes
                }, cancellationToken);

                var serviceName = container.Labels?.GetValueOrDefault("com.docker.compose.service");
                result.AffectedServices.Add(serviceName ?? container.ID);
                _logger.LogInformation("删除容器: {Id}", container.ID);
            }

            // 删除网络
            var networks = await client.Networks.ListNetworksAsync(
                new Docker.DotNet.Models.NetworksListParameters
                {
                    Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        ["label"] = new Dictionary<string, bool> { [$"com.docker.compose.project={projectName}"] = true }
                    }
                }, cancellationToken);

            foreach (var network in networks)
            {
                try
                {
                    await client.Networks.DeleteNetworkAsync(network.ID, cancellationToken);
                    _logger.LogInformation("删除网络: {Name}", network.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "删除网络失败: {Name}", network.Name);
                }
            }

            // 删除卷
            if (removeVolumes)
            {
                var volumes = await client.Volumes.ListAsync(
                    new Docker.DotNet.Models.VolumesListParameters
                    {
                        Filters = new Dictionary<string, IDictionary<string, bool>>
                        {
                            ["label"] = new Dictionary<string, bool> { [$"com.docker.compose.project={projectName}"] = true }
                        }
                    }, cancellationToken);

                foreach (var volume in volumes.Volumes ?? new List<Docker.DotNet.Models.VolumeResponse>())
                {
                    try
                    {
                        await client.Volumes.RemoveAsync(volume.Name, false, cancellationToken);
                        _logger.LogInformation("删除卷: {Name}", volume.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "删除卷失败: {Name}", volume.Name);
                    }
                }
            }

            result.Success = true;
            result.Message = $"项目 {projectName} 已删除";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除项目失败: {Name}", projectName);
            result.Success = false;
            result.Message = ex.Message;
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    #endregion

    #region 状态和日志

    /// <summary>
    /// 获取 compose 项目状态
    /// </summary>
    public async Task<ComposeProjectStatus> GetStatusAsync(string projectName, CancellationToken cancellationToken = default)
    {
        var status = new ComposeProjectStatus { ProjectName = projectName };

        try
        {
            var client = await GetDockerClientAsync();

            // 获取容器状态
            var containers = await client.Containers.ListContainersAsync(
                new Docker.DotNet.Models.ContainersListParameters
                {
                    All = true,
                    Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        ["label"] = new Dictionary<string, bool> { [$"com.docker.compose.project={projectName}"] = true }
                    }
                }, cancellationToken);

            var runningCount = 0;
            foreach (var container in containers)
            {
                var serviceName = container.Labels?.GetValueOrDefault("com.docker.compose.service") ?? "unknown";
                var isRunning = container.State == "running";

                if (isRunning) runningCount++;

                // 获取端口映射
                var ports = new List<string>();
                if (container.Ports != null)
                {
                    foreach (var port in container.Ports)
                    {
                        if (port.PublicPort > 0 && port.PrivatePort > 0)
                        {
                            ports.Add($"{port.PublicPort}:{port.PrivatePort}/{port.Type}");
                        }
                    }
                }

                status.Services.Add(new ComposeServiceStatus
                {
                    ServiceName = serviceName,
                    ContainerId = container.ID,
                    ContainerName = container.Names?.FirstOrDefault()?.TrimStart('/'),
                    Image = container.Image,
                    Status = container.Status ?? container.State,
                    IsRunning = isRunning,
                    Health = container.Status?.Contains("healthy") == true ? "healthy" :
                             container.Status?.Contains("unhealthy") == true ? "unhealthy" : null,
                    Ports = ports
                });
            }

            // 获取网络
            var networks = await client.Networks.ListNetworksAsync(
                new Docker.DotNet.Models.NetworksListParameters
                {
                    Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        ["label"] = new Dictionary<string, bool> { [$"com.docker.compose.project={projectName}"] = true }
                    }
                }, cancellationToken);

            status.Networks.AddRange(networks.Select(n => n.Name));

            // 获取卷
            var volumes = await client.Volumes.ListAsync(
                new Docker.DotNet.Models.VolumesListParameters
                {
                    Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        ["label"] = new Dictionary<string, bool> { [$"com.docker.compose.project={projectName}"] = true }
                    }
                }, cancellationToken);

            status.Volumes.AddRange(volumes.Volumes?.Select(v => v.Name) ?? Array.Empty<string>());

            // 确定整体状态
            status.Status = containers.Count == 0 ? "stopped" :
                           runningCount == containers.Count ? "running" :
                           runningCount > 0 ? "partial" : "stopped";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取项目状态失败: {Name}", projectName);
            status.Status = "error";
        }

        return status;
    }

    /// <summary>
    /// 获取 compose 项目日志
    /// </summary>
    public async Task<string> GetLogsAsync(string projectName, List<string>? services = null, int tail = 100, DateTime? since = null, CancellationToken cancellationToken = default)
    {
        var client = await GetDockerClientAsync();
        var logs = new System.Text.StringBuilder();

        var containers = await client.Containers.ListContainersAsync(
            new Docker.DotNet.Models.ContainersListParameters
            {
                All = true,
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["label"] = new Dictionary<string, bool> { [$"com.docker.compose.project={projectName}"] = true }
                }
            }, cancellationToken);

        foreach (var container in containers)
        {
            var serviceName = container.Labels?.GetValueOrDefault("com.docker.compose.service");
            if (services != null && !string.IsNullOrEmpty(serviceName) && !services.Contains(serviceName))
                continue;

            logs.AppendLine($"=== {serviceName} ({container.Names?.FirstOrDefault()?.TrimStart('/')}) ===");

            var parameters = new Docker.DotNet.Models.ContainerLogsParameters
            {
                ShowStdout = true,
                ShowStderr = true,
                Tail = tail.ToString(),
                Since = since?.ToString("o")
            };

            try
            {
                using var stream = await client.Containers.GetContainerLogsAsync(container.ID, parameters, cancellationToken);
                var (stdout, stderr) = await stream.ReadOutputToEndAsync(cancellationToken);
                logs.AppendLine(stdout + stderr);
            }
            catch (Exception ex)
            {
                logs.AppendLine($"获取日志失败: {ex.Message}");
            }
        }

        return logs.ToString();
    }

    /// <summary>
    /// 重启 compose 项目
    /// </summary>
    public async Task<Models.ComposeOperationResult> RestartAsync(
        string projectName,
        List<string>? services = null,
        int timeout = 30,
        CancellationToken cancellationToken = default)
    {
        var result = new Models.ComposeOperationResult
        {
            ComposeFileId = projectName,
            Operation = "restart",
            StartTime = DateTime.UtcNow
        };

        try
        {
            var client = await GetDockerClientAsync();
            var containers = await client.Containers.ListContainersAsync(new Docker.DotNet.Models.ContainersListParameters
            {
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["label"] = new Dictionary<string, bool> { [$"com.docker.compose.project={projectName}"] = true }
                }
            }, cancellationToken);

            foreach (var container in containers)
            {
                var serviceName = container.Labels?.GetValueOrDefault("com.docker.compose.service");
                if (services != null && !string.IsNullOrEmpty(serviceName) && !services.Contains(serviceName))
                    continue;

                try
                {
                    await client.Containers.RestartContainerAsync(
                        container.ID,
                        new Docker.DotNet.Models.ContainerRestartParameters { WaitBeforeKillSeconds = (uint)timeout },
                        cancellationToken);
                    _logger.LogInformation("已重启容器: {Container}", container.Names.FirstOrDefault());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "重启容器失败: {Container}", container.Names.FirstOrDefault());
                }
            }

            result.Success = true;
            result.Message = $"成功重启项目 {projectName}";
            result.EndTime = DateTime.UtcNow;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重启 Compose 项目失败: {Project}", projectName);
            result.Success = false;
            result.Message = ex.Message;
            result.EndTime = DateTime.UtcNow;
            return result;
        }
    }

    /// <summary>
    /// 列出所有 compose 项目
    /// </summary>
    public async Task<List<ComposeProjectSummary>> ListProjectsAsync(CancellationToken cancellationToken = default)
    {
        var client = await GetDockerClientAsync();
        var containers = await client.Containers.ListContainersAsync(new Docker.DotNet.Models.ContainersListParameters
        {
            All = true,  // 查询所有状态的容器，包括 created、exited 等
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["label"] = new Dictionary<string, bool> { ["com.docker.compose.project"] = true }
            }
        }, cancellationToken);

        var projects = containers
            .GroupBy(c => c.Labels?.GetValueOrDefault("com.docker.compose.project") ?? "")
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .Select(g => new ComposeProjectSummary
            {
                Name = g.Key,
                Status = g.Any(c => c.State == "running") ? "running" : "stopped",
                ContainerCount = g.Count(),
                RunningCount = g.Count(c => c.State == "running"),
                UpdatedAt = g.Max(c => c.Created)
            })
            .ToList();

        return projects;
    }

    #endregion

    #region 辅助方法

    private async Task<Docker.DotNet.DockerClient> GetDockerClientAsync()
    {
        return await _dockerEngine.GetClientAsync();
    }

    /// <summary>
    /// 推送操作进度到 SignalR
    /// </summary>
    private async Task PushOperationProgress(string projectName, string step, int progress, string? detail = null)
    {
        _logger.LogInformation("[Operation] {ProjectName}: {Step} - {Progress}% - {Detail}", projectName, step, progress, detail);
        await DockerPanelHub.BroadcastOperationProgress(_hubContext, projectName, step, progress, detail);
    }

    private static long ParseDuration(string? duration)
    {
        if (string.IsNullOrEmpty(duration)) return 0;

        // 简单解析: 10s, 1m30s, 1h 等
        var totalMs = 0L;
        var currentNumber = 0;

        foreach (var c in duration)
        {
            if (char.IsDigit(c))
            {
                currentNumber = currentNumber * 10 + (c - '0');
            }
            else
            {
                totalMs += c switch
                {
                    's' => currentNumber * 1000,
                    'm' => currentNumber * 60 * 1000,
                    'h' => currentNumber * 60 * 60 * 1000,
                    'd' => currentNumber * 24 * 60 * 60 * 1000,
                    _ => currentNumber
                };
                currentNumber = 0;
            }
        }

        return totalMs;
    }

    #endregion
}

/// <summary>
/// IDictionary 扩展方法
/// </summary>
internal static class DictionaryExtensions
{
    /// <summary>
    /// 获取字典中的值，如果不存在则返回默认值
    /// </summary>
    public static TValue? GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key) where TKey : notnull
    {
        return dictionary.TryGetValue(key, out var value) ? value : default;
    }
}