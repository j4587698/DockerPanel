using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DockerPanel.API.Data;
using DockerPanel.API.Models;
using DockerPanel.API.Hubs;
using DockerPanel.API.Serialization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace DockerPanel.API.Services;

/// <summary>
/// 容器自动升级服务接口
/// </summary>
public interface IAutoUpdateService
{
    /// <summary>
    /// 获取容器的自动升级配置
    /// </summary>
    Task<ContainerAutoUpdateConfig?> GetConfigAsync(string containerId);
    
    /// <summary>
    /// 设置容器的自动升级配置
    /// </summary>
    Task<ContainerAutoUpdateConfig> SetConfigAsync(string containerId, ContainerAutoUpdateConfig config);
    
    /// <summary>
    /// 删除容器的自动升级配置
    /// </summary>
    Task DeleteConfigAsync(string containerId);
    
    /// <summary>
    /// 检查单个容器的镜像更新
    /// </summary>
    Task<ImageUpdateCheckResult> CheckUpdateAsync(string containerId);
    
    /// <summary>
    /// 检查所有启用自动检测的容器
    /// </summary>
    Task<List<ImageUpdateCheckResult>> CheckAllUpdatesAsync();
    
    /// <summary>
    /// 拉取最新镜像并重启容器
    /// </summary>
    Task<UpdateResult> UpdateContainerAsync(string containerId, bool pullOnly = false);
    
    /// <summary>
    /// 获取所有需要更新的容器
    /// </summary>
    Task<List<ContainerAutoUpdateConfig>> GetContainersWithUpdatesAsync();
    
    /// <summary>
    /// 获取全局设置
    /// </summary>
    Task<GlobalAutoUpdateSettings> GetGlobalSettingsAsync();
    
    /// <summary>
    /// 设置全局设置
    /// </summary>
    Task<GlobalAutoUpdateSettings> SetGlobalSettingsAsync(GlobalAutoUpdateSettings settings);
    
    /// <summary>
    /// 获取所有自动升级配置
    /// </summary>
    Task<List<ContainerAutoUpdateConfig>> GetAllConfigsAsync();
    
    /// <summary>
    /// 获取镜像的所有可用标签
    /// </summary>
    Task<List<string>> GetImageTagsAsync(string imageName);
    
    /// <summary>
    /// 回滚容器到指定镜像标签
    /// </summary>
    Task<UpdateResult> RollbackContainerAsync(string containerId, string targetTag);
}

/// <summary>
/// 镜像更新检测结果
/// </summary>
public class ImageUpdateCheckResult
{
    public string ContainerId { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string? CurrentDigest { get; set; }
    public string? RemoteDigest { get; set; }
    public bool HasUpdate { get; set; }
    public DateTime CheckTime { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 容器升级结果
/// </summary>
public class UpdateResult
{
    public bool Success { get; set; }
    public string ContainerId { get; set; } = string.Empty;

    /// <summary>
    /// 重建后的新容器 ID（容器重建会生成新 ID，前端需要据此切换）
    /// </summary>
    public string? NewContainerId { get; set; }

    public string? OldDigest { get; set; }
    public string? NewDigest { get; set; }
    public string? ErrorMessage { get; set; }
    public long DurationMs { get; set; }
}

/// <summary>
/// 容器自动升级服务实现
/// </summary>
public class AutoUpdateService : IAutoUpdateService, IDisposable
{
    private readonly TinyDbContext _db;
    private readonly IContainerEngine _containerEngine;
    private readonly IContainerService _containerService;
    private readonly IRegistryService _registryService;
    private readonly ILogger<AutoUpdateService> _logger;
    private readonly IHubContext<DockerPanelHub> _hubContext;
    private readonly HttpClient _httpClient;

    public AutoUpdateService(
        TinyDbContext db,
        IContainerEngine containerEngine,
        IContainerService containerService,
        IRegistryService registryService,
        ILogger<AutoUpdateService> logger,
        IHubContext<DockerPanelHub> hubContext)
    {
        _db = db;
        _containerEngine = containerEngine;
        _containerService = containerService;
        _registryService = registryService;
        _logger = logger;
        _hubContext = hubContext;
        
        // 创建独立的 HttpClient，完全禁用代理
        var handler = new HttpClientHandler
        {
            UseProxy = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "DockerPanel/1.0");
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    public async Task<ContainerAutoUpdateConfig?> GetConfigAsync(string containerId)
    {
        var config = _db.AutoUpdateConfigs.FindOne(c => c.ContainerId == containerId);
        if (config != null)
        {
            return config;
        }

        // 容器被外部工具（docker compose / watchtower）重建后 ID 会变，
        // 旧配置仍挂在已删除的 ID 上。这里按容器名兜底认领一次，
        // 避免新容器上开关显示为"未配置"、用户以为自动升级丢了。
        return await TryAdoptStaleConfigAsync(containerId);
    }

    /// <summary>
    /// 按容器名认领指向旧容器 ID 的自动更新配置。
    /// </summary>
    private async Task<ContainerAutoUpdateConfig?> TryAdoptStaleConfigAsync(string containerId)
    {
        try
        {
            var container = await _containerEngine.GetContainerAsync(containerId);
            var name = (container?.Name ?? string.Empty).TrimStart('/');
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var stale = _db.AutoUpdateConfigs
                .Find(c => c.ContainerName == name && c.ContainerId != containerId)
                .FirstOrDefault();

            if (stale == null)
            {
                return null;
            }

            // 旧 ID 上还有容器活着说明不是重建，别抢别人的配置
            if (await _containerEngine.GetContainerAsync(stale.ContainerId) != null)
            {
                return null;
            }

            await MigrateConfigToNewContainerAsync(stale, container!);
            return stale;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "按容器名认领自动更新配置失败: {ContainerId}", containerId);
            return null;
        }
    }

    public async Task<ContainerAutoUpdateConfig> SetConfigAsync(string containerId, ContainerAutoUpdateConfig config)
    {
        var existing = await GetConfigAsync(containerId);
        
        if (existing != null)
        {
            existing.EnableUpdateCheck = config.EnableUpdateCheck;
            existing.EnableAutoPull = config.EnableAutoPull;
            existing.EnableAutoRestart = config.EnableAutoRestart;
            existing.CheckIntervalHours = config.CheckIntervalHours;
            existing.UpdatedAt = DateTime.UtcNow;
            _db.AutoUpdateConfigs.Update(existing);
            return existing;
        }
        
        config.ContainerId = containerId;
        config.Id = Guid.NewGuid().ToString();
        config.CreatedAt = DateTime.UtcNow;
        config.UpdatedAt = DateTime.UtcNow;
        _db.AutoUpdateConfigs.Insert(config);
        return config;
    }

    public async Task DeleteConfigAsync(string containerId)
    {
        _db.AutoUpdateConfigs.DeleteMany(c => c.ContainerId == containerId);
    }

    /// <summary>
    /// 把自动更新配置迁移到重建后的新容器上（容器重建会生成新的容器 ID）
    /// </summary>
    private async Task MigrateConfigToNewContainerAsync(ContainerAutoUpdateConfig config, ContainerInfo newContainer)
    {
        var oldContainerId = config.ContainerId;
        var newName = (newContainer.Name ?? string.Empty).TrimStart('/');

        // 新容器上可能已存在一条配置（历史遗留或用户手动创建），
        // 以当前这条正在升级的配置为准，删掉重复记录，避免配置越积越多。
        var duplicates = _db.AutoUpdateConfigs
            .Find(c => c.ContainerId == newContainer.Id)
            .Where(c => c.Id != config.Id)
            .ToList();

        foreach (var duplicate in duplicates)
        {
            _logger.LogWarning("删除重复的自动更新配置: 容器 {ContainerId}, 配置 {ConfigId}",
                newContainer.Id, duplicate.Id);
            _db.AutoUpdateConfigs.Delete(duplicate.Id);
        }

        config.ContainerId = newContainer.Id;
        config.ContainerName = newName;
        config.UpdatedAt = DateTime.UtcNow;
        _db.AutoUpdateConfigs.Update(config);

        _logger.LogInformation("自动更新配置已迁移到新容器: {OldId} -> {NewId} ({Name})",
            oldContainerId, newContainer.Id, newName);
    }

    /// <summary>
    /// 对账：ContainerId 已失效（容器被面板重建、被 docker compose 重建或已被删除）的配置，
    /// 按容器名迁移到现存容器上。
    /// </summary>
    private async Task<int> ReconcileStaleConfigsAsync()
    {
        try
        {
            var configs = _db.AutoUpdateConfigs.FindAll().ToList();
            if (configs.Count == 0)
            {
                return 0;
            }

            var containers = (await _containerService.GetContainersAsync(all: true)).ToList();
            if (containers.Count == 0)
            {
                return 0;
            }

            var existingIds = containers
                .Select(c => c.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var byName = containers
                .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                .GroupBy(c => c.Name!.TrimStart('/'), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var migrated = 0;
            foreach (var config in configs)
            {
                if (existingIds.Contains(config.ContainerId) || string.IsNullOrWhiteSpace(config.ContainerName))
                {
                    continue;
                }

                if (!byName.TryGetValue(config.ContainerName.TrimStart('/'), out var match))
                {
                    continue;
                }

                await MigrateConfigToNewContainerAsync(config, match);
                migrated++;
            }

            if (migrated > 0)
            {
                _logger.LogInformation("自动更新配置对账完成：{Count} 条失效配置已按容器名迁移到新容器", migrated);
            }

            return migrated;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "自动更新配置对账失败");
            return 0;
        }
    }

    public async Task<ImageUpdateCheckResult> CheckUpdateAsync(string containerId)
    {
        var result = new ImageUpdateCheckResult { ContainerId = containerId };
        
        try
        {
            // 获取容器信息
            var container = await _containerEngine.GetContainerAsync(containerId);
            if (container == null)
            {
                result.ErrorMessage = "容器不存在";
                return result;
            }
            
            result.ContainerName = container.Name ?? string.Empty;
            result.Image = container.Image ?? string.Empty;
            
            // 获取本地镜像摘要
            var localImage = await _containerEngine.GetImageAsync(container.Image ?? string.Empty);
            if (localImage == null)
            {
                result.ErrorMessage = "本地镜像不存在";
                return result;
            }
            
            result.CurrentDigest = GetComparableLocalDigest(localImage, container.Image ?? string.Empty) ?? localImage.Id;
            
            // 获取远程镜像摘要
            var imageName = container.Image ?? string.Empty;
            var remoteDigest = !string.IsNullOrEmpty(imageName) 
                ? await GetRemoteImageDigestAsync(imageName) 
                : null;
            result.RemoteDigest = remoteDigest;

            if (string.IsNullOrEmpty(remoteDigest))
            {
                result.ErrorMessage = "无法获取远程镜像摘要，请检查仓库地址或认证配置";
                await UpdateCheckConfigAsync(containerId, result, AutoUpdateStatus.UpdateFailed, result.ErrorMessage);
                return result;
            }
            
            // 比较摘要
            var localDigest = GetComparableLocalDigest(localImage, imageName);
            if (!string.IsNullOrEmpty(localDigest))
            {
                result.HasUpdate = !NormalizeDigest(localDigest).Equals(NormalizeDigest(remoteDigest), StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                result.ErrorMessage = "本地镜像缺少仓库摘要，无法可靠比较远程版本";
                await UpdateCheckConfigAsync(containerId, result, AutoUpdateStatus.UpdateFailed, result.ErrorMessage);
                return result;
            }
            
            // 更新配置
            await UpdateCheckConfigAsync(
                containerId,
                result,
                result.HasUpdate ? AutoUpdateStatus.UpdateAvailable : AutoUpdateStatus.UpToDate,
                result.HasUpdate ? "有新版本可用" : "已是最新版本");
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查容器 {ContainerId} 更新失败", containerId);
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    public async Task<List<ImageUpdateCheckResult>> CheckAllUpdatesAsync()
    {
        var results = new List<ImageUpdateCheckResult>();

        // 先修复失效的容器 ID，否则整轮检查都会拿旧 ID 去查，全部返回"容器不存在"
        await ReconcileStaleConfigsAsync();

        var configs = _db.AutoUpdateConfigs.Find(c => c.EnableUpdateCheck).ToList();
        
        foreach (var config in configs)
        {
            // 检查是否到达检测时间
            if (config.LastCheckTime.HasValue)
            {
                var nextCheck = config.LastCheckTime.Value.AddHours(config.CheckIntervalHours);
                if (DateTime.UtcNow < nextCheck)
                {
                    continue; // 还没到检测时间
                }
            }

            config.Status = AutoUpdateStatus.Checking;
            config.UpdatedAt = DateTime.UtcNow;
            _db.AutoUpdateConfigs.Update(config);
            
            var result = await CheckUpdateAsync(config.ContainerId);
            results.Add(result);
        }
        
        return results;
    }

    public async Task<UpdateResult> UpdateContainerAsync(string containerId, bool pullOnly = false)
    {
        var result = new UpdateResult { ContainerId = containerId };
        var startTime = DateTime.UtcNow;

        // 广播用的操作标识：前端据此关联对应容器的实时进度
        var pullId = $"update-{containerId}";

        try
        {
            var container = await _containerEngine.GetContainerAsync(containerId);
            if (container == null)
            {
                result.ErrorMessage = "容器不存在";
                return result;
            }

            // 手动触发升级时若尚未配置，自动创建默认配置
            var config = await GetConfigAsync(containerId)
                         ?? await SetConfigAsync(containerId, new ContainerAutoUpdateConfig
                         {
                             ContainerId = containerId,
                             ContainerName = container.Name ?? string.Empty,
                             EnableUpdateCheck = true,
                             EnableAutoPull = false,
                             EnableAutoRestart = false,
                             CheckIntervalHours = 6
                         });
            
            // 更新状态为正在拉取
            config.Status = AutoUpdateStatus.Pulling;
            config.UpdatedAt = DateTime.UtcNow;
            _db.AutoUpdateConfigs.Update(config);

            // 广播进度：开始拉取
            await PushPullProgressAsync(pullId, container.Image ?? "", "准备中", 5, "正在拉取最新镜像...");

            // 拉取新镜像
            // container.Image 不保证是 repo:tag —— 容器若以镜像 ID 或 digest 创建，
            // 这里拿到的是 sha256:...，直接切分会拼出「sha256」这个不存在的仓库。
            var imageName = await ResolvePullableImageNameAsync(container.Image);
            if (string.IsNullOrWhiteSpace(imageName))
            {
                // 抛出去交给统一 catch：失败状态、进度广播、历史记录都能一并落到配置上
                throw new InvalidOperationException(
                    "无法确定可拉取的镜像名：容器可能是用镜像 ID 启动的，且本地已没有对应 tag，请改用 repo:tag 形式重新创建容器");
            }

            var (name, tag) = ImageReferenceResolver.Split(imageName);

            var pullProgress = DockerPanelHub.CreatePullProgressBroadcaster(_hubContext, pullId, imageName);
            await _containerEngine.PullImageAsync(name, tag, pullProgress);

            // 获取新镜像摘要
            var newImage = await _containerEngine.GetImageAsync(imageName);
            result.NewDigest = newImage?.Id;
            result.OldDigest = config.CurrentLocalDigest;
            
            ContainerInfo? newContainer = null;
            if (!pullOnly)
            {
                // 更新状态为正在重启
                config.Status = AutoUpdateStatus.Restarting;
                _db.AutoUpdateConfigs.Update(config);

                // 广播进度：重新创建容器
                await PushPullProgressAsync(pullId, container.Image ?? "", "重启中", 90, "正在重建容器并重新启动...");

                // 重建容器（删除并使用相同配置重新创建），使容器真正使用刚拉取的新镜像
                newContainer = await _containerService.RecreateContainerAsync(containerId, pullLatest: false, autoStart: true);
            }

            // 重建会产生全新的容器 ID，自动更新配置必须跟着迁移。
            // 否则配置仍挂在已被删除的旧容器上：新容器读不到配置（开关显示为关闭），
            // 旧配置变成僵尸，每轮检查都会报"容器不存在"，之后也不会再自动升级。
            if (newContainer != null && !string.Equals(newContainer.Id, containerId, StringComparison.Ordinal))
            {
                await MigrateConfigToNewContainerAsync(config, newContainer);
                result.NewContainerId = newContainer.Id;
            }
            
            // 更新配置
            config.CurrentLocalDigest = result.NewDigest;
            config.HasUpdateAvailable = false;
            config.Status = AutoUpdateStatus.UpdateSuccess;
            config.StatusMessage = "升级成功";
            config.UpdatedAt = DateTime.UtcNow;
            
            // 添加升级记录
            config.UpdateHistory.Add(new ContainerUpdateRecord
            {
                Action = pullOnly ? UpdateAction.Pull : UpdateAction.FullUpdate,
                OldDigest = result.OldDigest,
                NewDigest = result.NewDigest,
                Success = true,
                Time = DateTime.UtcNow,
                DurationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds
            });
            
            _db.AutoUpdateConfigs.Update(config);
            
            result.Success = true;
            result.DurationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

            // 广播进度：完成
            await PushPullProgressAsync(pullId, container.Image ?? "", "完成", 100, pullOnly ? "镜像拉取完成" : "升级完成");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新容器 {ContainerId} 失败", containerId);
            result.ErrorMessage = ex.Message;
            result.DurationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

            // 广播失败状态，让前端收起进度
            await PushPullProgressAsync($"update-{containerId}", "", "失败", 100, $"升级失败: {ex.Message}");

            // 更新失败状态。若重建已经完成但后续步骤失败，配置已迁移到新容器，
            // 此时必须按新 ID 回查，否则失败状态会写不进去。
            var config = await GetConfigAsync(result.NewContainerId ?? containerId);
            if (config != null)
            {
                config.Status = AutoUpdateStatus.UpdateFailed;
                config.StatusMessage = ex.Message;
                config.UpdateHistory.Add(new ContainerUpdateRecord
                {
                    Action = UpdateAction.FullUpdate,
                    Success = false,
                    ErrorMessage = ex.Message,
                    Time = DateTime.UtcNow,
                    DurationMs = result.DurationMs
                });
                _db.AutoUpdateConfigs.Update(config);
            }
            
            return result;
        }
    }

    public async Task<List<ContainerAutoUpdateConfig>> GetContainersWithUpdatesAsync()
    {
        return _db.AutoUpdateConfigs.Find(c => c.HasUpdateAvailable).ToList();
    }

    /// <summary>
    /// 把容器上的镜像引用解析成可拉取的 <c>repo:tag</c>。
    /// 容器以镜像 ID 启动时 <c>Image</c> 字段是 <c>sha256:...</c>，以 digest 启动时是
    /// <c>repo@sha256:...</c>，这两种都不能直接拿去 pull。
    /// </summary>
    private Task<string?> ResolvePullableImageNameAsync(string? reference)
        => ImageReferenceResolver.ResolvePullableAsync(reference, async imageId =>
        {
            var image = await _containerEngine.GetImageAsync(imageId);
            return image?.RepoTags;
        });

    public async Task<GlobalAutoUpdateSettings> GetGlobalSettingsAsync()
    {
        var settings = _db.GlobalAutoUpdateSettings.FindById("global");
        return settings ?? new GlobalAutoUpdateSettings { Id = "global" };
    }

    public async Task<GlobalAutoUpdateSettings> SetGlobalSettingsAsync(GlobalAutoUpdateSettings settings)
    {
        settings.Id = "global";
        settings.UpdatedAt = DateTime.UtcNow;
        _db.GlobalAutoUpdateSettings.Upsert(settings);
        return settings;
    }

    public async Task<List<ContainerAutoUpdateConfig>> GetAllConfigsAsync()
    {
        // 读取前先对账，把指向已删除容器的僵尸配置按容器名修好
        await ReconcileStaleConfigsAsync();
        return _db.AutoUpdateConfigs.FindAll().ToList();
    }

    /// <summary>
    /// 向前端广播自动更新/回滚过程中的镜像拉取进度（复用 image-pull-progress 通道）。
    /// </summary>
    private async Task PushPullProgressAsync(string? pullId, string imageName, string step, int progress, string detail)
    {
        if (string.IsNullOrEmpty(pullId))
            return;
        await DockerPanelHub.BroadcastImagePullProgress(_hubContext, pullId, imageName, step, progress, detail);
    }

    private async Task UpdateCheckConfigAsync(string containerId, ImageUpdateCheckResult result, AutoUpdateStatus status, string? statusMessage)
    {
        var config = await GetConfigAsync(containerId);
        if (config == null)
        {
            return;
        }

        config.LastCheckTime = DateTime.UtcNow;
        config.LastRemoteDigest = result.RemoteDigest;
        config.CurrentLocalDigest = result.CurrentDigest;
        config.HasUpdateAvailable = result.HasUpdate;
        config.Status = status;
        config.StatusMessage = statusMessage;
        config.UpdatedAt = DateTime.UtcNow;
        _db.AutoUpdateConfigs.Update(config);
    }

    /// <summary>
    /// 获取远程镜像摘要
    /// </summary>
    private async Task<string?> GetRemoteImageDigestAsync(string imageName)
    {
        try
        {
            // 解析镜像名称
            var (registry, name, tag) = ParseImageName(imageName);
            
            // Docker Hub 镜像，尝试使用加速器
            if (string.IsNullOrEmpty(registry) || registry == "docker.io" || registry == "index.docker.io")
            {
                // 先尝试通过加速器查询
                var mirrorDigest = await GetDigestViaMirrorAsync(name, tag);
                if (!string.IsNullOrEmpty(mirrorDigest))
                {
                    _logger.LogInformation("通过加速器获取镜像摘要成功: {Image}", imageName);
                    return mirrorDigest;
                }
                
                // 加速器失败，尝试直连 Docker Hub
                _logger.LogWarning("加速器查询失败，尝试直连 Docker Hub: {Image}", imageName);
                return await GetDigestFromDockerHubAsync(name, tag);
            }
            else
            {
                // 私有仓库，直接查询
                return await GetDigestFromPrivateRegistryAsync(registry, name, tag);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取远程镜像摘要失败: {Image}", imageName);
            return null;
        }
    }

    /// <summary>
    /// 通过加速器获取镜像摘要
    /// </summary>
    private async Task<string?> GetDigestViaMirrorAsync(string name, string tag)
    {
        try
        {
            // 获取默认加速器
            var registries = await _registryService.GetRegistriesAsync();
            var mirror = registries.FirstOrDefault(r => r.Type == "Mirror" && r.IsDefault);
            
            if (mirror == null)
            {
                _logger.LogDebug("未配置默认加速器");
                return null;
            }

            _logger.LogInformation("尝试通过加速器 {Mirror} 查询镜像摘要", mirror.Name);
            
            // 大多数加速器不支持 Registry API，但我们尝试查询
            // 镜像路径格式: mirror.domain/library/name 或 mirror.domain/name
            var mirrorPath = name.Contains('/') 
                ? $"{mirror.Domain}/{name}" 
                : $"{mirror.Domain}/library/{name}";
            
            var manifestUrl = $"https://{mirrorPath}/manifests/{tag}";
            var request = new HttpRequestMessage(HttpMethod.Head, manifestUrl);
            request.Headers.Add("Accept", "application/vnd.docker.distribution.manifest.v2+json");
            
            // 如果加速器配置了认证
            if (!string.IsNullOrEmpty(mirror.Username) && !string.IsNullOrEmpty(mirror.Password))
            {
                var authBytes = Encoding.UTF8.GetBytes($"{mirror.Username}:{mirror.Password}");
                request.Headers.Add("Authorization", $"Basic {Convert.ToBase64String(authBytes)}");
            }
            
            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                if (response.Headers.TryGetValues("Docker-Content-Digest", out var digests))
                {
                    return digests.FirstOrDefault();
                }
            }
            
            _logger.LogDebug("加速器 {Mirror} 不支持 Registry API 或查询失败", mirror.Name);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "通过加速器查询镜像摘要失败");
            return null;
        }
    }

    /// <summary>
    /// 从 Docker Hub 获取镜像摘要
    /// </summary>
    private async Task<string?> GetDigestFromDockerHubAsync(string name, string tag)
    {
        try
        {
            // 先获取 token
            var tokenUrl = name.Contains('/')
                ? $"https://auth.docker.io/token?service=registry.docker.io&scope=repository:{name}:pull"
                : $"https://auth.docker.io/token?service=registry.docker.io&scope=repository:library/{name}:pull";
            
            var tokenResponse = await FetchDockerAuthTokenAsync(tokenUrl);
            if (tokenResponse?.Token == null) return null;

            // 获取 manifest
            var manifestUrl = name.Contains('/')
                ? $"https://registry.hub.docker.com/v2/{name}/manifests/{tag}"
                : $"https://registry.hub.docker.com/v2/library/{name}/manifests/{tag}";
            
            var request = new HttpRequestMessage(HttpMethod.Head, manifestUrl);
            request.Headers.Add("Authorization", $"Bearer {tokenResponse.Token}");
            request.Headers.Add("Accept", "application/vnd.docker.distribution.manifest.v2+json");
            
            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                if (response.Headers.TryGetValues("Docker-Content-Digest", out var digests))
                {
                    return digests.FirstOrDefault();
                }
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "直连 Docker Hub 获取镜像摘要失败");
            return null;
        }
    }

    /// <summary>
    /// 获取 Docker Hub 认证 Token（源生成上下文 + 大小写不敏感，AOT 兼容）。
    /// </summary>
    private async Task<DockerAuthToken?> FetchDockerAuthTokenAsync(string tokenUrl)
    {
        var content = await _httpClient.GetStringAsync(tokenUrl);
        return JsonSerializer.Deserialize<DockerAuthToken>(content,
            Serialization.DockerHubJsonContext.Default.DockerAuthToken);
    }

    /// <summary>
    /// 从私有仓库获取镜像摘要
    /// </summary>
    private async Task<string?> GetDigestFromPrivateRegistryAsync(string registry, string name, string tag)
    {
        try
        {
            // 尝试从配置的私有仓库获取认证信息
            var privateRegistry = await FindRegistryByDomainAsync(registry);
            return await GetRegistryContentDigestAsync(privateRegistry, registry, name, tag);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "从私有仓库 {Registry} 获取镜像摘要失败", registry);
            return null;
        }
    }

    private (string? registry, string name, string tag) ParseImageName(string imageName)
    {
        string? registry = null;
        string name;
        string tag = "latest";
        
        // 解析 registry
        var parts = imageName.Split('/');
        if (parts.Length > 1 && (parts[0].Contains('.') || parts[0].Contains(':')))
        {
            registry = parts[0];
            name = string.Join("/", parts.Skip(1));
        }
        else
        {
            name = imageName;
        }
        
        // 解析 tag
        var lastColon = name.LastIndexOf(':');
        if (lastColon > 0)
        {
            tag = name.Substring(lastColon + 1);
            name = name.Substring(0, lastColon);
        }
        
        return (registry, name, tag);
    }

    /// <summary>
    /// 获取镜像的所有可用标签
    /// </summary>
    public async Task<List<string>> GetImageTagsAsync(string imageName)
    {
        try
        {
            var (registry, name, _) = ParseImageName(imageName);
            
            // Docker Hub
            if (string.IsNullOrEmpty(registry) || registry == "docker.io" || registry == "index.docker.io")
            {
                // 先获取 token
                var tokenUrl = name.Contains('/')
                    ? $"https://auth.docker.io/token?service=registry.docker.io&scope=repository:{name}:pull"
                    : $"https://auth.docker.io/token?service=registry.docker.io&scope=repository:library/{name}:pull";
                
                var tokenResponse = await FetchDockerAuthTokenAsync(tokenUrl);
                if (tokenResponse?.Token == null) return new List<string>();
                
                // 获取标签列表
                var tagsUrl = name.Contains('/')
                    ? $"https://registry.hub.docker.com/v2/{name}/tags/list"
                    : $"https://registry.hub.docker.com/v2/library/{name}/tags/list";
                
                var request = new HttpRequestMessage(HttpMethod.Get, tagsUrl);
                request.Headers.Add("Authorization", $"Bearer {tokenResponse.Token}");
                
                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var tagsResult = JsonSerializer.Deserialize<DockerTagsResponse>(content, Serialization.DockerHubJsonContext.Default.DockerTagsResponse);
                    return tagsResult?.Tags?.OrderByDescending(t => t).ToList() ?? new List<string>();
                }
            }
            else
            {
                // 私有仓库
                var privateRegistry = await FindRegistryByDomainAsync(registry);
                var response = await SendRegistryRequestAsync(privateRegistry, HttpMethod.Get, registry, $"/v2/{name}/tags/list", name);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var tagsResult = JsonSerializer.Deserialize<DockerTagsResponse>(content, Serialization.DockerHubJsonContext.Default.DockerTagsResponse);
                    return tagsResult?.Tags?.OrderByDescending(t => t).ToList() ?? new List<string>();
                }
            }
            
            return new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取镜像标签失败: {Image}", imageName);
            return new List<string>();
        }
    }

    private async Task<ImageRegistry?> FindRegistryByDomainAsync(string registry)
    {
        var registries = await _registryService.GetRegistriesAsync();
        return registries.FirstOrDefault(r =>
            NormalizeRegistryDomain(r.Domain).Equals(NormalizeRegistryDomain(registry), StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(r.Type, "Mirror", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<string?> GetRegistryContentDigestAsync(ImageRegistry? registryConfig, string registry, string name, string tag)
    {
        var path = $"/v2/{name}/manifests/{tag}";
        var response = await SendRegistryRequestAsync(registryConfig, HttpMethod.Head, registry, path, name, addManifestAccept: true);
        if (!response.IsSuccessStatusCode || !response.Headers.TryGetValues("Docker-Content-Digest", out var digests))
        {
            response.Dispose();
            response = await SendRegistryRequestAsync(registryConfig, HttpMethod.Get, registry, path, name, addManifestAccept: true);
        }

        return response.IsSuccessStatusCode && response.Headers.TryGetValues("Docker-Content-Digest", out digests)
            ? digests.FirstOrDefault()
            : null;
    }

    private async Task<HttpResponseMessage> SendRegistryRequestAsync(
        ImageRegistry? registryConfig,
        HttpMethod method,
        string registry,
        string path,
        string repository,
        bool addManifestAccept = false)
    {
        var request = CreateRegistryRequest(registryConfig, method, registry, path, addManifestAccept);
        var response = await _httpClient.SendAsync(request);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        var challenge = response.Headers.WwwAuthenticate.FirstOrDefault(h =>
            h.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase));
        if (challenge == null)
        {
            return response;
        }

        var token = await GetRegistryBearerTokenAsync(challenge, registryConfig, repository);
        if (string.IsNullOrEmpty(token))
        {
            return response;
        }

        response.Dispose();
        request = CreateRegistryRequest(registryConfig, method, registry, path, addManifestAccept);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _httpClient.SendAsync(request);
    }

    private HttpRequestMessage CreateRegistryRequest(ImageRegistry? registryConfig, HttpMethod method, string registry, string path, bool addManifestAccept)
    {
        var scheme = registryConfig?.IsSecure == false ? "http" : "https";
        var request = new HttpRequestMessage(method, $"{scheme}://{NormalizeRegistryDomain(registry)}{path}");

        if (addManifestAccept)
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.index.v1+json"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.list.v2+json"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.manifest.v1+json"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.v2+json"));
        }

        if (registryConfig != null && !string.IsNullOrEmpty(registryConfig.Username) && !string.IsNullOrEmpty(registryConfig.Password))
        {
            var authBytes = Encoding.UTF8.GetBytes($"{registryConfig.Username}:{registryConfig.Password}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        }

        return request;
    }

    private async Task<string?> GetRegistryBearerTokenAsync(AuthenticationHeaderValue challenge, ImageRegistry? registryConfig, string repository)
    {
        var parameters = ParseAuthenticateParameters(challenge.Parameter);
        if (!parameters.TryGetValue("realm", out var realm) || string.IsNullOrWhiteSpace(realm))
        {
            return null;
        }

        var query = new List<string>();
        if (parameters.TryGetValue("service", out var service) && !string.IsNullOrWhiteSpace(service))
        {
            query.Add($"service={Uri.EscapeDataString(service)}");
        }

        var scope = parameters.TryGetValue("scope", out var parsedScope) && !string.IsNullOrWhiteSpace(parsedScope)
            ? parsedScope
            : $"repository:{repository}:pull";
        query.Add($"scope={Uri.EscapeDataString(scope)}");

        var tokenUrl = query.Count > 0 ? $"{realm}?{string.Join("&", query)}" : realm;
        using var request = new HttpRequestMessage(HttpMethod.Get, tokenUrl);
        if (registryConfig != null && !string.IsNullOrEmpty(registryConfig.Username) && !string.IsNullOrEmpty(registryConfig.Password))
        {
            var authBytes = Encoding.UTF8.GetBytes($"{registryConfig.Username}:{registryConfig.Password}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        }

        using var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);
        if (json.RootElement.TryGetProperty("token", out var tokenElement))
        {
            return tokenElement.GetString();
        }

        return json.RootElement.TryGetProperty("access_token", out var accessTokenElement)
            ? accessTokenElement.GetString()
            : null;
    }

    private static Dictionary<string, string> ParseAuthenticateParameters(string? parameter)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(parameter))
        {
            return result;
        }

        foreach (var part in parameter.Split(','))
        {
            var keyValue = part.Split('=', 2);
            if (keyValue.Length == 2)
            {
                result[keyValue[0].Trim()] = keyValue[1].Trim().Trim('"');
            }
        }

        return result;
    }

    private static string NormalizeRegistryDomain(string registry)
    {
        return registry
            .Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("http://", string.Empty, StringComparison.OrdinalIgnoreCase)
            .TrimEnd('/');
    }

    private static string NormalizeDigest(string digest)
    {
        var atIndex = digest.LastIndexOf('@');
        if (atIndex >= 0 && atIndex < digest.Length - 1)
        {
            digest = digest.Substring(atIndex + 1);
        }

        return digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
            ? digest
            : $"sha256:{digest}";
    }

    private static string? GetComparableLocalDigest(ImageInfo image, string imageName)
    {
        var repository = imageName.Contains(':') ? imageName.Substring(0, imageName.LastIndexOf(':')) : imageName;
        var digest = image.RepoDigests.FirstOrDefault(d => d.StartsWith(repository + "@", StringComparison.OrdinalIgnoreCase))
            ?? image.RepoDigests.FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(digest))
        {
            return NormalizeDigest(digest);
        }

        return string.IsNullOrWhiteSpace(image.Digest) ? null : NormalizeDigest(image.Digest);
    }

    /// <summary>
    /// 回滚容器到指定镜像标签
    /// </summary>
    public async Task<UpdateResult> RollbackContainerAsync(string containerId, string targetTag)
    {
        var result = new UpdateResult { ContainerId = containerId };
        var startTime = DateTime.UtcNow;
        
        try
        {
            var container = await _containerEngine.GetContainerAsync(containerId);
            if (container == null)
            {
                result.ErrorMessage = "容器不存在";
                return result;
            }
            
            var config = await GetConfigAsync(containerId);
            
            // 解析镜像名称
            // 同样不能用 Split(':')：镜像以 ID 启动时拿到的是 sha256:...，
            // 会切出 name=sha256 这种不存在的仓库。
            var imageName = await ResolvePullableImageNameAsync(container.Image);
            if (string.IsNullOrWhiteSpace(imageName))
            {
                result.ErrorMessage = "无法确定可拉取的镜像名：容器可能是用镜像 ID 启动的，且本地已没有对应 tag";
                return result;
            }
            var (name, _) = ImageReferenceResolver.Split(imageName);
            
            var pullId = $"rollback-{containerId}";

            // 广播进度：开始拉取目标标签镜像
            await PushPullProgressAsync(pullId, $"{name}:{targetTag}", "准备中", 5, $"正在拉取镜像 {targetTag}...");

            // 拉取目标标签的镜像
            _logger.LogInformation("回滚容器 {ContainerId} 到镜像 {Image}:{Tag}", containerId, name, targetTag);
            var pullProgress = DockerPanelHub.CreatePullProgressBroadcaster(_hubContext, pullId, $"{name}:{targetTag}");
            await _containerEngine.PullImageAsync(name, targetTag, pullProgress);
            
            // 获取新镜像摘要
            var newImage = await _containerEngine.GetImageAsync($"{name}:{targetTag}");
            result.NewDigest = newImage?.Id;
            result.OldDigest = config?.CurrentLocalDigest;

            // 广播进度：重新创建容器
            await PushPullProgressAsync(pullId, $"{name}:{targetTag}", "重启中", 90, "正在重建容器并重新启动...");

            // 真正回滚：删除原容器并用 targetTag 镜像按原配置重建，使容器切换到目标版本
            await _containerService.RecreateContainerAsync(containerId, pullLatest: false, autoStart: true, overrideImage: $"{name}:{targetTag}");

            // 广播进度：完成
            await PushPullProgressAsync(pullId, $"{name}:{targetTag}", "完成", 100, "回滚完成");
            
            // 更新配置
            if (config != null)
            {
                config.CurrentLocalDigest = result.NewDigest;
                config.HasUpdateAvailable = false;
                config.Status = AutoUpdateStatus.UpToDate;
                config.StatusMessage = $"已回滚到 {targetTag}";
                config.UpdatedAt = DateTime.UtcNow;
                
                // 添加记录
                config.UpdateHistory.Add(new ContainerUpdateRecord
                {
                    Time = DateTime.UtcNow,
                    Action = UpdateAction.Pull,
                    Success = true,
                    OldDigest = result.OldDigest,
                    NewDigest = result.NewDigest,
                    DurationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds
                });
                
                _db.AutoUpdateConfigs.Update(config);
            }
            
            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "回滚容器失败: {ContainerId}", containerId);
            result.ErrorMessage = ex.Message;

            // 广播失败状态，让前端收起进度
            await PushPullProgressAsync($"rollback-{containerId}", "", "失败", 100, $"回滚失败: {ex.Message}");
            
            // 更新状态
            var config = await GetConfigAsync(containerId);
            if (config != null)
            {
                config.Status = AutoUpdateStatus.UpdateFailed;
                config.StatusMessage = $"回滚失败: {ex.Message}";
                config.UpdatedAt = DateTime.UtcNow;
                _db.AutoUpdateConfigs.Update(config);
            }
            
            return result;
        }
    }
}

/// <summary>
/// Docker Hub 标签列表响应
/// </summary>
internal class DockerTagsResponse
{
    public string? Name { get; set; }
    public List<string>? Tags { get; set; }
}

/// <summary>
/// Docker Hub 认证 Token 响应
/// </summary>
internal class DockerAuthToken
{
    public string? Token { get; set; }
}
