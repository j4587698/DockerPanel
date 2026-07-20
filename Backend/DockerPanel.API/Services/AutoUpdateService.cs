using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DockerPanel.API.Data;
using DockerPanel.API.Models;
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
    private readonly IRegistryService _registryService;
    private readonly ILogger<AutoUpdateService> _logger;
    private readonly HttpClient _httpClient;

    public AutoUpdateService(
        TinyDbContext db,
        IContainerEngine containerEngine,
        IRegistryService registryService,
        ILogger<AutoUpdateService> logger)
    {
        _db = db;
        _containerEngine = containerEngine;
        _registryService = registryService;
        _logger = logger;
        
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
        return _db.AutoUpdateConfigs.FindOne(c => c.ContainerId == containerId);
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
            
            result.CurrentDigest = localImage.Id;
            
            // 获取远程镜像摘要
            var imageName = container.Image ?? string.Empty;
            var remoteDigest = !string.IsNullOrEmpty(imageName) 
                ? await GetRemoteImageDigestAsync(imageName) 
                : null;
            result.RemoteDigest = remoteDigest;
            
            // 比较摘要
            if (!string.IsNullOrEmpty(remoteDigest) && !string.IsNullOrEmpty(localImage.Id))
            {
                // 提取摘要部分（去掉 sha256: 前缀进行比较）
                var localDigestPart = localImage.Id.Contains("sha256:") 
                    ? localImage.Id.Substring(localImage.Id.IndexOf("sha256:")) 
                    : localImage.Id;
                var remoteDigestPart = remoteDigest.Contains("sha256:") 
                    ? remoteDigest 
                    : $"sha256:{remoteDigest}";
                
                result.HasUpdate = !localDigestPart.Equals(remoteDigestPart, StringComparison.OrdinalIgnoreCase) &&
                                   !localDigestPart.Contains(remoteDigest.Substring(Math.Min(8, remoteDigest.Length)));
            }
            
            // 更新配置
            var config = await GetConfigAsync(containerId);
            if (config != null)
            {
                config.LastCheckTime = DateTime.UtcNow;
                config.LastRemoteDigest = result.RemoteDigest;
                config.CurrentLocalDigest = result.CurrentDigest;
                config.HasUpdateAvailable = result.HasUpdate;
                config.Status = result.HasUpdate ? AutoUpdateStatus.UpdateAvailable : AutoUpdateStatus.UpToDate;
                config.StatusMessage = result.HasUpdate ? "有新版本可用" : "已是最新版本";
                config.UpdatedAt = DateTime.UtcNow;
                _db.AutoUpdateConfigs.Update(config);
            }
            
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
            
            var result = await CheckUpdateAsync(config.ContainerId);
            results.Add(result);
            
            // 更新状态为检测中
            config.Status = AutoUpdateStatus.Checking;
            config.UpdatedAt = DateTime.UtcNow;
            _db.AutoUpdateConfigs.Update(config);
        }
        
        return results;
    }

    public async Task<UpdateResult> UpdateContainerAsync(string containerId, bool pullOnly = false)
    {
        var result = new UpdateResult { ContainerId = containerId };
        var startTime = DateTime.UtcNow;
        
        try
        {
            var config = await GetConfigAsync(containerId);
            if (config == null)
            {
                result.ErrorMessage = "未找到自动升级配置";
                return result;
            }
            
            var container = await _containerEngine.GetContainerAsync(containerId);
            if (container == null)
            {
                result.ErrorMessage = "容器不存在";
                return result;
            }
            
            // 更新状态为正在拉取
            config.Status = AutoUpdateStatus.Pulling;
            config.UpdatedAt = DateTime.UtcNow;
            _db.AutoUpdateConfigs.Update(config);
            
            // 拉取新镜像
            var imageName = container.Image ?? string.Empty;
            var imageParts = imageName.Split(':');
            var name = imageParts[0];
            var tag = imageParts.Length > 1 ? imageParts[1] : "latest";
            
            await _containerEngine.PullImageAsync(name, tag, new Progress<ImagePullProgress>(p =>
            {
                _logger.LogInformation("拉取进度: {Status}", p.Status);
            }));
            
            // 获取新镜像摘要
            var newImage = await _containerEngine.GetImageAsync(imageName);
            result.NewDigest = newImage?.Id;
            result.OldDigest = config.CurrentLocalDigest;
            
            if (!pullOnly && config.EnableAutoRestart)
            {
                // 更新状态为正在重启
                config.Status = AutoUpdateStatus.Restarting;
                _db.AutoUpdateConfigs.Update(config);
                
                // 重启容器
                await _containerEngine.RestartContainerAsync(containerId);
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
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新容器 {ContainerId} 失败", containerId);
            result.ErrorMessage = ex.Message;
            result.DurationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            
            // 更新失败状态
            var config = await GetConfigAsync(containerId);
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
        return _db.AutoUpdateConfigs.FindAll().ToList();
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
            
            var tokenResponse = await _httpClient.GetFromJsonAsync<DockerAuthToken>(tokenUrl);
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
    /// 从私有仓库获取镜像摘要
    /// </summary>
    private async Task<string?> GetDigestFromPrivateRegistryAsync(string registry, string name, string tag)
    {
        try
        {
            // 尝试从配置的私有仓库获取认证信息
            var registries = await _registryService.GetRegistriesAsync();
            var privateRegistry = registries.FirstOrDefault(r => 
                r.Domain.Equals(registry, StringComparison.OrdinalIgnoreCase) &&
                r.Type == "Private");
            
            var manifestUrl = $"https://{registry}/v2/{name}/manifests/{tag}";
            var request = new HttpRequestMessage(HttpMethod.Head, manifestUrl);
            request.Headers.Add("Accept", "application/vnd.docker.distribution.manifest.v2+json");
            
            // 如果配置了认证
            if (privateRegistry != null && 
                !string.IsNullOrEmpty(privateRegistry.Username) && 
                !string.IsNullOrEmpty(privateRegistry.Password))
            {
                var authBytes = Encoding.UTF8.GetBytes($"{privateRegistry.Username}:{privateRegistry.Password}");
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
            
            return null;
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
                
                var tokenResponse = await _httpClient.GetFromJsonAsync<DockerAuthToken>(tokenUrl);
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
                    var tagsResult = JsonSerializer.Deserialize<DockerTagsResponse>(content, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                    return tagsResult?.Tags?.OrderByDescending(t => t).ToList() ?? new List<string>();
                }
            }
            else
            {
                // 私有仓库
                var tagsUrl = $"https://{registry}/v2/{name}/tags/list";
                var request = new HttpRequestMessage(HttpMethod.Get, tagsUrl);
                
                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var tagsResult = JsonSerializer.Deserialize<DockerTagsResponse>(content, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
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
            var imageName = container.Image ?? string.Empty;
            var imageParts = imageName.Split(':');
            var name = imageParts[0];
            
            // 拉取目标标签的镜像
            _logger.LogInformation("回滚容器 {ContainerId} 到镜像 {Image}:{Tag}", containerId, name, targetTag);
            await _containerEngine.PullImageAsync(name, targetTag, new Progress<ImagePullProgress>(p =>
            {
                _logger.LogInformation("拉取进度: {Status}", p.Status);
            }));
            
            // 获取新镜像摘要
            var newImage = await _containerEngine.GetImageAsync($"{name}:{targetTag}");
            result.NewDigest = newImage?.Id;
            result.OldDigest = config?.CurrentLocalDigest;
            
            // 重启容器（重建方式）
            // 先停止容器
            await _containerEngine.StopContainerAsync(containerId);
            
            // 重启容器会使用新镜像
            await _containerEngine.RestartContainerAsync(containerId);
            
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
