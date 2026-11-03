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

    /// <summary>
    /// 升级前置校验：确认 Docker 可用、能定位自身容器、算好目标镜像与重启参数。
    /// 全部是秒级操作，失败可以直接反馈给用户，不会留下半完成状态。
    /// </summary>
    Task<SelfUpgradePlan> PrepareSelfUpgradeAsync(SelfUpgradeRequest request, CancellationToken ct = default);

    /// <summary>
    /// 执行升级的耗时部分：拉镜像、启动 helper 容器。
    /// 由 <see cref="ISelfUpgradeJobManager"/> 在后台调用，令牌与 HTTP 请求无关。
    /// </summary>
    Task RunSelfUpgradeAsync(SelfUpgradePlan plan, ISelfUpgradeJobManager job, CancellationToken ct = default);
}

/// <summary>
/// 升级计划：前置校验的产物，交给后台任务执行。
/// </summary>
public sealed class SelfUpgradePlan
{
    public string TargetImage { get; init; } = string.Empty;
    public string CurrentVersion { get; init; } = string.Empty;
    public string SelfContainerId { get; init; } = string.Empty;
    public string ContainerName { get; init; } = string.Empty;
    public string RunArguments { get; init; } = string.Empty;
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

    public async Task<SelfUpgradePlan> PrepareSelfUpgradeAsync(SelfUpgradeRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("开始准备 DockerPanel 自身升级流程 (基于镜像 Registry/Digest 机制)...");

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

        // 目标镜像优先级：显式传入 > inspect 的 Config.Image（权威）> 列表的 Image > 默认值。
        // 容器可能以镜像 ID 或 digest 创建，此时这些字段都不是 repo:tag 形式，
        // 必须先解析成可拉取的名字，否则会去 pull 一个叫 sha256 的仓库。
        var targetImage = await ResolveTargetImageAsync(
            client,
            new[] { request.TargetImage, inspect.Config?.Image, selfContainer.Image },
            ct);

        var containerName = inspect.Name?.TrimStart('/') ?? "dockerpanel";
        var runArgs = BuildDockerRunArguments(inspect, targetImage, containerName);

        _logger.LogInformation("自身升级参数: 当前容器={Id}({Name}), 目标镜像={TargetImage}",
            selfContainer.ID, inspect.Name, targetImage);

        return new SelfUpgradePlan
        {
            TargetImage = targetImage,
            CurrentVersion = currentVersion,
            SelfContainerId = selfContainer.ID,
            ContainerName = containerName,
            RunArguments = runArgs
        };
    }

    public async Task RunSelfUpgradeAsync(SelfUpgradePlan plan, ISelfUpgradeJobManager job, CancellationToken ct = default)
    {
        var targetImage = plan.TargetImage;
        var client = await _dockerEngine.GetClientAsync();

        // 1. 预拉取目标镜像（实时广播进度）
        // 注意：这里必须区分「取消」与「失败」—— 取消是超时策略触发，直接透传让上层报告超时，
        // 不能包装成拉取失败，否则用户会以为镜像或网络有问题。
        var pullId = "self-upgrade";
        var progressBroadcaster = DockerPanelHub.CreatePullProgressBroadcaster(_hubContext, pullId, targetImage);
        job.Update(SelfUpgradePhase.Pulling, "正在预拉取最新版本镜像...", 5);
        await DockerPanelHub.BroadcastImagePullProgress(_hubContext, pullId, targetImage, "准备中", 5, "正在预拉取最新版本镜像...");

        try
        {
            await PullImageAsync(client, targetImage, progressBroadcaster, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "预拉取新镜像失败: {Image}", targetImage);
            throw new InvalidOperationException($"拉取新镜像 {targetImage} 失败: {ex.Message}", ex);
        }

        await DockerPanelHub.BroadcastImagePullProgress(_hubContext, pullId, targetImage, "完成", 100, "镜像拉取完成");
        job.Update(SelfUpgradePhase.Pulling, "镜像拉取完成，正在准备升级环境...", 85);

        // 2. 确保 helper 容器镜像 docker:cli 可用
        var helperImage = "docker:cli";
        try
        {
            await PullImageAsync(client, helperImage, null, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "拉取 helper 镜像 {Image} 失败，尝试继续", helperImage);
        }

        // 3. 组装 Helper 脚本
        // 接口已改为立即返回，前端拿到响应后才开始轮询状态；这里的延迟只为让
        // 「restarting」状态有时间被前端读到，不需要再等 HTTP 响应返回。
        var helperScript = $"sleep 2 && docker stop {plan.SelfContainerId} && docker rm {plan.SelfContainerId} && docker run -d --name {plan.ContainerName} {plan.RunArguments}";
        _logger.LogInformation("生成的 Helper 升级命令: {Script}", helperScript);

        // 4. 启动 Helper 容器（挂载 docker.sock）
        job.Update(SelfUpgradePhase.Pulling, "正在启动升级容器...", 95);
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

        job.Update(SelfUpgradePhase.Restarting, "升级指令已下发，面板服务正在重启...", 100);
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

    /// <summary>
    /// 从若干候选值中解析出真正可以 pull 的镜像名。
    /// 容器可能以镜像 ID 或 digest 启动，这些值直接拿去 pull 会得到
    /// 「pull access denied for sha256」这类错误。
    /// </summary>
    private async Task<string> ResolveTargetImageAsync(
        DockerClient client,
        IEnumerable<string?> candidates,
        CancellationToken ct)
    {
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;

            var resolved = await ImageReferenceResolver.ResolvePullableAsync(client, candidate, ct);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                if (!string.Equals(resolved, candidate.Trim(), StringComparison.Ordinal))
                {
                    _logger.LogInformation("镜像引用 {Original} 不是可拉取形式，已解析为 {Resolved}", candidate, resolved);
                }
                return resolved;
            }

            _logger.LogWarning("镜像引用 {Reference} 无法解析为可拉取的名字，尝试下一个来源", candidate);
        }

        throw new InvalidOperationException(
            "无法确定可拉取的镜像名：容器似乎是用镜像 ID 启动的，且本地已没有指向该镜像的 tag。" +
            "请在宿主机用 repo:tag 形式重新创建容器（例如 docker run --name dockerpanel j4587698/dockerpanel:latest），" +
            "或在升级时显式指定目标镜像。");
    }

    private static (string Repository, string Tag) ParseImageName(string fullImage)
    {
        if (string.IsNullOrWhiteSpace(fullImage)) return ("j4587698/dockerpanel", "latest");

        var (repository, tag) = ImageReferenceResolver.Split(fullImage);
        // digest 引用在上游已转成 repo:latest，这里的兜底只是防止空 tag 传到 Docker
        return (repository, string.IsNullOrEmpty(tag) ? "latest" : tag);
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

        // 2. 端口映射与网络 (若非 host 网络)
        var isHostNet = string.Equals(inspect.HostConfig?.NetworkMode, "host", StringComparison.OrdinalIgnoreCase);
        if (isHostNet)
        {
            sb.Append("--net host ");
        }
        else
        {
            var netName = inspect.HostConfig?.NetworkMode;
            if (!string.IsNullOrEmpty(netName) && netName != "default" && netName != "bridge")
            {
                sb.Append($"--network \"{netName}\" ");
            }
            else
            {
                var customNet = inspect.NetworkSettings?.Networks?.Keys?.FirstOrDefault(k => k != "bridge" && k != "host" && k != "none");
                if (!string.IsNullOrEmpty(customNet))
                {
                    sb.Append($"--network \"{customNet}\" ");
                }
            }

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
               ?? "0.0.0";
        return version.TrimStart('v', 'V').Split('+')[0];
    }
}
