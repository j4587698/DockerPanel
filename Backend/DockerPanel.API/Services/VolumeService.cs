using DockerPanel.API.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Docker.DotNet;
using Docker.DotNet.Models;
using System.Text;
using DockerPanel.API.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.IO.Compression;
using System.Formats.Tar;

namespace DockerPanel.API.Services;

/// <summary>
/// 简单存储卷服务实现 - 使用 Docker.DotNet 库
/// </summary>
public class VolumeService : IVolumeService
{
    private readonly ILogger<VolumeService> _logger;
    private readonly IContainerEngine _engine;
    private readonly IHubContext<DockerPanelHub> _hubContext;
    private DockerClient? _dockerClient;

    public VolumeService(ILogger<VolumeService> logger, IContainerEngine engine, IHubContext<DockerPanelHub> hubContext)
    {
        _logger = logger;
        _engine = engine;
        _hubContext = hubContext;
    }

    private async Task<DockerClient> GetDockerClientAsync(string? nodeId = null)
    {
        if (_engine is DockerEngine dockerEngine)
        {
            return await dockerEngine.GetClientAsync(nodeId);
        }

        return GetLocalDockerClient();
    }

    private DockerClient GetLocalDockerClient()
    {
        if (_dockerClient == null)
        {
            // 根据操作系统选择正确的 Docker endpoint
            var endpoint = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new Uri("npipe://./pipe/docker_engine")
                : new Uri("unix:///var/run/docker.sock");
            _dockerClient = new DockerClientBuilder().WithEndpoint(endpoint).Build();
        }
        return _dockerClient;
    }

    public async Task<IEnumerable<Models.VolumeInfo>> GetVolumesAsync(string? nodeId = null)
    {
        _logger.LogInformation("获取存储卷列表");
        return await _engine.ListVolumesAsync(nodeId);
    }

    public async Task<Models.VolumeInfo?> GetVolumeAsync(string name)
    {
        return await _engine.GetVolumeAsync(name);
    }

    public async Task<VolumeDetailInfo?> GetVolumeByIdAsync(string volumeId, string? nodeId = null)
    {
        var volume = await _engine.GetVolumeAsync(volumeId, nodeId);
        if (volume == null) return null;

        // 获取所有容器（包括已停止的），查找使用此卷的容器
        var containers = await _engine.ListContainersAsync(true, nodeId);
        var mounts = new List<VolumeMount>();
        
        foreach (var container in containers)
        {
            if (container.Mounts != null)
            {
                foreach (var mount in container.Mounts)
                {
                    // 匹配卷名或挂载源
                    if (mount.Name == volumeId || mount.Source == volume.Mountpoint)
                    {
                        mounts.Add(new VolumeMount
                        {
                            ContainerId = container.Id,
                            ContainerName = container.Name?.TrimStart('/') ?? string.Empty,
                            Source = mount.Source ?? string.Empty,
                            Destination = mount.Destination ?? string.Empty,
                            Mode = mount.Mode ?? "rw",
                            Driver = mount.Driver
                        });
                    }
                }
            }
        }

        return new VolumeDetailInfo
        {
            Name = volume.Name,
            Driver = volume.Driver ?? string.Empty,
            Mountpoint = volume.Mountpoint ?? string.Empty,
            CreatedAt = volume.CreatedAt,
            Labels = volume.Labels ?? new Dictionary<string, string>(),
            Options = volume.Options ?? new Dictionary<string, string>(),
            Scope = volume.Scope ?? string.Empty,
            Usage = new VolumeUsage { Size = volume.Size, RefCount = mounts.Count },
            Mounts = mounts,
            Status = new VolumeStatus { State = mounts.Count > 0 ? "in-use" : "available", IsHealthy = true },
            NodeId = volume.NodeId
        };
    }

    public async Task<string> CreateVolumeAsync(CreateVolumeRequest request)
    {
        _logger.LogInformation("创建存储卷: {Name}", request.Name);
        return await _engine.CreateVolumeAsync(request, request.NodeId);
    }

    public async Task RemoveVolumeAsync(string name, bool force = false)
    {
        _logger.LogInformation("删除存储卷: {VolumeName}", name);
        await _engine.RemoveVolumeAsync(name, force);
    }

    public async Task<bool> DeleteVolumeAsync(string volumeId, bool force = false, string? nodeId = null)
    {
        try
        {
            await _engine.RemoveVolumeAsync(volumeId, force, nodeId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<Models.VolumeInfo> UpdateVolumeAsync(string volumeId, UpdateVolumeRequest request, string? nodeId = null)
    {
        // Docker 存储卷不支持更新，返回现有信息
        var volume = await GetVolumeAsync(volumeId);
        if (volume == null)
        {
            throw new ArgumentException($"存储卷不存在: {volumeId}");
        }

        if (request.Labels != null)
        {
            volume.Labels = request.Labels;
        }
        if (request.Options != null)
        {
            volume.Options = request.Options;
        }

        return volume;
    }

    public async Task<int> PruneVolumesAsync()
    {
        var result = await PruneVolumesAsync(new VolumePruneOptions());
        return result.VolumesDeleted;
    }

    public async Task<VolumePruneResult> PruneVolumesAsync(VolumePruneOptions options, string? nodeId = null)
    {
        try
        {
            _logger.LogInformation("清理未使用的存储卷, All={All}, Filters={Filters}", options.All, options.Filters);

            var client = await GetDockerClientAsync(nodeId);
            var filters = new Dictionary<string, IDictionary<string, bool>>();

            // 设置 all 过滤器：当 All=true 时，清理所有未使用的卷（不仅仅是匿名卷）
            // Docker 默认只清理匿名卷（dangling volumes），设置 all=true 才会清理所有未使用的卷
            if (options.All)
            {
                filters["all"] = new Dictionary<string, bool> { { "true", true } };
                _logger.LogInformation("设置 all 过滤器为 true，将清理所有未使用的卷");
            }
            else
            {
                _logger.LogInformation("未设置 all 过滤器，只清理匿名卷");
            }

            if (!string.IsNullOrEmpty(options.LabelFilter))
            {
                filters["label"] = new Dictionary<string, bool> { { options.LabelFilter, true } };
            }

            _logger.LogInformation("调用 Docker API PruneAsync, filters={FiltersCount}", filters.Count);
            
            var pruneResponse = await client.Volumes.PruneAsync(new VolumesPruneParameters
            {
                Filters = filters
            });

            _logger.LogInformation("Docker API 响应: VolumesDeleted={Count}, SpaceReclaimed={Space}", 
                pruneResponse.VolumesDeleted?.Count ?? 0, 
                pruneResponse.SpaceReclaimed);

            if (pruneResponse.VolumesDeleted != null && pruneResponse.VolumesDeleted.Count > 0)
            {
                _logger.LogInformation("已删除的卷: {Volumes}", string.Join(", ", pruneResponse.VolumesDeleted));
            }

            return new VolumePruneResult
            {
                VolumesDeleted = pruneResponse.VolumesDeleted?.Count ?? 0,
                SpaceReclaimed = (long)(pruneResponse.SpaceReclaimed),
                DeletedVolumeNames = pruneResponse.VolumesDeleted?.ToList() ?? new List<string>(),
                Errors = new List<string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理存储卷失败");
            return new VolumePruneResult
            {
                VolumesDeleted = 0,
                SpaceReclaimed = 0,
                DeletedVolumeNames = new List<string>(),
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<VolumeStatistics> GetVolumeStatisticsAsync(string? nodeId = null)
    {
        try
        {
            var volumes = await GetVolumesAsync(nodeId);

            return new VolumeStatistics
            {
                VolumeName = "statistics",
                NodeId = nodeId ?? "local",
                Timestamp = DateTime.UtcNow,
                SizeBytes = 0,
                TotalSize = volumes.Sum(v => v.Size)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取存储卷统计信息失败");
            return new VolumeStatistics
            {
                VolumeName = "statistics",
                NodeId = nodeId ?? "local",
                Timestamp = DateTime.UtcNow,
                SizeBytes = 0,
                TotalSize = 0
            };
        }
    }

    public async Task<bool> VolumeExistsAsync(string volumeId, string? nodeId = null)
    {
        var volume = await _engine.GetVolumeAsync(volumeId, nodeId);
        return volume != null;
    }

    public async Task<VolumeUsageInfo> GetVolumeUsageAsync(string volumeId, string? nodeId = null)
    {
        try
        {
            _logger.LogInformation("获取存储卷使用情况: {VolumeName}", volumeId);

            string? containerId = null;
            try
            {
                containerId = await CreateVolumeAccessContainer(volumeId, nodeId);
                var client = await GetDockerClientAsync(nodeId);
                var execCreate = await client.Exec.CreateContainerExecAsync(containerId, new ContainerExecCreateParameters
                {
                    Cmd = new[] { "sh", "-c", "used=$(du -sk /data 2>/dev/null | awk '{print $1 * 1024}'); files=$(find /data -type f 2>/dev/null | wc -l); dfinfo=$(df -Pk /data 2>/dev/null | tail -1 | awk '{print $2 * 1024 \"|\" $4 * 1024}'); used=${used:-0}; files=${files:-0}; dfinfo=${dfinfo:-0|0}; printf '%s|%s|%s\n' \"$used\" \"$files\" \"$dfinfo\"" },
                    AttachStdout = true,
                    AttachStderr = true
                });

                var stdout = new MemoryStream();
                var stderr = new MemoryStream();
                using var stream = await client.Exec.StartContainerExecAsync(execCreate.ID, new ContainerExecStartParameters());
                await stream.CopyOutputToAsync(Console.OpenStandardOutput(), stdout, stderr, CancellationToken.None);

                var output = Encoding.UTF8.GetString(stdout.ToArray()).Trim().Split('|');
                var usedBytes = ParseLongAt(output, 0);
                var fileCount = (int)ParseLongAt(output, 1);
                var sizeBytes = ParseLongAt(output, 2);
                var availableBytes = ParseLongAt(output, 3);

                return new VolumeUsageInfo
                {
                    VolumeName = volumeId,
                    VolumeId = volumeId,
                    SizeBytes = sizeBytes,
                    Size = usedBytes,
                    UsedBytes = usedBytes,
                    AvailableBytes = availableBytes,
                    UsagePercent = sizeBytes > 0 ? usedBytes * 100.0 / sizeBytes : 0,
                    FileCount = fileCount,
                    UsageCount = 0,
                    LastUpdated = DateTime.UtcNow
                };
            }
            finally
            {
                if (containerId != null)
                {
                    await CleanupVolumeAccessContainer(containerId, nodeId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取存储卷使用情况失败: {VolumeName}", volumeId);
            throw;
        }
    }

    public async Task<byte[]> BackupVolumeAsync(string name)
    {
        var (content, _) = await ArchiveVolumeFilesAsync(name, "/");
        return content;
    }

    public async Task<VolumeBackupResult> BackupVolumeAsync(string volumeId, VolumeBackupRequest request)
    {
        try
        {
            var volumeName = string.IsNullOrWhiteSpace(request.VolumeName) ? volumeId : request.VolumeName;
            var (content, _) = await ArchiveVolumeFilesAsync(volumeName, "/", request.NodeId);
            var backupDirectory = GetBackupDirectory(request.BackupLocation);
            Directory.CreateDirectory(backupDirectory);

            var backupId = $"{SanitizeFileName(volumeName)}_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}.tar.gz";
            var backupPath = Path.Combine(backupDirectory, backupId);
            await File.WriteAllBytesAsync(backupPath, content);

            return new VolumeBackupResult
            {
                Success = true,
                VolumeName = volumeName,
                BackupId = backupId,
                BackupPath = backupPath,
                BackupSize = content.Length,
                CreatedAt = DateTime.UtcNow,
                Metadata = request.Metadata
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "备份存储卷失败: {VolumeId}", volumeId);
            return new VolumeBackupResult
            {
                Success = false,
                VolumeName = string.IsNullOrWhiteSpace(request.VolumeName) ? volumeId : request.VolumeName,
                ErrorMessage = ex.Message,
                CreatedAt = DateTime.UtcNow
            };
        }
    }

    public async Task RestoreVolumeAsync(string name, byte[] data)
    {
        using var stream = new MemoryStream(data);
        await RestoreVolumeFromArchiveAsync(name, stream);
    }

    public async Task<VolumeRestoreResult> RestoreVolumeAsync(VolumeRestoreRequest request)
    {
        try
        {
            var backupPath = ResolveBackupPath(request.BackupId);
            if (backupPath == null || !File.Exists(backupPath))
            {
                throw new FileNotFoundException("备份文件不存在", request.BackupId);
            }

            var targetVolumeName = request.TargetVolumeName ?? request.VolumeId;
            if (!string.IsNullOrWhiteSpace(targetVolumeName) && request.OverwriteExisting && await VolumeExistsAsync(targetVolumeName))
            {
                await RemoveVolumeAsync(targetVolumeName, true);
            }

            await using var stream = File.OpenRead(backupPath);
            var restored = await RestoreVolumeFromArchiveAsync(targetVolumeName, stream);
            var fileInfo = new FileInfo(backupPath);

            return new VolumeRestoreResult
            {
                Success = true,
                BackupId = request.BackupId,
                RestoredVolumeName = restored.Name,
                RestoredAt = DateTime.UtcNow,
                RestoredSize = fileInfo.Length
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "恢复存储卷失败: {BackupId}", request.BackupId);
            return new VolumeRestoreResult
            {
                Success = false,
                BackupId = request.BackupId,
                ErrorMessage = ex.Message,
                RestoredAt = DateTime.UtcNow
            };
        }
    }

    public async Task<IEnumerable<VolumeBackupInfo>> GetVolumeBackupsAsync(string volumeId)
    {
        await Task.CompletedTask;
        var backupDirectory = GetBackupDirectory(null);
        if (!Directory.Exists(backupDirectory)) return new List<VolumeBackupInfo>();

        var prefix = SanitizeFileName(volumeId) + "_";
        return Directory.EnumerateFiles(backupDirectory, "*.tar.gz")
            .Where(path => Path.GetFileName(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(path =>
            {
                var fileInfo = new FileInfo(path);
                return new VolumeBackupInfo
                {
                    BackupId = fileInfo.Name,
                    VolumeName = volumeId,
                    CreatedAt = fileInfo.CreationTimeUtc,
                    Size = fileInfo.Length,
                    Status = "completed",
                    Location = fileInfo.FullName
                };
            })
            .OrderByDescending(b => b.CreatedAt)
            .ToList();
    }

    public async Task<bool> DeleteVolumeBackupAsync(string volumeId, string backupId)
    {
        await Task.CompletedTask;
        var backupPath = ResolveBackupPath(backupId);
        if (backupPath == null || !File.Exists(backupPath)) return false;

        var prefix = SanitizeFileName(volumeId) + "_";
        if (!Path.GetFileName(backupPath).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;

        File.Delete(backupPath);
        return true;
    }

    private static long ParseLongAt(string[] values, int index)
    {
        return index < values.Length && long.TryParse(values[index].Trim(), out var value) ? value : 0;
    }

    private static string GetBackupDirectory(string? requestedPath)
    {
        return string.IsNullOrWhiteSpace(requestedPath)
            ? Path.Combine(AppContext.BaseDirectory, "Data", "volume-backups")
            : requestedPath;
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "volume" : sanitized;
    }

    private static string? ResolveBackupPath(string backupId)
    {
        var safeName = Path.GetFileName(backupId);
        var defaultPath = Path.Combine(GetBackupDirectory(null), safeName);
        return File.Exists(defaultPath) ? defaultPath : null;
    }

    private static string NormalizeVolumePath(string? path, bool allowRoot = true)
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
            throw new ArgumentException("不允许对卷根路径执行该操作");
        }

        return normalized;
    }

    private static string NormalizeVolumeFileName(string? fileName)
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

    private static string ToMountedVolumePath(string normalizedVolumePath)
    {
        normalizedVolumePath = NormalizeVolumePath(normalizedVolumePath);
        return normalizedVolumePath == "/" ? "/data" : $"/data{normalizedVolumePath}";
    }

    private static string ShellQuote(string value)
    {
        return "'" + value.Replace("'", "'\"'\"'") + "'";
    }

    #region 文件操作 - 通过临时容器实现

    /// <summary>
    /// 创建一个临时容器用于访问卷文件
    /// </summary>
    private async Task<string> CreateVolumeAccessContainer(string volumeId, string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        const string imageName = "alpine:latest";

        try
        {
            // 检查镜像是否存在，不存在则拉取
            await EnsureImageExistsAsync(client, imageName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "拉取镜像失败: {ImageName}", imageName);
            throw new InvalidOperationException($"无法拉取镜像 {imageName}，请确保 Docker 可以访问网络或手动拉取该镜像");
        }

        var createParams = new CreateContainerParameters
        {
            Image = imageName,
            Name = $"volume-access-{Guid.NewGuid():N}",
            HostConfig = new HostConfig
            {
                Mounts = new List<Mount>
                {
                    new Mount
                    {
                        Type = "volume",
                        Source = volumeId,
                        Target = "/data",
                        ReadOnly = false
                    }
                },
                AutoRemove = false
            },
            Cmd = new[] { "tail", "-f", "/dev/null" },  // 保持容器运行
        };

        try
        {
            var createResponse = await client.Containers.CreateContainerAsync(createParams);
            
            // 启动容器
            await client.Containers.StartContainerAsync(createResponse.ID, new ContainerStartParameters());
            
            _logger.LogInformation("创建卷访问容器成功: {ContainerId}, 卷: {VolumeId}", createResponse.ID, volumeId);
            return createResponse.ID;
        }
        catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // 镜像不存在，尝试拉取后重试
            _logger.LogWarning("镜像不存在，尝试拉取: {ImageName}", imageName);
            await EnsureImageExistsAsync(client, imageName);
            
            createParams.Image = imageName;
            var createResponse = await client.Containers.CreateContainerAsync(createParams);
            await client.Containers.StartContainerAsync(createResponse.ID, new ContainerStartParameters());
            
            _logger.LogInformation("创建卷访问容器成功(重试): {ContainerId}", createResponse.ID);
            return createResponse.ID;
        }
    }

    /// <summary>
    /// 确保镜像存在
    /// </summary>
    private async Task EnsureImageExistsAsync(DockerClient client, string imageName)
    {
        try
        {
            // 尝试检查镜像是否存在
            var images = await client.Images.ListImagesAsync(new ImagesListParameters
            {
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["reference"] = new Dictionary<string, bool> { [imageName] = true }
                }
            });

            if (images == null || !images.Any())
            {
                _logger.LogInformation("镜像不存在，正在拉取: {ImageName}", imageName);
                
                await client.Images.CreateImageAsync(
                    new ImagesCreateParameters { FromImage = imageName },
                    new AuthConfig(),
                    new Progress<JSONMessage>(msg =>
                    {
                        if (!string.IsNullOrEmpty(msg.Status))
                        {
                            _logger.LogDebug("拉取镜像: {Status}", msg.Status);
                        }
                    })
                );
                
                _logger.LogInformation("镜像拉取完成: {ImageName}", imageName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查/拉取镜像失败: {ImageName}", imageName);
            throw;
        }
    }

    /// <summary>
    /// 清理临时容器
    /// </summary>
    private async Task CleanupVolumeAccessContainer(string containerId, string? nodeId = null)
    {
        var client = await GetDockerClientAsync(nodeId);
        
        // 尝试直接强制删除（更可靠）
        try
        {
            await client.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters { Force = true });
            _logger.LogInformation("已清理临时容器: {ContainerId}", containerId);
            return;
        }
        catch (DockerContainerNotFoundException)
        {
            _logger.LogDebug("临时容器已不存在: {ContainerId}", containerId);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "强制删除临时容器失败，尝试先停止再删除: {ContainerId}", containerId);
        }
        
        // 如果强制删除失败，尝试先停止再删除
        try
        {
            await client.Containers.StopContainerAsync(containerId, new ContainerStopParameters { WaitBeforeKillSeconds = 1 });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "停止临时容器失败: {ContainerId}", containerId);
        }
        
        try
        {
            await client.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters { Force = true });
            _logger.LogInformation("已清理临时容器(停止后删除): {ContainerId}", containerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理临时容器最终失败: {ContainerId}", containerId);
        }
    }

    public async Task<ContainerFileListResponse> GetVolumeFilesAsync(string volumeId, string path, string? nodeId = null)
    {
        _logger.LogInformation("获取卷 {VolumeId} 文件列表, 路径: {Path}", volumeId, path);
        path = NormalizeVolumePath(path);
        
        string? containerId = null;
        try
        {
            containerId = await CreateVolumeAccessContainer(volumeId, nodeId);
            
            // 调用容器文件操作
            // 卷挂载在 /data，所以路径需要加上 /data 前缀
            var volumePath = ToMountedVolumePath(path);
            var files = await _engine.GetContainerFilesAsync(containerId, volumePath, nodeId);
            
            return new ContainerFileListResponse
            {
                ContainerId = containerId,
                CurrentPath = path,
                Files = files.Files,
                Mounts = new List<ContainerMountInfo>()
            };
        }
        finally
        {
            if (containerId != null)
            {
                await CleanupVolumeAccessContainer(containerId, nodeId);
            }
        }
    }

    public async Task<byte[]> DownloadVolumeFileAsync(string volumeId, string path, string? nodeId = null)
    {
        _logger.LogInformation("下载卷文件: {VolumeId}, {Path}", volumeId, path);
        path = NormalizeVolumePath(path, allowRoot: false);
        
        string? containerId = null;
        try
        {
            containerId = await CreateVolumeAccessContainer(volumeId, nodeId);
            var volumePath = ToMountedVolumePath(path);
            return await _engine.DownloadContainerFileAsync(containerId, volumePath, nodeId);
        }
        finally
        {
            if (containerId != null)
            {
                await CleanupVolumeAccessContainer(containerId, nodeId);
            }
        }
    }

    public async Task<(byte[] content, string fileName)> ArchiveVolumeFilesAsync(string volumeId, string path, string? nodeId = null)
    {
        _logger.LogInformation("打包卷文件: {VolumeId}, {Path}", volumeId, path);
        path = NormalizeVolumePath(path);
        
        string? containerId = null;
        try
        {
            // 进度: 准备中
            await DockerPanelHub.BroadcastVolumeArchiveProgress(_hubContext, volumeId, "archive.preparing", 10, "Creating temporary container...");
            
            containerId = await CreateVolumeAccessContainer(volumeId, nodeId);
            
            // 进度: 容器已创建
            await DockerPanelHub.BroadcastVolumeArchiveProgress(_hubContext, volumeId, "archive.packing", 30, "Packing files...");
            
            var client = await GetDockerClientAsync(nodeId);
            var volumePath = ToMountedVolumePath(path);
            
            // 使用 tar 打包
            var execCreate = await client.Exec.CreateContainerExecAsync(containerId, new ContainerExecCreateParameters
            {
                Cmd = new[] { "sh", "-c", $"cd {ShellQuote(volumePath)} && tar czf - ." },
                AttachStdout = true,
                AttachStderr = true
            });

            // 进度: 执行打包命令
            await DockerPanelHub.BroadcastVolumeArchiveProgress(_hubContext, volumeId, "archive.compressing", 50, "Compressing data...");

            var stdout = new MemoryStream();
            var stderr = new MemoryStream();
            
            using var stream = await client.Exec.StartContainerExecAsync(execCreate.ID, new ContainerExecStartParameters());
            await stream.CopyOutputToAsync(Console.OpenStandardOutput(), stdout, stderr, CancellationToken.None);
            
            // 获取文件内容
            var content = stdout.ToArray();
            var fileName = $"{volumeId}_{DateTime.Now:yyyyMMdd_HHmmss}.tar.gz";
            
            // 清理临时容器
            await DockerPanelHub.BroadcastVolumeArchiveProgress(_hubContext, volumeId, "archive.cleaning", 90, "Cleaning up...");
            if (containerId != null)
            {
                await CleanupVolumeAccessContainer(containerId, nodeId);
                containerId = null; // 标记已清理
            }
            
            // 进度: 完成（在文件准备好之后推送）
            await DockerPanelHub.BroadcastVolumeArchiveProgress(_hubContext, volumeId, "archive.completed", 100, "Archive completed");
            
            return (content, fileName);
        }
        catch (Exception)
        {
            // 出错时清理临时容器
            if (containerId != null)
            {
                try { await CleanupVolumeAccessContainer(containerId, nodeId); } catch { }
            }
            throw;
        }
    }

    public async Task UploadVolumeFileAsync(string volumeId, string path, string fileName, Stream content, string? nodeId = null)
    {
        _logger.LogInformation("上传文件到卷: {VolumeId}, {Path}/{FileName}", volumeId, path, fileName);
        path = NormalizeVolumePath(path);
        fileName = NormalizeVolumeFileName(fileName);
        
        string? containerId = null;
        try
        {
            containerId = await CreateVolumeAccessContainer(volumeId, nodeId);
            
            var volumePath = ToMountedVolumePath(path);
            
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms);
            await _engine.UploadContainerFileAsync(containerId, volumePath, fileName, ms.ToArray(), nodeId);
        }
        finally
        {
            if (containerId != null)
            {
                await CleanupVolumeAccessContainer(containerId, nodeId);
            }
        }
    }

    public async Task CreateVolumeFolderAsync(string volumeId, string path, string name, string? nodeId = null)
    {
        _logger.LogInformation("在卷中创建文件夹: {VolumeId}, {Path}/{Name}", volumeId, path, name);
        path = NormalizeVolumePath(path);
        name = NormalizeVolumeFileName(name);
        
        string? containerId = null;
        try
        {
            containerId = await CreateVolumeAccessContainer(volumeId, nodeId);
            var volumePath = ToMountedVolumePath(path);
            await _engine.CreateContainerFolderAsync(containerId, volumePath, name, nodeId);
        }
        finally
        {
            if (containerId != null)
            {
                await CleanupVolumeAccessContainer(containerId, nodeId);
            }
        }
    }

    public async Task RenameVolumeFileAsync(string volumeId, string path, string oldName, string newName, string? nodeId = null)
    {
        _logger.LogInformation("重命名卷文件: {VolumeId}, {Path}/{OldName} -> {NewName}", volumeId, path, oldName, newName);
        path = NormalizeVolumePath(path);
        oldName = NormalizeVolumeFileName(oldName);
        newName = NormalizeVolumeFileName(newName);
        
        string? containerId = null;
        try
        {
            containerId = await CreateVolumeAccessContainer(volumeId, nodeId);
            var volumePath = ToMountedVolumePath(path);
            await _engine.RenameContainerFileAsync(containerId, volumePath, oldName, newName, nodeId);
        }
        finally
        {
            if (containerId != null)
            {
                await CleanupVolumeAccessContainer(containerId, nodeId);
            }
        }
    }

    public async Task DeleteVolumeFileAsync(string volumeId, string path, bool recursive = false, string? nodeId = null)
    {
        _logger.LogInformation("删除卷文件: {VolumeId}, {Path}", volumeId, path);
        path = NormalizeVolumePath(path, allowRoot: false);
        
        string? containerId = null;
        try
        {
            containerId = await CreateVolumeAccessContainer(volumeId, nodeId);
            var volumePath = ToMountedVolumePath(path);
            await _engine.DeleteContainerFileAsync(containerId, volumePath, recursive, nodeId);
        }
        finally
        {
            if (containerId != null)
            {
                await CleanupVolumeAccessContainer(containerId, nodeId);
            }
        }
    }

    public async Task<string> GetVolumeFileContentAsync(string volumeId, string path, string? nodeId = null)
    {
        _logger.LogInformation("获取卷文件内容: {VolumeId}, {Path}", volumeId, path);
        path = NormalizeVolumePath(path, allowRoot: false);
        
        string? containerId = null;
        try
        {
            containerId = await CreateVolumeAccessContainer(volumeId, nodeId);
            var volumePath = ToMountedVolumePath(path);
            return await _engine.GetContainerFileContentAsync(containerId, volumePath, nodeId);
        }
        finally
        {
            if (containerId != null)
            {
                await CleanupVolumeAccessContainer(containerId, nodeId);
            }
        }
    }

    public async Task SaveVolumeFileContentAsync(string volumeId, string path, string content, string? nodeId = null)
    {
        _logger.LogInformation("保存卷文件内容: {VolumeId}, {Path}", volumeId, path);
        path = NormalizeVolumePath(path, allowRoot: false);
        
        string? containerId = null;
        try
        {
            containerId = await CreateVolumeAccessContainer(volumeId, nodeId);
            var volumePath = ToMountedVolumePath(path);
            await _engine.WriteContainerFileContentAsync(containerId, volumePath, content, nodeId);
        }
        finally
        {
            if (containerId != null)
            {
                await CleanupVolumeAccessContainer(containerId, nodeId);
            }
        }
    }

    /// <summary>
    /// 从 tar.gz 归档文件恢复创建新卷
    /// </summary>
    public async Task<Models.VolumeInfo> RestoreVolumeFromArchiveAsync(string? volumeName, Stream archiveStream, string? nodeId = null)
    {
        _logger.LogInformation("从归档恢复卷: {VolumeName}", volumeName ?? "(自动生成)");
        
        var client = await GetDockerClientAsync(nodeId);
        string? containerId = null;
        
        try
        {
            await using var safeArchiveStream = await CreateValidatedArchiveCopyAsync(archiveStream);

            // 1. 创建新卷
            var createParams = new VolumesCreateParameters();
            if (!string.IsNullOrWhiteSpace(volumeName))
            {
                createParams.Name = volumeName;
            }
            
            var createResponse = await client.Volumes.CreateAsync(createParams);
            var createdVolumeName = createResponse.Name;
            
            // 用于进度显示的名称
            var displayName = createdVolumeName;
            await DockerPanelHub.BroadcastVolumeArchiveProgress(_hubContext, displayName, "restore.preparing", 10, "Creating volume...");
            
            _logger.LogInformation("已创建卷: {VolumeName}", createdVolumeName);
            
            // 2. 创建临时容器挂载卷
            await DockerPanelHub.BroadcastVolumeArchiveProgress(_hubContext, displayName, "restore.preparing", 30, "Preparing restore environment...");
            
            containerId = await CreateVolumeAccessContainer(createdVolumeName, nodeId);
            
            // 3. 准备归档数据流
            await DockerPanelHub.BroadcastVolumeArchiveProgress(_hubContext, displayName, "restore.extracting", 50, "Extracting data...");
            
            // 4. 使用 tar 从 stdin 解压到 /data 目录
            await DockerPanelHub.BroadcastVolumeArchiveProgress(_hubContext, displayName, "restore.restoring", 70, "Restoring files...");
            
            var extractCmd = new[] { "sh", "-c", "cd /data && tar xzf -" };
            var execCreate = await client.Exec.CreateContainerExecAsync(containerId, new ContainerExecCreateParameters
            {
                Cmd = extractCmd,
                AttachStdin = true,
                AttachStdout = true,
                AttachStderr = true
            });
            
            using var execStream = await client.Exec.StartContainerExecAsync(execCreate.ID, new ContainerExecStartParameters());
            
            // 将归档数据写入 stdin
            safeArchiveStream.Position = 0;
            await execStream.CopyFromAsync(safeArchiveStream, CancellationToken.None);
            
            // 关闭写入端
            execStream.CloseWrite();
            
            // 等待解压完成
            await execStream.ReadOutputToEndAsync(CancellationToken.None);
            
            // 5. 清理临时容器
            await DockerPanelHub.BroadcastVolumeArchiveProgress(_hubContext, displayName, "restore.cleaning", 90, "Cleaning up...");
            
            if (containerId != null)
            {
                await CleanupVolumeAccessContainer(containerId, nodeId);
                containerId = null;
            }
            
            // 6. 完成
            await DockerPanelHub.BroadcastVolumeArchiveProgress(_hubContext, displayName, "restore.completed", 100, "Restore completed");
            
            // 返回创建的卷信息
            return new Models.VolumeInfo
            {
                Name = createdVolumeName,
                Id = createdVolumeName,
                Driver = "local"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从归档恢复卷失败: {VolumeName}", volumeName);
            
            // 清理临时容器
            if (containerId != null)
            {
                try { await CleanupVolumeAccessContainer(containerId, nodeId); } catch { }
            }
            
            throw;
        }
    }

    private static async Task<MemoryStream> CreateValidatedArchiveCopyAsync(Stream archiveStream)
    {
        var copy = new MemoryStream();
        if (archiveStream.CanSeek) archiveStream.Position = 0;
        await archiveStream.CopyToAsync(copy);
        copy.Position = 0;

        using (var gzip = new GZipStream(copy, CompressionMode.Decompress, leaveOpen: true))
        using (var reader = new TarReader(gzip, leaveOpen: true))
        {
            TarEntry? entry;
            while ((entry = await reader.GetNextEntryAsync()) != null)
            {
                ValidateTarEntryName(entry.Name);
            }
        }

        copy.Position = 0;
        return copy;
    }

    private static void ValidateTarEntryName(string? entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName)) return;
        var normalized = entryName.Replace('\\', '/');
        if (normalized.StartsWith('/') || normalized.Contains('\0') || normalized.Contains('\r') || normalized.Contains('\n'))
        {
            throw new InvalidDataException($"归档包含非法路径: {entryName}");
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(s => s == "." || s == ".."))
        {
            throw new InvalidDataException($"归档包含路径穿越条目: {entryName}");
        }
    }

    #endregion
}
