using DockerPanel.API.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Formats.Tar;
using System.Text.Json;

namespace DockerPanel.API.Services;

/// <summary>
/// 简单镜像服务实现 - 使用真实 Docker 命令
/// </summary>
public class ImageService : IImageService
{
    private readonly ILogger<ImageService> _logger;
    private readonly IContainerEngine _engine;

    public ImageService(ILogger<ImageService> logger, IContainerEngine engine)
    {
        _logger = logger;
        _engine = engine;
    }

    public async Task<IEnumerable<ImageInfo>> GetImagesAsync(string? nodeId = null)
    {
        _logger.LogInformation("获取镜像列表");
        return await _engine.ListImagesAsync();
    }

    public async Task<ImageInfo?> GetImageAsync(string id)
    {
        _logger.LogInformation("获取镜像详情: {ImageId}", id);
        return await _engine.GetImageAsync(id);
    }

    public async Task PullImageAsync(string name, string? tag = null, string? nodeId = null, IProgress<ImagePullProgress>? progress = null, string? registryId = null)
    {
        _logger.LogInformation("拉取镜像: {Name}:{Tag}, Registry: {RegistryId}", name, tag ?? "latest", registryId ?? "default");
        await _engine.PullImageAsync(name, tag, progress, registryId);
    }

    public async Task RemoveImageAsync(string id, bool force = false)
    {
        _logger.LogInformation("删除镜像: {ImageId}", id);
        await _engine.RemoveImageAsync(id, force);
    }

    public async Task<string> BuildImageAsync(BuildImageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ContextPath) || !Directory.Exists(request.ContextPath))
        {
            throw new DirectoryNotFoundException($"构建上下文不存在: {request.ContextPath}");
        }

        var dockerfilePath = Path.Combine(request.ContextPath, request.DockerfileName);
        if (!File.Exists(dockerfilePath))
        {
            throw new FileNotFoundException("Dockerfile 不存在", dockerfilePath);
        }

        var tag = string.IsNullOrWhiteSpace(request.Tag)
            ? request.Repository
            : $"{request.Repository}:{request.Tag}";

        var parameters = new BuildImageParams
        {
            Tag = tag,
            Dockerfile = request.DockerfileName,
            BuildArgs = request.BuildArgs,
            NoCache = request.NoCache,
            Remove = request.RemoveIntermediateContainers
        };

        await using var contextStream = await CreateBuildContextTarAsync(request.ContextPath);
        return await _engine.BuildImageFromContextAsync(contextStream, parameters, nodeId: string.IsNullOrWhiteSpace(request.NodeId) ? null : request.NodeId);
    }

    public async Task<string> BuildImageFromContextAsync(Stream contextStream, BuildImageParams parameters, IProgress<ImageBuildProgress>? progress = null)
    {
        _logger.LogInformation("从上下文构建镜像: {Tag}", parameters.Tag);
        return await _engine.BuildImageFromContextAsync(contextStream, parameters, progress);
    }

    public async Task<string> BuildImageFromDockerfileAsync(string dockerfileContent, BuildImageParams parameters, IProgress<ImageBuildProgress>? progress = null)
    {
        _logger.LogInformation("从 Dockerfile 构建镜像: {Tag}", parameters.Tag);
        return await _engine.BuildImageFromDockerfileAsync(dockerfileContent, parameters, progress);
    }

    private static async Task<MemoryStream> CreateBuildContextTarAsync(string contextPath)
    {
        var basePath = Path.GetFullPath(contextPath);
        var stream = new MemoryStream();

        await using (var writer = new TarWriter(stream, TarEntryFormat.Pax, leaveOpen: true))
        {
            foreach (var filePath in Directory.EnumerateFiles(basePath, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(basePath, filePath).Replace(Path.DirectorySeparatorChar, '/');
                await using var fileStream = File.OpenRead(filePath);
                var entry = new PaxTarEntry(TarEntryType.RegularFile, relativePath)
                {
                    DataStream = fileStream,
                    ModificationTime = File.GetLastWriteTimeUtc(filePath)
                };
                await writer.WriteEntryAsync(entry);
            }
        }

        stream.Position = 0;
        return stream;
    }

    public async Task PushImageAsync(string name, string? tag = null, IProgress<ImagePushProgress>? progress = null)
    {
        _logger.LogInformation("推送镜像: {Name}:{Tag}", name, tag ?? "latest");
        await _engine.PushImageAsync(name, tag, progress);
    }

    public async Task TagImageAsync(string sourceId, string targetName)
    {
        _logger.LogInformation("标记镜像: {SourceId} -> {TargetName}", sourceId, targetName);
        await _engine.TagImageAsync(sourceId, targetName);
    }

    public async Task<IEnumerable<ImageHistory>> GetImageHistoryAsync(string id)
    {
        return await _engine.GetImageHistoryAsync(id);
    }

    public async Task<IEnumerable<ImageSearchResult>> SearchImagesAsync(string term)
    {
        return await _engine.SearchImagesAsync(term);
    }

    public async Task<byte[]> SaveImageAsync(string id)
    {
        _logger.LogInformation("导出镜像: {ImageId}", id);
        using var stream = await _engine.SaveImageAsync(id);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }

    public async Task<List<string>> LoadImageAsync(Stream data)
    {
        _logger.LogInformation("导入镜像");
        return await _engine.LoadImageAsync(data);
    }

    public async Task<int> PruneImagesAsync()
    {
        return 0;
    }

    public async Task<ImageLayersInfo> GetImageLayersAsync(string imageId, string? nodeId = null)
    {
        return new ImageLayersInfo { ImageId = imageId };
    }

    public async Task<ImageBatchOperationResult> BatchRemoveImagesAsync(BatchRemoveImagesRequest request)
    {
        var result = new ImageBatchOperationResult { Results = new List<ImageBatchOperationItem>() };
        foreach (var id in request.ImageIds)
        {
            try { await RemoveImageAsync(id, request.Force); result.SuccessCount++; }
            catch { result.FailedCount++; }
        }
        return result;
    }

    public async Task<ImageStatistics> GetImageStatisticsAsync(string? nodeId = null)
    {
        var images = await GetImagesAsync(nodeId);
        return new ImageStatistics
        {
            TotalImages = images.Count(),
            TotalSize = images.Sum(i => i.Size),
            LastUpdated = DateTime.UtcNow
        };
    }

    public async Task<PruneResult> PruneImagesAsync(PruneOptions options, string? nodeId = null)
    {
        _logger.LogInformation("开始清理未使用镜像，参数: Dangling={Dangling}, All={All}", options.Dangling, options.All);
        var result = await _engine.PruneImagesAsync(danglingOnly: !options.All);
        _logger.LogInformation("清理完成，删除 {Count} 个镜像，释放 {Space} 字节", result.ImagesDeleted, result.SpaceReclaimed);
        return result;
    }

    public async Task<ImageInspect?> InspectImageAsync(string imageId)
    {
        _logger.LogInformation("检查镜像详情: {ImageId}", imageId);
        var imageInfo = await _engine.GetImageAsync(imageId);
        if (imageInfo == null) return null;
        
        // 转换为 ImageDetailInfo 以获取更多详细信息
        var detail = imageInfo as ImageDetailInfo ?? new ImageDetailInfo
        {
            Id = imageInfo.Id,
            Repository = imageInfo.Repository,
            Tag = imageInfo.Tag,
            Size = imageInfo.Size,
            Created = imageInfo.Created,
            CreatedAt = imageInfo.CreatedAt,
            Architecture = imageInfo.Architecture,
            Os = imageInfo.Os,
            Labels = imageInfo.Labels,
            RepoTags = imageInfo.RepoTags
        };
        
        return new ImageInspect
        {
            Id = detail.Id,
            RepoTags = detail.RepoTags?.ToList() ?? new(),
            RepoDigests = new(),
            Parent = detail.Parent ?? "",
            Comment = detail.Comment ?? "",
            Created = detail.Created,
            Architecture = detail.Architecture,
            Os = detail.Os,
            Size = detail.Size,
            VirtualSize = detail.VirtualSize,
            SharedSize = 0,
            RootFS = new(),
            Config = new DockerContainerConfig
            {
                Env = detail.Env ?? new(),
                ExposedPorts = detail.ExposedPorts ?? new(),
                Volumes = detail.Volumes ?? new(),
                WorkingDir = detail.WorkingDir ?? "",
                Cmd = detail.Cmd ?? new(),
                Entrypoint = detail.Entrypoint ?? new(),
                User = detail.User ?? "",
                Labels = (detail.Labels ?? new()).ToDictionary(kv => kv.Key, kv => (object)kv.Value)
            }
        };
    }
}