using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
    public string? ImageName { get; set; }
    public string? CurrentDigest { get; set; }
    public string? RemoteDigest { get; set; }
    public bool HasUpdate { get; set; }
    public bool CanSelfUpgrade { get; set; }
    public string? ContainerId { get; set; }
    public string? ContainerName { get; set; }
    public string? Reason { get; set; }
    public DateTime CheckTime { get; set; } = DateTime.UtcNow;
}

public class SelfUpgradeRequest
{
    public string? TargetImage { get; set; }
    public string? ConnectionId { get; set; }
}

public class SelfUpgradeResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string CurrentVersion { get; set; } = string.Empty;
    public string TargetImage { get; set; } = string.Empty;
    public string? OldContainerId { get; set; }
}

public class SelfUpdateService : ISelfUpdateService
{
    private readonly ILogger<SelfUpdateService> _logger;
    private readonly IAutoUpdateService _autoUpdateService;
    private readonly DockerEngine _dockerEngine;
    private readonly IHubContext<DockerPanelHub> _hubContext;

    private static SelfUpdateCheckResult? _cachedCheckResult;
    private static DateTime _lastCheckTime = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public SelfUpdateService(
        ILogger<SelfUpdateService> logger,
        IAutoUpdateService autoUpdateService,
        DockerEngine dockerEngine,
        IHubContext<DockerPanelHub> hubContext)
    {
        _logger = logger;
        _autoUpdateService = autoUpdateService;
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
            HasUpdate = false,
            CheckTime = DateTime.UtcNow
        };

        // 1. 查找自身运行中的 Docker 容器
        var selfContainer = await FindSelfContainerAsync(ct);
        if (selfContainer == null)
        {
            result.CanSelfUpgrade = false;
            result.Reason = "未检测到 DockerPanel 容器化环境，请通过二进制或手动方式更新";
            _cachedCheckResult = result;
            _lastCheckTime = DateTime.UtcNow;
            return result;
        }

        result.CanSelfUpgrade = true;
        result.ContainerId = selfContainer.ID;
        result.ContainerName = selfContainer.Names?.FirstOrDefault()?.TrimStart('/') ?? "dockerpanel";
        result.ImageName = selfContainer.Image;

        // 2. 复用现有的 AutoUpdateService 镜像摘要检测机制（Registry / Mirrors）
        try
        {
            var imageCheck = await _autoUpdateService.CheckUpdateAsync(selfContainer.ID);
            result.HasUpdate = imageCheck.HasUpdate;
            result.CurrentDigest = imageCheck.CurrentDigest;
            result.RemoteDigest = imageCheck.RemoteDigest;

            if (!string.IsNullOrEmpty(imageCheck.ErrorMessage) && !imageCheck.HasUpdate)
            {
                result.Reason = imageCheck.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "通过 AutoUpdateService 检测自身镜像更新失败");
            result.Reason = $"检测镜像摘要失败: {ex.Message}";
        }

        _cachedCheckResult = result;
        _lastCheckTime = DateTime.UtcNow;
        return result;
    }

    public async Task<SelfUpgradeResponse> ExecuteSelfUpgradeAsync(SelfUpgradeRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("开始执行 DockerPanel 自身升级流程 (基于镜像 Registry/Digest 机制)...");

        if (!await _dockerEngine.IsAvailableAsync())
        {
            throw new InvalidOperationException("Docker 引擎不可用，无法执行升级");
        }

        var client = await _dockerEngine.GetClientAsync();
        var selfContainer = await FindSelfContainerAsync(ct);
        if (selfContainer == null)
        {
            throw new InvalidOperationException("未能定位 DockerPanel 自身容器，无法通过 Sidecar 自动升级。请在宿主机执行升级命令。");
        }

        var inspect = await client.Containers.InspectContainerAsync(selfContainer.ID, ct);
        var currentVersion = GetCurrentApplicationVersion();

        // 目标镜像默认使用当前容器镜像名（如 j4587698/dockerpanel:latest 或私有仓库镜像名）
        var targetImage = !string.IsNullOrWhiteSpace(request.TargetImage)
            ? request.TargetImage.Trim()
            : (!string.IsNullOrWhiteSpace(selfContainer.Image) ? selfContainer.Image : "j4587698/dockerpanel:latest");

        _logger.LogInformation("自身升级参数: 当前容器={Id}({Name}), 目标镜像={TargetImage}",
            selfContainer.ID, inspect.Name, targetImage);

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

        // 2. 确保 helper 容器镜像 docker:cli 可用
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

        // 4. 组装 Helper 脚本（延迟 2 秒让当前 HTTP 响应完整返回）
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
            TargetImage = targetImage,
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
               ?? "0.9.6";
        return version.TrimStart('v', 'V').Split('+')[0];
    }
}
