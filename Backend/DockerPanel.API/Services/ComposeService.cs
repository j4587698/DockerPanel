using DockerPanel.API.Models;
using System.Text.Json;

namespace DockerPanel.API.Services;

/// <summary>
/// Docker Compose 服务实现 - 使用 Compose.NET 解析和 Docker.DotNet API 部署
/// </summary>
public class ComposeService : IComposeService
{
    private readonly ILogger<ComposeService> _logger;
    private readonly DataBaseService _databaseService;
    private readonly IComposeDeployService _composeDeployService;

    public ComposeService(
        ILogger<ComposeService> logger,
        DataBaseService databaseService,
        IComposeDeployService composeDeployService)
    {
        _logger = logger;
        _databaseService = databaseService;
        _composeDeployService = composeDeployService;

        _logger.LogInformation("ComposeService 已初始化，使用 Compose.NET 和 Docker API");
    }

    private static Dictionary<string, object> NormalizeMetadata(Dictionary<string, object>? metadata)
    {
        if (metadata == null || metadata.Count == 0)
        {
            return new Dictionary<string, object>();
        }

        return metadata.ToDictionary(kvp => kvp.Key, kvp => NormalizeMetadataValue(kvp.Value));
    }

    private static object NormalizeMetadataValue(object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
                JsonValueKind.Number when element.TryGetDouble(out var doubleValue) => doubleValue,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Array => element.EnumerateArray().Select(item => NormalizeMetadataValue(item)).ToList(),
                JsonValueKind.Object => element.EnumerateObject().ToDictionary(item => item.Name, item => NormalizeMetadataValue(item.Value)),
                _ => string.Empty
            };
        }

        return value;
    }

    /// <summary>
    /// 获取 Compose 文件列表
    /// </summary>
    public async Task<IEnumerable<ComposeFile>> GetComposeFilesAsync(string? nodeId = null, bool includeContent = false)
    {
        try
        {
            _logger.LogInformation("获取 Compose 文件列表, NodeId: {NodeId}, IncludeContent: {IncludeContent}", nodeId, includeContent);

            var query = _databaseService.ComposeFiles.Query();

            if (!string.IsNullOrWhiteSpace(nodeId))
            {
                query = query.Where(x => x.NodeName == nodeId);
            }

            var files = await Task.FromResult(query.ToList());

            if (!includeContent)
            {
                files.ForEach(x => x.Content = string.Empty);
            }

            // 同步真实状态
            await SyncProjectStatusesAsync(files);

            _logger.LogInformation("获取到 {Count} 个 Compose 文件", files.Count);
            return files;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 Compose 文件列表失败");
            throw;
        }
    }

    /// <summary>
    /// 根据ID获取 Compose 文件
    /// </summary>
    public async Task<ComposeFile?> GetComposeFileAsync(string id, bool includeContent = true)
    {
        try
        {
            await Task.CompletedTask;
            var composeFile = _databaseService.ComposeFiles.FindById(id);
            if (composeFile == null)
            {
                _logger.LogWarning("未找到ID为 {Id} 的 Compose 文件", id);
                return null;
            }

            if (!includeContent)
            {
                composeFile.Content = string.Empty;
            }

            // 同步真实状态
            await SyncSingleFileStatusAsync(composeFile);

            _logger.LogInformation("获取到 Compose 文件: {Id}", id);
            return composeFile;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 Compose 文件失败: {Id}", id);
            throw;
        }
    }

    /// <summary>
    /// 创建 Compose 文件
    /// </summary>
    public async Task<ComposeFile> CreateComposeFileAsync(CreateComposeFileRequest request)
    {
        try
        {
            // 使用 Compose.NET 解析内容
            var parseResult = await _composeDeployService.ParseAsync(request.Content, request.Name);
            var services = parseResult.Project?.Services?.Keys.ToList() ?? new List<string>();
            var networks = parseResult.Project?.Networks?.Keys.ToList() ?? new List<string>();
            var volumes = parseResult.Project?.Volumes?.Keys.ToList() ?? new List<string>();

            var composeFile = new ComposeFile
            {
                Id = $"compose-{Guid.NewGuid():N}",
                Name = request.Name,
                Description = request.Description ?? string.Empty,
                Content = request.Content,
                Path = request.Path ?? $"/opt/docker-compose/{request.Name}/docker-compose.yml",
                NodeId = request.NodeId,
                NodeName = string.IsNullOrWhiteSpace(request.NodeId) ? "localhost" : request.NodeId,
                Version = "3.8",
                Services = services,
                Networks = networks,
                Volumes = volumes,
                Metadata = NormalizeMetadata(request.Metadata),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = "user",
                UpdatedBy = "user",
                FileSize = System.Text.Encoding.UTF8.GetByteCount(request.Content),
                Hash = ComputeHash(request.Content),
                IsActive = false,
                Status = ComposeStatus.Created
            };

            _databaseService.ComposeFiles.Insert(composeFile);
            _logger.LogInformation("创建 Compose 文件成功: {Id}", composeFile.Id);

            return composeFile;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建 Compose 文件失败");
            throw;
        }
    }

    /// <summary>
    /// 更新 Compose 文件
    /// </summary>
    public async Task<ComposeFile> UpdateComposeFileAsync(string id, UpdateComposeFileRequest request)
    {
        try
        {
            var existingFile = await GetComposeFileAsync(id, includeContent: true);
            if (existingFile == null)
            {
                throw new KeyNotFoundException($"未找到ID为 {id} 的 Compose 文件");
            }

            if (!string.IsNullOrWhiteSpace(request.Name))
                existingFile.Name = request.Name;
            if (request.Description != null)
                existingFile.Description = request.Description;
            if (request.Content != null)
            {
                // 使用 Compose.NET 解析内容
                var parseResult = await _composeDeployService.ParseAsync(request.Content, existingFile.Name);
                existingFile.Content = request.Content;
                existingFile.Services = parseResult.Project?.Services?.Keys.ToList() ?? new List<string>();
                existingFile.Networks = parseResult.Project?.Networks?.Keys.ToList() ?? new List<string>();
                existingFile.Volumes = parseResult.Project?.Volumes?.Keys.ToList() ?? new List<string>();
                existingFile.FileSize = System.Text.Encoding.UTF8.GetByteCount(request.Content);
                existingFile.Hash = ComputeHash(request.Content);
            }
            if (request.Path != null)
                existingFile.Path = request.Path;
            if (request.Metadata != null)
                existingFile.Metadata = NormalizeMetadata(request.Metadata);

            existingFile.UpdatedAt = DateTime.UtcNow;
            existingFile.UpdatedBy = "user";

            _databaseService.ComposeFiles.Update(existingFile);
            _logger.LogInformation("更新 Compose 文件成功: {Id}", id);

            return existingFile;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新 Compose 文件失败: {Id}", id);
            throw;
        }
    }

    /// <summary>
    /// 删除 Compose 文件
    /// </summary>
    public async Task<bool> DeleteComposeFileAsync(string id, bool force = false)
    {
        try
        {
            var existingFile = await GetComposeFileAsync(id);
            if (existingFile == null)
            {
                return false;
            }

            // 如果正在运行，先停止
            if (existingFile.Status == ComposeStatus.Running)
            {
                if (!force)
                {
                    throw new InvalidOperationException("无法删除正在运行的 Compose 文件，请先停止项目或使用 force 参数");
                }
                
                await _composeDeployService.RemoveAsync(existingFile.Name, removeVolumes: false, removeImages: false);
            }

            var result = _databaseService.ComposeFiles.Delete(id);
            _logger.LogInformation("删除 Compose 文件 {Id}: {Result}", id, result);

            return result > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除 Compose 文件失败: {Id}", id);
            throw;
        }
    }

    /// <summary>
    /// 验证 Compose 文件
    /// </summary>
    public async Task<ComposeValidationResult> ValidateComposeFileAsync(string id, string? content = null)
    {
        try
        {
            var composeFile = await GetComposeFileAsync(id, includeContent: true);
            if (composeFile == null)
            {
                throw new KeyNotFoundException($"未找到ID为 {id} 的 Compose 文件");
            }

            var contentToValidate = content ?? composeFile.Content;
            
            // 使用 ComposeDeployService 进行验证
            var parseResult = await _composeDeployService.ParseAsync(contentToValidate);
            var result = new ComposeValidationResult
            {
                ComposeFileId = id,
                IsValid = parseResult.Success,
                ValidatedAt = DateTime.UtcNow,
                Errors = parseResult.Errors.Select(e => new ValidationError { Message = e }).ToList(),
                Warnings = parseResult.Warnings.Select(w => new ValidationWarning { Message = w }).ToList()
            };

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证 Compose 文件失败: {Id}", id);
            throw;
        }
    }

    /// <summary>
    /// 解析 Compose 文件内容
    /// </summary>
    public async Task<ComposeFile> ParseComposeContentAsync(string content)
    {
        try
        {
            // 使用 Compose.NET 解析完整的服务配置
            var parseResult = await _composeDeployService.ParseAsync(content);
            
            if (!parseResult.Success || parseResult.Project == null)
            {
                throw new ArgumentException($"解析失败: {string.Join(", ", parseResult.Errors)}");
            }

            var project = parseResult.Project;
            
            // 构建服务详情列表
            var serviceDetails = new List<ComposeServiceDetail>();
            foreach (var service in project.Services)
            {
                var detail = new ComposeServiceDetail
                {
                    Name = service.Key,
                    Image = service.Value.Image ?? "",
                    Ports = service.Value.Ports?.Select(p => 
                        !string.IsNullOrEmpty(p.Published) 
                            ? $"{p.Published}:{p.Target}" 
                            : p.Target.ToString()).ToList() ?? new List<string>(),
                    Environment = service.Value.Environment?.ToDictionary(e => e.Key, e => e.Value) ?? new Dictionary<string, string?>(),
                    Volumes = service.Value.Volumes?.Select(v => 
                        v.Source != null && v.Target != null 
                            ? $"{v.Source}:{v.Target}" 
                            : v.Target ?? "").Where(v => !string.IsNullOrEmpty(v)).ToList() ?? new List<string>(),
                    
                    // 构建配置
                    Build = service.Value.Build?.Context ?? service.Value.Build?.Dockerfile,
                    Context = service.Value.Build?.Context,
                    Dockerfile = service.Value.Build?.Dockerfile,
                    
                    // 运行配置
                    ContainerName = service.Value.ContainerName,
                    Command = service.Value.Command != null ? string.Join(" ", service.Value.Command) : null,
                    Entrypoint = service.Value.Entrypoint != null ? string.Join(" ", service.Value.Entrypoint) : null,
                    WorkingDir = service.Value.WorkingDir,
                    User = service.Value.User,
                    Hostname = service.Value.Hostname,
                    
                    // 重启策略
                    Restart = service.Value.Restart,
                    
                    // 依赖
                    DependsOn = service.Value.DependsOn?.Keys.ToList() ?? new List<string>(),
                    
                    // 网络配置
                    Networks = service.Value.Networks?.Keys.ToList() ?? new List<string>(),
                    NetworkMode = service.Value.NetworkMode,
                    
                    // 标签
                    Labels = service.Value.Labels != null 
                        ? service.Value.Labels.ToDictionary(k => k.Key, v => v.Value ?? "") 
                        : new Dictionary<string, string>(),
                    
                    // 健康检查
                    HealthCheck = service.Value.HealthCheck != null ? new ComposeHealthCheck
                    {
                        Test = service.Value.HealthCheck.Test,
                        Interval = service.Value.HealthCheck.Interval.HasValue 
                            ? (int)((TimeSpan)service.Value.HealthCheck.Interval.Value).TotalSeconds 
                            : null,
                        Timeout = service.Value.HealthCheck.Timeout.HasValue 
                            ? (int)((TimeSpan)service.Value.HealthCheck.Timeout.Value).TotalSeconds 
                            : null,
                        Retries = service.Value.HealthCheck.Retries.HasValue 
                            ? (int)service.Value.HealthCheck.Retries.Value 
                            : null,
                        StartPeriod = service.Value.HealthCheck.StartPeriod.HasValue 
                            ? (int)((TimeSpan)service.Value.HealthCheck.StartPeriod.Value).TotalSeconds 
                            : null,
                        Disable = service.Value.HealthCheck.Disable
                    } : null,
                    
                    // 资源限制 - UnitBytes 有隐式转换为 long
                    MemLimit = service.Value.MemLimit,
                    MemReservation = service.Value.MemReservation,
                    CpuCount = service.Value.CPUCount,
                    CpuShares = service.Value.CPUShares,
                    
                    // 其他
                    Privileged = service.Value.Privileged,
                    CapAdd = service.Value.CapAdd != null ? service.Value.CapAdd.ToList() : new List<string>(),
                    CapDrop = service.Value.CapDrop != null ? service.Value.CapDrop.ToList() : new List<string>(),
                    ExtraHosts = service.Value.ExtraHosts != null 
                        ? service.Value.ExtraHosts.Select(h => h.ToString()).ToList() 
                        : new List<string>(),
                    Pid = service.Value.Pid,
                    Ipc = service.Value.Ipc
                };
                serviceDetails.Add(detail);
            }

            var composeFile = new ComposeFile
            {
                Id = $"temp-{Guid.NewGuid():N}",
                Name = project.Name ?? "parsed-compose",
                Content = content,
                Services = project.Services.Keys.ToList(),
                Networks = project.Networks?.Keys.ToList() ?? new List<string>(),
                Volumes = project.Volumes?.Keys.ToList() ?? new List<string>(),
                ServiceDetails = serviceDetails,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                FileSize = System.Text.Encoding.UTF8.GetByteCount(content),
                Hash = ComputeHash(content)
            };

            return composeFile;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析 Compose 内容失败");
            throw;
        }
    }

    /// <summary>
    /// 部署 Compose 项目
    /// </summary>
    public async Task<ComposeOperationResult> DeployComposeAsync(DeployComposeRequest request)
    {
        var result = new ComposeOperationResult
        {
            ComposeFileId = request.ComposeFileId,
            Operation = "deploy",
            StartTime = DateTime.UtcNow
        };

        try
        {
            var composeFile = await GetComposeFileAsync(request.ComposeFileId, includeContent: true);
            if (composeFile == null)
            {
                result.Success = false;
                result.Message = $"未找到ID为 {request.ComposeFileId} 的 Compose 文件";
                result.EndTime = DateTime.UtcNow;
                return result;
            }

            // 调用 ComposeDeployService 部署
            var deployOptions = new ComposeDeployOptions
            {
                Detach = true,
                Build = !request.NoBuild,
                Pull = true,
                ForceRecreate = request.ForceRecreate,
                RemoveOrphans = request.RemoveOrphans,
                Services = request.Services
            };

            var deployResult = await _composeDeployService.DeployFromContentAsync(
                composeFile.Content,
                composeFile.Name,
                deployOptions);

            if (deployResult.Success)
            {
                composeFile.Status = ComposeStatus.Running;
                composeFile.IsActive = true;
                composeFile.UpdatedAt = DateTime.UtcNow;
                _databaseService.ComposeFiles.Update(composeFile);

                result.Success = true;
                result.Message = deployResult.Message;
                result.AffectedServices = deployResult.CreatedContainers.Select(c => c.ServiceName).ToList();
            }
            else
            {
                composeFile.Status = ComposeStatus.Error;
                composeFile.UpdatedAt = DateTime.UtcNow;
                _databaseService.ComposeFiles.Update(composeFile);

                result.Success = false;
                result.Message = string.Join("\n", deployResult.Errors);
                result.Errors.AddRange(deployResult.Errors);
            }

            result.EndTime = DateTime.UtcNow;
            _logger.LogInformation("部署 Compose 项目: {Id}, 成功: {Success}", request.ComposeFileId, result.Success);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "部署 Compose 项目失败: {Id}", request.ComposeFileId);
            result.Success = false;
            result.Message = ex.Message;
            result.Errors.Add(ex.Message);
            result.EndTime = DateTime.UtcNow;
            return result;
        }
    }

    /// <summary>
    /// 停止 Compose 项目
    /// </summary>
    public async Task<ComposeOperationResult> StopComposeAsync(ComposeOperationRequest request)
    {
        var result = new ComposeOperationResult
        {
            ComposeFileId = request.ComposeFileId,
            Operation = "stop",
            StartTime = DateTime.UtcNow
        };

        try
        {
            var composeFile = await GetComposeFileAsync(request.ComposeFileId);
            if (composeFile == null)
            {
                result.Success = false;
                result.Message = $"未找到ID为 {request.ComposeFileId} 的 Compose 文件";
                result.EndTime = DateTime.UtcNow;
                return result;
            }

            var stopResult = await _composeDeployService.StopAsync(
                composeFile.Name, 
                request.Services, 
                request.Timeout);

            if (stopResult.Success)
            {
                composeFile.Status = ComposeStatus.Stopped;
                composeFile.IsActive = false;
                composeFile.UpdatedAt = DateTime.UtcNow;
                _databaseService.ComposeFiles.Update(composeFile);

                result.Success = true;
                result.Message = stopResult.Message;
                result.AffectedServices = composeFile.Services;
            }
            else
            {
                result.Success = false;
                result.Message = stopResult.Message;
                result.Errors.Add(stopResult.Message);
            }

            result.EndTime = DateTime.UtcNow;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止 Compose 项目失败: {Id}", request.ComposeFileId);
            result.Success = false;
            result.Message = ex.Message;
            result.EndTime = DateTime.UtcNow;
            return result;
        }
    }

    /// <summary>
    /// 启动 Compose 项目
    /// </summary>
    public async Task<ComposeOperationResult> StartComposeAsync(ComposeOperationRequest request)
    {
        var result = new ComposeOperationResult
        {
            ComposeFileId = request.ComposeFileId,
            Operation = "start",
            StartTime = DateTime.UtcNow
        };

        try
        {
            var composeFile = await GetComposeFileAsync(request.ComposeFileId);
            if (composeFile == null)
            {
                result.Success = false;
                result.Message = $"未找到ID为 {request.ComposeFileId} 的 Compose 文件";
                result.EndTime = DateTime.UtcNow;
                return result;
            }

            var startResult = await _composeDeployService.StartAsync(
                composeFile.Name, 
                request.Services);

            if (startResult.Success)
            {
                composeFile.Status = ComposeStatus.Running;
                composeFile.IsActive = true;
                composeFile.UpdatedAt = DateTime.UtcNow;
                _databaseService.ComposeFiles.Update(composeFile);

                result.Success = true;
                result.Message = startResult.Message;
                result.AffectedServices = composeFile.Services;
            }
            else
            {
                result.Success = false;
                result.Message = startResult.Message;
                result.Errors.Add(startResult.Message);
            }

            result.EndTime = DateTime.UtcNow;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动 Compose 项目失败: {Id}", request.ComposeFileId);
            result.Success = false;
            result.Message = ex.Message;
            result.EndTime = DateTime.UtcNow;
            return result;
        }
    }

    /// <summary>
    /// 重启 Compose 项目
    /// </summary>
    public async Task<ComposeOperationResult> RestartComposeAsync(ComposeOperationRequest request)
    {
        var result = new ComposeOperationResult
        {
            ComposeFileId = request.ComposeFileId,
            Operation = "restart",
            StartTime = DateTime.UtcNow
        };

        try
        {
            var composeFile = await GetComposeFileAsync(request.ComposeFileId);
            if (composeFile == null)
            {
                result.Success = false;
                result.Message = $"未找到ID为 {request.ComposeFileId} 的 Compose 文件";
                result.EndTime = DateTime.UtcNow;
                return result;
            }

            var restartResult = await _composeDeployService.RestartAsync(
                composeFile.Name, 
                request.Services, 
                request.Timeout);

            if (restartResult.Success)
            {
                composeFile.Status = ComposeStatus.Running;
                composeFile.IsActive = true;
                composeFile.UpdatedAt = DateTime.UtcNow;
                _databaseService.ComposeFiles.Update(composeFile);

                result.Success = true;
                result.Message = restartResult.Message;
                result.AffectedServices = composeFile.Services;
            }
            else
            {
                result.Success = false;
                result.Message = restartResult.Message;
                result.Errors.Add(restartResult.Message);
            }

            result.EndTime = DateTime.UtcNow;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重启 Compose 项目失败: {Id}", request.ComposeFileId);
            result.Success = false;
            result.Message = ex.Message;
            result.EndTime = DateTime.UtcNow;
            return result;
        }
    }

    /// <summary>
    /// 删除 Compose 项目
    /// </summary>
    public async Task<ComposeOperationResult> RemoveComposeAsync(ComposeOperationRequest request)
    {
        var result = new ComposeOperationResult
        {
            ComposeFileId = request.ComposeFileId,
            Operation = "remove",
            StartTime = DateTime.UtcNow
        };

        try
        {
            var composeFile = await GetComposeFileAsync(request.ComposeFileId);
            if (composeFile == null)
            {
                result.Success = false;
                result.Message = $"未找到ID为 {request.ComposeFileId} 的 Compose 文件";
                result.EndTime = DateTime.UtcNow;
                return result;
            }

            var downResult = await _composeDeployService.RemoveAsync(
                composeFile.Name, 
                removeVolumes: request.Force, 
                removeImages: false);

            if (downResult.Success)
            {
                composeFile.Status = ComposeStatus.Created;
                composeFile.IsActive = false;
                composeFile.UpdatedAt = DateTime.UtcNow;
                _databaseService.ComposeFiles.Update(composeFile);

                result.Success = true;
                result.Message = downResult.Message;
                result.AffectedServices = composeFile.Services;
            }
            else
            {
                result.Success = false;
                result.Message = downResult.Message;
                result.Errors.Add(downResult.Message);
            }

            result.EndTime = DateTime.UtcNow;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除 Compose 项目失败: {Id}", request.ComposeFileId);
            result.Success = false;
            result.Message = ex.Message;
            result.EndTime = DateTime.UtcNow;
            return result;
        }
    }

    /// <summary>
    /// 获取 Compose 项目状态
    /// </summary>
    public async Task<ComposeProject?> GetComposeProjectStatusAsync(string composeFileId, string? nodeId = null)
    {
        try
        {
            var composeFile = await GetComposeFileAsync(composeFileId);
            if (composeFile == null)
            {
                return null;
            }

            // 获取真实的容器状态
            var projectStatus = await _composeDeployService.GetStatusAsync(composeFile.Name);
            
            var services = new List<ComposeServiceInfo>();
            foreach (var status in projectStatus.Services)
            {
                services.Add(new ComposeServiceInfo
                {
                    Name = status.ServiceName,
                    Image = status.Image ?? "",
                    Status = status.Status,
                    ContainerId = status.ContainerId ?? "",
                    IsRunning = status.IsRunning,
                    Health = status.Health == "healthy" ? 1 : (status.Health == "unhealthy" ? 2 : 0),
                    HealthStatus = status.Health,
                    Ports = status.Ports
                });
            }

            // 补充未运行的服务的定义
            foreach (var serviceName in composeFile.Services)
            {
                if (!services.Any(s => s.Name == serviceName))
                {
                    services.Add(new ComposeServiceInfo
                    {
                        Name = serviceName,
                        Status = "stopped",
                        IsRunning = false
                    });
                }
            }

            var project = new ComposeProject
            {
                Id = composeFile.Id,
                Name = composeFile.Name,
                FilePath = composeFile.Path,
                NodeId = composeFile.NodeId,
                NodeName = composeFile.NodeName,
                Status = DetermineProjectStatus(services),
                CreatedAt = composeFile.CreatedAt,
                UpdatedAt = composeFile.UpdatedAt,
                Services = services,
                Networks = composeFile.Networks.Select(n => new ComposeNetworkInfo
                {
                    Name = n,
                    Driver = "bridge",
                    External = false
                }).ToList(),
                Volumes = composeFile.Volumes.Select(v => new ComposeVolumeInfo
                {
                    Name = v,
                    Driver = "local",
                    External = false
                }).ToList()
            };

            return project;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 Compose 项目状态失败: {Id}", composeFileId);
            throw;
        }
    }

    /// <summary>
    /// 获取 Compose 项目列表
    /// </summary>
    public async Task<IEnumerable<ComposeProject>> GetComposeProjectsAsync(string? nodeId = null)
    {
        try
        {
            var composeFiles = await GetComposeFilesAsync(nodeId);
            var projects = new List<ComposeProject>();

            foreach (var file in composeFiles)
            {
                var project = await GetComposeProjectStatusAsync(file.Id, nodeId);
                if (project != null)
                {
                    projects.Add(project);
                }
            }

            return projects;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 Compose 项目列表失败");
            throw;
        }
    }

    /// <summary>
    /// 获取 Compose 日志
    /// </summary>
    public async Task<ComposeLogsResponse> GetComposeLogsAsync(ComposeLogsRequest request)
    {
        try
        {
            var composeFile = await GetComposeFileAsync(request.ComposeFileId);
            if (composeFile == null)
            {
                return new ComposeLogsResponse
                {
                    ComposeFileId = request.ComposeFileId
                };
            }

            DateTime? since = null;
            if (!string.IsNullOrEmpty(request.Since))
            {
                DateTime.TryParse(request.Since, out var sinceDate);
                since = sinceDate;
            }

            var logsContent = await _composeDeployService.GetLogsAsync(
                composeFile.Name,
                request.Services,
                request.Tail ?? 100,
                since);

            var logs = new List<ComposeLogEntry>();
            var lines = logsContent.Split('\n');
            
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                // 解析日志格式 "=== service-name (container-name) ==="
                if (line.StartsWith("==="))
                {
                    continue;
                }

                logs.Add(new ComposeLogEntry
                {
                    Message = line,
                    Timestamp = DateTime.UtcNow,
                    Stream = "stdout"
                });
            }

            return new ComposeLogsResponse
            {
                ComposeFileId = request.ComposeFileId,
                Logs = logs,
                Since = !string.IsNullOrEmpty(request.Since) ? DateTime.Parse(request.Since) : null,
                Until = !string.IsNullOrEmpty(request.Until) ? DateTime.Parse(request.Until) : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 Compose 日志失败: {Id}", request.ComposeFileId);
            throw;
        }
    }

    /// <summary>
    /// 获取 Compose 项目统计信息
    /// </summary>
    public async Task<ComposeProjectStats?> GetComposeProjectStatsAsync(string composeFileId, string? nodeId = null)
    {
        try
        {
            var project = await GetComposeProjectStatusAsync(composeFileId, nodeId);
            if (project == null)
            {
                return null;
            }

            var stats = new ComposeProjectStats
            {
                ComposeFileId = composeFileId,
                ProjectName = project.Name,
                TotalServices = project.Services.Count,
                RunningServices = project.Services.Count(s => s.IsRunning),
                StoppedServices = project.Services.Count(s => !s.IsRunning),
                UnhealthyServices = project.Services.Count(s => s.Health == 2),
                TotalNetworks = project.Networks.Count,
                TotalVolumes = project.Volumes.Count,
                CreatedAt = project.CreatedAt,
                LastDeployed = project.UpdatedAt,
                Status = project.Status.ToString()
            };

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 Compose 项目统计信息失败: {Id}", composeFileId);
            throw;
        }
    }

    /// <summary>
    /// 导出 Compose 文件
    /// </summary>
    public async Task<string> ExportComposeFileAsync(string id, string format = "yaml")
    {
        try
        {
            var composeFile = await GetComposeFileAsync(id, includeContent: true);
            if (composeFile == null)
            {
                throw new KeyNotFoundException($"未找到ID为 {id} 的 Compose 文件");
            }

            return composeFile.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出 Compose 文件失败: {Id}", id);
            throw;
        }
    }

    /// <summary>
    /// 导入 Compose 文件
    /// </summary>
    public async Task<ComposeFile> ImportComposeFileAsync(string content, string name, string? description = null, string? nodeId = null)
    {
        try
        {
            var request = new CreateComposeFileRequest
            {
                Name = name,
                Description = description,
                Content = content,
                NodeId = nodeId
            };

            return await CreateComposeFileAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入 Compose 文件失败: {Name}", name);
            throw;
        }
    }

    /// <summary>
    /// 克隆 Compose 文件
    /// </summary>
    public async Task<ComposeFile> CloneComposeFileAsync(string id, string newName, string? description = null)
    {
        try
        {
            var sourceCompose = await GetComposeFileAsync(id, includeContent: true);
            if (sourceCompose == null)
            {
                throw new KeyNotFoundException($"未找到ID为 {id} 的 Compose 文件");
            }

            var clonedCompose = new ComposeFile
            {
                Id = $"compose-{Guid.NewGuid():N}",
                Name = newName,
                Description = description ?? $"克隆自 {sourceCompose.Name}",
                Content = sourceCompose.Content,
                Path = sourceCompose.Path,
                NodeId = sourceCompose.NodeId,
                NodeName = sourceCompose.NodeName,
                Version = sourceCompose.Version,
                Services = new List<string>(sourceCompose.Services),
                Networks = new List<string>(sourceCompose.Networks),
                Volumes = new List<string>(sourceCompose.Volumes),
                Metadata = new Dictionary<string, object>(sourceCompose.Metadata),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = "system",
                UpdatedBy = "system",
                FileSize = sourceCompose.FileSize,
                Hash = ComputeHash(sourceCompose.Content),
                IsActive = false,
                Status = ComposeStatus.Created
            };

            _databaseService.ComposeFiles.Insert(clonedCompose);
            _logger.LogInformation("克隆 Compose 文件成功: {SourceId} -> {NewId}", id, clonedCompose.Id);

            return clonedCompose;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "克隆 Compose 文件失败: {Id}", id);
            throw;
        }
    }

    /// <summary>
    /// 获取 Compose 模板列表
    /// </summary>
    public async Task<IEnumerable<ComposeTemplate>> GetComposeTemplatesAsync(string? category = null, List<string>? tags = null)
    {
        try
        {
            // 返回空模板列表，实际应用中可以从数据库或文件系统加载
            return new List<ComposeTemplate>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 Compose 模板列表失败");
            throw;
        }
    }

    /// <summary>
    /// 根据模板创建 Compose 文件
    /// </summary>
    public async Task<ComposeFile> CreateFromTemplateAsync(string templateId, Dictionary<string, object> variables, string name, string? description = null)
    {
        try
        {
            // 简单实现，实际应用中需要根据模板ID加载模板内容
            var templateContent = @"version: '3.8'
services:
  web:
    image: nginx:alpine
    ports:
      - '80:80'
  db:
    image: postgres:alpine
    environment:
      POSTGRES_DB: myapp
      POSTGRES_USER: admin
      POSTGRES_PASSWORD: password";

            return await ImportComposeFileAsync(templateContent, name, description);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "根据模板创建 Compose 文件失败: {TemplateId}", templateId);
            throw;
        }
    }

    /// <summary>
    /// 批量操作 Compose 文件
    /// </summary>
    public async Task<Dictionary<string, ComposeOperationResult>> BatchOperationAsync(List<string> fileIds, string operation, Dictionary<string, object>? parameters = null)
    {
        try
        {
            var results = new Dictionary<string, ComposeOperationResult>();

            foreach (var fileId in fileIds)
            {
                try
                {
                    ComposeOperationResult result;
                    switch (operation.ToLower())
                    {
                        case "start":
                            result = await StartComposeAsync(new ComposeOperationRequest { ComposeFileId = fileId });
                            break;
                        case "stop":
                            result = await StopComposeAsync(new ComposeOperationRequest { ComposeFileId = fileId });
                            break;
                        case "restart":
                            result = await RestartComposeAsync(new ComposeOperationRequest { ComposeFileId = fileId });
                            break;
                        default:
                            result = new ComposeOperationResult
                            {
                                ComposeFileId = fileId,
                                Operation = operation,
                                Success = false,
                                Message = $"不支持的操作: {operation}",
                                StartTime = DateTime.UtcNow,
                                EndTime = DateTime.UtcNow
                            };
                            break;
                    }
                    results[fileId] = result;
                }
                catch (Exception ex)
                {
                    results[fileId] = new ComposeOperationResult
                    {
                        ComposeFileId = fileId,
                        Operation = operation,
                        Success = false,
                        Message = ex.Message,
                        StartTime = DateTime.UtcNow,
                        EndTime = DateTime.UtcNow
                    };
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量操作 Compose 文件失败: {Operation}", operation);
            throw;
        }
    }

    /// <summary>
    /// 获取 Compose 文件历史版本
    /// </summary>
    public async Task<IEnumerable<ComposeFileVersion>> GetComposeFileHistoryAsync(string id)
    {
        try
        {
            // 简单实现，返回空历史列表
            return new List<ComposeFileVersion>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 Compose 文件历史版本失败: {Id}", id);
            throw;
        }
    }

    /// <summary>
    /// 恢复 Compose 文件到指定版本
    /// </summary>
    public async Task<ComposeFile> RestoreComposeFileVersionAsync(string id, string versionId)
    {
        try
        {
            var composeFile = await GetComposeFileAsync(id);
            if (composeFile == null)
            {
                throw new KeyNotFoundException($"未找到ID为 {id} 的 Compose 文件");
            }

            // 简单实现，直接返回当前文件
            return composeFile;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "恢复 Compose 文件版本失败: {Id}", id);
            throw;
        }
    }

    /// <summary>
    /// 同步 Compose 文件到节点
    /// </summary>
    public async Task<ComposeOperationResult> SyncComposeFileToNodeAsync(string id, string nodeId)
    {
        var result = new ComposeOperationResult
        {
            ComposeFileId = id,
            Operation = "sync",
            StartTime = DateTime.UtcNow
        };

        try
        {
            var composeFile = await GetComposeFileAsync(id);
            if (composeFile == null)
            {
                result.Success = false;
                result.Message = $"未找到ID为 {id} 的 Compose 文件";
                result.EndTime = DateTime.UtcNow;
                return result;
            }

            // 更新节点信息
            composeFile.NodeId = nodeId;
            composeFile.NodeName = nodeId;
            composeFile.UpdatedAt = DateTime.UtcNow;
            _databaseService.ComposeFiles.Update(composeFile);

            result.Success = true;
            result.Message = $"已同步到节点 {nodeId}";
            result.EndTime = DateTime.UtcNow;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步 Compose 文件到节点失败: {Id}", id);
            result.Success = false;
            result.Message = ex.Message;
            result.EndTime = DateTime.UtcNow;
            return result;
        }
    }

    /// <summary>
    /// 检查 Compose 文件依赖
    /// </summary>
    public async Task<ComposeDependencyCheck> CheckComposeDependenciesAsync(string id)
    {
        try
        {
            var composeFile = await GetComposeFileAsync(id);
            if (composeFile == null)
            {
                throw new KeyNotFoundException($"未找到ID为 {id} 的 Compose 文件");
            }

            var check = new ComposeDependencyCheck
            {
                ComposeFileId = id,
                IsHealthy = true,
                CheckedAt = DateTime.UtcNow
            };

            // 简单实现，返回健康状态
            return check;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查 Compose 文件依赖失败: {Id}", id);
            throw;
        }
    }

    #region 私有方法

    /// <summary>
    /// 同步所有项目状态
    /// </summary>
    private async Task SyncProjectStatusesAsync(List<ComposeFile> files)
    {
        try
        {
            var projects = await _composeDeployService.ListProjectsAsync();
            var projectDict = projects.ToDictionary(p => p.Name, p => p);

            foreach (var file in files)
            {
                if (projectDict.TryGetValue(file.Name, out var project))
                {
                    file.IsActive = project.RunningCount > 0;
                    file.Status = DetermineStatusFromSummary(project);
                }
                else
                {
                    file.IsActive = false;
                    file.Status = ComposeStatus.Created;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "同步项目状态失败");
        }
    }

    /// <summary>
    /// 同步单个文件状态
    /// </summary>
    private async Task SyncSingleFileStatusAsync(ComposeFile file)
    {
        try
        {
            var projectStatus = await _composeDeployService.GetStatusAsync(file.Name);
            
            if (projectStatus.Services.Count == 0)
            {
                file.IsActive = false;
                file.Status = ComposeStatus.Created;
                return;
            }

            var runningCount = projectStatus.Services.Count(s => s.IsRunning);
            file.IsActive = runningCount > 0;
            
            if (runningCount == projectStatus.Services.Count)
                file.Status = ComposeStatus.Running;
            else if (runningCount == 0)
                file.Status = ComposeStatus.Stopped;
            else
                file.Status = ComposeStatus.PartiallyRunning;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "同步文件状态失败: {Id}", file.Id);
        }
    }

    /// <summary>
    /// 根据摘要确定状态
    /// </summary>
    private ComposeStatus DetermineStatusFromSummary(ComposeProjectSummary summary)
    {
        if (summary.RunningCount == summary.ContainerCount && summary.ContainerCount > 0)
            return ComposeStatus.Running;
        if (summary.RunningCount == 0)
            return ComposeStatus.Stopped;
        return ComposeStatus.PartiallyRunning;
    }

    /// <summary>
    /// 根据服务列表确定项目状态
    /// </summary>
    private ComposeStatus DetermineProjectStatus(List<ComposeServiceInfo> services)
    {
        if (services.Count == 0)
            return ComposeStatus.Created;

        var runningCount = services.Count(s => s.IsRunning);
        
        if (runningCount == services.Count)
            return ComposeStatus.Running;
        if (runningCount == 0)
                        return ComposeStatus.Stopped;
                    return ComposeStatus.PartiallyRunning;
                }
            
                /// <summary>
                /// 计算哈希值
                /// </summary>
                private string ComputeHash(string content)
                {
                    using var sha = System.Security.Cryptography.SHA256.Create();
                    var bytes = System.Text.Encoding.UTF8.GetBytes(content);
                    var hash = sha.ComputeHash(bytes);
                    return Convert.ToHexString(hash).ToLower();
                }
            
                #endregion
            }
            
