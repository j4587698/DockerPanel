using Docker.DotNet;
using Docker.DotNet.Models;
using DockerPanel.API.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Net.Http;
using System.Text.Json;

namespace DockerPanel.API.Services;

/// <summary>
/// 基于 Docker.DotNet 的真实 Docker 引擎实现
/// </summary>
public class DockerEngine : IContainerEngine, IDisposable
{
    private readonly ILogger<DockerEngine> _logger;
    private readonly IServiceProvider _serviceProvider;
    private DockerClient? _dockerClient;
    private string? _lastUsedEndpoint;
    private string? _lastUsedNodeId;
    private string? _lastLoggedDefaultNodeId;
    private readonly SemaphoreSlim _clientLock = new(1, 1);

    public DockerEngine(ILogger<DockerEngine> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public string EngineName => "Docker (Real API)";

    /// <summary>
    /// 获取 Docker 客户端（支持多节点）
    /// </summary>
    private async Task<DockerClient> GetDockerClientAsync(string? nodeId = null)
    {
        // 如果指定了 nodeId，直接使用 INodeService 获取对应节点的客户端
        if (!string.IsNullOrEmpty(nodeId))
        {
            using var scope = _serviceProvider.CreateScope();
            var nodeService = scope.ServiceProvider.GetRequiredService<INodeService>();
            return await nodeService.GetDockerClientAsync(nodeId);
        }

        // 本地连接或缓存命中
        if (_dockerClient != null && _lastUsedEndpoint != null && _lastUsedNodeId == nodeId)
            return _dockerClient;

        await _clientLock.WaitAsync();
        try
        {
            if (_dockerClient != null && _lastUsedEndpoint != null && _lastUsedNodeId == nodeId)
                return _dockerClient;

            // 尝试获取默认节点
            using var scope = _serviceProvider.CreateScope();
            var nodeService = scope.ServiceProvider.GetService<INodeService>();
            
            if (nodeService != null)
            {
                var defaultNode = await nodeService.GetDefaultNodeAsync();
                if (defaultNode != null && !string.IsNullOrEmpty(defaultNode.Id))
                {
                    if (!string.Equals(_lastLoggedDefaultNodeId, defaultNode.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("[DockerEngine] 使用默认节点: {NodeId}", defaultNode.Id);
                        _lastLoggedDefaultNodeId = defaultNode.Id;
                    }

                    return await nodeService.GetDockerClientAsync(defaultNode.Id);
                }
            }

            // 回退到本地连接
            using var settingsScope = _serviceProvider.CreateScope();
            var settingsService = settingsScope.ServiceProvider.GetService<ISettingsService>();
            
            string endpointStr = "local";
            if (settingsService != null)
            {
                var settings = await settingsService.GetSettingsAsync();
                var config = settings.ContainerEngines.Docker;
                endpointStr = config.Host;
            }

            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            // 本地连接自动修正
            if (isWindows)
            {
                if (string.IsNullOrEmpty(endpointStr) || 
                    endpointStr.StartsWith("unix://") || 
                    endpointStr.Contains("localhost") || 
                    endpointStr.Contains("127.0.0.1"))
                {
                    _logger.LogInformation("[DockerEngine] Windows本地环境，使用命名管道 (npipe)");
                    endpointStr = "npipe://./pipe/docker_engine";
                }
            }
            else
            {
                if (string.IsNullOrEmpty(endpointStr) || endpointStr.StartsWith("npipe://"))
                {
                    endpointStr = "unix:///var/run/docker.sock";
                }
            }

            if (_dockerClient == null || endpointStr != _lastUsedEndpoint)
            {
                _logger.LogInformation("[DockerEngine] 正在建立本地连接: {Endpoint}", endpointStr);
                
                _dockerClient?.Dispose();
                _dockerClient = new DockerClientBuilder()
                    .WithEndpoint(new Uri(endpointStr))
                    .WithTimeout(Timeout.InfiniteTimeSpan)
                    .Build();
                _lastUsedEndpoint = endpointStr;
                _lastUsedNodeId = nodeId;
            }

            return _dockerClient!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DockerEngine] 客户端初始化致命错误");
            throw;
        }
        finally
        {
            _clientLock.Release();
        }
    }

    public Task<DockerClient> GetClientAsync(string? nodeId = null) => GetDockerClientAsync(nodeId);

    // --- 接口实现 ---

    public async Task<bool> IsAvailableAsync(string? nodeId = null)
    {
        try
        {
            var client = await GetDockerClientAsync(nodeId);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await client.System.PingAsync(cts.Token);
            return true;
        }
        catch { return false; }
    }

    public async Task<IEnumerable<ContainerInfo>> ListContainersAsync(bool all = false, string? nodeId = null)
    {
        try
        {
            var client = await GetDockerClientAsync(nodeId);
            var containers = await client.Containers.ListContainersAsync(new ContainersListParameters { All = all });
            return containers.Select(c => new ContainerInfo { 
                Id = c.ID, 
                Name = c.Names?.FirstOrDefault()?.TrimStart('/') ?? "", 
                Image = c.Image,
                ImageId = c.ImageID,
                Status = c.Status, 
                State = c.State, 
                Created = c.Created,
                Ports = c.Ports?.Select(p => new ContainerPortMapping {
                    Ip = p.IP,
                    PrivatePort = p.PrivatePort,
                    PublicPort = p.PublicPort ?? 0,
                    Type = p.Type
                }).ToList() ?? new List<ContainerPortMapping>(),
                Mounts = c.Mounts?.Select(m => new ContainerMount {
                    Type = m.Type,
                    Name = m.Name,
                    Source = m.Source,
                    Destination = m.Destination,
                    Mode = m.Mode,
                    Rw = m.RW,
                    Driver = m.Driver,
                    Propagation = m.Propagation
                }).ToList() ?? new List<ContainerMount>()
            });
        }
        catch (Exception ex) {
            _logger.LogError(ex, "ListContainersAsync 出错");
            return new List<ContainerInfo>();
        }
    }

    public async Task<ContainerInfo?> GetContainerAsync(string id, string? nodeId = null)
    {
        try
        {
            var client = await GetDockerClientAsync(nodeId);
            var response = await client.Containers.InspectContainerAsync(id);
            if (response == null) return null;

            // 详细 Docker inspect 响应仅在 Debug 级别输出，避免正常查看容器详情时产生大量日志。
            _logger.LogDebug("Docker inspect: ID={ID}, Name={Name}", response.ID, response.Name);
            _logger.LogDebug("NetworkSettings.IPAddress: {IP}", response.NetworkSettings?.Networks?.Values.FirstOrDefault()?.IPAddress);
            _logger.LogDebug("NetworkSettings.MacAddress: {MAC}", response.NetworkSettings?.Networks?.Values.FirstOrDefault()?.MacAddress);
            _logger.LogDebug("NetworkSettings.Gateway: {Gateway}", response.NetworkSettings?.Networks?.Values.FirstOrDefault()?.Gateway);
            _logger.LogDebug("NetworkSettings.Networks.Count: {Count}", response.NetworkSettings?.Networks?.Count ?? 0);
            _logger.LogDebug("NetworkSettings.Ports.Count: {Count}", response.NetworkSettings?.Ports?.Count ?? 0);
            _logger.LogDebug("HostConfig.PortBindings.Count: {Count}", response.HostConfig?.PortBindings?.Count ?? 0);
            _logger.LogDebug("HostConfig.NetworkMode: {Mode}", response.HostConfig?.NetworkMode);

            _logger.LogDebug("容器 {Id} 检查结果，NetworkSettings null: {IsNull}", id, response.NetworkSettings == null);
            if (response.NetworkSettings != null)
            {
                _logger.LogDebug("Ports null: {IsNull}", response.NetworkSettings.Ports == null);
                _logger.LogDebug("Networks null: {IsNull}", response.NetworkSettings.Networks == null);
                
                if (response.NetworkSettings.Ports != null)
                {
                    _logger.LogDebug("Ports count: {Count}", response.NetworkSettings.Ports.Count);
                    foreach (var p in response.NetworkSettings.Ports)
                    {
                        _logger.LogDebug("Port: {Key} -> {Value}", p.Key, p.Value != null ? string.Join(",", p.Value.Select(v => $"{v.HostIP}:{v.HostPort}")) : "null");
                    }
                }
                
                if (response.NetworkSettings.Networks != null)
                {
                    _logger.LogDebug("Networks count: {Count}", response.NetworkSettings.Networks.Count);
                    foreach (var n in response.NetworkSettings.Networks)
                    {
                        _logger.LogDebug("Network: {Key} -> IP: {IP}, Gateway: {Gateway}, MAC: {MAC}", 
                            n.Key, n.Value?.IPAddress, n.Value?.Gateway, n.Value?.MacAddress);
                    }
                }
                
                // 额外检查 IPAddress
                _logger.LogDebug("NetworkSettings.IPAddress: {IP}", response.NetworkSettings.Networks?.Values.FirstOrDefault()?.IPAddress);
                _logger.LogDebug("NetworkSettings.MacAddress: {MAC}", response.NetworkSettings.Networks?.Values.FirstOrDefault()?.MacAddress);
                _logger.LogDebug("NetworkSettings.Gateway: {Gateway}", response.NetworkSettings.Networks?.Values.FirstOrDefault()?.Gateway);
            }
            
            // 检查 HostConfig
            _logger.LogDebug("HostConfig null: {IsNull}", response.HostConfig == null);
            if (response.HostConfig != null)
            {
                _logger.LogDebug("HostConfig.PortBindings null: {IsNull}", response.HostConfig.PortBindings == null);
                if (response.HostConfig.PortBindings != null)
                {
                    _logger.LogDebug("HostConfig.PortBindings count: {Count}", response.HostConfig.PortBindings.Count);
                    foreach (var p in response.HostConfig.PortBindings)
                    {
                        _logger.LogDebug("HostConfig Port: {Key} -> {Value}", p.Key, p.Value != null ? string.Join(",", p.Value.Select(v => $"{v.HostIP}:{v.HostPort}")) : "null");
                    }
                }
            }

            // 解析端口映射
            var ports = new List<ContainerPortMapping>();
            
            // 从 NetworkSettings.Ports 获取
            if (response.NetworkSettings?.Ports != null)
            {
                foreach (var portBinding in response.NetworkSettings.Ports)
                {
                    // portBinding.Key 格式如 "80/tcp", "443/udp"
                    var portParts = portBinding.Key.Split('/');
                    var privatePort = portParts.Length > 0 && ushort.TryParse(portParts[0], out var pp) ? pp : (ushort)0;
                    var type = portParts.Length > 1 ? portParts[1] : "tcp";
                    
                    if (portBinding.Value != null && portBinding.Value.Count > 0)
                    {
                        foreach (var binding in portBinding.Value)
                        {
                            ports.Add(new ContainerPortMapping
                            {
                                Ip = binding.HostIP ?? "",
                                PrivatePort = privatePort,
                                PublicPort = ushort.TryParse(binding.HostPort, out var hp) ? hp : (ushort)0,
                                Type = type
                            });
                        }
                    }
                    else if (privatePort > 0)
                    {
                        // 仅暴露端口但未映射
                        ports.Add(new ContainerPortMapping
                        {
                            PrivatePort = privatePort,
                            PublicPort = 0,
                            Type = type
                        });
                    }
                }
            }
            
            // 如果没有端口，尝试从 HostConfig.PortBindings 获取
            if (ports.Count == 0 && response.HostConfig?.PortBindings != null)
            {
                _logger.LogInformation("尝试从 HostConfig.PortBindings 获取端口");
                foreach (var portBinding in response.HostConfig.PortBindings)
                {
                    var portParts = portBinding.Key.Split('/');
                    var privatePort = portParts.Length > 0 && ushort.TryParse(portParts[0], out var pp) ? pp : (ushort)0;
                    var type = portParts.Length > 1 ? portParts[1] : "tcp";
                    
                    if (portBinding.Value != null)
                    {
                        foreach (var binding in portBinding.Value)
                        {
                            ports.Add(new ContainerPortMapping
                            {
                                Ip = binding.HostIP ?? "",
                                PrivatePort = privatePort,
                                PublicPort = ushort.TryParse(binding.HostPort, out var hp) ? hp : (ushort)0,
                                Type = type
                            });
                        }
                    }
                }
            }

            // 解析网络设置
            var networkSettings = new Models.NetworkSettings();
            
            // 从 Networks 字典获取
            if (response.NetworkSettings?.Networks != null && response.NetworkSettings.Networks.Count > 0)
            {
                foreach (var network in response.NetworkSettings.Networks)
                {
                    networkSettings.Networks[network.Key] = new NetworkEndpoint
                    {
                        IpAddress = network.Value?.IPAddress ?? "",
                        IPPrefixLen = network.Value?.IPPrefixLen.ToString() ?? "",
                        Gateway = network.Value?.Gateway ?? "",
                        MacAddress = network.Value?.MacAddress ?? "",
                        Aliases = network.Value?.Aliases?.ToList() ?? new List<string>()
                    };
                }
            }
            // 如果 Networks 为空，新版 Docker.DotNet.Enhanced 不再在 NetworkSettings 顶层提供 IPAddress 等字段
            else if (response.NetworkSettings != null)
            {
                _logger.LogDebug("NetworkSettings.Networks 为空，无法获取网络信息");
            }

            // 解析重启策略
            var restartPolicyName = response.HostConfig?.RestartPolicy?.Name.ToString()?.ToLower() ?? "no";
            var restartPolicy = new Models.RestartPolicy
            {
                Name = restartPolicyName,
                MaximumRetryCount = (int)(response.HostConfig?.RestartPolicy?.MaximumRetryCount ?? 0L)
            };

            _logger.LogInformation("最终端口数: {PortCount}, 网络数: {NetworkCount}", ports.Count, networkSettings.Networks.Count);

            // 解析日期字符串
            var state = response.State;
            var config = response.Config;
            DateTime? startedAt = null;
            DateTime? finishedAt = null;
            if (!string.IsNullOrEmpty(state?.StartedAt) && DateTime.TryParse(state.StartedAt, out var startTime))
                startedAt = startTime;
            if (!string.IsNullOrEmpty(state?.FinishedAt) && DateTime.TryParse(state.FinishedAt, out var finishTime))
                finishedAt = finishTime;

            // 解析挂载卷
            var mounts = new List<ContainerMount>();
            if (response.Mounts != null)
            {
                foreach (var mount in response.Mounts)
                {
                    mounts.Add(new ContainerMount
                    {
                        Type = mount.Type,
                        Source = mount.Source,
                        Destination = mount.Destination,
                        Mode = mount.Mode,
                        Rw = mount.RW,
                        Propagation = mount.Propagation
                    });
                }
            }

            return new ContainerInfo
            {
                Id = response.ID,
                Name = response.Name?.TrimStart('/'),
                Image = config?.Image,
                ImageId = response.Image,
                Status = state?.Status,
                State = state?.Status,
                Created = response.Created,
                StartedAt = startedAt,
                FinishedAt = finishedAt,
                Command = config?.Cmd?.ToList(),
                Entrypoint = config?.Entrypoint?.ToList(),
                Environment = config?.Env?.ToList(),
                WorkingDir = config?.WorkingDir,
                Ports = ports,
                NetworkSettings = networkSettings,
                Labels = config?.Labels?.ToDictionary(k => k.Key, v => v.Value) ?? new Dictionary<string, string>(),
                RestartPolicy = restartPolicy,
                Mounts = mounts,
                HostName = config?.Hostname ?? "",
                NodeId = "local"
            };
        }
        catch (Exception) { return null; }
    }

    public async Task<string> CreateContainerAsync(CreateContainerRequest request, string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        
        // 默认网络名称
        const string DEFAULT_NETWORK = "dockerpanel-network";
        
        // 构建端口绑定
        var portBindings = new Dictionary<string, IList<PortBinding>>();
        var exposedPorts = new Dictionary<string, EmptyStruct>();
        
        if (request.Ports != null)
        {
            foreach (var port in request.Ports)
            {
                var key = $"{port.ContainerPort}/{port.Protocol.ToLower()}";
                exposedPorts[key] = default(EmptyStruct);
                portBindings[key] = new List<PortBinding>
                {
                    new PortBinding { HostPort = port.HostPort, HostIP = port.HostIp ?? "" }
                };
            }
        }
        
        // 构建卷绑定
        var volumeBindings = new List<string>();
        if (request.Volumes != null)
        {
            foreach (var vol in request.Volumes)
            {
                var mode = vol.ReadOnly ? "ro" : "rw";
                volumeBindings.Add($"{vol.HostPath}:{vol.ContainerPath}:{mode}");
            }
        }
        
        // 构建设备映射
        var devices = new List<Docker.DotNet.Models.DeviceMapping>();
        if (request.Devices != null)
        {
            foreach (var device in request.Devices)
            {
                devices.Add(new Docker.DotNet.Models.DeviceMapping
                {
                    PathOnHost = device.HostPath,
                    PathInContainer = device.ContainerPath,
                    CgroupPermissions = "rwm"
                });
            }
        }
        
        // 构建环境变量
        var envVars = new List<string>();
        if (request.Environment != null)
        {
            foreach (var env in request.Environment)
            {
                envVars.Add($"{env.Key}={env.Value}");
            }
        }
        
        // 确定网络模式
        string networkMode;
        if (request.NetworkMode == "host")
        {
            networkMode = "host";
        }
        else if (request.NetworkMode == "none")
        {
            networkMode = "none";
        }
        else
        {
            networkMode = request.Network?.NetworkId ?? DEFAULT_NETWORK;
        }

        // 如果没有指定容器名，但指定了 hostname，则用 hostname 作为容器名
        var containerName = request.Name ?? request.Hostname;

        _logger.LogInformation("创建容器 {Name}，网络: {Network}", containerName, networkMode);

        // 构建日志配置
        Dictionary<string, string>? logConfigDict = null;
        if (request.LogConfig != null)
        {
            logConfigDict = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(request.LogConfig.MaxSize))
                logConfigDict["max-size"] = request.LogConfig.MaxSize;
            if (request.LogConfig.MaxFile.HasValue)
                logConfigDict["max-file"] = request.LogConfig.MaxFile.Value.ToString();
        }

        // 构建健康检查配置
        Docker.DotNet.Models.HealthcheckConfig? healthConfig = null;
        if (request.HealthCheck != null && !string.IsNullOrEmpty(request.HealthCheck.Test))
        {
            // 解析健康检查命令
            var testParts = request.HealthCheck.Test.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var testList = new List<string>();
            
            if (testParts.Length > 0)
            {
                // 检查是否以 CMD 或 CMD-SHELL 开头
                if (testParts[0].Equals("CMD", StringComparison.OrdinalIgnoreCase))
                {
                    testList.Add("CMD");
                    testList.AddRange(testParts.Skip(1));
                }
                else if (testParts[0].Equals("CMD-SHELL", StringComparison.OrdinalIgnoreCase))
                {
                    testList.Add("CMD-SHELL");
                    testList.Add(string.Join(" ", testParts.Skip(1)));
                }
                else
                {
                    // 默认使用 CMD-SHELL
                    testList.Add("CMD-SHELL");
                    testList.Add(request.HealthCheck.Test);
                }
            }
            
            healthConfig = new Docker.DotNet.Models.HealthcheckConfig
            {
                Test = testList,
                Interval = TimeSpan.FromSeconds(request.HealthCheck.Interval),
                Timeout = TimeSpan.FromSeconds(request.HealthCheck.Timeout),
                Retries = request.HealthCheck.Retries,
                StartPeriod = TimeSpan.FromSeconds(request.HealthCheck.StartPeriod)
            };
        }

        // 解析共享内存大小
        long shmSize = 67108864; // 默认 64MB
        if (!string.IsNullOrEmpty(request.ShmSize))
        {
            shmSize = ParseSize(request.ShmSize);
        }
        else if (request.Resources?.ShmSize != null)
        {
            shmSize = ParseSize(request.Resources.ShmSize);
        }

        // 确定网络模式字符串
        // networkMode 已经是确定的网络名（host/none/具体网络名），直接使用
        var hostNetworkMode = networkMode;

        var parameters = new CreateContainerParameters {
            Name = containerName,
            Image = request.Image!,
            Hostname = request.Hostname!,
            User = request.User!,
            Tty = request.Tty,
            OpenStdin = request.Interactive,
            StopSignal = request.StopSignal!,
            StopTimeout = request.StopTimeout.HasValue ? TimeSpan.FromSeconds(request.StopTimeout.Value) : null,
            Env = envVars.Count > 0 ? envVars : null!,
            ExposedPorts = exposedPorts.Count > 0 ? exposedPorts : null!,
            Cmd = request.Command?.Count > 0 ? request.Command : null!,
            Entrypoint = request.Entrypoint?.Count > 0 ? request.Entrypoint : null!,
            Healthcheck = healthConfig!,
            HostConfig = new HostConfig { 
                AutoRemove = request.AutoRemove, 
                Privileged = request.Privileged,
                ReadonlyRootfs = request.ReadOnlyRootfs,
                DNS = request.Dns?.Count > 0 ? request.Dns : null!,
                DNSSearch = request.DnsSearch?.Count > 0 ? request.DnsSearch : null!,
                ExtraHosts = request.ExtraHosts?.Count > 0 ? request.ExtraHosts : null!,
                GroupAdd = request.GroupAdd?.Count > 0 ? request.GroupAdd : null!,
                CapAdd = request.CapAdd?.Count > 0 ? request.CapAdd : null!,
                CapDrop = request.CapDrop?.Count > 0 ? request.CapDrop : null!,
                Devices = devices.Count > 0 ? devices : null!,
                ShmSize = shmSize,
                Init = request.Init ? true : null,
                PidMode = request.HostPid ? "host" : null!,
                Runtime = request.Runtime!,
                Tmpfs = request.Tmpfs?.Count > 0 
                    ? request.Tmpfs.ToDictionary(t => t.ContainerPath, t => t.Options ?? "") 
                    : null!,
                RestartPolicy = new Docker.DotNet.Models.RestartPolicy { 
                    Name = request.RestartPolicy?.Name?.ToLower() switch
                    {
                        "always" => RestartPolicyKind.Always,
                        "unless-stopped" => RestartPolicyKind.UnlessStopped,
                        "on-failure" => RestartPolicyKind.OnFailure,
                        _ => RestartPolicyKind.No
                    },
                    MaximumRetryCount = request.RestartPolicy?.MaximumRetryCount ?? 0
                },
                PortBindings = portBindings.Count > 0 ? portBindings : null!,
                Binds = volumeBindings.Count > 0 ? volumeBindings : null!,
                NetworkMode = hostNetworkMode,
                LogConfig = logConfigDict != null ? new Docker.DotNet.Models.LogConfig
                {
                    Type = request.LogConfig?.Driver ?? "json-file",
                    Config = logConfigDict
                } : null!,
                // 资源限制扩展
                Memory = !string.IsNullOrEmpty(request.Resources?.MemoryLimit) ? long.Parse(request.Resources.MemoryLimit) : 0,
                MemorySwap = !string.IsNullOrEmpty(request.Resources?.MemorySwap) ? long.Parse(request.Resources.MemorySwap) : 0,
                MemoryReservation = !string.IsNullOrEmpty(request.Resources?.MemoryReservation) ? long.Parse(request.Resources.MemoryReservation) : 0,
                CPUQuota = !string.IsNullOrEmpty(request.Resources?.CpuQuota) ? long.Parse(request.Resources.CpuQuota) : 0,
                CPUPeriod = !string.IsNullOrEmpty(request.Resources?.CpuPeriod) ? long.Parse(request.Resources.CpuPeriod) : 0,
                CPUShares = !string.IsNullOrEmpty(request.Resources?.CpuShares) ? long.Parse(request.Resources.CpuShares) : 0,
                CpusetCpus = request.Resources?.CpusetCpus!,
                PidsLimit = request.Resources?.PidsLimit ?? 0
            },
            WorkingDir = request.WorkingDir!,
            Labels = request.Labels!
        };

        // 设置网络配置（IP地址、别名、Mac地址等）
        if (!string.IsNullOrEmpty(networkMode) && networkMode != "host" && networkMode != "none" && request.Network != null)
        {
            var endpointSettings = new EndpointSettings();

            // 设置 IP 地址
            if (!string.IsNullOrEmpty(request.Network.IpAddress))
            {
                endpointSettings.IPAMConfig = new EndpointIPAMConfig
                {
                    IPv4Address = request.Network.IpAddress
                };
            }

            // 设置别名
            if (request.Network.Aliases != null && request.Network.Aliases.Count > 0)
            {
                endpointSettings.Aliases = request.Network.Aliases;
            }

            // 设置 Mac 地址
            if (!string.IsNullOrEmpty(request.MacAddress))
            {
                endpointSettings.MacAddress = request.MacAddress;
            }

            parameters.NetworkingConfig = new NetworkingConfig
            {
                EndpointsConfig = new Dictionary<string, EndpointSettings>
                {
                    [networkMode] = endpointSettings
                }
            };

            _logger.LogInformation("容器网络配置: 网络={Network}, IP={IP}, Mac={Mac}, 别名={Aliases}",
                networkMode, request.Network.IpAddress, request.MacAddress, string.Join(",", request.Network.Aliases ?? new List<string>()));
        }

        var res = await client.Containers.CreateContainerAsync(parameters);
        _logger.LogInformation("容器创建成功: {Id}", res.ID);

        // 连接到额外的网络
        if (request.Network?.AdditionalNetworks != null && request.Network.AdditionalNetworks.Count > 0)
        {
            foreach (var additionalNetId in request.Network.AdditionalNetworks)
            {
                try
                {
                    var connectParams = new NetworkConnectParameters
                    {
                        Container = res.ID
                    };
                    await client.Networks.ConnectNetworkAsync(additionalNetId, connectParams);
                    _logger.LogInformation("容器 {ContainerId} 已连接到额外网络 {NetworkId}", res.ID, additionalNetId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "无法将容器 {ContainerId} 连接到网络 {NetworkId}", res.ID, additionalNetId);
                }
            }
        }

        return res.ID;
    }

    public async Task StartContainerAsync(string id, string? nodeId = null) => await (await GetDockerClientAsync(nodeId)).Containers.StartContainerAsync(id, new ContainerStartParameters());
    public async Task StopContainerAsync(string id, int timeout = 30, string? nodeId = null) => await (await GetDockerClientAsync(nodeId)).Containers.StopContainerAsync(id, new ContainerStopParameters { WaitBeforeKillSeconds = (uint)timeout });
    public async Task RestartContainerAsync(string id, int timeout = 30, string? nodeId = null) => await (await GetDockerClientAsync(nodeId)).Containers.RestartContainerAsync(id, new ContainerRestartParameters { WaitBeforeKillSeconds = (uint)timeout });
    public async Task RemoveContainerAsync(string id, bool force = false, string? nodeId = null) => await (await GetDockerClientAsync(nodeId)).Containers.RemoveContainerAsync(id, new ContainerRemoveParameters { Force = force });

    public async Task<ContainerLogs> GetContainerLogsAsync(string id, DateTime? since = null, DateTime? until = null, int tail = 100, bool follow = false, string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        var parameters = new ContainerLogsParameters { ShowStdout = true, ShowStderr = true, Tail = tail.ToString() };
        using var multiplexedStream = await client.Containers.GetContainerLogsAsync(id, parameters, CancellationToken.None);
        var (stdout, stderr) = await multiplexedStream.ReadOutputToEndAsync(CancellationToken.None);
        var content = stdout + stderr;
        return new ContainerLogs { ContainerId = id, Logs = new List<ContainerLogEntry> { new ContainerLogEntry { Message = content, Timestamp = DateTime.UtcNow } } };
    }

    public async Task<ContainerStats> GetContainerStatsAsync(string id, string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        ContainerStatsResponse? firstStats = null;
        ContainerStatsResponse? secondStats = null;
        
        // 使用流式模式获取两次采样，以正确计算 CPU 使用率
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        
        try
        {
            var count = 0;
            var progress = new Progress<ContainerStatsResponse>(s =>
            {
                if (count == 0)
                {
                    firstStats = s;
                    count++;
                }
                else if (count == 1)
                {
                    secondStats = s;
                    count++;
                    cts.Cancel(); // 收到第二次数据后取消
                }
            });
            
            await client.Containers.GetContainerStatsAsync(id, new ContainerStatsParameters { Stream = true }, progress, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // 预期的取消，忽略
        }
        
        var stats = new ContainerStats 
        { 
            ContainerId = id, 
            Timestamp = DateTime.UtcNow,
            CpuStats = new ContainerCpuStats(),
            MemoryStats = new ContainerMemoryStats()
        };
        
        // 使用第二次采样（如果有），否则使用第一次
        var statsResponse = secondStats ?? firstStats;
        
        if (statsResponse != null)
        {
            var cpuStats = statsResponse.CPUStats;
            var cpuUsage = cpuStats?.CPUUsage;
            var memoryStats = statsResponse.MemoryStats;

            // CPU 使用率计算
            stats.CpuStats.CpuUsage = (long)(cpuUsage?.TotalUsage ?? 0UL);
            stats.CpuStats.SystemUsage = (long)(cpuStats?.SystemUsage.GetValueOrDefault() ?? 0UL);
            stats.CpuStats.OnlineCpus = Environment.ProcessorCount;
            
            // 计算CPU百分比：需要两次采样的差值
            if (firstStats?.CPUStats?.CPUUsage != null && secondStats?.CPUStats?.CPUUsage != null)
            {
                var cpuDelta = (double)secondStats.CPUStats.CPUUsage.TotalUsage - firstStats.CPUStats.CPUUsage.TotalUsage;
                var systemDelta = (double)secondStats.CPUStats.SystemUsage.GetValueOrDefault() - firstStats.CPUStats.SystemUsage.GetValueOrDefault();
                
                if (systemDelta > 0 && cpuDelta >= 0)
                {
                    var numCpus = Environment.ProcessorCount;
                    stats.CpuStats.PercentCpu = (cpuDelta / systemDelta) * numCpus * 100.0;
                }
            }
            else if (cpuUsage != null && statsResponse.PreCPUStats?.CPUUsage != null)
            {
                // 回退：使用 PreCPUStats（可能不准确）
                var cpuDelta = (double)cpuUsage.TotalUsage - statsResponse.PreCPUStats.CPUUsage.TotalUsage;
                var systemDelta = (double)(cpuStats?.SystemUsage.GetValueOrDefault() ?? 0UL) - statsResponse.PreCPUStats.SystemUsage.GetValueOrDefault();
                
                if (systemDelta > 0 && cpuDelta >= 0)
                {
                    var numCpus = Environment.ProcessorCount;
                    stats.CpuStats.PercentCpu = (cpuDelta / systemDelta) * numCpus * 100.0;
                }
            }
            
            // 内存使用
            stats.MemoryStats.Usage = (long)(memoryStats?.Usage.GetValueOrDefault() ?? 0UL);
            stats.MemoryStats.Limit = (long)(memoryStats?.Limit.GetValueOrDefault() ?? 0UL);
            stats.MemoryStats.MaxUsage = (long)(memoryStats?.MaxUsage.GetValueOrDefault() ?? 0UL);
            
            // 计算内存使用百分比
            if (stats.MemoryStats.Limit > 0)
            {
                stats.MemoryStats.PercentMemory = (double)stats.MemoryStats.Usage / stats.MemoryStats.Limit * 100;
                stats.MemoryStats.UsagePercent = stats.MemoryStats.PercentMemory;
            }
            
            // 网络统计
            if (statsResponse.Networks != null)
            {
                stats.Networks = statsResponse.Networks.Select(n => new ContainerNetworkStats
                {
                    Name = n.Key,
                    RxBytes = (long)n.Value.RxBytes,
                    TxBytes = (long)n.Value.TxBytes,
                    RxPackets = (long)n.Value.RxPackets,
                    TxPackets = (long)n.Value.TxPackets
                }).ToList();
            }
        }
        
        // Ensure all double values are valid (not Infinity or NaN) to avoid JSON serialization errors
        if (double.IsNaN(stats.CpuStats.PercentCpu) || double.IsInfinity(stats.CpuStats.PercentCpu))
        {
            stats.CpuStats.PercentCpu = 0;
        }
        if (double.IsNaN(stats.MemoryStats.PercentMemory) || double.IsInfinity(stats.MemoryStats.PercentMemory))
        {
            stats.MemoryStats.PercentMemory = 0;
        }
        if (double.IsNaN(stats.MemoryStats.UsagePercent) || double.IsInfinity(stats.MemoryStats.UsagePercent))
        {
            stats.MemoryStats.UsagePercent = 0;
        }
        
        return stats;
    }

    public async Task PullImageAsync(string name, string? tag = null, IProgress<ImagePullProgress>? progress = null, string? registryId = null, string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        var imageRef = SplitImageNameAndTag(name, tag);
        var dockerProgress = new Progress<JSONMessage>(m => {
            progress?.Report(new ImagePullProgress { Id = m.ID ?? "layer", Status = m.Status ?? "", ProgressDetail = m.Progress != null ? $"{m.Progress.Current}/{m.Progress.Total}" : "", Current = m.Progress?.Current ?? 0, Total = m.Progress?.Total ?? 0 });
        });

        // 获取认证配置
        AuthConfig authConfig;
        string pullImageName = imageRef.Name;
        bool useMirror = false;
        string? mirrorDomain = null;
        
        // 如果指定了加速器 ID，使用加速器
        if (!string.IsNullOrEmpty(registryId))
        {
            var mirrorConfig = await GetMirrorConfigAsync(registryId);
            if (mirrorConfig != null)
            {
                useMirror = true;
                mirrorDomain = mirrorConfig.Domain;
                _logger.LogInformation("使用镜像加速器: {MirrorName} ({MirrorDomain})", mirrorConfig.Name, mirrorConfig.Domain);
                
                // 构建加速器镜像地址
                // Docker Hub 镜像格式: mirror.domain.com/library/nginx 或 mirror.domain.com/nginx
                // 私有镜像格式: mirror.domain.com/namespace/image
                if (!imageRef.Name.Contains('/') || (imageRef.Name.Split('/')[0].Equals("library", StringComparison.OrdinalIgnoreCase)))
                {
                    // Docker Hub 官方镜像或无命名空间的镜像
                    var imageParts = imageRef.Name.Split('/');
                    var imageNameWithTag = imageParts.Length > 1 && imageParts[0].Equals("library", StringComparison.OrdinalIgnoreCase)
                        ? string.Join("/", imageParts.Skip(1))
                        : imageRef.Name;
                    pullImageName = $"{mirrorConfig.Domain}/{imageNameWithTag}";
                }
                else
                {
                    // 私有仓库镜像，直接使用加速器地址
                    var imageNameOnly = imageRef.Name.Contains('/') ? imageRef.Name.Substring(imageRef.Name.IndexOf('/') + 1) : imageRef.Name;
                    pullImageName = $"{mirrorConfig.Domain}/{imageNameOnly}";
                }
                
                _logger.LogInformation("加速器镜像地址: {PullImageName}:{Tag}", pullImageName, imageRef.Tag);
                
                // 如果加速器需要认证，设置认证配置
                if (!string.IsNullOrEmpty(mirrorConfig.Username) && !string.IsNullOrEmpty(mirrorConfig.Password))
                {
                    authConfig = new AuthConfig
                    {
                        ServerAddress = mirrorConfig.Domain,
                        Username = mirrorConfig.Username,
                        Password = mirrorConfig.Password
                    };
                }
                else
                {
                    authConfig = new AuthConfig { ServerAddress = mirrorConfig.Domain };
                }
            }
            else
            {
                // 加速器未找到，使用默认方式
                authConfig = await GetAuthConfigForImageAsync(name);
            }
        }
        else
        {
            // 尝试根据镜像名称匹配已配置的注册表凭证
            authConfig = await GetAuthConfigForImageAsync(imageRef.Name);
        }
        
        // 调试：输出 AuthConfig JSON
        var authJson = System.Text.Json.JsonSerializer.Serialize(authConfig);
        _logger.LogInformation("AuthConfig JSON: {AuthJson}", authJson);
        
        var actualTag = imageRef.Tag;
        await client.Images.CreateImageAsync(new ImagesCreateParameters { FromImage = pullImageName, Tag = actualTag }, authConfig, dockerProgress);
        
        // 如果使用了加速器，需要重命名镜像回原始名称
        if (useMirror && pullImageName != imageRef.Name)
        {
            try
            {
                // 获取拉取的镜像
                var mirrorRef = $"{pullImageName}:{actualTag}";
                _logger.LogInformation("重命名镜像: {MirrorRef} -> {Name}:{Tag}", mirrorRef, imageRef.Name, actualTag);
                
                // 使用 tag 命令重命名
                await client.Images.TagImageAsync(mirrorRef, new ImageTagParameters
                {
                    RepositoryName = imageRef.Name,
                    Tag = actualTag
                });
                
                // 删除加速器命名的镜像标签（保留镜像本身）
                try
                {
                    await client.Images.DeleteImageAsync(mirrorRef, new ImageDeleteParameters { Force = false, NoPrune = true });
                    _logger.LogInformation("已删除加速器镜像标签: {MirrorRef}", mirrorRef);
                }
                catch (Exception delEx)
                {
                    // 删除失败不影响主流程，镜像可能还被其他标签引用
                    _logger.LogDebug(delEx, "删除加速器镜像标签失败（可忽略）");
                }
                
                _logger.LogInformation("镜像重命名完成: {Name}:{Tag}", imageRef.Name, actualTag);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "重命名镜像失败，镜像可能已存在");
            }
        }
    }

    private static (string Name, string Tag) SplitImageNameAndTag(string name, string? tag)
    {
        if (!string.IsNullOrWhiteSpace(tag))
        {
            return (name, tag);
        }

        var slashIndex = name.LastIndexOf('/');
        var colonIndex = name.LastIndexOf(':');
        if (colonIndex > slashIndex)
        {
            return (name[..colonIndex], name[(colonIndex + 1)..]);
        }

        return (name, "latest");
    }

    /// <summary>
    /// 根据 ID 获取镜像加速器配置
    /// </summary>
    private async Task<ImageRegistry?> GetMirrorConfigAsync(string registryId)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var registryService = scope.ServiceProvider.GetService<IRegistryService>();
            
            if (registryService == null)
            {
                _logger.LogWarning("注册表服务不可用");
                return null;
            }

            var registry = await registryService.GetRegistryByIdAsync(registryId);
            if (registry != null && registry.Type == "Mirror")
            {
                return registry;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取镜像加速器配置失败: {RegistryId}", registryId);
            return null;
        }
    }

    /// <summary>
    /// 根据镜像名称获取匹配的注册表认证配置
    /// </summary>
    private async Task<AuthConfig> GetAuthConfigForImageAsync(string imageName)
    {
        try
        {
            // 解析镜像名称中的注册表地址
            // 格式: [registry/][namespace/]image[:tag]
            // 例如: docker.io/library/nginx, registry.example.com/myimage, myregistry.com/namespace/image
            string? registryHost = null;
            
            // 如果镜像名包含 '/'，第一部分可能是注册表地址
            var parts = imageName.Split('/');
            if (parts.Length > 1)
            {
                // 检查第一部分是否包含 '.' 或 ':' 或是 'localhost'，这些通常是注册表地址
                var firstPart = parts[0];
                if (firstPart.Contains('.') || firstPart.Contains(':') || firstPart == "localhost")
                {
                    registryHost = firstPart;
                }
            }

            if (string.IsNullOrEmpty(registryHost))
            {
                // Docker Hub 镜像，不需要特殊认证（或使用默认认证）
                return new AuthConfig();
            }

            // 从注册表服务获取匹配的注册表
            using var scope = _serviceProvider.CreateScope();
            var registryService = scope.ServiceProvider.GetService<IRegistryService>();
            
            if (registryService == null)
            {
                _logger.LogDebug("注册表服务不可用，使用空认证配置");
                return new AuthConfig();
            }

            var registries = await registryService.GetRegistriesAsync();
            var matchingRegistry = registries.FirstOrDefault(r => 
                r.Domain?.TrimEnd('/').Equals(registryHost, StringComparison.OrdinalIgnoreCase) == true ||
                r.Domain?.Contains(registryHost, StringComparison.OrdinalIgnoreCase) == true
            );

            if (matchingRegistry == null)
            {
                _logger.LogDebug("未找到匹配的注册表配置: {RegistryHost}", registryHost);
                return new AuthConfig();
            }

            _logger.LogInformation("为镜像 {ImageName} 匹配到注册表: {RegistryName} ({Domain})", imageName, matchingRegistry.Name, matchingRegistry.Domain);
            
            // 调试日志：显示凭据信息
            var username = matchingRegistry.Username ?? matchingRegistry.AuthConfig?.Username ?? "";
            var password = matchingRegistry.Password ?? matchingRegistry.AuthConfig?.Password ?? "";
            var serverAddress = matchingRegistry.Domain ?? registryHost;
            
            _logger.LogInformation("认证配置详情 - Username: '{Username}', Password长度: {PwdLen}, ServerAddress: '{ServerAddress}'", 
                username, 
                string.IsNullOrEmpty(password) ? 0 : password.Length,
                serverAddress);

            // 按照 Docker.DotNet 官方示例格式
            return new AuthConfig
            {
                Username = username,
                Password = password,
                ServerAddress = serverAddress
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取镜像 {ImageName} 的认证配置失败", imageName);
            return new AuthConfig();
        }
    }

    /// <summary>
    /// 使用 Docker API 验证仓库凭据
    /// </summary>
    public async Task<RegistryAuthResult> AuthenticateRegistryAsync(string serverAddress, string username, string password, string? nodeId = null)
    {
        try
        {
            var client = await GetDockerClientAsync(nodeId);
            
            var authConfig = new AuthConfig
            {
                ServerAddress = serverAddress,
                Username = username,
                Password = password
            };
            
            _logger.LogInformation("使用 Docker API 验证仓库凭据: {ServerAddress}, Username: {Username}", serverAddress, username);
            
            await client.System.AuthenticateAsync(authConfig);
            
            return new RegistryAuthResult
            {
                IsValid = true,
                Message = "凭据验证成功",
                TestTime = DateTime.UtcNow
            };
        }
        catch (Docker.DotNet.DockerApiException ex)
        {
            _logger.LogWarning(ex, "仓库凭据验证失败: {ServerAddress}", serverAddress);
            
            var message = ex.Message;
            if (message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("401", StringComparison.OrdinalIgnoreCase))
            {
                message = "用户名或密码错误";
            }
            else if (message.Contains("403", StringComparison.OrdinalIgnoreCase))
            {
                message = "权限不足";
            }
            else if (message.Contains("404", StringComparison.OrdinalIgnoreCase))
            {
                message = "仓库地址不存在";
            }
            
            return new RegistryAuthResult
            {
                IsValid = false,
                Message = message,
                TestTime = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "仓库凭据验证异常: {ServerAddress}", serverAddress);
            return new RegistryAuthResult
            {
                IsValid = false,
                Message = $"验证失败: {ex.Message}",
                TestTime = DateTime.UtcNow
            };
        }
    }

    public async Task<IEnumerable<ImageInfo>> ListImagesAsync(string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        var images = await client.Images.ListImagesAsync(new ImagesListParameters());
        
        // 获取所有容器，统计每个镜像的使用次数
        var containers = await client.Containers.ListContainersAsync(new ContainersListParameters { All = true });
        var imageUsageCount = containers
            .GroupBy(c => c.ImageID)
            .ToDictionary(g => g.Key, g => g.Count());
        
        return images.Select(i => {
            var repoTag = i.RepoTags?.FirstOrDefault() ?? "<none>";
            var parts = repoTag.Split(':');
            var imageId = i.ID;
            
            return new ImageInfo 
            { 
                Id = imageId, 
                Repository = parts[0], 
                Tag = parts.Length > 1 ? parts[1] : "latest",
                Size = i.Size, 
                Created = i.Created,
                CreatedAt = i.Created,
                RepoTags = i.RepoTags?.ToArray() ?? Array.Empty<string>(),
                ContainersCount = imageUsageCount.TryGetValue(imageId, out var count) ? count : 0
            };
        });
    }
    
    public async Task<ImageInfo?> GetImageAsync(string id, string? nodeId = null)
    {
        try
        {
            var client = await GetDockerClientAsync(nodeId);
            
            // 尝试通过 ID 或名称 inspect 镜像
            ImageInspectResponse? inspectResult = null;
            try
            {
                inspectResult = await client.Images.InspectImageAsync(id);
            }
            catch (DockerApiException ex)
            {
                // 如果通过名称找不到，尝试从列表中模糊匹配
                _logger.LogDebug("通过名称 {Id} 查找镜像失败，尝试模糊匹配: {Message}", id, ex.Message);
                var images = await client.Images.ListImagesAsync(new ImagesListParameters());
                
                // 构建匹配条件：支持 ID、名称、名称:tag 格式
                var matchingImage = images.FirstOrDefault(i => 
                    i.ID.Equals(id, StringComparison.OrdinalIgnoreCase) || 
                    i.ID.EndsWith(id, StringComparison.OrdinalIgnoreCase) ||
                    i.RepoTags?.Any(t => 
                        t.Equals(id, StringComparison.OrdinalIgnoreCase) ||
                        (id.Contains(':') && t.Equals(id, StringComparison.OrdinalIgnoreCase)) ||
                        (!id.Contains(':') && t.StartsWith(id + ":", StringComparison.OrdinalIgnoreCase))
                    ) == true
                );
                
                if (matchingImage != null)
                {
                    _logger.LogDebug("模糊匹配到镜像: {Id}", matchingImage.ID);
                    inspectResult = await client.Images.InspectImageAsync(matchingImage.ID);
                }
            }
            
            if (inspectResult == null) return null;
            
            var info = new ImageDetailInfo
            {
                Id = inspectResult.ID,
                Repository = inspectResult.RepoTags?.FirstOrDefault() ?? "<none>",
                Tag = inspectResult.RepoTags?.FirstOrDefault()?.Split(':').LastOrDefault() ?? "latest",
                Size = inspectResult.Size,
                VirtualSize = 0, // ImageInspectResponse 没有 VirtualSize
                Created = inspectResult.Created.GetValueOrDefault(),
                CreatedAt = inspectResult.Created.GetValueOrDefault(),
                Architecture = inspectResult.Architecture,
                Os = inspectResult.Os,
                Author = inspectResult.Author ?? "",
                Comment = inspectResult.Comment ?? "",
                Parent = "", // ImageInspectResponse 没有 Parent
                Labels = inspectResult.Config?.Labels?.ToDictionary(kv => kv.Key, kv => kv.Value) ?? new(),
                RepoTags = inspectResult.RepoTags?.ToArray() ?? Array.Empty<string>(),
                
                // 从 Config 中提取镜像配置信息
                ExposedPorts = inspectResult.Config?.ExposedPorts?.Keys?.ToList() ?? new(),
                Volumes = inspectResult.Config?.Volumes?.Keys?.ToList() ?? new(),
                Env = inspectResult.Config?.Env?.ToList() ?? new(),
                WorkingDir = inspectResult.Config?.WorkingDir,
                Entrypoint = inspectResult.Config?.Entrypoint?.ToList(),
                Cmd = inspectResult.Config?.Cmd?.ToList(),
                User = inspectResult.Config?.User
            };
            
            return info;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取镜像详情失败: {ImageId}", id);
            return null;
        }
    }
    public async Task RemoveImageAsync(string id, bool force = false, string? nodeId = null) => await (await GetDockerClientAsync(nodeId)).Images.DeleteImageAsync(id, new ImageDeleteParameters { Force = force });
    public async Task<IEnumerable<ImageSearchResult>> SearchImagesAsync(string term, string? nodeId = null) => (await (await GetDockerClientAsync(nodeId)).Images.SearchImagesAsync(new ImagesSearchParameters { Term = term })).Select(r => new ImageSearchResult { Name = r.Name, Description = r.Description ?? string.Empty, Stars = (int)r.StarCount, IsOfficial = r.IsOfficial, IsAutomated = r.IsAutomated });

    public async Task<IEnumerable<ImageHistory>> GetImageHistoryAsync(string id, string? nodeId = null)
    {
        try
        {
            var client = await GetDockerClientAsync(nodeId);
            var history = await client.Images.GetImageHistoryAsync(id);
            return history.Select(h => new ImageHistory
            {
                Id = h.ID,
                Created = h.Created,
                CreatedBy = h.CreatedBy ?? "",
                Tags = h.Tags?.ToArray() ?? Array.Empty<string>(),
                Size = h.Size,
                Comment = h.Comment ?? ""
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取镜像历史失败: {ImageId}", id);
            return new List<ImageHistory>();
        }
    }

    public async Task<Stream> SaveImageAsync(string imageId, string? nodeId = null)
    {
        _logger.LogInformation("导出镜像: {ImageId}", imageId);
        var client = await GetDockerClientAsync(nodeId);
        return await client.Images.SaveImageAsync(imageId);
    }

    public async Task<List<string>> LoadImageAsync(Stream imageStream, string? nodeId = null)
    {
        _logger.LogInformation("导入镜像");
        var client = await GetDockerClientAsync(nodeId);
        var loadedImages = new List<string>();
        
        var progress = new Progress<Docker.DotNet.Models.JSONMessage>(msg =>
        {
            if (!string.IsNullOrEmpty(msg.Stream))
            {
                _logger.LogInformation("导入进度: {Stream}", msg.Stream.Trim());
                // 解析镜像名称，格式如: "Loaded image: ubuntu:latest"
                if (msg.Stream.Contains("Loaded image:") || msg.Stream.Contains("Loaded image ID:"))
                {
                    var parts = msg.Stream.Split(new[] { "Loaded image:", "Loaded image ID:" }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0)
                    {
                        loadedImages.Add(parts[0].Trim());
                    }
                }
            }
            if (msg.Error != null)
            {
                _logger.LogError("导入镜像错误: {Error}", msg.Error.Message);
            }
        });
        
        await client.Images.LoadImageAsync(new Docker.DotNet.Models.ImageLoadParameters(), imageStream, progress);
        return loadedImages;
    }

    public async Task<string> BuildImageFromContextAsync(Stream contextStream, BuildImageParams parameters, IProgress<ImageBuildProgress>? progress = null, string? nodeId = null)
    {
        _logger.LogInformation("构建镜像: {Tag}", parameters.Tag);
        var client = await GetDockerClientAsync(nodeId);

        var buildParams = new Docker.DotNet.Models.ImageBuildParameters
        {
            Tags = new List<string> { parameters.Tag },
            Dockerfile = parameters.Dockerfile,
            NoCache = parameters.NoCache,
            Remove = true, // 清理中间容器
            ForceRemove = true, // 强制清理中间容器（即使构建失败）
            BuildArgs = parameters.BuildArgs
        };

        var builtImageId = "";
        var buildProgress = new Progress<Docker.DotNet.Models.JSONMessage>(msg =>
        {
            if (progress != null)
            {
                var buildInfo = new ImageBuildProgress
                {
                    Stream = msg.Stream ?? "",
                    Status = msg.Status ?? "",
                    Error = msg.Error?.Message ?? "",
                    Aux = msg.Aux?.ToString() ?? ""
                };

                // 解析镜像 ID
                if (!string.IsNullOrEmpty(msg.Stream) && msg.Stream.Contains("Successfully built"))
                {
                    var parts = msg.Stream.Split(new[] { "Successfully built" }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0)
                    {
                        builtImageId = parts[0].Trim();
                    }
                }
                if (!string.IsNullOrEmpty(msg.Aux?.ToString()))
                {
                    var auxStr = msg.Aux.ToString() ?? "";
                    if (auxStr.Contains("ID"))
                    {
                        // 解析 {"ID":"sha256:xxx"} 格式
                        var idMatch = System.Text.RegularExpressions.Regex.Match(auxStr, @"""ID""\s*:\s*""([^""]+)""");
                        if (idMatch.Success)
                        {
                            builtImageId = idMatch.Groups[1].Value;
                        }
                    }
                }

                progress.Report(buildInfo);
            }
            if (!string.IsNullOrEmpty(msg.Stream))
            {
                _logger.LogInformation("构建进度: {Stream}", msg.Stream.Trim());
            }
            if (msg.Error != null)
            {
                _logger.LogError("构建镜像错误: {Error}", msg.Error.Message);
            }
        });

        await client.Images.BuildImageFromDockerfileAsync(buildParams, contextStream, null, null, buildProgress);

        // 构建成功后清理无标签的中间镜像
        try
        {
            _logger.LogInformation("清理无标签的中间镜像...");
            await client.Images.PruneImagesAsync(new Docker.DotNet.Models.ImagesPruneParameters
            {
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["dangling"] = new Dictionary<string, bool> { ["true"] = true }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "清理无标签镜像失败（可忽略）");
        }

        return builtImageId;
    }

    public async Task<string> BuildImageFromDockerfileAsync(string dockerfileContent, BuildImageParams parameters, IProgress<ImageBuildProgress>? progress = null, string? nodeId = null)
    {
        _logger.LogInformation("从 Dockerfile 构建镜像: {Tag}", parameters.Tag);

        // 创建一个只包含 Dockerfile 的最小 tar 包
        using var tarStream = new MemoryStream();
        using (var tarWriter = new System.Formats.Tar.TarWriter(tarStream, System.Formats.Tar.TarEntryFormat.Pax, leaveOpen: true))
        {
            // 写入 Dockerfile
            var dockerfileEntry = new System.Formats.Tar.PaxTarEntry(System.Formats.Tar.TarEntryType.RegularFile, parameters.Dockerfile)
            {
                DataStream = new MemoryStream(Encoding.UTF8.GetBytes(dockerfileContent)),
                ModificationTime = DateTimeOffset.UtcNow
            };
            await tarWriter.WriteEntryAsync(dockerfileEntry);
        }

        tarStream.Position = 0;
        return await BuildImageFromContextAsync(tarStream, parameters, progress, nodeId);
    }

    public async Task TagImageAsync(string sourceImage, string targetName, string? nodeId = null)
    {
        _logger.LogInformation("标记镜像: {SourceImage} -> {TargetName}", sourceImage, targetName);
        var client = await GetDockerClientAsync(nodeId);
        await client.Images.TagImageAsync(sourceImage, new Docker.DotNet.Models.ImageTagParameters
        {
            RepositoryName = targetName.Contains(":") ? targetName.Split(':')[0] : targetName,
            Tag = targetName.Contains(":") ? targetName.Split(':')[1] : "latest"
        });
    }

    public async Task PushImageAsync(string imageName, string? tag = null, IProgress<ImagePushProgress>? progress = null, string? registryId = null, string? nodeId = null)
    {
        _logger.LogInformation("推送镜像: {ImageName}:{Tag}", imageName, tag ?? "latest");
        var client = await GetDockerClientAsync(nodeId);
        
        var pushProgress = new Progress<Docker.DotNet.Models.JSONMessage>(msg =>
        {
            if (progress != null)
            {
                var pushInfo = new ImagePushProgress
                {
                    Id = msg.ID ?? "",
                    Status = msg.Status ?? "",
                    Current = msg.Progress?.Current ?? 0,
                    Total = msg.Progress?.Total ?? 0
                };
                progress.Report(pushInfo);
            }
        });
        
        var pushParams = new Docker.DotNet.Models.ImagePushParameters
        {
            Tag = tag ?? "latest"
        };
        
        await client.Images.PushImageAsync(imageName, pushParams, null, pushProgress);
    }

    public async Task<EngineVersionInfo> GetVersionAsync(string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        var version = await client.System.GetVersionAsync();
        return new EngineVersionInfo
        {
            Version = version.Version ?? string.Empty,
            ApiVersion = version.APIVersion ?? string.Empty,
            Arch = version.Arch ?? string.Empty,
            Os = version.Os ?? string.Empty,
            KernelVersion = version.KernelVersion ?? string.Empty,
            BuildTime = version.BuildTime ?? string.Empty
        };
    }

    public async Task<EngineSystemInfo> GetSystemInfoAsync(string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        var info = await client.System.GetSystemInfoAsync();
        return new EngineSystemInfo
        {
            OSType = info.OSType ?? string.Empty,
            Architecture = info.Architecture ?? string.Empty,
            KernelVersion = info.KernelVersion ?? string.Empty,
            NCPU = (int)info.NCPU,
            MemTotal = info.MemTotal,
            DockerRootDir = info.DockerRootDir ?? string.Empty,
            Name = info.Name ?? string.Empty
        };
    }

    public async Task UpdateContainerResourcesAsync(string id, UpdateContainerResourcesRequest request, string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        
        Docker.DotNet.Models.RestartPolicy? restartPolicy = null;
        if (request.RestartPolicy != null && !string.IsNullOrEmpty(request.RestartPolicy.Name))
        {
            var kind = request.RestartPolicy.Name.ToLowerInvariant() switch
            {
                "no" => Docker.DotNet.Models.RestartPolicyKind.No,
                "always" => Docker.DotNet.Models.RestartPolicyKind.Always,
                "on-failure" => Docker.DotNet.Models.RestartPolicyKind.OnFailure,
                "unless-stopped" => Docker.DotNet.Models.RestartPolicyKind.UnlessStopped,
                _ => Docker.DotNet.Models.RestartPolicyKind.No
            };
            restartPolicy = new Docker.DotNet.Models.RestartPolicy
            {
                Name = kind,
                MaximumRetryCount = request.RestartPolicy.MaximumRetryCount
            };
        }
        
        var parameters = new ContainerUpdateParameters
        {
            RestartPolicy = restartPolicy!,
            Memory = request.Memory ?? 0,
            MemoryReservation = request.MemoryReservation ?? 0,
            MemorySwap = request.MemorySwap ?? 0,
            CPUShares = request.CpuShares ?? 0,
            CPUPeriod = request.CpuPeriod ?? 0,
            CPUQuota = request.CpuQuota ?? 0,
            CpusetCpus = request.CpusetCpus!,
            CpusetMems = request.CpusetMems!
        };
        
        await client.Containers.UpdateContainerAsync(id, parameters);
        _logger.LogInformation("容器 {ContainerId} 资源配置已更新", id);
    }
    public async Task<ResourceLimits?> GetContainerResourcesAsync(string id, string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        var container = await client.Containers.InspectContainerAsync(id);
        var hostConfig = container.HostConfig;
        if (hostConfig == null) return null;

        return new ResourceLimits
        {
            MemoryLimit = hostConfig.Memory > 0 ? hostConfig.Memory.ToString() : null,
            MemorySwap = hostConfig.MemorySwap > 0 ? hostConfig.MemorySwap.ToString() : null,
            MemoryReservation = hostConfig.MemoryReservation > 0 ? hostConfig.MemoryReservation.ToString() : null,
            CpuQuota = hostConfig.CPUQuota > 0 ? hostConfig.CPUQuota.ToString() : null,
            CpuPeriod = hostConfig.CPUPeriod > 0 ? hostConfig.CPUPeriod.ToString() : null,
            CpuShares = hostConfig.CPUShares > 0 ? hostConfig.CPUShares.ToString() : null,
            CpusetCpus = hostConfig.CpusetCpus,
            ShmSize = hostConfig.ShmSize > 0 ? hostConfig.ShmSize.ToString() : null,
            PidsLimit = hostConfig.PidsLimit
        };
    }

    public async Task<HealthCheckStatus?> GetContainerHealthStatusAsync(string id, string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        var container = await client.Containers.InspectContainerAsync(id);
        var health = container.State?.Health;
        if (health == null)
        {
            return new HealthCheckStatus { Status = "none", LastCheck = DateTime.UtcNow };
        }

        var lastLog = health.Log?.LastOrDefault();
        return new HealthCheckStatus
        {
            Status = health.Status ?? "unknown",
            FailingStreak = health.FailingStreak.ToString(),
            Log = lastLog?.Output,
            LastCheck = lastLog?.End,
            StartedAt = lastLog?.Start
        };
    }

    public async Task<IEnumerable<HealthCheckLog>> GetContainerHealthLogsAsync(string id, DateTime? since = null, DateTime? until = null, int limit = 100, string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        var container = await client.Containers.InspectContainerAsync(id);
        return (container.State?.Health?.Log ?? new List<HealthcheckResult>())
            .Where(l => (!since.HasValue || l.Start >= since.Value) && (!until.HasValue || l.End <= until.Value))
            .OrderByDescending(l => l.Start)
            .Take(limit)
            .Select(l => new HealthCheckLog
            {
                ContainerId = id,
                Timestamp = l.Start,
                Status = l.ExitCode == 0 ? "healthy" : "unhealthy",
                Output = l.Output,
                ExitCode = (int)l.ExitCode,
                Duration = l.End >= l.Start ? l.End - l.Start : TimeSpan.Zero
            })
            .ToList();
    }

    public async Task<HealthCheckStats?> GetContainerHealthStatsAsync(string id, string? nodeId = null)
    {
        var logs = (await GetContainerHealthLogsAsync(id, limit: 1000, nodeId: nodeId)).ToList();
        if (logs.Count == 0) return null;

        return new HealthCheckStats
        {
            ContainerId = id,
            TotalChecks = logs.Count,
            SuccessfulChecks = logs.Count(l => l.ExitCode == 0),
            FailedChecks = logs.Count(l => l.ExitCode != 0),
            AverageResponseTime = TimeSpan.FromMilliseconds(logs.Average(l => l.Duration.TotalMilliseconds)),
            LastSuccess = logs.Where(l => l.ExitCode == 0).OrderByDescending(l => l.Timestamp).FirstOrDefault()?.Timestamp ?? DateTime.MinValue,
            LastFailure = logs.Where(l => l.ExitCode != 0).OrderByDescending(l => l.Timestamp).FirstOrDefault()?.Timestamp ?? DateTime.MinValue,
            ConsecutiveFailures = CountConsecutiveHealthChecks(logs, false),
            ConsecutiveSuccesses = CountConsecutiveHealthChecks(logs, true),
            SuccessRate = logs.Count > 0 ? logs.Count(l => l.ExitCode == 0) * 100.0 / logs.Count : 0
        };
    }

    public Task UpdateContainerHealthCheckAsync(string id, HealthCheckConfig config, string? nodeId = null) => throw new NotSupportedException("Docker 不支持对已创建容器直接热更新 Healthcheck，请通过重建容器应用健康检查配置。");
    public Task RemoveContainerHealthCheckAsync(string id, string? nodeId = null) => throw new NotSupportedException("Docker 不支持对已创建容器直接移除 Healthcheck，请通过重建容器应用配置。");

    private static int CountConsecutiveHealthChecks(IEnumerable<HealthCheckLog> logs, bool success)
    {
        var count = 0;
        foreach (var log in logs.OrderByDescending(l => l.Timestamp))
        {
            if ((log.ExitCode == 0) != success) break;
            count++;
        }

        return count;
    }
    public async Task<IEnumerable<Models.VolumeInfo>> ListVolumesAsync(string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        var response = await client.Volumes.ListAsync(CancellationToken.None);
        
        // 获取所有容器以计算卷使用情况
        var containers = await client.Containers.ListContainersAsync(new ContainersListParameters { All = true });
        var volumeUsage = new Dictionary<string, int>();
        
        foreach (var container in containers)
        {
            if (container.Mounts != null)
            {
                foreach (var mount in container.Mounts)
                {
                    if (!string.IsNullOrEmpty(mount.Name))
                    {
                        if (!volumeUsage.ContainsKey(mount.Name))
                            volumeUsage[mount.Name] = 0;
                        volumeUsage[mount.Name]++;
                    }
                }
            }
        }
        
        return response.Volumes.Select(v => new Models.VolumeInfo
        {
            Id = v.Name,
            Name = v.Name,
            Driver = v.Driver ?? "local",
            Mountpoint = v.Mountpoint,
            Scope = v.Scope,
            Created = ParseDockerTimestamp(v.CreatedAt),
            Size = v.UsageData?.Size ?? 0,
            UsageCount = volumeUsage.GetValueOrDefault(v.Name, 0),
            Labels = v.Labels?.ToDictionary(kv => kv.Key, kv => kv.Value) ?? new Dictionary<string, string>(),
            Options = v.Options?.ToDictionary(kv => kv.Key, kv => kv.Value) ?? new Dictionary<string, string>()
        });
    }
    
    private DateTime ParseDockerTimestamp(string? timestamp)
    {
        if (string.IsNullOrEmpty(timestamp)) return DateTime.MinValue;
        try
        {
            // Docker 返回的是 RFC3339 格式，例如: 2024-01-15T10:30:00Z
            return DateTime.Parse(timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }
    
    public async Task<Models.VolumeInfo?> GetVolumeAsync(string name, string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        try
        {
            var response = await client.Volumes.InspectAsync(name);
            return new Models.VolumeInfo
            {
                Id = response.Name,
                Name = response.Name,
                Driver = response.Driver ?? "local",
                Mountpoint = response.Mountpoint,
                Scope = response.Scope,
                Created = ParseDockerTimestamp(response.CreatedAt),
                Size = response.UsageData?.Size ?? 0,
                Labels = response.Labels?.ToDictionary(kv => kv.Key, kv => kv.Value) ?? new Dictionary<string, string>(),
                Options = response.Options?.ToDictionary(kv => kv.Key, kv => kv.Value) ?? new Dictionary<string, string>()
            };
        }
        catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
    
    public async Task<string> CreateVolumeAsync(CreateVolumeRequest request, string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        
        var parameters = new VolumesCreateParameters
        {
            Name = request.Name ?? "",  // 空字符串让 Docker 自动生成随机名称
            Driver = request.Driver ?? "local"
        };
        
        if (request.Labels != null && request.Labels.Count > 0)
        {
            parameters.Labels = request.Labels;
        }
        
        var response = await client.Volumes.CreateAsync(parameters);
        return response.Name;
    }
    
    public async Task RemoveVolumeAsync(string name, bool force = false, string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        await client.Volumes.RemoveAsync(name, force);
    }
    
    public async Task<IEnumerable<NetworkInfo>> ListNetworksAsync(string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        var networks = await client.Networks.ListNetworksAsync();

        // 不可删除的网络：Docker 预定义网络 + 系统核心网络
        var defaultNetworkNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bridge",      // Docker 默认桥接网络
            "host",        // Docker 主机网络
            "none",        // Docker 空网络
            "dockerpanel-network"  // 系统核心网络（YARP 反代依赖）
        };

        return networks.Select(n => new NetworkInfo
        {
            Id = n.ID,
            Name = n.Name,
            Driver = n.Driver,
            Scope = n.Scope,
            IsDefault = defaultNetworkNames.Contains(n.Name),
            Internal = n.Internal,
            EnableIPv6 = n.EnableIPv6,
            Attachable = n.Attachable,
            Ingress = n.Ingress,
            Labels = n.Labels != null ? new Dictionary<string, string>(n.Labels) : new Dictionary<string, string>(),
            Options = n.Options != null ? new Dictionary<string, string>(n.Options) : new Dictionary<string, string>(),
            CreatedAt = n.Created.ToString("o"),
            IPAM = n.IPAM != null ? JsonSerializer.Serialize(n.IPAM) : null,
            Containers = n.Containers?.Select(c => new NetworkContainer
            {
                Id = c.Key,
                Name = c.Value.Name ?? string.Empty,
                IpAddress = c.Value.IPv4Address ?? string.Empty,
                MacAddress = c.Value.MacAddress ?? string.Empty
            }).ToList() ?? new List<NetworkContainer>()
        });
    }
    
    public async Task<NetworkInfo?> GetNetworkAsync(string id, string? nodeId = null)
    {
        try
        {
            var client = await GetDockerClientAsync(nodeId);
            var network = await client.Networks.InspectNetworkAsync(id);

            // 不可删除的网络
            var defaultNetworkNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "bridge", "host", "none", "dockerpanel-network"
            };

            return new NetworkInfo
            {
                Id = network.ID,
                Name = network.Name,
                Driver = network.Driver,
                Scope = network.Scope,
                IsDefault = defaultNetworkNames.Contains(network.Name),
                Internal = network.Internal,
                EnableIPv6 = network.EnableIPv6,
                Attachable = network.Attachable,
                Ingress = network.Ingress,
                Labels = network.Labels != null ? new Dictionary<string, string>(network.Labels) : new Dictionary<string, string>(),
                Options = network.Options != null ? new Dictionary<string, string>(network.Options) : new Dictionary<string, string>(),
                CreatedAt = network.Created.ToString("o"),
                IPAM = network.IPAM != null ? JsonSerializer.Serialize(network.IPAM) : null,
                Containers = network.Containers?.Select(c => new NetworkContainer
                {
                    Id = c.Key,
                    Name = c.Value.Name ?? string.Empty,
                    IpAddress = c.Value.IPv4Address ?? string.Empty,
                    MacAddress = c.Value.MacAddress ?? string.Empty
                }).ToList() ?? new List<NetworkContainer>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取网络 {Id} 失败", id);
            return null;
        }
    }
    
    public async Task<NetworkInfo> CreateNetworkAsync(CreateNetworkRequest request, string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        
        var parameters = new NetworksCreateParameters
        {
            Name = request.Name,
            Driver = request.Driver ?? "bridge"
        };
        
        // 设置网络选项
        if (request.Internal)
        {
            parameters.Internal = true;
        }
        
        if (request.EnableIPv6)
        {
            parameters.EnableIPv6 = true;
        }
        
        if (request.Ingress)
        {
            parameters.Ingress = true;
        }
        
        if (request.Attachable)
        {
            parameters.Attachable = true;
        }
        
        // 设置标签
        if (request.Labels != null && request.Labels.Count > 0)
        {
            parameters.Labels = request.Labels;
        }
        
        // 设置选项
        if (request.Options != null && request.Options.Count > 0)
        {
            parameters.Options = request.Options;
        }
        
        // 设置 IPAM 配置
        if (request.IPAM != null)
        {
            parameters.IPAM = new IPAM
            {
                Driver = request.IPAM.Driver ?? "default"
            };
            
            if (request.IPAM.Config != null && request.IPAM.Config.Count > 0)
            {
                parameters.IPAM.Config = request.IPAM.Config.Select(c => new IPAMConfig
                {
                    Subnet = c.Subnet!,
                    Gateway = c.Gateway!,
                    IPRange = c.IPRange!,
                    AuxAddress = c.AuxiliaryAddresses?.Select((addr, i) => new { Key = $"aux{i}", Value = addr })
                        .ToDictionary(x => x.Key, x => x.Value)!
                }).ToList();
            }
        }
        
        var response = await client.Networks.CreateNetworkAsync(parameters);
        _logger.LogInformation("创建网络 {Name} 成功: {Id}", request.Name, response.ID);
        
        return new NetworkInfo 
        { 
            Id = response.ID, 
            Name = request.Name, 
            Driver = request.Driver ?? "bridge",
            Scope = request.Scope ?? "local"
        };
    }
    
    public async Task RemoveNetworkAsync(string id, string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        await client.Networks.DeleteNetworkAsync(id);
        _logger.LogInformation("删除网络 {Id} 成功", id);
    }

    public async Task ConnectContainerToNetworkAsync(string networkId, string containerId, NetworkConnectionConfig? config = null, string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        var parameters = new NetworkConnectParameters
        {
            Container = containerId,
            EndpointConfig = new EndpointSettings
            {
                Aliases = config?.Aliases!
            }
        };
        await client.Networks.ConnectNetworkAsync(networkId, parameters);
        _logger.LogInformation("容器 {ContainerId} 已连接到网络 {NetworkId}", containerId, networkId);
    }

    public async Task DisconnectContainerFromNetworkAsync(string networkId, string containerId, string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        var parameters = new NetworkDisconnectParameters
        {
            Container = containerId,
            Force = false
        };
        await client.Networks.DisconnectNetworkAsync(networkId, parameters);
        _logger.LogInformation("容器 {ContainerId} 已从网络 {NetworkId} 断开", containerId, networkId);
    }

    public async Task<int> PruneNetworksAsync(string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        var response = await client.Networks.PruneNetworksAsync(new NetworksDeleteUnusedParameters());
        return response.NetworksDeleted?.Count ?? 0;
    }

    public async Task<PruneResult> PruneImagesAsync(bool danglingOnly = true, string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        
        // Docker API: dangling=true 只清理悬空镜像，dangling=false 清理所有未使用镜像
        var parameters = new ImagesPruneParameters
        {
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                { "dangling", new Dictionary<string, bool> { { danglingOnly.ToString().ToLower(), true } } }
            }
        };
        
        var response = await client.Images.PruneImagesAsync(parameters);
        
        // 统计：Untagged 是镜像标签，Deleted 是镜像层
        var untagged = response.ImagesDeleted?.Where(i => !string.IsNullOrEmpty(i.Untagged)).ToList() ?? new List<ImageDeleteResponse>();
        var deleted = response.ImagesDeleted?.Where(i => !string.IsNullOrEmpty(i.Deleted)).ToList() ?? new List<ImageDeleteResponse>();
        
        return new PruneResult
        {
            ImagesDeleted = untagged.Count,  // 只统计镜像数量，不包括层
            SpaceReclaimed = (long)response.SpaceReclaimed,
            DeletedImageIds = untagged.Select(i => i.Untagged).Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToList()
        };
    }

    public async Task<ExecResult> ExecuteCommandAsync(string id, ExecCommandRequest command, string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        
        try
        {
            // 创建 exec 实例
            var execCreate = await client.Exec.CreateContainerExecAsync(id, new ContainerExecCreateParameters
            {
                Cmd = command.Command,
                AttachStdout = true,
                AttachStderr = true
            });

            // 执行并获取输出
            var stdout = new MemoryStream();
            var stderr = new MemoryStream();
            
            using var stream = await client.Exec.StartContainerExecAsync(execCreate.ID, new ContainerExecStartParameters());
            await stream.CopyOutputToAsync(Console.OpenStandardOutput(), stdout, stderr, CancellationToken.None);
            
            // 获取退出代码
            var inspect = await client.Exec.InspectContainerExecAsync(execCreate.ID);
            
            return new ExecResult
            {
                ExitCode = (int)inspect.ExitCode.GetValueOrDefault(),
                Stdout = Encoding.UTF8.GetString(stdout.ToArray()),
                Stderr = Encoding.UTF8.GetString(stderr.ToArray())
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行容器 {ContainerId} 命令失败", id);
            return new ExecResult
            {
                ExitCode = -1,
                Stderr = ex.Message
            };
        }
    }
    
    /// <summary>
    /// 解析大小字符串（如 "64m", "1g", "1024k"）为字节数
    /// </summary>
    private static long ParseSize(string size)
    {
        if (string.IsNullOrEmpty(size)) return 0;
        
        size = size.Trim().ToLower();
        var multiplier = 1L;
        
        if (size.EndsWith("k") || size.EndsWith("kb"))
        {
            multiplier = 1024;
            size = size[..^1];
        }
        else if (size.EndsWith("m") || size.EndsWith("mb"))
        {
            multiplier = 1024 * 1024;
            size = size.EndsWith("mb") ? size[..^2] : size[..^1];
        }
        else if (size.EndsWith("g") || size.EndsWith("gb"))
        {
            multiplier = 1024 * 1024 * 1024;
            size = size.EndsWith("gb") ? size[..^2] : size[..^1];
        }
        
        if (long.TryParse(size, out var value))
        {
            return value * multiplier;
        }
        
        return 0;
    }

    #region 文件管理

    /// <summary>
    /// 获取容器文件列表
    /// </summary>
    public async Task<ContainerFileListResponse> GetContainerFilesAsync(string containerId, string path, string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        
        // 标准化路径
        path = NormalizeContainerPath(path);
        
        _logger.LogInformation("获取容器 {ContainerId} 文件列表, 路径: {Path}", containerId, path);
        
        // 先获取容器信息以获取挂载点
        var container = await client.Containers.InspectContainerAsync(containerId);
        var mounts = container.Mounts ?? new List<MountPoint>();
        
        // 使用 exec 执行命令获取文件列表
        var files = new List<ContainerFileInfo>();
        
        try
        {
            // 使用 stat 命令获取文件信息，兼容 GNU coreutils 和 BusyBox
            // stat -c '%n|%Y|%s|%A|%U|%G' 格式：文件名|修改时间戳|大小|权限|用户|组
            // 注意：不用 %F（文件类型）因为输出可能含空格如 "regular file"
            var statCmd = $"cd {ShellQuote(path)} 2>/dev/null && stat -c '%n|%Y|%s|%A|%U|%G' * 2>/dev/null || echo 'STAT_NOT_SUPPORTED'";
            
            var execCreate = await client.Exec.CreateContainerExecAsync(containerId, new ContainerExecCreateParameters
            {
                Cmd = new[] { "sh", "-c", statCmd },
                AttachStdout = true,
                AttachStderr = true
            });

            var stdout = new MemoryStream();
            var stderr = new MemoryStream();
            
            using var stream = await client.Exec.StartContainerExecAsync(execCreate.ID, new ContainerExecStartParameters());
            await stream.CopyOutputToAsync(Console.OpenStandardOutput(), stdout, stderr, CancellationToken.None);
            
            var output = Encoding.UTF8.GetString(stdout.ToArray()).Trim();
            
            _logger.LogInformation("stat 输出: {Output}", output);
            
            if (output.Contains("STAT_NOT_SUPPORTED") || string.IsNullOrEmpty(output))
            {
                // stat 不支持，回退到 ls -la
                _logger.LogInformation("stat 不支持，回退到 ls -la");
                return await GetContainerFilesWithLsAsync(client, containerId, path, mounts);
            }

            // 解析 stat 输出
            // 格式：文件名|时间戳|大小|权限|用户|组
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                var parts = line.Split('|');
                if (parts.Length < 6) continue;
                
                var name = parts[0];
                var timestampStr = parts[1];
                var sizeStr = parts[2];
                var permissions = parts[3];
                var user = parts[4];
                var group = parts[5];
                
                if (name == "." || name == "..") continue;
                
                // 解析时间戳（Unix 时间戳，秒）
                DateTime? modified = null;
                if (long.TryParse(timestampStr, out var timestamp))
                {
                    modified = DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime;
                }
                
                // 用权限判断是否是目录
                var isDirectory = permissions.StartsWith("d");
                var isMount = mounts.Any(m => m.Destination == $"{path.TrimEnd('/')}/{name}" || 
                                               (path == "/" && m.Destination == $"/{name}"));
                var mount = mounts.FirstOrDefault(m => m.Destination == $"{path.TrimEnd('/')}/{name}" || 
                                                       (path == "/" && m.Destination == $"/{name}"));
                
                _ = long.TryParse(sizeStr, out var size);
                
                files.Add(new ContainerFileInfo
                {
                    Name = name,
                    Path = $"{path.TrimEnd('/')}/{name}",
                    Type = isDirectory ? "directory" : "file",
                    Size = isDirectory ? 0 : size,
                    Permissions = permissions,
                    Modified = modified,
                    Owner = user,
                    Group = group,
                    IsMount = isMount,
                    MountSource = mount?.Source,
                    MountType = mount?.Type
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取容器 {ContainerId} 文件列表失败，尝试 ls 回退", containerId);
            try
            {
                return await GetContainerFilesWithLsAsync(client, containerId, path, mounts);
            }
            catch
            {
                // 忽略回退失败
            }
        }

        // 获取文件变更状态
        var changes = await GetContainerChangesAsync(containerId, nodeId);
        foreach (var file in files)
        {
            if (changes.TryGetValue(file.Path, out var status))
            {
                file.ChangeStatus = status;
            }
        }

        return new ContainerFileListResponse
        {
            ContainerId = containerId,
            CurrentPath = path,
            Files = files,
            Mounts = mounts.Select(m => new ContainerMountInfo
            {
                Destination = m.Destination ?? "",
                Source = m.Source,
                Type = m.Type ?? "",
                Name = m.Name,
                Rw = m.RW,
                Driver = m.Driver
            }).ToList()
        };
    }

    /// <summary>
    /// 使用 ls 命令获取文件列表（回退方案）
    /// </summary>
    private async Task<ContainerFileListResponse> GetContainerFilesWithLsAsync(DockerClient client, string containerId, string path, IList<MountPoint> mounts)
    {
        var files = new List<ContainerFileInfo>();
        
        var execCreate = await client.Exec.CreateContainerExecAsync(containerId, new ContainerExecCreateParameters
        {
            Cmd = new[] { "sh", "-c", $"ls -la {ShellQuote(path)} 2>/dev/null || echo 'ERROR'" },
            AttachStdout = true,
            AttachStderr = true
        });

        var stdout = new MemoryStream();
        var stderr = new MemoryStream();
        
        using var stream = await client.Exec.StartContainerExecAsync(execCreate.ID, new ContainerExecStartParameters());
        await stream.CopyOutputToAsync(Console.OpenStandardOutput(), stdout, stderr, CancellationToken.None);
        
        var output = Encoding.UTF8.GetString(stdout.ToArray());
        
        _logger.LogInformation("ls 回退输出: {Output}", output);
        
        if (output.Contains("ERROR"))
        {
            throw new Exception($"无法访问路径: {path}");
        }

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            var parts = System.Text.RegularExpressions.Regex.Split(line.Trim(), @"\s+");
            if (parts.Length < 8) continue;
            
            var permissions = parts[0];
            var size = ParseSize(parts[4]);
            var name = string.Join(" ", parts.Skip(7));
            
            if (name == "." || name == "..") continue;
            
            // ls 默认格式时间解析（不精确）
            DateTime? modified = ParseDateTime(parts[5], parts[6], parts[7]);
            
            var isDirectory = permissions.StartsWith("d");
            var isMount = mounts.Any(m => m.Destination == $"{path.TrimEnd('/')}/{name}" || 
                                           (path == "/" && m.Destination == $"/{name}"));
            var mount = mounts.FirstOrDefault(m => m.Destination == $"{path.TrimEnd('/')}/{name}" || 
                                                   (path == "/" && m.Destination == $"/{name}"));
            
            files.Add(new ContainerFileInfo
            {
                Name = name,
                Path = $"{path.TrimEnd('/')}/{name}",
                Type = isDirectory ? "directory" : "file",
                Size = isDirectory ? 0 : size,
                Permissions = permissions,
                Modified = modified,
                IsMount = isMount,
                MountSource = mount?.Source,
                MountType = mount?.Type
            });
        }
        
        // 获取文件变更状态
        var changes = await GetContainerChangesAsync(containerId);
        foreach (var file in files)
        {
            if (changes.TryGetValue(file.Path, out var status))
            {
                file.ChangeStatus = status;
            }
        }
        
        return new ContainerFileListResponse
        {
            ContainerId = containerId,
            CurrentPath = path,
            Files = files,
            Mounts = mounts.Select(m => new ContainerMountInfo
            {
                Destination = m.Destination ?? "",
                Source = m.Source,
                Type = m.Type ?? "",
                Name = m.Name,
                Rw = m.RW,
                Driver = m.Driver
            }).ToList()
        };
    }

    /// <summary>
    /// 获取容器挂载点信息
    /// </summary>
    public async Task<List<ContainerMountInfo>> GetContainerMountsAsync(string containerId, string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        var container = await client.Containers.InspectContainerAsync(containerId);
        var mounts = container.Mounts ?? new List<MountPoint>();
        
        return mounts.Select(m => new ContainerMountInfo
        {
            Destination = m.Destination ?? "",
            Source = m.Source,
            Type = m.Type ?? "",
            Name = m.Name,
            Rw = m.RW,
            Driver = m.Driver
        }).ToList();
    }

    /// <summary>
    /// 下载容器文件
    /// </summary>
    public async Task<byte[]> DownloadContainerFileAsync(string containerId, string path, string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        path = NormalizeContainerPath(path, allowRoot: false);
        
        // 使用 Docker API 的 archive endpoint 获取文件
        var response = await client.Containers.GetArchiveFromContainerAsync(containerId, new ContainerPathStatParameters
        {
            Path = path
        }, false);

        if (response.Stream == null)
        {
            throw new Exception("无法获取文件流");
        }

        using var memoryStream = new MemoryStream();
        await response.Stream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }

    /// <summary>
    /// 上传文件到容器
    /// </summary>
    public async Task UploadContainerFileAsync(string containerId, string path, string fileName, byte[] content, string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        path = NormalizeContainerPath(path);
        fileName = NormalizeContainerFileName(fileName);
        
        // 创建 tar 格式的文件流
        using var tarStream = CreateTarStream(fileName, content);
        
        // 使用 docker cp 上传文件
        await client.Containers.ExtractArchiveToContainerAsync(containerId, new CopyToContainerParameters
        {
            Path = path
        }, tarStream);
    }

    /// <summary>
    /// 创建文件夹
    /// </summary>
    public async Task CreateContainerFolderAsync(string containerId, string path, string name, string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        var targetPath = CombineContainerPath(path, name);
        
        var execCreate = await client.Exec.CreateContainerExecAsync(containerId, new ContainerExecCreateParameters
        {
            Cmd = new[] { "mkdir", "-p", targetPath },
            AttachStdout = true,
            AttachStderr = true
        });

        using var stream = await client.Exec.StartContainerExecAsync(execCreate.ID, new ContainerExecStartParameters());
        var stdout = new MemoryStream();
        var stderr = new MemoryStream();
        await stream.CopyOutputToAsync(Console.OpenStandardOutput(), stdout, stderr, CancellationToken.None);
        
        var error = Encoding.UTF8.GetString(stderr.ToArray());
        if (!string.IsNullOrEmpty(error))
        {
            throw new Exception($"创建文件夹失败: {error}");
        }
    }

    /// <summary>
    /// 重命名文件
    /// </summary>
    public async Task RenameContainerFileAsync(string containerId, string path, string oldName, string newName, string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        var oldPath = CombineContainerPath(path, oldName);
        var newPath = CombineContainerPath(path, newName);
        
        var execCreate = await client.Exec.CreateContainerExecAsync(containerId, new ContainerExecCreateParameters
        {
            Cmd = new[] { "mv", oldPath, newPath },
            AttachStdout = true,
            AttachStderr = true
        });

        using var stream = await client.Exec.StartContainerExecAsync(execCreate.ID, new ContainerExecStartParameters());
        var stdout = new MemoryStream();
        var stderr = new MemoryStream();
        await stream.CopyOutputToAsync(Console.OpenStandardOutput(), stdout, stderr, CancellationToken.None);
        
        var error = Encoding.UTF8.GetString(stderr.ToArray());
        if (!string.IsNullOrEmpty(error))
        {
            throw new Exception($"重命名失败: {error}");
        }
    }

    /// <summary>
    /// 删除文件
    /// </summary>
    public async Task DeleteContainerFileAsync(string containerId, string path, bool recursive, string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        path = NormalizeContainerPath(path, allowRoot: false);
        
        var cmd = recursive ? new[] { "rm", "-rf", path } : new[] { "rm", path };
        
        var execCreate = await client.Exec.CreateContainerExecAsync(containerId, new ContainerExecCreateParameters
        {
            Cmd = cmd,
            AttachStdout = true,
            AttachStderr = true
        });

        using var stream = await client.Exec.StartContainerExecAsync(execCreate.ID, new ContainerExecStartParameters());
        var stdout = new MemoryStream();
        var stderr = new MemoryStream();
        await stream.CopyOutputToAsync(Console.OpenStandardOutput(), stdout, stderr, CancellationToken.None);
        
        var error = Encoding.UTF8.GetString(stderr.ToArray());
        if (!string.IsNullOrEmpty(error))
        {
            throw new Exception($"删除失败: {error}");
        }
    }

    /// <summary>
    /// 获取容器文件内容
    /// </summary>
    public async Task<string> GetContainerFileContentAsync(string containerId, string path, string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        path = NormalizeContainerPath(path, allowRoot: false);
        
        // 先检查文件类型
        var typeCheck = await client.Exec.CreateContainerExecAsync(containerId, new ContainerExecCreateParameters
        {
            Cmd = new[] { "sh", "-c", $"file -b {ShellQuote(path)} 2>/dev/null || echo 'unknown'" },
            AttachStdout = true,
            AttachStderr = true
        });

        using (var typeStream = await client.Exec.StartContainerExecAsync(typeCheck.ID, new ContainerExecStartParameters()))
        {
            var typeStdout = new MemoryStream();
            var typeStderr = new MemoryStream();
            await typeStream.CopyOutputToAsync(Console.OpenStandardOutput(), typeStdout, typeStderr, CancellationToken.None);
            var fileType = Encoding.UTF8.GetString(typeStdout.ToArray()).Trim().ToLower();
            
            // 检查是否为文本文件
            var isText = fileType.Contains("text") || 
                         fileType.Contains("ascii") || 
                         fileType.Contains("utf-8") ||
                         fileType.Contains("json") ||
                         fileType.Contains("xml") ||
                         fileType.Contains("shell") ||
                         fileType.Contains("script") ||
                         fileType.Contains("empty");
            
            if (!isText && !fileType.Contains("unknown"))
            {
                throw new Exception($"该文件类型为 {fileType}，不支持文本编辑");
            }
        }
        
        // 使用 cat 命令读取文件内容
        var execCreate = await client.Exec.CreateContainerExecAsync(containerId, new ContainerExecCreateParameters
        {
            Cmd = new[] { "cat", path },
            AttachStdout = true,
            AttachStderr = true
        });

        using var stream = await client.Exec.StartContainerExecAsync(execCreate.ID, new ContainerExecStartParameters());
        var stdout = new MemoryStream();
        var stderr = new MemoryStream();
        await stream.CopyOutputToAsync(Console.OpenStandardOutput(), stdout, stderr, CancellationToken.None);
        
        var error = Encoding.UTF8.GetString(stderr.ToArray());
        if (!string.IsNullOrEmpty(error))
        {
            throw new Exception($"读取文件失败: {error}");
        }
        
        return Encoding.UTF8.GetString(stdout.ToArray());
    }

    /// <summary>
    /// 写入容器文件内容
    /// </summary>
    public async Task WriteContainerFileContentAsync(string containerId, string path, string content, string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        path = NormalizeContainerPath(path, allowRoot: false);
        
        // 使用 base64 编码避免 shell 转义问题
        var base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(content));
        var tmpFile = $"/tmp/edit_{Guid.NewGuid():N}";
        
        // 先写入临时文件，再移动到目标位置（保留原文件权限）
        var execCreate = await client.Exec.CreateContainerExecAsync(containerId, new ContainerExecCreateParameters
        {
            Cmd = new[] { "sh", "-c", $"printf %s {ShellQuote(base64Content)} | base64 -d > {ShellQuote(tmpFile)} && cat {ShellQuote(tmpFile)} > {ShellQuote(path)} && rm -f {ShellQuote(tmpFile)}" },
            AttachStdout = true,
            AttachStderr = true
        });

        using var stream = await client.Exec.StartContainerExecAsync(execCreate.ID, new ContainerExecStartParameters());
        var stdout = new MemoryStream();
        var stderr = new MemoryStream();
        await stream.CopyOutputToAsync(Console.OpenStandardOutput(), stdout, stderr, CancellationToken.None);
        
        var error = Encoding.UTF8.GetString(stderr.ToArray());
        if (!string.IsNullOrEmpty(error))
        {
            throw new Exception($"写入文件失败: {error}");
        }
    }

    /// <summary>
    /// 修改容器文件权限
    /// </summary>
    public async Task ChangeContainerFilePermissionsAsync(string containerId, string path, string permissions, string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        path = NormalizeContainerPath(path, allowRoot: false);
        
        // 验证权限格式
        if (!System.Text.RegularExpressions.Regex.IsMatch(permissions, @"^[0-7]{3,4}$"))
        {
            throw new ArgumentException($"无效的权限格式: {permissions}，应为3-4位八进制数字（如 755, 644）");
        }
        
        var execCreate = await client.Exec.CreateContainerExecAsync(containerId, new ContainerExecCreateParameters
        {
            Cmd = new[] { "chmod", permissions, path },
            AttachStdout = true,
            AttachStderr = true
        });

        using var stream = await client.Exec.StartContainerExecAsync(execCreate.ID, new ContainerExecStartParameters());
        var stdout = new MemoryStream();
        var stderr = new MemoryStream();
        await stream.CopyOutputToAsync(Console.OpenStandardOutput(), stdout, stderr, CancellationToken.None);
        
        var error = Encoding.UTF8.GetString(stderr.ToArray());
        if (!string.IsNullOrEmpty(error))
        {
            throw new Exception($"修改权限失败: {error}");
        }
    }

    /// <summary>
    /// 获取容器文件变更列表（docker diff）
    /// </summary>
    public async Task<Dictionary<string, string>> GetContainerChangesAsync(string containerId, string? nodeId = null)
    {
        var changes = new Dictionary<string, string>();
        
        try
        {
            var client = await GetDockerClientAsync(nodeId);
            var changesList = await client.Containers.InspectChangesAsync(containerId);
            
            // docker diff 返回格式: { Path = "/path", Kind = Modify/Add/Delete } 
            foreach (var change in changesList)
            {
                var status = change.Kind switch
                {
                    FileSystemChangeKind.Modify => "C",
                    FileSystemChangeKind.Add => "A",
                    FileSystemChangeKind.Delete => "D",
                    _ => null
                };
                
                if (status != null && !string.IsNullOrEmpty(change.Path))
                {
                    changes[change.Path] = status;
                }
            }
            
            _logger.LogInformation("获取容器 {Id} 变更列表: {Count} 项", containerId, changes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取容器 {Id} 变更列表失败", containerId);
        }
        
        return changes;
    }

    private static string NormalizeContainerPath(string? path, bool allowRoot = true)
    {
        if (string.IsNullOrWhiteSpace(path)) path = "/";
        path = path.Replace('\\', '/').Trim();
        if (path.Contains('\0') || path.Contains('\r') || path.Contains('\n'))
        {
            throw new ArgumentException("路径包含非法控制字符");
        }

        if (!path.StartsWith('/')) path = "/" + path;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(s => s == "." || s == ".."))
        {
            throw new ArgumentException("路径不能包含 . 或 .. 片段");
        }

        var normalized = "/" + string.Join('/', segments);
        if (normalized.Length > 1) normalized = normalized.TrimEnd('/');

        if (!allowRoot && normalized == "/")
        {
            throw new ArgumentException("不允许对根路径执行该操作");
        }

        return normalized;
    }

    private static string NormalizeContainerFileName(string? fileName)
    {
        fileName = Path.GetFileName((fileName ?? string.Empty).Trim());
        if (string.IsNullOrWhiteSpace(fileName) || fileName is "." or "..")
        {
            throw new ArgumentException("文件名不能为空或路径片段");
        }

        if (fileName.Contains('\0') || fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains('\r') || fileName.Contains('\n'))
        {
            throw new ArgumentException("文件名包含非法字符");
        }

        return fileName;
    }

    private static string CombineContainerPath(string path, string name)
    {
        var parent = NormalizeContainerPath(path);
        var fileName = NormalizeContainerFileName(name);
        return parent == "/" ? $"/{fileName}" : $"{parent}/{fileName}";
    }

    private static string ShellQuote(string value)
    {
        return "'" + value.Replace("'", "'\"'\"'") + "'";
    }

    /// <summary>
    /// 创建包含单个文件的 tar 流
    /// </summary>
    private static MemoryStream CreateTarStream(string fileName, byte[] content)
    {
        fileName = NormalizeContainerFileName(fileName);
        var stream = new MemoryStream();
        using (var writer = new System.Formats.Tar.TarWriter(stream, leaveOpen: true))
        {
            var entry = new System.Formats.Tar.PaxTarEntry(System.Formats.Tar.TarEntryType.RegularFile, fileName)
            {
                DataStream = new MemoryStream(content)
            };
            writer.WriteEntry(entry);
        }
        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// 解析 ls --full-time 输出的日期时间 (格式: YYYY-MM-DD HH:MM:SS)
    /// </summary>
    private static DateTime? ParseDateTimeFull(string date, string time)
    {
        try
        {
            // 解析日期 YYYY-MM-DD
            var dateParts = date.Split('-');
            if (dateParts.Length != 3) return null;
            
            if (!int.TryParse(dateParts[0], out var year)) return null;
            if (!int.TryParse(dateParts[1], out var month)) return null;
            if (!int.TryParse(dateParts[2], out var day)) return null;
            
            // 解析时间 HH:MM:SS
            var timeParts = time.Split(':');
            if (timeParts.Length < 3) return null;
            
            if (!int.TryParse(timeParts[0], out var hour)) return null;
            if (!int.TryParse(timeParts[1], out var minute)) return null;
            if (!int.TryParse(timeParts[2], out var second)) return null;
            
            return new DateTime(year, month, day, hour, minute, second);
        }
        catch { }
        
        return null;
    }

    /// <summary>
    /// 解析 ls 输出的日期时间 (long-iso 格式: YYYY-MM-DD HH:MM)
    /// </summary>
    private static DateTime? ParseDateTimeIso(string date, string time)
    {
        try
        {
            // 解析日期 YYYY-MM-DD
            var dateParts = date.Split('-');
            if (dateParts.Length != 3) return null;
            
            if (!int.TryParse(dateParts[0], out var year)) return null;
            if (!int.TryParse(dateParts[1], out var month)) return null;
            if (!int.TryParse(dateParts[2], out var day)) return null;
            
            // 解析时间 HH:MM
            var timeParts = time.Split(':');
            if (timeParts.Length < 2) return null;
            
            if (!int.TryParse(timeParts[0], out var hour)) return null;
            if (!int.TryParse(timeParts[1], out var minute)) return null;
            
            return new DateTime(year, month, day, hour, minute, 0);
        }
        catch { }
        
        return null;
    }

    /// <summary>
    /// 解析 ls 输出的日期时间
    /// </summary>
    private static DateTime? ParseDateTime(string month, string day, string timeOrYear)
    {
        try
        {
            // 尝试解析月份
            var months = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            var monthIndex = Array.IndexOf(months, month) + 1;
            if (monthIndex == 0) return null;
            
            if (!int.TryParse(day, out var dayNum)) return null;
            
            // 判断是时间还是年份
            if (timeOrYear.Contains(':'))
            {
                // 是时间格式，表示当年
                var timeParts = timeOrYear.Split(':');
                if (timeParts.Length >= 2 && int.TryParse(timeParts[0], out var hour) && int.TryParse(timeParts[1], out var minute))
                {
                    return new DateTime(DateTime.Now.Year, monthIndex, dayNum, hour, minute, 0);
                }
            }
            else if (int.TryParse(timeOrYear, out var year))
            {
                // 是年份
                return new DateTime(year, monthIndex, dayNum);
            }
        }
        catch { }
        
        return null;
    }

    #endregion
    
    public void Dispose() { _dockerClient?.Dispose(); }
}
