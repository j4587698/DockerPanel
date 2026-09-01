using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using DockerPanel.API.Hubs;
using DockerPanel.API.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace DockerPanel.API.Services;

public interface ISelfUpdateService
{
    Task<SelfUpdateCheckResult> CheckUpdateAsync(bool forceRefresh = false, CancellationToken ct = default);
    Task<SelfUpgradeResponse> ExecuteSelfUpgradeAsync(SelfUpgradeRequest request, CancellationToken ct = default);
}

public class SelfUpdateCheckResult
{
    public string CurrentVersion { get; set; } = string.Empty;
    public string LatestVersion { get; set; } = string.Empty;
    public bool HasUpdate { get; set; }
    public string? ReleaseTitle { get; set; }
    public string? ReleaseNotes { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? HtmlUrl { get; set; }
    public bool CanSelfUpgrade { get; set; }
    public string? ContainerId { get; set; }
    public string? ContainerName { get; set; }
    public string? ImageName { get; set; }
    public string? Reason { get; set; }
}

public class SelfUpgradeRequest
{
    public string? TargetVersion { get; set; }
    public string? TargetImage { get; set; }
    public string? ConnectionId { get; set; }
}

public class SelfUpgradeResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string CurrentVersion { get; set; } = string.Empty;
    public string TargetVersion { get; set; } = string.Empty;
    public string? OldContainerId { get; set; }
}

public class GitHubReleaseDto
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("published_at")]
    public DateTime? PublishedAt { get; set; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; set; }
}

public class SelfUpdateService : ISelfUpdateService
{
    private readonly ILogger<SelfUpdateService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DockerEngine _dockerEngine;
    private readonly IHubContext<DockerPanelHub> _hubContext;

    private static SelfUpdateCheckResult? _cachedCheckResult;
    private static DateTime _lastCheckTime = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public SelfUpdateService(
        ILogger<SelfUpdateService> logger,
        IHttpClientFactory httpClientFactory,
        DockerEngine dockerEngine,
        IHubContext<DockerPanelHub> hubContext)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _dockerEngine = dockerEngine;
        _hubContext = hubContext;
    }

    public async Task<SelfUpdateCheckResult> CheckUpdateAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        var currentVersion = GetCurrentApplicationVersion();

        if (!forceRefresh && _cachedCheckResult != null && DateTime.UtcNow - _lastCheckTime < CacheDuration)
        {
            return _cachedCheckResult;
        }

        var result = new SelfUpdateCheckResult
        {
            CurrentVersion = currentVersion,
            LatestVersion = currentVersion,
            HasUpdate = false
        };

        // 1. 检查 Docker 容器运行环境与自身容器信息
        var selfContainer = await FindSelfContainerAsync(ct);
        if (selfContainer != null)
        {
            result.CanSelfUpgrade = true;
            result.ContainerId = selfContainer.ID;
            result.ContainerName = selfContainer.Names?.FirstOrDefault()?.TrimStart('/') ?? "dockerpanel";
            result.ImageName = selfContainer.Image;
        }
        else
        {
            result.CanSelfUpgrade = false;
            result.Reason = "未检测到 DockerPanel 容器化环境，请通过二进制或手动更新";
        }

        // 2. 从 GitHub Releases 获取最新版本
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "DockerPanel-SelfUpdate");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            var release = await httpClient.GetFromJsonAsync<GitHubReleaseDto>(
                "https://api.github.com/repos/j4587698/DockerPanel/releases/latest", ct);

            if (release != null && !string.IsNullOrWhiteSpace(release.TagName))
            {
                var latestTag = release.TagName.TrimStart('v', 'V').Trim();
                result.LatestVersion = latestTag;
                result.ReleaseTitle = release.Name ?? release.TagName;
                result.ReleaseNotes = release.Body ?? string.Empty;
                result.PublishedAt = release.PublishedAt;
                result.HtmlUrl = release.HtmlUrl;

                result.HasUpdate = IsNewerVersion(currentVersion, latestTag);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "检查 GitHub Releases 最新版本失败");
            if (string.IsNullOrEmpty(result.Reason))
            {
                result.Reason = $"检查新版本失败: {ex.Message}";
            }
        }

        _cachedCheckResult = result;
        _lastCheckTime = DateTime.UtcNow;
        return result;
    }

    public async Task<SelfUpgradeResponse> ExecuteSelfUpgradeAsync(SelfUpgradeRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("开始执行 DockerPanel 自身升级流程...");

        if (!await _dockerEngine.IsAvailableAsync())
        {
            throw new InvalidOperationException("Docker 引擎不可用，无法执行升级");
        }

        var client = await _dockerEngine.GetClientAsync();
        var selfContainer = await FindSelfContainerAsync(ct);
        if (selfContainer == null)
        {
            throw new InvalidOperationException("未能定位 DockerPanel 自身容器，无法通过 Sidecar 自动升级。请在宿主机执行升级脚本。");
        }

        var inspect = await client.Containers.InspectContainerAsync(selfContainer.ID, ct);
        var currentVersion = GetCurrentApplicationVersion();
        var targetVersion = request.TargetVersion;

        if (string.IsNullOrEmpty(targetVersion))
        {
            var check = await CheckUpdateAsync(forceRefresh: false, ct);
            targetVersion = check.LatestVersion;
        }

        // 确定目标镜像名称
        var currentImage = inspect.Config?.Image ?? "j4587698/dockerpanel:latest";
        var baseRepo = currentImage.Contains(':') ? currentImage.Substring(0, currentImage.LastIndexOf(':')) : currentImage;
        if (string.IsNullOrWhiteSpace(baseRepo) || baseRepo.Equals("dockerpanel", StringComparison.OrdinalIgnoreCase))
        {
            baseRepo = "j4587698/dockerpanel";
        }

        var targetImage = !string.IsNullOrEmpty(request.TargetImage)
            ? request.TargetImage
            : (!string.IsNullOrEmpty(targetVersion) ? $"{baseRepo}:v{targetVersion.TrimStart('v')}" : $"{baseRepo}:latest");

        _logger.LogInformation("自身升级参数: 当前容器={Id}({Name}), 当前镜像={CurImage}, 目标镜像={TargetImage}",
            selfContainer.ID, inspect.Name, currentImage, targetImage);

        // 1. 预拉取目标镜像（实时广播进度）
        var pullId = "self-upgrade";
        var progressBroadcaster = DockerPanelHub.CreatePullProgressBroadcaster(_hubContext, pullId, targetImage);
        try
        {
            await DockerPanelHub.BroadcastImagePullProgress(_hubContext, pullId, targetImage, "准备中", 5, "正在预拉取最新版本镜像...");
            await PullImageAsync(client, targetImage, progressBroadcaster, ct);
            await DockerPanelHub.BroadcastImagePullProgress(_hubContext, pullId, targetImage, "完成", 100, "镜像拉取完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "预拉取新镜像失败: {Image}", targetImage);
            throw new InvalidOperationException($"拉取新镜像 {targetImage} 失败: {ex.Message}");
        }

        // 2. 预拉取/确保 helper 容器镜像 docker:cli 可用
        var helperImage = "docker:cli";
        try
        {
            await PullImageAsync(client, helperImage, null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "拉取 helper 镜像 {Image} 失败，尝试继续", helperImage);
        }

        // 3. 构建容器运行命令参数
        var containerName = inspect.Name?.TrimStart('/') ?? "dockerpanel";
        var runArgs = BuildDockerRunArguments(inspect, targetImage, containerName);

        // 4. 组装 Helper 脚本
        // 延迟 2 秒让当前 HTTP 请求完整返回 200 响应
        var helperScript = $"sleep 2 && docker stop {selfContainer.ID} && docker rm {selfContainer.ID} && docker run -d --name {containerName} {runArgs}";
        _logger.LogInformation("生成的 Helper 升级命令: {Script}", helperScript);

        // 5. 启动 Helper 容器（挂载 docker.sock）
        var helperContainer = await client.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Image = helperImage,
            Name = $"dockerpanel-updater-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
            Entrypoint = new List<string> { "sh", "-c", helperScript },
            HostConfig = new HostConfig
            {
                AutoRemove = true,
                Binds = new List<string> { "/var/run/docker.sock:/var/run/docker.sock" }
            }
        }, ct);

        await client.Containers.StartContainerAsync(helperContainer.ID, new ContainerStartParameters(), ct);
        _logger.LogInformation("Helper 升级容器已启动: {Id}", helperContainer.ID);

        return new SelfUpgradeResponse
        {
            Success = true,
            Message = "升级指令已下发，面板服务正在进行重启交接，请稍候约 5~10 秒刷新页面...",
            CurrentVersion = currentVersion,
            TargetVersion = targetVersion ?? "latest",
            OldContainerId = selfContainer.ID
        };
    }

    private async Task<ContainerListResponse?> FindSelfContainerAsync(CancellationToken ct)
    {
        try
        {
            if (!await _dockerEngine.IsAvailableAsync()) return null;
            var client = await _dockerEngine.GetClientAsync();
            var containers = await client.Containers.ListContainersAsync(new ContainersListParameters { All = true }, ct);

            // 1. 通过环境变量指定的 CONTAINER_NAME 或 DOCKERPANEL_CONTAINER_ID 匹配
            var envId = Environment.GetEnvironmentVariable("DOCKERPANEL_CONTAINER_ID");
            if (!string.IsNullOrEmpty(envId))
            {
                var match = containers.FirstOrDefault(c => c.ID.StartsWith(envId, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            // 2. 通过主机名 (Hostname) 匹配（默认容器主机名为容器短ID）
            var hostname = Environment.MachineName;
            if (!string.IsNullOrEmpty(hostname))
            {
                var match = containers.FirstOrDefault(c => c.ID.StartsWith(hostname, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            // 3. 通过容器名称包含 dockerpanel 匹配
            var byName = containers.FirstOrDefault(c => c.Names != null && c.Names.Any(n =>
                n.TrimStart('/').Equals("dockerpanel", StringComparison.OrdinalIgnoreCase) ||
                n.TrimStart('/').Contains("dockerpanel", StringComparison.OrdinalIgnoreCase)));
            if (byName != null) return byName;

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "查找 DockerPanel 自身容器信息失败");
            return null;
        }
    }

    private static async Task PullImageAsync(DockerClient client, string imageName, IProgress<ImagePullProgress>? progress, CancellationToken ct)
    {
        var (repo, tag) = ParseImageName(imageName);
        await client.Images.CreateImageAsync(new ImagesCreateParameters
        {
            FromImage = repo,
            Tag = tag
        }, new AuthConfig(), progress != null ? new Progress<JSONMessage>(msg =>
        {
            progress.Report(new ImagePullProgress
            {
                Id = msg.ID ?? string.Empty,
                Status = msg.Status ?? "Pulling",
                ProgressDetail = msg.Progress?.ToString() ?? string.Empty,
                Current = msg.Progress?.Current ?? 0,
                Total = msg.Progress?.Total ?? 0
            });
        }) : new Progress<JSONMessage>(_ => { }), ct);
    }

    private static (string Repository, string Tag) ParseImageName(string fullImage)
    {
        if (string.IsNullOrWhiteSpace(fullImage)) return ("j4587698/dockerpanel", "latest");
        var colon = fullImage.LastIndexOf(':');
        if (colon > 0 && !fullImage.Substring(colon).Contains('/'))
        {
            return (fullImage.Substring(0, colon), fullImage.Substring(colon + 1));
        }
        return (fullImage, "latest");
    }

    private static string BuildDockerRunArguments(ContainerInspectResponse inspect, string targetImage, string containerName)
    {
        var sb = new StringBuilder();

        // 1. 重启策略
        var restartKind = inspect.HostConfig?.RestartPolicy?.Name;
        var restartStr = restartKind switch
        {
            RestartPolicyKind.UnlessStopped => "unless-stopped",
            RestartPolicyKind.Always => "always",
            RestartPolicyKind.OnFailure => "on-failure",
            _ => "unless-stopped"
        };
        sb.Append($"--restart {restartStr} ");

        // 2. 端口映射 (若非 host 网络)
        var isHostNet = string.Equals(inspect.HostConfig?.NetworkMode, "host", StringComparison.OrdinalIgnoreCase);
        if (isHostNet)
        {
            sb.Append("--net host ");
        }
        else
        {
            if (inspect.HostConfig?.PortBindings != null)
            {
                foreach (var (containerPort, bindings) in inspect.HostConfig.PortBindings)
                {
                    if (bindings != null)
                    {
                        foreach (var b in bindings)
                        {
                            var hostPort = b.HostPort;
                            var hostIp = b.HostIP;
                            if (!string.IsNullOrEmpty(hostPort))
                            {
                                if (!string.IsNullOrEmpty(hostIp) && hostIp != "0.0.0.0" && hostIp != "::")
                                {
                                    sb.Append($"-p {hostIp}:{hostPort}:{containerPort} ");
                                }
                                else
                                {
                                    sb.Append($"-p {hostPort}:{containerPort} ");
                                }
                            }
                        }
                    }
                }
            }
        }

        // 3. 数据卷挂载 (Binds)
        if (inspect.HostConfig?.Binds != null)
        {
            foreach (var bind in inspect.HostConfig.Binds)
            {
                if (!string.IsNullOrWhiteSpace(bind))
                {
                    sb.Append($"-v \"{bind}\" ");
                }
            }
        }

        // 4. 环境变量
        if (inspect.Config?.Env != null)
        {
            foreach (var env in inspect.Config.Env)
            {
                if (!string.IsNullOrWhiteSpace(env) && !env.StartsWith("HOSTNAME=") && !env.StartsWith("SHLVL="))
                {
                    // 转义双引号
                    var escaped = env.Replace("\"", "\\\"");
                    sb.Append($"-e \"{escaped}\" ");
                }
            }
        }

        // 5. 目标镜像
        sb.Append(targetImage);

        return sb.ToString().Trim();
    }

    private static string GetCurrentApplicationVersion()
    {
        var assembly = typeof(SelfUpdateService).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? "0.9.5";
        return version.TrimStart('v', 'V').Split('+')[0];
    }

    private static bool IsNewerVersion(string currentVersion, string latestVersion)
    {
        if (string.IsNullOrWhiteSpace(latestVersion)) return false;
        if (string.Equals(currentVersion, latestVersion, StringComparison.OrdinalIgnoreCase)) return false;

        if (System.Version.TryParse(CleanVersionString(currentVersion), out var cur) &&
            System.Version.TryParse(CleanVersionString(latestVersion), out var lat))
        {
            return lat > cur;
        }

        return !string.Equals(currentVersion, latestVersion, StringComparison.OrdinalIgnoreCase);
    }

    private static string CleanVersionString(string v)
    {
        v = v.TrimStart('v', 'V').Trim().Split('-')[0].Split('+')[0];
        var parts = v.Split('.');
        if (parts.Length == 1) return $"{parts[0]}.0.0";
        if (parts.Length == 2) return $"{parts[0]}.{parts[1]}.0";
        return v;
    }
}
