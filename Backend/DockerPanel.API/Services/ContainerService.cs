using DockerPanel.API.Models;
using Docker.DotNet;
using Microsoft.Extensions.DependencyInjection;
using TinyDb;
using System.Text.Json;
using DockerPanel.API.Data;

namespace DockerPanel.API.Services;

/// <summary>
/// 基于 FluentDocker 的容器服务实现
/// </summary>
public class ContainerService : IContainerService
{
    private readonly IContainerEngine _engine;
    private readonly ILogger<ContainerService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public ContainerService(
        IContainerEngine engine, 
        ILogger<ContainerService> logger,
        IServiceProvider serviceProvider)
    {
        _engine = engine;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    private async Task<DockerClient> GetDockerClientAsync()
    {
        if (_engine is DockerEngine dockerEngine)
        {
            return await dockerEngine.GetClientAsync();
        }

        throw new NotSupportedException("当前容器引擎不支持该 Docker 原生操作");
    }

    public async Task<IEnumerable<ContainerInfo>> GetContainersAsync(string? nodeId = null, bool all = false, int limit = 100)
    {
        var containers = await _engine.ListContainersAsync(all, nodeId);
        return containers.Take(limit);
    }

    public async Task<ContainerInfo?> GetContainerAsync(string id, string? nodeId = null)
    {
        return await _engine.GetContainerAsync(id, nodeId);
    }

    public async Task<ContainerInfo> CreateContainerAsync(CreateContainerRequest request, IProgress<ImagePullProgress>? progress = null)
    {
        try
        {
            // 检查镜像是否存在
            var requestImage = request.Image ?? string.Empty;
            if (requestImage.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                requestImage = requestImage.Substring(8);
            else if (requestImage.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                requestImage = requestImage.Substring(7);
            
            request.Image = requestImage;
            
            var localImage = await _engine.GetImageAsync(requestImage);
            if (localImage == null)
            {
                throw new InvalidOperationException($"镜像 {request.Image} 不存在，请先拉取镜像");
            }

            // 创建容器
            var containerId = await _engine.CreateContainerAsync(request);
            var container = await _engine.GetContainerAsync(containerId);
            
            if (container == null)
            {
                throw new InvalidOperationException("创建容器后无法获取容器信息");
            }
            return container;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建容器流程失败");
            throw;
        }
    }

    public async Task PullImageAsync(string name, string? tag = null, string? nodeId = null, IProgress<ImagePullProgress>? progress = null, string? registryId = null)
    {
        await _engine.PullImageAsync(name, tag, progress, registryId, nodeId);
    }

    public async Task<ContainerInfo> RecreateContainerAsync(string id, bool pullLatest = false, bool autoStart = true, string? overrideImage = null, IProgress<ImagePullProgress>? progress = null)
    {
        var container = await GetContainerAsync(id);
        if (container == null)
        {
            throw new InvalidOperationException("容器未找到");
        }

        var effectiveImage = overrideImage ?? container.Image;

        // 构建创建请求
        var createRequest = new CreateContainerRequest
        {
            Name = container.Name?.TrimStart('/'),
            Image = effectiveImage,
            Entrypoint = container.Entrypoint,
            Command = container.Command,
            WorkingDir = container.WorkingDir,
            Hostname = container.HostName,
            NetworkMode = container.HostConfig?.NetworkMode ?? "bridge",
            Labels = container.Labels
        };

        if (container.Environment != null && container.Environment.Count > 0)
        {
            createRequest.Environment = new Dictionary<string, string>();
            foreach (var env in container.Environment)
            {
                var idx = env.IndexOf('=');
                if (idx > 0)
                {
                    createRequest.Environment[env.Substring(0, idx)] = env.Substring(idx + 1);
                }
            }
        }

        if (container.Ports != null && container.Ports.Count > 0)
        {
            createRequest.Ports = container.Ports
                .Where(p => p.PublicPort > 0)
                .Select(p => new PortMapping
                {
                    ContainerPort = p.PrivatePort.ToString(),
                    HostPort = p.PublicPort.ToString(),
                    Protocol = p.Type ?? "tcp"
                }).ToList();
        }

        if (container.Mounts != null && container.Mounts.Count > 0)
        {
            createRequest.Volumes = container.Mounts
                .Select(m => new VolumeMapping
                {
                    HostPath = m.Source ?? "",
                    ContainerPath = m.Destination ?? "",
                    ReadOnly = !m.Rw
                }).ToList();
        }

        if (container.RestartPolicy != null)
        {
            createRequest.RestartPolicy = new RestartPolicy
            {
                Name = container.RestartPolicy.Name ?? "no",
                MaximumRetryCount = container.RestartPolicy.MaximumRetryCount
            };
        }

        // 拉取最新镜像
        if (pullLatest && !string.IsNullOrEmpty(container.Image))
        {
            var imageName = container.Image!;
            var colonIdx = imageName.LastIndexOf(':');
            var slashIdx = imageName.LastIndexOf('/');
            var (pullName, pullTag) = colonIdx > slashIdx
                ? (imageName[..colonIdx], imageName[(colonIdx + 1)..])
                : (imageName, "latest");
            await PullImageAsync(pullName, pullTag, progress: progress);
        }

        // 备份原容器域名映射
        var dbContext = _serviceProvider.GetRequiredService<TinyDbContext>();
        var mappingsCollection = dbContext.GetCollection<DomainMapping>("domain_mappings");
        var oldMappings = mappingsCollection.Find(m => m.ContainerId == id).ToList();

        // 删除旧容器
        await RemoveContainerAsync(id, force: true);

        // 创建新容器
        var newContainer = await CreateContainerAsync(createRequest);

        // 恢复域名映射
        if (oldMappings.Count > 0)
        {
            var reverseProxyFactory = _serviceProvider.GetRequiredService<IReverseProxyFactory>();
            foreach (var mapping in oldMappings)
            {
                mapping.ContainerId = newContainer.Id;
                mapping.ContainerName = newContainer.Name ?? "unknown";
                await reverseProxyFactory.AddDomainMappingAsync(mapping);
            }
        }

        // 启动新容器
        if (container.State == "running" || autoStart)
        {
            await StartContainerAsync(newContainer.Id);
        }

        return newContainer;
    }

    public async Task StartContainerAsync(string id, string? nodeId = null)
    {
        try
        {
            await _engine.StartContainerAsync(id, nodeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动容器 {Id} 失败", id);
            throw;
        }
    }

    public async Task StopContainerAsync(string id, int timeout = 30, string? nodeId = null)
    {
        try
        {
            await _engine.StopContainerAsync(id, timeout, nodeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止容器 {Id} 失败", id);
            throw;
        }
    }

    public async Task RestartContainerAsync(string id, int timeout = 30, string? nodeId = null)
    {
        try
        {
            await _engine.RestartContainerAsync(id, timeout, nodeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重启容器 {Id} 失败", id);
            throw;
        }
    }

    public async Task RemoveContainerAsync(string id, bool force = false, bool removeVolumes = false, string? nodeId = null)
    {
        // 先清理域名映射（使用延迟解析避免循环依赖）
        try
        {
            var domainMappingService = _serviceProvider.GetService<DomainMappingService>();
            if (domainMappingService != null)
            {
                await domainMappingService.RemoveDomainMappingAsync(id);
                _logger.LogInformation("已清理容器 {Id} 的域名映射", id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "清理容器 {Id} 的域名映射失败，继续删除容器", id);
        }

        // 删除容器
        await _engine.RemoveContainerAsync(id, force, nodeId);
    }

    public async Task<ContainerLogs> GetContainerLogsAsync(string id, DateTime? since = null, DateTime? until = null, int tail = 100, bool follow = false, string? nodeId = null)
    {
        try
        {
            return await _engine.GetContainerLogsAsync(id, since, until, tail, follow, nodeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取容器 {Id} 日志失败", id);
            throw;
        }
    }

    public async Task<ContainerStats> GetContainerStatsAsync(string id, string? nodeId = null)
    {
        try
        {
            return await _engine.GetContainerStatsAsync(id, nodeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取容器 {Id} 统计信息失败", id);
            throw;
        }
    }

    public async Task<ExecResult> ExecuteCommandAsync(string id, ExecCommandRequest command, string? nodeId = null)
    {
        try
        {
            return await _engine.ExecuteCommandAsync(id, command, nodeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "在容器 {Id} 中执行命令失败", id);
            throw;
        }
    }

    public async Task UpdateContainerResourcesAsync(string id, UpdateContainerResourcesRequest request)
    {
        try
        {
            await _engine.UpdateContainerResourcesAsync(id, request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新容器 {Id} 资源配置失败", id);
            throw;
        }
    }

    public async Task<ResourceLimits?> GetContainerResourcesAsync(string id)
    {
        try
        {
            return await _engine.GetContainerResourcesAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取容器 {Id} 资源配置失败", id);
            return null;
        }
    }

    public async Task<HealthCheckStatus?> GetContainerHealthStatusAsync(string id)
    {
        try
        {
            return await _engine.GetContainerHealthStatusAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取容器 {Id} 健康检查状态失败", id);
            return null;
        }
    }

    public async Task<IEnumerable<HealthCheckLog>> GetContainerHealthLogsAsync(string id, DateTime? since = null, DateTime? until = null, int limit = 100)
    {
        try
        {
            return await _engine.GetContainerHealthLogsAsync(id, since, until, limit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取容器 {Id} 健康检查日志失败", id);
            throw;
        }
    }

    public async Task<HealthCheckStats?> GetContainerHealthStatsAsync(string id)
    {
        try
        {
            return await _engine.GetContainerHealthStatsAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取容器 {Id} 健康检查统计失败", id);
            return null;
        }
    }

    public async Task UpdateContainerHealthCheckAsync(string id, HealthCheckConfig config)
    {
        try
        {
            await _engine.UpdateContainerHealthCheckAsync(id, config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新容器 {Id} 健康检查配置失败", id);
            throw;
        }
    }

    public async Task RemoveContainerHealthCheckAsync(string id)
    {
        try
        {
            await _engine.RemoveContainerHealthCheckAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "移除容器 {Id} 健康检查配置失败", id);
            throw;
        }
    }

    public async Task<ContainerEngineHealthStatus> GetEngineHealthStatusAsync()
    {
        try
        {
            var isAvailable = await _engine.IsAvailableAsync();
            var engineName = _engine.EngineName;
            return new ContainerEngineHealthStatus
            {
                IsHealthy = isAvailable,
                TotalEngines = 1,
                AvailableEngines = isAvailable ? 1 : 0,
                EngineStatuses = new Dictionary<string, bool> { [engineName] = isAvailable },
                DefaultEngine = engineName,
                DefaultEngineAvailable = isAvailable
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取容器引擎健康状态失败");
            return new ContainerEngineHealthStatus
            {
                IsHealthy = false,
                TotalEngines = 1,
                AvailableEngines = 0,
                EngineStatuses = new Dictionary<string, bool> { ["unknown"] = false },
                DefaultEngine = "unknown",
                DefaultEngineAvailable = false
            };
        }
    }

    public async Task PauseContainerAsync(string id)
    {
        _logger.LogInformation("暂停容器 {Id}", id);
        var client = await GetDockerClientAsync();
        await client.Containers.PauseContainerAsync(id);
    }

    public async Task UnpauseContainerAsync(string id)
    {
        _logger.LogInformation("恢复容器 {Id}", id);
        var client = await GetDockerClientAsync();
        await client.Containers.UnpauseContainerAsync(id);
    }

    public async Task RenameContainerAsync(string id, string newName)
    {
        _logger.LogInformation("重命名容器 {Id} 为 {Name}", id, newName);
        var client = await GetDockerClientAsync();
        await client.Containers.RenameContainerAsync(id, new Docker.DotNet.Models.ContainerRenameParameters
        {
            NewName = newName
        }, CancellationToken.None);
    }

    public async Task<IEnumerable<ContainerProcess>> GetContainerProcessesAsync(string id)
    {
        _logger.LogInformation("获取容器 {Id} 进程列表", id);
        var top = await GetContainerTopAsync(id, "aux");
        return top.Processes.Select(process => new ContainerProcess
        {
            User = GetProcessColumn(top.Titles, process, "UID", "USER"),
            Pid = GetProcessColumn(top.Titles, process, "PID"),
            CpuTime = GetProcessColumn(top.Titles, process, "TIME"),
            Command = process.Count > 0 ? process[^1] : string.Empty
        }).ToList();
    }

    public async Task<IEnumerable<FileSystemChange>> GetContainerChangesAsync(string id)
    {
        _logger.LogInformation("获取容器 {Id} 文件变更", id);
        
        var changes = await _engine.GetContainerChangesAsync(id);
        
        return changes.Select(c => new FileSystemChange
        {
            Path = c.Key,
            Kind = c.Value, // A=新增, C=修改, D=删除
            Timestamp = DateTime.UtcNow
        }).ToList();
    }

    public async Task<byte[]> ExportContainerAsync(string id)
    {
        _logger.LogInformation("导出容器 {Id}", id);
        var client = await GetDockerClientAsync();
        await using var stream = await client.Containers.ExportContainerAsync(id);
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }

    public async Task UpdateContainerAsync(string id, UpdateContainerRequest request)
    {
        _logger.LogInformation("更新容器 {Id}", id);
        await UpdateContainerResourcesAsync(id, new UpdateContainerResourcesRequest
        {
            RestartPolicy = ToRestartPolicyRequest(request.RestartPolicy),
            Memory = ParseNullableLong(request.Resources?.MemoryLimit),
            MemoryReservation = ParseNullableLong(request.Resources?.MemoryReservation),
            MemorySwap = ParseNullableLong(request.Resources?.MemorySwap),
            CpuShares = ParseNullableLong(request.Resources?.CpuShares),
            CpuQuota = ParseNullableLong(request.Resources?.CpuQuota),
            CpuPeriod = ParseNullableLong(request.Resources?.CpuPeriod),
            CpusetCpus = request.Resources?.CpusetCpus
        });
    }

    public async Task<ContainerResourceUsage> GetContainerResourceUsageAsync(string id)
    {
        _logger.LogInformation("获取容器 {Id} 资源使用情况", id);
        var stats = await GetContainerStatsAsync(id);
        return new ContainerResourceUsage
        {
            ContainerId = id,
            ContainerName = stats.ContainerName,
            CpuUsagePercent = stats.CpuStats.PercentCpu,
            MemoryUsage = stats.MemoryStats.Usage,
            MemoryLimit = stats.MemoryStats.Limit,
            MemoryUsagePercent = stats.MemoryStats.PercentMemory,
            NetworkRxBytes = stats.Networks.Sum(n => n.RxBytes),
            NetworkTxBytes = stats.Networks.Sum(n => n.TxBytes),
            DiskReadBytes = stats.BlockIo.Sum(b => b.ReadBytes),
            DiskWriteBytes = stats.BlockIo.Sum(b => b.WriteBytes)
        };
    }

    public async Task<ContainerTop> GetContainerTopAsync(string id, string? psArgs = null)
    {
        _logger.LogInformation("获取容器 {Id} 进程信息", id);
        var client = await GetDockerClientAsync();
        var response = await client.Containers.ListProcessesAsync(id, new Docker.DotNet.Models.ContainerListProcessesParameters
        {
            PsArgs = psArgs ?? "aux"
        });
        return new ContainerTop
        {
            ContainerId = id,
            Titles = response.Titles?.ToList() ?? new List<string>(),
            Processes = response.Processes?.Select(p => p.ToList()).ToList() ?? new List<List<string>>()
        };
    }

    public async Task<ContainerInspect> InspectContainerAsync(string id)
    {
        _logger.LogInformation("检查容器 {Id}", id);
        var client = await GetDockerClientAsync();
        var response = await client.Containers.InspectContainerAsync(id);
        return new ContainerInspect
        {
            Id = response.ID,
            Created = response.Created,
            Path = response.Path ?? string.Empty,
            Args = response.Args?.ToList() ?? new List<string>(),
            State = response.State?.Status ?? string.Empty,
            Image = response.Image ?? string.Empty,
            Name = response.Name ?? string.Empty,
            Config = ToObjectDictionary(response.Config),
            HostConfig = ToObjectDictionary(response.HostConfig),
            NetworkSettings = ToObjectDictionary(response.NetworkSettings)
        };
    }

    public async Task<ContainerStatsSummary> GetContainerStatsSummaryAsync(string id)
    {
        _logger.LogInformation("获取容器 {Id} 统计摘要", id);
        var stats = await GetContainerStatsAsync(id);
        return new ContainerStatsSummary
        {
            ContainerId = id,
            ContainerName = stats.ContainerName,
            CpuUsage = stats.CpuStats.PercentCpu,
            MemoryUsage = stats.MemoryStats.Usage,
            MemoryLimit = stats.MemoryStats.Limit,
            NetworkRx = stats.Networks.Sum(n => n.RxBytes),
            NetworkTx = stats.Networks.Sum(n => n.TxBytes),
            BlockRead = stats.BlockIo.Sum(b => b.ReadBytes),
            BlockWrite = stats.BlockIo.Sum(b => b.WriteBytes),
            Timestamp = stats.Timestamp
        };
    }

    public async Task<IEnumerable<ContainerEvent>> GetContainerEventsAsync(DateTime? since = null, DateTime? until = null, string? filters = null)
    {
        _logger.LogInformation("获取容器事件");
        return new List<ContainerEvent>();
    }

    public async Task<ContainerStatsHistory> GetContainerStatsHistoryAsync(string id, DateTime? since = null, DateTime? until = null)
    {
        _logger.LogInformation("获取容器 {Id} 历史统计", id);
        var current = await GetContainerStatsAsync(id);
        return new ContainerStatsHistory
        {
            ContainerId = id,
            Stats = new List<ContainerStats> { current },
            StartTime = since ?? current.Timestamp,
            EndTime = until ?? current.Timestamp
        };
    }

    public async Task<bool> ContainerExistsAsync(string id)
    {
        var container = await GetContainerAsync(id);
        return container != null;
    }

    public async Task<IEnumerable<ContainerPortBinding>> GetContainerPortBindingsAsync(string id)
    {
        _logger.LogInformation("获取容器 {Id} 端口绑定", id);
        var client = await GetDockerClientAsync();
        var container = await client.Containers.InspectContainerAsync(id);
        var ports = container.NetworkSettings?.Ports ?? new Dictionary<string, IList<Docker.DotNet.Models.PortBinding>>();
        return ports.SelectMany(kv => (kv.Value ?? new List<Docker.DotNet.Models.PortBinding>()).Select(binding =>
        {
            var parts = kv.Key.Split('/');
            return new ContainerPortBinding
            {
                ContainerPort = parts[0],
                Protocol = parts.Length > 1 ? parts[1] : "tcp",
                HostIp = binding.HostIP ?? string.Empty,
                HostPort = binding.HostPort ?? string.Empty
            };
        })).ToList();
    }

    public async Task UpdateContainerRestartPolicyAsync(string id, RestartPolicy policy)
    {
        _logger.LogInformation("更新容器 {Id} 重启策略", id);
        await UpdateContainerResourcesAsync(id, new UpdateContainerResourcesRequest { RestartPolicy = ToRestartPolicyRequest(policy) });
    }

    public async Task UpdateContainerLabelsAsync(string id, Dictionary<string, string> labels)
    {
        _logger.LogInformation("更新容器 {Id} 标签", id);
        await CommitContainerAsync(id, new ContainerCommitRequest { ContainerId = id, Labels = labels });
    }

    public async Task UpdateContainerEnvironmentAsync(string id, Dictionary<string, string> environment)
    {
        _logger.LogInformation("更新容器 {Id} 环境变量", id);
        throw new NotSupportedException("Docker 不支持对已创建容器热更新环境变量，请重建容器应用新环境变量。");
    }

    public async Task<ContainerDiff> DiffContainerAsync(string id)
    {
        _logger.LogInformation("比较容器 {Id} 文件系统差异", id);
        var changes = await GetContainerChangesAsync(id);
        return new ContainerDiff
        {
            ContainerId = id,
            Additions = changes.Where(c => c.Kind == "A").Select(c => c.Path).ToList(),
            Deletions = changes.Where(c => c.Kind == "D").Select(c => c.Path).ToList(),
            Modifications = changes.Where(c => c.Kind == "C").Select(c => c.Path).ToList()
        };
    }

    public async Task<ContainerCommitResult> CommitContainerAsync(string id, ContainerCommitRequest request)
    {
        _logger.LogInformation("提交容器 {Id} 为镜像", id);
        var client = await GetDockerClientAsync();
        var response = await client.Images.CommitContainerChangesAsync(new Docker.DotNet.Models.CommitContainerChangesParameters
        {
            ContainerID = id,
            RepositoryName = request.Repository!,
            Tag = request.Tag!,
            Comment = request.Message!,
            Author = request.Author!,
            Pause = request.Pause,
            Labels = request.Labels!
        });
        return new ContainerCommitResult
        {
            ImageId = response.ID,
            Repository = request.Repository ?? string.Empty,
            Tag = request.Tag ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        };
    }

    public async Task<ContainerBatchOperationResult> BatchOperationAsync(BatchContainerOperationRequest request)
    {
        _logger.LogInformation("批量容器操作");
        var result = new ContainerBatchOperationResult { TotalCount = request.ContainerIds.Count };
        foreach (var containerId in request.ContainerIds)
        {
            try
            {
                switch (request.Operation.ToLowerInvariant())
                {
                    case "start":
                        await StartContainerAsync(containerId);
                        break;
                    case "stop":
                        await StopContainerAsync(containerId, request.Timeout ?? 30);
                        break;
                    case "restart":
                        await RestartContainerAsync(containerId, request.Timeout ?? 30);
                        break;
                    case "remove":
                    case "delete":
                        await RemoveContainerAsync(containerId, request.Force ?? false);
                        break;
                    case "pause":
                        await PauseContainerAsync(containerId);
                        break;
                    case "unpause":
                        await UnpauseContainerAsync(containerId);
                        break;
                    default:
                        throw new NotSupportedException($"不支持的批量操作: {request.Operation}");
                }

                result.SuccessCount++;
                result.SuccessfulCount++;
                result.Successful.Add(containerId);
                result.Results.Add(new ContainerBatchOperationItem { ContainerId = containerId, Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量操作失败: {Operation} {ContainerId}", request.Operation, containerId);
                result.FailureCount++;
                result.FailedCount++;
                result.Failed.Add(new BatchOperationError { ContainerId = containerId, Error = ex.Message, Message = ex.Message });
                result.Errors.Add($"{containerId}: {ex.Message}");
                result.Results.Add(new ContainerBatchOperationItem { ContainerId = containerId, Success = false, Error = ex.Message });
            }
        }

        result.Success = result.FailureCount == 0;
        return result;
    }

    private static string GetProcessColumn(IReadOnlyList<string> titles, IReadOnlyList<string> row, params string[] names)
    {
        var index = titles.Select((title, i) => new { title, i })
            .FirstOrDefault(x => names.Any(n => string.Equals(x.title, n, StringComparison.OrdinalIgnoreCase)))?.i ?? -1;
        return index >= 0 && index < row.Count ? row[index] : string.Empty;
    }

    private static Dictionary<string, object> ToObjectDictionary(object? value)
    {
        if (value == null) return new Dictionary<string, object>();
        return JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(value)) ?? new Dictionary<string, object>();
    }

    private static long? ParseNullableLong(string? value)
    {
        return long.TryParse(value, out var parsed) ? parsed : null;
    }

    private static RestartPolicyRequest? ToRestartPolicyRequest(RestartPolicy? policy)
    {
        return policy == null
            ? null
            : new RestartPolicyRequest
            {
                Name = policy.Name,
                MaximumRetryCount = policy.MaximumRetryCount ?? 0
            };
    }

    public async Task<ContainerFileListResponse> GetContainerFilesAsync(string id, string path, string? nodeId = null)
    {
        // 直接调用 engine 的实现，使用 stat 命令获取文件信息
        return await _engine.GetContainerFilesAsync(id, path, nodeId);
    }

    private ContainerFileInfo? ParseLsLine(string line, string basePath)
    {
        // 解析 ls -la --full-time 输出
        // 格式: drwxr-xr-x  2 root root 4096 2026-01-27 21:19:34 +0000 dirname
        // 格式: -rw-r--r--  1 root root 1234 2026-01-27 21:19:34 +0000 filename
        // 格式: lrwxrwxrwx  1 root root   12 2026-01-27 21:19:34 +0000 link -> target
        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        // --full-time 格式至少需要 9 列: 权限 链接 用户 组 大小 日期 时间 时区 文件名
        if (parts.Length < 9) return null;

        var permissions = parts[0];
        var isDir = permissions.StartsWith('d');
        var isLink = permissions.StartsWith('l');
        
        // 获取文件名（可能包含空格，从第8列开始）
        var name = string.Join(" ", parts.Skip(8));
        
        // 跳过 . 和 ..
        if (name == "." || name == "..") return null;

        // 解析大小
        long size = 0;
        if (long.TryParse(parts[4], out var parsedSize))
        {
            size = parsedSize;
        }

        // 解析 --full-time 格式的修改时间: parts[5]=日期(YYYY-MM-DD), parts[6]=时间(HH:MM:SS)
        DateTime? modified = ParseDateTimeFull(parts[5], parts[6]);

        return new ContainerFileInfo
        {
            Name = name,
            Path = basePath.TrimEnd('/') + "/" + name,
            Type = isDir ? "directory" : "file",
            Size = size,
            Permissions = permissions,
            Owner = parts[2],
            Group = parts[3],
            Modified = modified
        };
    }
    
    /// <summary>
    /// 解析 --full-time 格式的日期时间 (YYYY-MM-DD HH:MM:SS)
    /// </summary>
    private static DateTime? ParseDateTimeFull(string date, string time)
    {
        try
        {
            // 日期格式: YYYY-MM-DD
            var dateParts = date.Split('-');
            if (dateParts.Length != 3) return null;
            
            if (!int.TryParse(dateParts[0], out var year)) return null;
            if (!int.TryParse(dateParts[1], out var month)) return null;
            if (!int.TryParse(dateParts[2], out var day)) return null;
            
            // 时间格式: HH:MM:SS
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

    public async Task<byte[]> DownloadContainerFileAsync(string id, string path, string? nodeId = null)
    {
        _logger.LogInformation("下载容器 {Id} 文件: {Path}", id, path);
        return await _engine.DownloadContainerFileAsync(id, path, nodeId);
    }

    public async Task UploadContainerFileAsync(string id, string path, string fileName, byte[] content, string? nodeId = null)
    {
        _logger.LogInformation("上传文件到容器 {Id}: {Path}/{FileName}", id, path, fileName);
        await _engine.UploadContainerFileAsync(id, path, fileName, content, nodeId);
    }

    public async Task CreateContainerFolderAsync(string id, string path, string name, string? nodeId = null)
    {
        _logger.LogInformation("在容器 {Id} 创建文件夹: {Path}/{Name}", id, path, name);
        await _engine.CreateContainerFolderAsync(id, path, name, nodeId);
    }

    public async Task RenameContainerFileAsync(string id, string path, string oldName, string newName, string? nodeId = null)
    {
        _logger.LogInformation("重命名容器 {Id} 文件: {Path}/{OldName} -> {NewName}", id, path, oldName, newName);
        await _engine.RenameContainerFileAsync(id, path, oldName, newName, nodeId);
    }

    public async Task DeleteContainerFileAsync(string id, string path, bool recursive = false, string? nodeId = null)
    {
        _logger.LogInformation("删除容器 {Id} 文件: {Path}", id, path);
        await _engine.DeleteContainerFileAsync(id, path, recursive, nodeId);
    }

    public async Task<List<ContainerMountInfo>> GetContainerMountsAsync(string id, string? nodeId = null)
    {
        _logger.LogInformation("获取容器 {Id} 挂载点信息", id);
        
        var mounts = new List<ContainerMountInfo>();
        
        try
        {
            var container = await GetContainerAsync(id, nodeId);
            if (container?.Mounts != null)
            {
                foreach (var mount in container.Mounts)
                {
                    mounts.Add(new ContainerMountInfo
                    {
                        Destination = mount.Destination ?? "",
                        Source = mount.Source,
                        Type = mount.Type ?? "volume",
                        Name = mount.Name,
                        Rw = mount.Rw,
                        Driver = mount.Driver
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取容器挂载点信息失败");
        }

        return mounts;
    }

    public async Task<string> GetContainerFileContentAsync(string id, string path, string? nodeId = null)
    {
        _logger.LogInformation("获取容器 {Id} 文件内容: {Path}", id, path);
        return await _engine.GetContainerFileContentAsync(id, path, nodeId);
    }

    public async Task WriteContainerFileContentAsync(string id, string path, string content, string? nodeId = null)
    {
        _logger.LogInformation("写入容器 {Id} 文件内容: {Path}", id, path);
        await _engine.WriteContainerFileContentAsync(id, path, content, nodeId);
    }

    public async Task ChangeContainerFilePermissionsAsync(string id, string path, string permissions, string? nodeId = null)
    {
        _logger.LogInformation("修改容器 {Id} 文件权限: {Path} -> {Permissions}", id, path, permissions);
        await _engine.ChangeContainerFilePermissionsAsync(id, path, permissions, nodeId);
    }
}