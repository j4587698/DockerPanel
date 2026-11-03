using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DockerPanel.API.Services;

/// <summary>
/// 自升级任务阶段。前端按此决定弹窗展示与是否转入健康轮询。
/// </summary>
public static class SelfUpgradePhase
{
    /// <summary>没有进行中的升级任务</summary>
    public const string Idle = "idle";
    /// <summary>正在拉取镜像</summary>
    public const string Pulling = "pulling";
    /// <summary>升级指令已下发，容器即将/正在重启</summary>
    public const string Restarting = "restarting";
    /// <summary>升级失败（容器仍在运行，可重试）</summary>
    public const string Failed = "failed";
}

/// <summary>
/// 自升级任务状态。作为接口响应体返回，前端据此渲染进度与错误。
/// </summary>
public class SelfUpgradeJobStatus
{
    public string JobId { get; set; } = string.Empty;
    public string Phase { get; set; } = SelfUpgradePhase.Idle;
    public string Message { get; set; } = string.Empty;
    public int Progress { get; set; }
    public bool IsRunning { get; set; }
    public string? TargetImage { get; set; }
    public string? CurrentVersion { get; set; }
    public string? OldContainerId { get; set; }
    public string? Error { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    /// <summary>最后一次状态变更时间，用于判定 restarting 阶段是否已经卡死</summary>
    public DateTime? UpdatedAt { get; set; }
}

public interface ISelfUpgradeJobManager
{
    /// <summary>获取当前任务快照</summary>
    SelfUpgradeJobStatus GetStatus();

    /// <summary>
    /// 登记并启动一个后台升级任务。
    /// 返回 false 表示已有任务在执行，<paramref name="status"/> 为现有任务快照。
    /// </summary>
    bool TryStart(SelfUpgradePlan plan, out SelfUpgradeJobStatus status);

    /// <summary>更新当前任务状态（仅当任务仍在运行）</summary>
    void Update(string phase, string message, int progress);

    /// <summary>标记任务失败</summary>
    void Fail(string error);
}

/// <summary>
/// 自升级后台任务管理器。
///
/// 存在的理由：拉取镜像 + 重建容器是分钟级操作，原先直接绑定 HTTP 请求的
/// <c>RequestAborted</c>，用户刷新页面、代理超时都会让升级中途夭折。
/// 这里把耗时部分移出请求生命周期，用独立超时令牌执行，并保留状态供前端轮询，
/// 页面刷新后仍能看到进度或失败原因。
///
/// 状态保存在内存中，容器重启后自然清空 —— 重启本身即升级成功的信号，
/// 前端此时已转入健康检查轮询，不再依赖该状态。
/// </summary>
public class SelfUpgradeJobManager : ISelfUpgradeJobManager
{
    /// <summary>拉镜像 + 重建的总超时。够慢速网络拉一个多架构镜像，又不会永久卡住。</summary>
    private static readonly TimeSpan JobTimeout = TimeSpan.FromMinutes(30);

    /// <summary>
    /// restarting 阶段的最长存活时间。
    /// helper 容器里的 `docker stop` 若因权限或 dockerd 异常没生效，面板容器会继续运行，
    /// 状态就会永远停在 restarting —— 那样用户连重试都被挡住。超过这个时间即判定为卡死。
    /// </summary>
    private static readonly TimeSpan RestartingGrace = TimeSpan.FromMinutes(3);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SelfUpgradeJobManager> _logger;
    private readonly object _sync = new();

    private SelfUpgradeJobStatus _status = new() { Phase = SelfUpgradePhase.Idle };

    public SelfUpgradeJobManager(
        IServiceScopeFactory scopeFactory,
        ILogger<SelfUpgradeJobManager> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public SelfUpgradeJobStatus GetStatus()
    {
        lock (_sync)
        {
            ExpireStaleRestarting();
            return Clone(_status);
        }
    }

    /// <summary>
    /// 兜底：restarting 阶段超时未重启，判定为失败，允许用户重试。
    /// 必须在持锁时调用。
    /// </summary>
    private void ExpireStaleRestarting()
    {
        if (!_status.IsRunning || _status.Phase != SelfUpgradePhase.Restarting) return;

        var since = _status.UpdatedAt ?? _status.StartedAt;
        if (since == null || DateTime.UtcNow - since.Value < RestartingGrace) return;

        _status.Phase = SelfUpgradePhase.Failed;
        _status.Message = "升级失败";
        _status.Error = $"面板未在 {RestartingGrace.TotalMinutes:0} 分钟内重启，升级容器可能未成功执行。" +
                        "请检查 dockerpanel-updater-* 容器日志，或在宿主机手动执行 docker pull / docker run 完成升级。";
        _status.IsRunning = false;
        _status.FinishedAt = DateTime.UtcNow;
        _status.UpdatedAt = DateTime.UtcNow;

        _logger.LogError("自身升级在 restarting 阶段超时未重启，已标记为失败");
    }

    public bool TryStart(SelfUpgradePlan plan, out SelfUpgradeJobStatus status)
    {
        lock (_sync)
        {
            if (_status.IsRunning)
            {
                status = Clone(_status);
                return false;
            }

            _status = new SelfUpgradeJobStatus
            {
                JobId = Guid.NewGuid().ToString("N")[..12],
                Phase = SelfUpgradePhase.Pulling,
                Message = "正在预拉取最新版本镜像...",
                Progress = 5,
                IsRunning = true,
                TargetImage = plan.TargetImage,
                CurrentVersion = plan.CurrentVersion,
                OldContainerId = plan.SelfContainerId,
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            status = Clone(_status);
        }

        var jobId = status.JobId;
        var cts = new CancellationTokenSource(JobTimeout);

        // 关键：这个任务的令牌不来自任何 HTTP 请求，客户端断开不会影响升级
        _ = Task.Run(async () => await RunJobAsync(plan, jobId, cts), CancellationToken.None);

        return true;
    }

    public void Update(string phase, string message, int progress)
    {
        lock (_sync)
        {
            if (!_status.IsRunning) return;

            _status.Phase = phase;
            _status.Message = message;
            _status.UpdatedAt = DateTime.UtcNow;
            if (progress >= 0)
            {
                _status.Progress = progress;
            }
        }
    }

    public void Fail(string error)
    {
        lock (_sync)
        {
            _status.Phase = SelfUpgradePhase.Failed;
            _status.Message = "升级失败";
            _status.Error = error;
            _status.IsRunning = false;
            _status.FinishedAt = DateTime.UtcNow;
            _status.UpdatedAt = DateTime.UtcNow;
        }
    }

    private async Task RunJobAsync(SelfUpgradePlan plan, string jobId, CancellationTokenSource cts)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var selfUpdateService = scope.ServiceProvider.GetRequiredService<ISelfUpdateService>();
            await selfUpdateService.RunSelfUpgradeAsync(plan, this, cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("自身升级超时（{Minutes} 分钟），已中止", JobTimeout.TotalMinutes);
            Fail($"升级超时（超过 {JobTimeout.TotalMinutes:0} 分钟），请检查网络或手动在宿主机执行 docker pull 验证");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "自身升级后台任务失败");
            Fail(ex.Message);
        }
        finally
        {
            cts.Dispose();
            _logger.LogInformation("自身升级后台任务结束: {JobId}", jobId);
        }
    }

    private static SelfUpgradeJobStatus Clone(SelfUpgradeJobStatus source) => new()
    {
        JobId = source.JobId,
        Phase = source.Phase,
        Message = source.Message,
        Progress = source.Progress,
        IsRunning = source.IsRunning,
        TargetImage = source.TargetImage,
        CurrentVersion = source.CurrentVersion,
        OldContainerId = source.OldContainerId,
        Error = source.Error,
        StartedAt = source.StartedAt,
        FinishedAt = source.FinishedAt,
        UpdatedAt = source.UpdatedAt
    };
}
