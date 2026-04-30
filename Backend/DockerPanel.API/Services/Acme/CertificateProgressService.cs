using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DockerPanel.API.Models.Acme;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using DockerPanel.API.Hubs;

namespace DockerPanel.API.Services.Acme
{
    /// <summary>
    /// 证书申请进度跟踪服务
    /// </summary>
    public interface ICertificateProgressService
    {
        /// <summary>
        /// 创建进度跟踪
        /// </summary>
        Task<string> CreateProgressAsync(ProgressTrackRequest request);

        /// <summary>
        /// 获取进度信息
        /// </summary>
        Task<ProgressTrackResponse?> GetProgressAsync(string progressId);

        /// <summary>
        /// 获取证书的进度信息
        /// </summary>
        Task<ProgressTrackResponse?> GetProgressByCertificateIdAsync(string certificateId);

        /// <summary>
        /// 更新进度步骤
        /// </summary>
        Task UpdateProgressStepAsync(string progressId, CertificateApplicationStep step, string message, bool isCompleted = false);

        /// <summary>
        /// 完成当前步骤
        /// </summary>
        Task CompleteCurrentStepAsync(string progressId, string? message = null);

        /// <summary>
        /// 添加错误信息
        /// </summary>
        Task AddErrorAsync(string progressId, string error);

        /// <summary>
        /// 添加警告信息
        /// </summary>
        Task AddWarningAsync(string progressId, string warning);

        /// <summary>
        /// 标记进度完成
        /// </summary>
        Task MarkAsCompletedAsync(string progressId);

        /// <summary>
        /// 标记进度失败
        /// </summary>
        Task MarkAsFailedAsync(string progressId, string errorMessage);

        /// <summary>
        /// 获取所有进度列表
        /// </summary>
        Task<List<ProgressTrackResponse>> GetAllProgressAsync();

        /// <summary>
        /// 清理过期的进度记录
        /// </summary>
        Task CleanupExpiredProgressAsync();

        /// <summary>
        /// 删除进度记录
        /// </summary>
        Task DeleteProgressAsync(string progressId);

        /// <summary>
        /// 检查并标记超时的进度记录
        /// </summary>
        Task CheckAndMarkTimeoutsAsync();
    }

    /// <summary>
    /// 证书申请进度跟踪服务实现
    /// </summary>
    public class CertificateProgressService : ICertificateProgressService
    {
        private readonly ILogger<CertificateProgressService> _logger;
        private readonly IHubContext<DockerPanelHub> _hubContext;
        private readonly ConcurrentDictionary<string, CertificateApplicationProgress> _progressStore;
        private readonly TimeSpan _progressExpiration = TimeSpan.FromHours(24);
        private readonly TimeSpan _challengeTimeout = TimeSpan.FromMinutes(10); // 挑战验证10分钟超时

        public CertificateProgressService(
            ILogger<CertificateProgressService> logger,
            IHubContext<DockerPanelHub> hubContext)
        {
            _logger = logger;
            _hubContext = hubContext;
            _progressStore = new ConcurrentDictionary<string, CertificateApplicationProgress>();
        }

        /// <summary>
        /// 创建进度跟踪
        /// </summary>
        public async Task<string> CreateProgressAsync(ProgressTrackRequest request)
        {
            try
            {
                var progressId = Guid.NewGuid().ToString("N")[..16];
                var progress = new CertificateApplicationProgress
                {
                    ProgressId = progressId,
                    CertificateId = request.CertificateId,
                    ApplicationName = request.ApplicationName,
                    Status = CertificateApplicationStatus.Pending,
                    Metadata = new Dictionary<string, object>(request.Metadata)
                };

                // 添加初始步骤
                progress.AddStep(CertificateApplicationStep.NotStarted, "证书申请已初始化");

                _progressStore[progressId] = progress;

                // 发送初始进度通知
                await NotifyProgressUpdateAsync(progress);

                _logger.LogInformation("创建证书申请进度跟踪: {ProgressId}, 证书: {CertificateId}",
                    progressId, request.CertificateId);

                return progressId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建进度跟踪失败: {CertificateId}", request.CertificateId);
                throw;
            }
        }

        /// <summary>
        /// 获取进度信息
        /// </summary>
        public async Task<ProgressTrackResponse?> GetProgressAsync(string progressId)
        {
            try
            {
                if (_progressStore.TryGetValue(progressId, out var progress))
                {
                    return await ConvertToResponseAsync(progress);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取进度信息失败: {ProgressId}", progressId);
                return null;
            }
        }

        /// <summary>
        /// 获取证书的进度信息
        /// </summary>
        public async Task<ProgressTrackResponse?> GetProgressByCertificateIdAsync(string certificateId)
        {
            try
            {
                var progress = _progressStore.Values.FirstOrDefault(p => p.CertificateId == certificateId);
                if (progress != null)
                {
                    return await ConvertToResponseAsync(progress);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取证书进度信息失败: {CertificateId}", certificateId);
                return null;
            }
        }

        /// <summary>
        /// 更新进度步骤
        /// </summary>
        public async Task UpdateProgressStepAsync(string progressId, CertificateApplicationStep step, string message, bool isCompleted = false)
        {
            try
            {
                if (_progressStore.TryGetValue(progressId, out var progress))
                {
                    // 如果是新的步骤，先完成当前步骤
                    if (step != progress.CurrentStep && !progress.IsCompleted)
                    {
                        progress.CompleteCurrentStep($"切换到步骤: {step.GetDescription()}");
                    }

                    // 更新状态为进行中
                    if (progress.Status == CertificateApplicationStatus.Pending)
                    {
                        progress.Status = CertificateApplicationStatus.InProgress;
                    }

                    // 添加新步骤
                    progress.AddStep(step, message, isCompleted);

                    // 更新预计剩余时间
                    UpdateEstimatedRemainingTime(progress);

                    // 发送进度更新通知
                    await NotifyProgressUpdateAsync(progress);

                    _logger.LogInformation("更新进度步骤: {ProgressId}, 步骤: {Step}, 消息: {Message}",
                        progressId, step, message);
                }
                else
                {
                    _logger.LogWarning("未找到进度记录: {ProgressId}", progressId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新进度步骤失败: {ProgressId}, 步骤: {Step}", progressId, step);
            }
        }

        /// <summary>
        /// 完成当前步骤
        /// </summary>
        public async Task CompleteCurrentStepAsync(string progressId, string? message = null)
        {
            try
            {
                if (_progressStore.TryGetValue(progressId, out var progress))
                {
                    progress.CompleteCurrentStep(message);
                    UpdateEstimatedRemainingTime(progress);
                    await NotifyProgressUpdateAsync(progress);

                    _logger.LogInformation("完成当前步骤: {ProgressId}, 消息: {Message}", progressId, message);
                }
                else
                {
                    _logger.LogWarning("未找到进度记录: {ProgressId}", progressId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "完成当前步骤失败: {ProgressId}", progressId);
            }
        }

        /// <summary>
        /// 添加错误信息
        /// </summary>
        public async Task AddErrorAsync(string progressId, string error)
        {
            try
            {
                if (_progressStore.TryGetValue(progressId, out var progress))
                {
                    progress.AddError(error);
                    await NotifyProgressUpdateAsync(progress);

                    _logger.LogError("添加进度错误: {ProgressId}, 错误: {Error}", progressId, error);
                }
                else
                {
                    _logger.LogWarning("未找到进度记录: {ProgressId}", progressId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "添加错误信息失败: {ProgressId}", progressId);
            }
        }

        /// <summary>
        /// 添加警告信息
        /// </summary>
        public async Task AddWarningAsync(string progressId, string warning)
        {
            try
            {
                if (_progressStore.TryGetValue(progressId, out var progress))
                {
                    progress.AddWarning(warning);
                    await NotifyProgressUpdateAsync(progress);

                    _logger.LogWarning("添加进度警告: {ProgressId}, 警告: {Warning}", progressId, warning);
                }
                else
                {
                    _logger.LogWarning("未找到进度记录: {ProgressId}", progressId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "添加警告信息失败: {ProgressId}", progressId);
            }
        }

        /// <summary>
        /// 标记进度完成
        /// </summary>
        public async Task MarkAsCompletedAsync(string progressId)
        {
            try
            {
                if (_progressStore.TryGetValue(progressId, out var progress))
                {
                    progress.MarkAsCompleted();
                    await NotifyProgressUpdateAsync(progress);

                    _logger.LogInformation("标记进度完成: {ProgressId}", progressId);
                }
                else
                {
                    _logger.LogWarning("未找到进度记录: {ProgressId}", progressId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "标记进度完成失败: {ProgressId}", progressId);
            }
        }

        /// <summary>
        /// 标记进度失败
        /// </summary>
        public async Task MarkAsFailedAsync(string progressId, string errorMessage)
        {
            try
            {
                if (_progressStore.TryGetValue(progressId, out var progress))
                {
                    progress.MarkAsFailed(errorMessage);
                    await NotifyProgressUpdateAsync(progress);

                    _logger.LogError("标记进度失败: {ProgressId}, 错误: {Error}", progressId, errorMessage);
                }
                else
                {
                    _logger.LogWarning("未找到进度记录: {ProgressId}", progressId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "标记进度失败失败: {ProgressId}", progressId);
            }
        }

        /// <summary>
        /// 获取所有进度列表
        /// </summary>
        public async Task<List<ProgressTrackResponse>> GetAllProgressAsync()
        {
            try
            {
                var tasks = _progressStore.Values.Select(ConvertToResponseAsync);
                var responses = await Task.WhenAll(tasks);
                return responses.Where(r => r != null).ToList()!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取所有进度列表失败");
                return new List<ProgressTrackResponse>();
            }
        }

        /// <summary>
        /// 清理过期的进度记录
        /// </summary>
        public async Task CleanupExpiredProgressAsync()
        {
            try
            {
                var now = DateTime.UtcNow;
                var expiredIds = _progressStore
                    .Where(kvp => kvp.Value.LastUpdatedAt.Add(_progressExpiration) < now)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var id in expiredIds)
                {
                    if (_progressStore.TryRemove(id, out var progress))
                    {
                        _logger.LogInformation("清理过期进度记录: {ProgressId}, 证书: {CertificateId}",
                            id, progress.CertificateId);
                    }
                }

                if (expiredIds.Any())
                {
                    _logger.LogInformation("清理了 {Count} 个过期进度记录", expiredIds.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理过期进度记录失败");
            }
        }

        /// <summary>
        /// 删除进度记录
        /// </summary>
        public async Task DeleteProgressAsync(string progressId)
        {
            try
            {
                if (_progressStore.TryRemove(progressId, out var progress))
                {
                    _logger.LogInformation("删除进度记录: {ProgressId}, 证书: {CertificateId}",
                        progressId, progress.CertificateId);
                }
                else
                {
                    _logger.LogWarning("未找到要删除的进度记录: {ProgressId}", progressId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除进度记录失败: {ProgressId}", progressId);
            }
        }

        /// <summary>
        /// 检查并标记超时的进度记录
        /// </summary>
        public async Task CheckAndMarkTimeoutsAsync()
        {
            try
            {
                var now = DateTime.UtcNow;
                var timeoutProgressIds = new List<string>();

                foreach (var kvp in _progressStore)
                {
                    var progress = kvp.Value;

                    // 检查是否在进行挑战验证且超时
                    if (progress.Status == CertificateApplicationStatus.InProgress &&
                        progress.CurrentStep == CertificateApplicationStep.ValidatingDomains &&
                        progress.LastUpdatedAt.Add(_challengeTimeout) < now)
                    {
                        timeoutProgressIds.Add(kvp.Key);
                    }
                }

                // 标记超时的进度为失败
                foreach (var progressId in timeoutProgressIds)
                {
                    if (_progressStore.TryGetValue(progressId, out var progress))
                    {
                        await MarkAsFailedAsync(progressId,
                            $"挑战验证超时（{_challengeTimeout.TotalMinutes}分钟）。请检查域名解析是否正确指向此服务器，并确保防火墙允许HTTP访问。");

                        _logger.LogWarning("进度挑战验证超时: {ProgressId}, 证书: {CertificateId}",
                            progressId, progress.CertificateId);
                    }
                }

                if (timeoutProgressIds.Any())
                {
                    _logger.LogInformation("标记了 {Count} 个超时的进度记录", timeoutProgressIds.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查进度超时失败");
            }
        }

        /// <summary>
        /// 转换为响应对象
        /// </summary>
        private async Task<ProgressTrackResponse> ConvertToResponseAsync(CertificateApplicationProgress progress)
        {
            return new ProgressTrackResponse
            {
                ProgressId = progress.ProgressId,
                CertificateId = progress.CertificateId,
                ApplicationName = progress.ApplicationName,
                CurrentStep = progress.CurrentStep,
                CurrentStepDescription = progress.GetCurrentStepDescription(),
                ProgressPercentage = progress.ProgressPercentage,
                Status = progress.Status,
                StartedAt = progress.StartedAt,
                LastUpdatedAt = progress.LastUpdatedAt,
                CompletedAt = progress.CompletedAt,
                EstimatedRemainingSeconds = progress.EstimatedRemainingSeconds,
                Steps = new List<CertificateApplicationStepDetail>(progress.Steps),
                Errors = new List<string>(progress.Errors),
                Warnings = new List<string>(progress.Warnings),
                Metadata = new Dictionary<string, object>(progress.Metadata),
                IsCompleted = progress.IsCompleted,
                IsSuccess = progress.IsSuccess
            };
        }

        /// <summary>
        /// 发送进度更新通知
        /// </summary>
        private async Task NotifyProgressUpdateAsync(CertificateApplicationProgress progress)
        {
            try
            {
                var notification = new ProgressUpdateNotification
                {
                    ProgressId = progress.ProgressId,
                    CertificateId = progress.CertificateId,
                    CurrentStep = progress.CurrentStep,
                    CurrentStepDescription = progress.GetCurrentStepDescription(),
                    ProgressPercentage = progress.ProgressPercentage,
                    Status = progress.Status,
                    Message = progress.Steps.LastOrDefault()?.Message ?? "",
                    Errors = new List<string>(progress.Errors.TakeLast(3)), // 只发送最近3个错误
                    Warnings = new List<string>(progress.Warnings.TakeLast(3)), // 只发送最近3个警告
                    EstimatedRemainingSeconds = progress.EstimatedRemainingSeconds
                };

                // 通过SignalR发送实时更新
                await _hubContext.Clients.All.SendAsync("CertificateProgressUpdate", notification);

                _logger.LogDebug("发送进度更新通知: {ProgressId}, 进度: {ProgressPercentage}%",
                    progress.ProgressId, progress.ProgressPercentage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送进度更新通知失败: {ProgressId}", progress.ProgressId);
            }
        }

        /// <summary>
        /// 更新预计剩余时间
        /// </summary>
        private void UpdateEstimatedRemainingTime(CertificateApplicationProgress progress)
        {
            try
            {
                var remainingSteps = Enum.GetValues<CertificateApplicationStep>()
                    .Where(step => step > progress.CurrentStep)
                    .ToList();

                var estimatedSeconds = remainingSteps.Sum(step => step.GetEstimatedDuration());
                progress.EstimatedRemainingSeconds = estimatedSeconds > 0 ? estimatedSeconds : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新预计剩余时间失败: {ProgressId}", progress.ProgressId);
            }
        }
    }
}