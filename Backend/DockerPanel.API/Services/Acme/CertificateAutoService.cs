using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DockerPanel.API.Models;
using DockerPanel.API.Models.Acme;
using DockerPanel.API.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DockerPanel.API.Services.Acme
{
    /// <summary>
    /// 证书自动申请和续期服务实现
    /// </summary>
    public class CertificateAutoService : ICertificateAutoService, IHostedService
    {
        private readonly ILogger<CertificateAutoService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly ICertificateProgressService _progressService;
        private readonly CertificateSettings _certificateSettings;
        private readonly Timer? _renewalCheckTimer;
        private readonly ConcurrentDictionary<string, AutoRenewalTaskStatus> _activeTasks;
        private readonly ConcurrentDictionary<string, AutoRenewalConfiguration> _renewalConfigurations;
        private readonly SemaphoreSlim _renewalSemaphore;
        private GlobalAutoRenewalConfiguration _globalConfig;

        public CertificateAutoService(
            ILogger<CertificateAutoService> logger,
            IConfiguration configuration,
            IServiceScopeFactory scopeFactory,
            IBackgroundTaskQueue taskQueue,
            ICertificateProgressService progressService,
            IOptions<CertificateSettings> certificateSettings)
        {
            _logger = logger;
            _configuration = configuration;
            _scopeFactory = scopeFactory;
            _taskQueue = taskQueue;
            _progressService = progressService;
            _certificateSettings = certificateSettings.Value;
            _activeTasks = new ConcurrentDictionary<string, AutoRenewalTaskStatus>();
            _renewalConfigurations = new ConcurrentDictionary<string, AutoRenewalConfiguration>();
            _renewalSemaphore = new SemaphoreSlim(5, 5); // 限制并发续期数量
            _globalConfig = LoadGlobalConfiguration();

            // 设置定时检查器
            _renewalCheckTimer = new Timer(CheckForRenewals, null, TimeSpan.Zero, _globalConfig.RenewalCheckInterval);
        }

        /// <summary>
        /// 自动申请证书
        /// </summary>
        public async Task<AutoCertificateResult> AutoRequestCertificateAsync(AutoCertificateRequest request, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("开始自动申请证书: {Domains}", string.Join(",", request.Domains));

            var result = new AutoCertificateResult
            {
                RequestedAt = DateTime.UtcNow,
                Domains = request.Domains
            };

            // 创建临时证书ID用于进度跟踪
            var tempCertificateId = $"temp_{Guid.NewGuid().ToString("N").Substring(0, 16)}";
            string? progressId = null;

            try
            {
                // 创建进度跟踪
                progressId = await _progressService.CreateProgressAsync(new ProgressTrackRequest
                {
                    CertificateId = tempCertificateId,
                    ApplicationName = $"自动证书申请 - {string.Join(", ", request.Domains)}",
                    Domains = request.Domains,
                    Provider = "ACME",
                    ChallengeType = string.Join(", ", request.PreferredChallengeTypes),
                    Metadata = new Dictionary<string, object>
                    {
                        ["accountId"] = request.AccountId,
                        ["keyType"] = request.KeyType,
                        ["useWildcard"] = request.UseWildcard,
                        ["requestedAt"] = result.RequestedAt
                    }
                });

                // 更新进度：开始初始化ACME客户端
                await _progressService.UpdateProgressStepAsync(progressId, CertificateApplicationStep.InitializingAcmeClient, "正在初始化ACME客户端");
                using var scope = _scopeFactory.CreateScope();
                var acmeService = scope.ServiceProvider.GetRequiredService<IAcmeService>();
                var challengeValidationService = scope.ServiceProvider.GetRequiredService<IChallengeValidationService>();

                // 更新进度：预检查申请条件
                await _progressService.UpdateProgressStepAsync(progressId, CertificateApplicationStep.GettingAuthorizations, "正在进行预检查");

                var preCheckResult = await PreCheckCertificateRequestAsync(request, cancellationToken);
                if (!preCheckResult.CanProceed)
                {
                    // 记录预检查失败的详细错误
                    foreach (var error in preCheckResult.Errors)
                    {
                        await _progressService.AddErrorAsync(progressId, $"预检查失败: {error}");
                    }

                    await _progressService.MarkAsFailedAsync(progressId, "预检查失败，无法申请证书");

                    result.Success = false;
                    result.Message = "预检查失败，无法申请证书";
                    result.Errors.AddRange(preCheckResult.Errors);
                    return result;
                }

                await _progressService.UpdateProgressStepAsync(progressId, CertificateApplicationStep.GettingAuthorizations, "预检查通过");
                result.ValidationSteps.Add("预检查通过");

                // 更新进度：创建证书订单
                await _progressService.UpdateProgressStepAsync(progressId, CertificateApplicationStep.CreatingOrder, "正在创建证书订单");

                // 创建证书申请请求
                var certificateRequest = new AcmeCertificateRequest
                {
                    AccountId = request.AccountId,
                    Domains = request.Domains,
                    KeyType = request.KeyType,
                    UseWildcard = request.UseWildcard,
                    ChallengeTypes = request.PreferredChallengeTypes,
                    Metadata = request.Metadata
                };

                // 申请证书
                var order = await acmeService.OrderCertificateAsync(certificateRequest);
                if (order == null)
                {
                    throw new InvalidOperationException("证书申请失败：无法创建订单");
                }
                result.OrderId = order.Id;

                await _progressService.UpdateProgressStepAsync(progressId, CertificateApplicationStep.CreatingOrder, $"证书订单创建成功，订单ID: {order.Id}");
                result.ValidationSteps.Add("证书订单创建成功");

                // 更新进度：开始挑战验证
                await _progressService.UpdateProgressStepAsync(progressId, CertificateApplicationStep.ValidatingDomains, "正在进行域名挑战验证");

                // 处理挑战验证
                foreach (var authorization in order.Authorizations)
                {
                    await _progressService.UpdateProgressStepAsync(progressId, CertificateApplicationStep.ValidatingDomains,
                        $"正在处理域名 {authorization.Domain} 的挑战验证");

                    var pendingChallenges = await acmeService.GetPendingChallengesAsync(order.Id);

                    foreach (var challenge in pendingChallenges)
                    {
                        await _progressService.UpdateProgressStepAsync(progressId, CertificateApplicationStep.ValidatingDomains,
                            $"正在配置 {authorization.Domain} 的 {challenge.Type} 挑战");

                        var challengeResult = await challengeValidationService.AutoConfigureChallengeAsync(
                            challenge, authorization.Domain, request.PreferredChallengeTypes, request.DnsCredentials);

                        if (!challengeResult.Success)
                        {
                            var errorMsg = $"挑战配置失败: {authorization.Domain} - {challenge.Type}";
                            result.Errors.Add(errorMsg);
                            await _progressService.AddErrorAsync(progressId, errorMsg);
                            continue;
                        }

                        await _progressService.UpdateProgressStepAsync(progressId, CertificateApplicationStep.ValidatingDomains,
                            $"挑战配置成功: {authorization.Domain} - {challenge.Type}");
                        result.ValidationSteps.Add($"挑战配置成功: {authorization.Domain} - {challenge.Type}");

                        try
                        {
                            // 完成挑战
                            var completeRequest = new CompleteChallengeRequest
                            {
                                ChallengeType = challenge.Type
                            };

                            await _progressService.UpdateProgressStepAsync(progressId, CertificateApplicationStep.ValidatingDomains,
                                $"正在完成 {authorization.Domain} 的 {challenge.Type} 挑战验证");

                            var challengeCompleteResult = await acmeService.CompleteChallengeAsync(
                                order.Id, authorization.Id, completeRequest);

                            if (!challengeCompleteResult.Success)
                            {
                                var errorMsg = $"挑战完成失败: {authorization.Domain} - {challenge.Type}";
                                result.Errors.Add(errorMsg);
                                await _progressService.AddErrorAsync(progressId, errorMsg);
                            }
                            else
                            {
                                await _progressService.UpdateProgressStepAsync(progressId, CertificateApplicationStep.ValidatingDomains,
                                    $"挑战完成成功: {authorization.Domain} - {challenge.Type}");
                                result.ValidationSteps.Add($"挑战完成成功: {authorization.Domain} - {challenge.Type}");
                                // ACME 验证只需成功一个挑战即可，成功后跳出当前域名的所有挑战循环
                                break;
                            }
                        }
                        finally
                        {
                            // 无论验证成功还是失败，都必须清理挑战资源（特别是 TLS-ALPN-01 内存证书，防止影响正常流量）
                            await challengeValidationService.CleanupChallengeAsync(challenge, authorization.Domain, challenge.Type);
                        }
                    }
                }

                // 更新进度：下载证书
                await _progressService.UpdateProgressStepAsync(progressId, CertificateApplicationStep.DownloadingCertificate, "正在下载证书");

                var certificateData = await acmeService.DownloadCertificateAsync(order.Id);
                result.CertificateId = certificateData.CertificateFingerprint;
                result.CertificateData = certificateData.Certificate;
                result.PrivateKeyData = certificateData.PrivateKey;
                result.CompletedAt = DateTime.UtcNow;

                await _progressService.UpdateProgressStepAsync(progressId, CertificateApplicationStep.DownloadingCertificate, "证书下载成功");
                result.ValidationSteps.Add("证书下载成功");

                // 设置自动续期配置
                if (request.EnableAutoRenewal)
                {
                    var renewalConfig = new AutoRenewalConfiguration
                    {
                        CertificateId = result.CertificateId ?? string.Empty,
                        AccountId = request.AccountId,
                        AutoRenewalEnabled = true,
                        RenewalDaysBeforeExpiry = request.RenewalDaysBeforeExpiry,
                        NotificationEmails = request.NotificationEmails,
                        Settings = new Dictionary<string, object>
                        {
                            ["KeyType"] = request.KeyType,
                            ["UseWildcard"] = request.UseWildcard,
                            ["PreferredChallengeTypes"] = request.PreferredChallengeTypes,
                            ["DnsCredentials"] = request.DnsCredentials ?? new Dictionary<string, Dictionary<string, object>>()
                        }
                    };

                    await _progressService.UpdateProgressStepAsync(progressId, CertificateApplicationStep.SavingCertificate, "正在配置自动续期");
                    await SetAutoRenewalConfigurationAsync(result.CertificateId ?? string.Empty, renewalConfig);
                    result.AutoRenewalConfig = renewalConfig;
                    await _progressService.UpdateProgressStepAsync(progressId, CertificateApplicationStep.SavingCertificate, "自动续期配置成功");
                    result.ValidationSteps.Add("自动续期配置成功");
                }

                // 更新进度：保存证书
                await _progressService.UpdateProgressStepAsync(progressId, CertificateApplicationStep.SavingCertificate, "正在保存证书到存储");

                result.Success = true;
                result.Message = "证书自动申请成功";

                // 标记进度完成
                await _progressService.UpdateProgressStepAsync(progressId, CertificateApplicationStep.Completed, "证书申请完成");
                await _progressService.MarkAsCompletedAsync(progressId);

                _logger.LogInformation("证书自动申请成功: {CertificateId}, Domains: {Domains}",
                    result.CertificateId, string.Join(",", result.Domains));

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "证书自动申请失败: {Domains}", string.Join(",", request.Domains));

                // 记录异常到进度跟踪
                if (progressId != null)
                {
                    await _progressService.AddErrorAsync(progressId, $"证书申请异常: {ex.Message}");
                    await _progressService.AddErrorAsync(progressId, $"异常详情: {ex.StackTrace}");
                    await _progressService.MarkAsFailedAsync(progressId, $"证书申请失败: {ex.Message}");
                }

                result.Success = false;
                result.Message = $"申请失败: {ex.Message}";
                result.Errors.Add(ex.Message);
                result.CompletedAt = DateTime.UtcNow;

                return result;
            }
        }

        /// <summary>
        /// 自动续期证书
        /// </summary>
        public async Task<AutoRenewalResult> AutoRenewCertificateAsync(string certificateId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("开始自动续期证书: {CertificateId}", certificateId);

            var taskId = GenerateTaskId();
            var taskStatus = new AutoRenewalTaskStatus
            {
                TaskId = taskId,
                CertificateId = certificateId,
                Status = "pending",
                CreatedAt = DateTime.UtcNow,
                ProgressPercentage = 0
            };

            _activeTasks[taskId] = taskStatus;

            try
            {
                await _renewalSemaphore.WaitAsync(cancellationToken);

                taskStatus.Status = "running";
                taskStatus.StartedAt = DateTime.UtcNow;
                taskStatus.CurrentStep = "准备续期";
                taskStatus.ProgressPercentage = 10;

                var result = new AutoRenewalResult
                {
                    CertificateId = certificateId,
                    RenewalStartedAt = DateTime.UtcNow,
                    TaskId = taskId
                };

                // 获取续期配置
                var renewalConfig = await GetAutoRenewalConfigurationAsync(certificateId);
                if (renewalConfig == null || !renewalConfig.AutoRenewalEnabled)
                {
                    result.Success = false;
                    result.Message = "证书未启用自动续期或配置不存在";
                    result.Errors.Add("自动续期配置未找到或已禁用");
                    return result;
                }

                result.RenewalSteps.Add("续期配置验证成功");
                taskStatus.ProgressPercentage = 20;
                taskStatus.CurrentStep = "创建续期申请";

                using var certificateScope = _scopeFactory.CreateScope();
                var certificateManagementService = certificateScope.ServiceProvider.GetRequiredService<ICertificateManagementService>();
                var certificateDetails = await certificateManagementService.GetCertificateDetailsAsync(certificateId, cancellationToken);
                if (certificateDetails == null)
                {
                    result.Success = false;
                    result.Message = "证书不存在，无法续期";
                    result.Errors.Add("证书不存在");
                    return result;
                }

                // 创建自动申请请求
                var autoRequest = new AutoCertificateRequest
                {
                    AccountId = renewalConfig.AccountId,
                    Domains = certificateDetails.Domains,
                    KeyType = renewalConfig.Settings.GetValueOrDefault("KeyType", "RSA2048")?.ToString() ?? "RSA2048",
                    UseWildcard = renewalConfig.Settings.GetValueOrDefault("UseWildcard", false) as bool? ?? false,
                    PreferredChallengeTypes = renewalConfig.Settings.GetValueOrDefault("PreferredChallengeTypes") as List<string> ?? new List<string> { "http-01", "dns-01" },
                    DnsCredentials = renewalConfig.Settings.GetValueOrDefault("DnsCredentials") as Dictionary<string, Dictionary<string, object>>,
                    EnableAutoRenewal = true,
                    RenewalDaysBeforeExpiry = renewalConfig.RenewalDaysBeforeExpiry,
                    NotificationEmails = renewalConfig.NotificationEmails
                };

                taskStatus.ProgressPercentage = 30;
                taskStatus.CurrentStep = "执行证书申请";

                // 执行自动申请
                var autoResult = await AutoRequestCertificateAsync(autoRequest, cancellationToken);

                if (!autoResult.Success)
                {
                    result.Success = false;
                    result.Message = "续期申请失败";
                    result.Errors.AddRange(autoResult.Errors);
                    result.RenewalSteps.AddRange(autoResult.ValidationSteps);
                    return result;
                }

                result.NewCertificateId = autoResult.CertificateId;
                result.RenewalSteps.AddRange(autoResult.ValidationSteps);
                taskStatus.ProgressPercentage = 80;
                taskStatus.CurrentStep = "更新续期配置";

                // 更新新证书的续期配置
                if (autoResult.CertificateId != null)
                {
                    var newRenewalConfig = new AutoRenewalConfiguration
                    {
                        CertificateId = autoResult.CertificateId,
                        AccountId = renewalConfig.AccountId,
                        AutoRenewalEnabled = true,
                        RenewalDaysBeforeExpiry = renewalConfig.RenewalDaysBeforeExpiry,
                        NotificationEmails = renewalConfig.NotificationEmails,
                        Settings = renewalConfig.Settings,
                        LastRenewalAttempt = DateTime.UtcNow,
                        NextRenewalAttempt = await CalculateNextRenewalDateAsync(autoResult.CertificateId, renewalConfig.RenewalDaysBeforeExpiry),
                        RenewalAttempts = 1
                    };

                    await SetAutoRenewalConfigurationAsync(autoResult.CertificateId, newRenewalConfig);
                    result.RenewalSteps.Add("新证书续期配置更新成功");
                }

                taskStatus.ProgressPercentage = 90;
                taskStatus.CurrentStep = "清理旧配置";

                // 禁用旧证书的自动续期
                await DisableAutoRenewalAsync(certificateId);
                result.RenewalSteps.Add("旧证书自动续期已禁用");

                taskStatus.ProgressPercentage = 100;
                taskStatus.CurrentStep = "续期完成";
                taskStatus.Status = "completed";
                taskStatus.CompletedAt = DateTime.UtcNow;

                result.Success = true;
                result.Message = "证书自动续期成功";
                result.RenewalCompletedAt = DateTime.UtcNow;
                result.RenewalSteps.Add("续期流程完成");

                _logger.LogInformation("证书自动续期成功: {OldCertificateId} -> {NewCertificateId}",
                    certificateId, result.NewCertificateId);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "证书自动续期失败: {CertificateId}", certificateId);

                taskStatus.Status = "failed";
                taskStatus.CompletedAt = DateTime.UtcNow;
                taskStatus.Errors.Add(ex.Message);

                return new AutoRenewalResult
                {
                    Success = false,
                    CertificateId = certificateId,
                    TaskId = taskId,
                    Message = $"续期失败: {ex.Message}",
                    RenewalStartedAt = DateTime.UtcNow,
                    RenewalCompletedAt = DateTime.UtcNow,
                    Errors = new List<string> { ex.Message }
                };
            }
            finally
            {
                _renewalSemaphore.Release();
                _activeTasks.TryRemove(taskId, out _);
            }
        }

        /// <summary>
        /// 批量自动续期即将到期的证书
        /// </summary>
        public async Task<BatchAutoRenewalResult> BatchAutoRenewCertificatesAsync(int daysBeforeExpiry = 15, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("开始批量自动续期证书，到期前天数: {Days}", daysBeforeExpiry);

            var result = new BatchAutoRenewalResult
            {
                BatchStartedAt = DateTime.UtcNow
            };

            try
            {
                // 获取即将到期的证书
                var expiringCertificates = await GetExpiringCertificatesAsync(daysBeforeExpiry, cancellationToken);
                result.TotalCertificates = expiringCertificates.Count();

                _logger.LogInformation("发现 {Count} 个即将到期的证书", result.TotalCertificates);

                var renewalTasks = new List<Task<AutoRenewalResult>>();

                foreach (var certificate in expiringCertificates.Where(c => c.AutoRenewalEnabled))
                {
                    var renewalTask = AutoRenewCertificateAsync(certificate.CertificateId, cancellationToken);
                    renewalTasks.Add(renewalTask);
                }

                // 等待所有续期任务完成
                var renewalResults = await Task.WhenAll(renewalTasks);

                result.RenewalResults.AddRange(renewalResults);
                result.SuccessfulRenewals = renewalResults.Count(r => r.Success);
                result.FailedRenewals = renewalResults.Count(r => !r.Success);
                result.SkippedRenewals = result.TotalCertificates - renewalResults.Count();
                result.BatchCompletedAt = DateTime.UtcNow;
                result.TotalDuration = result.BatchCompletedAt.Value - result.BatchStartedAt;

                result.Success = result.SuccessfulRenewals > 0 || result.FailedRenewals == 0;
                result.Message = result.Success
                    ? $"批量续期完成，成功: {result.SuccessfulRenewals}，失败: {result.FailedRenewals}，跳过: {result.SkippedRenewals}"
                    : "批量续期失败";

                _logger.LogInformation("批量续期完成: {Successful} 成功, {Failed} 失败, {Skipped} 跳过, 耗时: {Duration}ms",
                    result.SuccessfulRenewals, result.FailedRenewals, result.SkippedRenewals, result.TotalDuration.TotalMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量自动续期失败");

                result.Success = false;
                result.Message = $"批量续期失败: {ex.Message}";
                result.BatchCompletedAt = DateTime.UtcNow;
                result.BatchErrors.Add(ex.Message);

                return result;
            }
        }

        /// <summary>
        /// 设置证书自动续期配置
        /// </summary>
        public async Task<CertificateAutoRenewalConfigResult> SetAutoRenewalConfigurationAsync(string certificateId, AutoRenewalConfiguration configuration)
        {
            try
            {
                _logger.LogInformation("设置证书自动续期配置: {CertificateId}", certificateId);

                var validationErrors = ValidateRenewalConfiguration(configuration);
                if (validationErrors.Any())
                {
                    return new CertificateAutoRenewalConfigResult
                    {
                        Success = false,
                        Message = "配置验证失败",
                        CertificateId = certificateId,
                        ValidationErrors = validationErrors,
                        ConfiguredAt = DateTime.UtcNow
                    };
                }

                configuration.CertificateId = certificateId;
                configuration.NextRenewalAttempt = await CalculateNextRenewalDateAsync(certificateId, configuration.RenewalDaysBeforeExpiry);

                _renewalConfigurations[certificateId] = configuration;

                _logger.LogInformation("证书自动续期配置设置成功: {CertificateId}", certificateId);

                return new CertificateAutoRenewalConfigResult
                {
                    Success = true,
                    Message = "配置设置成功",
                    CertificateId = certificateId,
                    Configuration = configuration,
                    ConfiguredAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "设置证书自动续期配置失败: {CertificateId}", certificateId);

                return new CertificateAutoRenewalConfigResult
                {
                    Success = false,
                    Message = $"配置设置失败: {ex.Message}",
                    CertificateId = certificateId,
                    ValidationErrors = new List<string> { ex.Message },
                    ConfiguredAt = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// 获取证书自动续期配置
        /// </summary>
        public async Task<AutoRenewalConfiguration?> GetAutoRenewalConfigurationAsync(string certificateId)
        {
            await Task.CompletedTask;
            _renewalConfigurations.TryGetValue(certificateId, out var configuration);
            return configuration;
        }

        /// <summary>
        /// 启用证书自动续期
        /// </summary>
        public async Task<bool> EnableAutoRenewalAsync(string certificateId)
        {
            try
            {
                var config = await GetAutoRenewalConfigurationAsync(certificateId);
                if (config == null)
                {
                    config = new AutoRenewalConfiguration
                    {
                        CertificateId = certificateId,
                        AutoRenewalEnabled = true,
                        RenewalDaysBeforeExpiry = _globalConfig.DefaultRenewalDaysBeforeExpiry
                    };
                }
                else
                {
                    config.AutoRenewalEnabled = true;
                }

                var result = await SetAutoRenewalConfigurationAsync(certificateId, config);
                return result.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启用证书自动续期失败: {CertificateId}", certificateId);
                return false;
            }
        }

        /// <summary>
        /// 禁用证书自动续期
        /// </summary>
        public async Task<bool> DisableAutoRenewalAsync(string certificateId)
        {
            try
            {
                var config = await GetAutoRenewalConfigurationAsync(certificateId);
                if (config != null)
                {
                    config.AutoRenewalEnabled = false;
                    var result = await SetAutoRenewalConfigurationAsync(certificateId, config);
                    return result.Success;
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "禁用证书自动续期失败: {CertificateId}", certificateId);
                return false;
            }
        }

        /// <summary>
        /// 获取即将到期的证书列表
        /// </summary>
        public async Task<IEnumerable<ExpiringCertificate>> GetExpiringCertificatesAsync(int daysBeforeExpiry = 15, CancellationToken cancellationToken = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var certificateManagementService = scope.ServiceProvider.GetRequiredService<ICertificateManagementService>();
            
            return await certificateManagementService.GetExpiringCertificatesAsync(daysBeforeExpiry, cancellationToken);
        }

        /// <summary>
        /// 获取自动续期任务状态
        /// </summary>
        public async Task<AutoRenewalTaskStatus> GetAutoRenewalTaskStatusAsync(string taskId)
        {
            await Task.CompletedTask;
            return _activeTasks.TryGetValue(taskId, out var status) ? status : new AutoRenewalTaskStatus
            {
                TaskId = taskId,
                Status = "not_found",
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 获取所有自动续期任务
        /// </summary>
        public async Task<IEnumerable<AutoRenewalTaskStatus>> GetAllAutoRenewalTasksAsync(CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return _activeTasks.Values.ToList();
        }

        /// <summary>
        /// 取消自动续期任务
        /// </summary>
        public async Task<bool> CancelAutoRenewalTaskAsync(string taskId)
        {
            await Task.CompletedTask;
            return _activeTasks.TryRemove(taskId, out _);
        }

        /// <summary>
        /// 监控自动续期进度
        /// </summary>
        public async IAsyncEnumerable<AutoRenewalProgressUpdate> MonitorAutoRenewalProgressAsync(string taskId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_activeTasks.TryGetValue(taskId, out var status))
                {
                    yield return new AutoRenewalProgressUpdate
                    {
                        TaskId = taskId,
                        CertificateId = status.CertificateId,
                        Status = status.Status,
                        ProgressPercentage = status.ProgressPercentage,
                        CurrentStep = status.CurrentStep,
                        Timestamp = DateTime.UtcNow,
                        Message = GetStatusMessage(status.Status),
                        Details = status.TaskDetails
                    };

                    if (status.Status == "completed" || status.Status == "failed" || status.Status == "cancelled")
                    {
                        yield break;
                    }
                }
                else
                {
                    yield break;
                }

                await Task.Delay(2000, cancellationToken); // 每2秒检查一次
            }
        }

        /// <summary>
        /// 预检查证书申请条件
        /// </summary>
        public async Task<CertificatePreCheckResult> PreCheckCertificateRequestAsync(AutoCertificateRequest request, CancellationToken cancellationToken = default)
        {
            var result = new CertificatePreCheckResult
            {
                CanProceed = true
            };

            try
            {
                // 检查域名格式
                foreach (var domain in request.Domains)
                {
                    if (!IsValidDomain(domain))
                    {
                        result.Errors.Add($"无效的域名格式: {domain}");
                    }
                }

                // 检查账户是否存在
                using var scope = _scopeFactory.CreateScope();
                var acmeService = scope.ServiceProvider.GetRequiredService<IAcmeService>();
                var account = await acmeService.GetAccountAsync(request.AccountId);
                if (account == null)
                {
                    result.Errors.Add($"ACME账户不存在: {request.AccountId}");
                }

                // 检查DNS凭据（如果使用DNS-01挑战）
                if (request.PreferredChallengeTypes.Contains("dns-01"))
                {
                    if (request.DnsCredentials == null || !request.DnsCredentials.Any())
                    {
                        result.Warnings.Add("使用DNS-01挑战但未提供DNS凭据");
                    }
                }

                // 检查通配符域名
                if (request.UseWildcard)
                {
                    var hasWildcardDomain = request.Domains.Any(d => d.StartsWith("*."));
                    if (!hasWildcardDomain)
                    {
                        result.Warnings.Add("启用了通配符但域名列表中没有通配符域名");
                    }

                    if (!request.PreferredChallengeTypes.Contains("dns-01"))
                    {
                        result.Warnings.Add("通配符证书建议使用DNS-01挑战");
                    }
                }

                result.Passed = !result.Errors.Any();
                result.CanProceed = result.Passed;
                result.Message = result.Passed ? "预检查通过" : "预检查失败";

                if (result.Errors.Any())
                {
                    result.RecommendedActions.Add("修复所有错误后重试");
                }
                if (result.Warnings.Any())
                {
                    result.RecommendedActions.Add("检查警告信息并确认配置");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "证书申请预检查失败");

                result.Passed = false;
                result.CanProceed = false;
                result.Message = "预检查过程中发生错误";
                result.Errors.Add(ex.Message);

                return result;
            }
        }

        /// <summary>
        /// 获取证书申请历史
        /// </summary>
        public async Task<IEnumerable<CertificateRequestHistory>> GetCertificateRequestHistoryAsync(string certificateId, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var certificateManagementService = scope.ServiceProvider.GetRequiredService<ICertificateManagementService>();
            var histories = await certificateManagementService.GetCertificateOperationHistoryAsync(
                certificateId,
                null,
                limit,
                offset,
                cancellationToken);

            return histories.Select(h => new CertificateRequestHistory
            {
                Id = h.Id,
                CertificateId = h.CertificateId,
                RequestType = h.OperationType,
                RequestedAt = h.OperatedAt,
                CompletedAt = h.OperatedAt + h.Duration,
                Status = h.Success ? "completed" : "failed",
                Success = h.Success,
                ErrorMessage = h.ErrorMessage,
                RequestDetails = h.OperationDetails,
                OrderId = h.OperationDetails.TryGetValue("OrderId", out var orderId) ? orderId?.ToString() : null
            }).ToList();
        }

        /// <summary>
        /// 获取自动续期统计信息
        /// </summary>
        public async Task<AutoRenewalStatistics> GetAutoRenewalStatisticsAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var last30Days = now.AddDays(-30);
            using var scope = _scopeFactory.CreateScope();
            var certificateManagementService = scope.ServiceProvider.GetRequiredService<ICertificateManagementService>();
            var certificates = await certificateManagementService.GetCertificatesAsync(
                includeExpired: true,
                pageSize: int.MaxValue,
                cancellationToken: cancellationToken);
            var certificateList = certificates.Certificates.ToList();
            var autoRenewalCertificates = certificateList.Where(c => c.AutoRenewalEnabled).ToList();
            var expiringNext30Days = await GetExpiringCertificatesAsync(30, cancellationToken);
            var expiringNext7Days = await GetExpiringCertificatesAsync(7, cancellationToken);

            var renewalHistories = new List<CertificateOperationHistory>();
            foreach (var certificate in autoRenewalCertificates)
            {
                var histories = await certificateManagementService.GetCertificateOperationHistoryAsync(
                    certificate.Id,
                    "renew",
                    100,
                    0,
                    cancellationToken);
                renewalHistories.AddRange(histories.Where(h => h.OperatedAt >= last30Days));
            }

            var stats = new AutoRenewalStatistics
            {
                TotalCertificates = certificateList.Count,
                CertificatesWithAutoRenewal = autoRenewalCertificates.Count,
                LastRenewalCheck = now.Add(-_globalConfig.RenewalCheckInterval),
                NextScheduledCheck = now.Add(_globalConfig.RenewalCheckInterval),
                SuccessfulRenewalsLast30Days = renewalHistories.Count(h => h.Success),
                FailedRenewalsLast30Days = renewalHistories.Count(h => !h.Success),
                ExpiringNext30Days = expiringNext30Days.Count(),
                ExpiringNext7Days = expiringNext7Days.Count(),
                MostRecentRenewals = renewalHistories
                    .OrderByDescending(h => h.OperatedAt)
                    .Take(5)
                    .Select(h => h.CertificateId)
                    .ToList(),
                UpcomingRenewals = autoRenewalCertificates
                    .Where(c => c.NextRenewalAttempt.HasValue)
                    .OrderBy(c => c.NextRenewalAttempt)
                    .Take(5)
                    .Select(c => c.Id)
                    .ToList(),
                RenewalFailureReasons = renewalHistories
                    .Where(h => !h.Success)
                    .GroupBy(h => string.IsNullOrWhiteSpace(h.ErrorMessage) ? "Unknown" : h.ErrorMessage!)
                    .ToDictionary(g => g.Key, g => g.Count())
            };

            return stats;
        }

        /// <summary>
        /// 设置自动续期全局配置
        /// </summary>
        public async Task<GlobalAutoRenewalConfigResult> SetGlobalAutoRenewalConfigurationAsync(GlobalAutoRenewalConfiguration configuration)
        {
            try
            {
                var validationErrors = ValidateGlobalConfiguration(configuration);
                if (validationErrors.Any())
                {
                    return new GlobalAutoRenewalConfigResult
                    {
                        Success = false,
                        Message = "全局配置验证失败",
                        ValidationErrors = validationErrors,
                        ConfiguredAt = DateTime.UtcNow
                    };
                }

                _globalConfig = configuration;

                // 重新设置定时器
                _renewalCheckTimer?.Change(TimeSpan.Zero, configuration.RenewalCheckInterval);

                _logger.LogInformation("全局自动续期配置更新成功");

                return new GlobalAutoRenewalConfigResult
                {
                    Success = true,
                    Message = "全局配置更新成功",
                    Configuration = configuration,
                    ConfiguredAt = DateTime.UtcNow,
                    AppliedChanges = new List<string> { "定时检查间隔已更新", "配置已应用" }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "设置全局自动续期配置失败");

                return new GlobalAutoRenewalConfigResult
                {
                    Success = false,
                    Message = $"配置更新失败: {ex.Message}",
                    ValidationErrors = new List<string> { ex.Message },
                    ConfiguredAt = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// 获取自动续期全局配置
        /// </summary>
        public async Task<GlobalAutoRenewalConfiguration> GetGlobalAutoRenewalConfigurationAsync()
        {
            await Task.CompletedTask;
            return _globalConfig;
        }

        /// <summary>
        /// 测试自动续期流程
        /// </summary>
        public async Task<AutoRenewalTestResult> TestAutoRenewalFlowAsync(string certificateId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("开始测试自动续期流程: {CertificateId}", certificateId);

            var result = new AutoRenewalTestResult
            {
                CertificateId = certificateId,
                TestStartedAt = DateTime.UtcNow
            };

            try
            {
                // 测试1: 检查续期配置
                result.TestSteps.Add("检查续期配置");
                var config = await GetAutoRenewalConfigurationAsync(certificateId);
                if (config == null)
                {
                    result.FailedTests.Add("续期配置不存在");
                    result.CanAutoRenew = false;
                }
                else
                {
                    result.PassedTests.Add("续期配置存在");
                    if (!config.AutoRenewalEnabled)
                    {
                        result.Warnings.Add("自动续期已禁用");
                    }
                }

                // 测试2: 检查账户状态
                result.TestSteps.Add("检查ACME账户状态");
                if (config != null)
                {
                    using var scope2 = _scopeFactory.CreateScope();
                    var acmeService = scope2.ServiceProvider.GetRequiredService<IAcmeService>();
                    var challengeValidationService = scope2.ServiceProvider.GetRequiredService<IChallengeValidationService>();

                    var account = await acmeService.GetAccountAsync(config.AccountId);
                    if (account == null)
                    {
                        result.FailedTests.Add("ACME账户不存在");
                        result.CanAutoRenew = false;
                    }
                    else
                    {
                        result.PassedTests.Add("ACME账户状态正常");
                    }

                    // 测试3: 检查DNS提供商连接
                    result.TestSteps.Add("检查DNS提供商连接");
                    if (config?.Settings.ContainsKey("DnsCredentials") == true)
                    {
                        var dnsCredentials = config.Settings["DnsCredentials"] as Dictionary<string, Dictionary<string, object>>;
                        if (dnsCredentials != null)
                        {
                            foreach (var kvp in dnsCredentials)
                            {
                                var testResult = await challengeValidationService.TestDnsProviderConnectionAsync(kvp.Key, kvp.Value);
                                if (testResult.Success)
                                {
                                    result.PassedTests.Add($"DNS提供商 {kvp.Key} 连接正常");
                                }
                                else
                                {
                                    result.FailedTests.Add($"DNS提供商 {kvp.Key} 连接失败: {testResult.Message}");
                                    result.CanAutoRenew = false;
                                }
                            }
                        }
                    }
                }

                // 测试4: 使用当前证书域名执行预检查
                result.TestSteps.Add("执行预检查");
                if (config != null)
                {
                    using var certificateScope = _scopeFactory.CreateScope();
                    var certificateManagementService = certificateScope.ServiceProvider.GetRequiredService<ICertificateManagementService>();
                    var certificateDetails = await certificateManagementService.GetCertificateDetailsAsync(certificateId, cancellationToken);
                    if (certificateDetails == null)
                    {
                        result.FailedTests.Add("证书不存在");
                        result.CanAutoRenew = false;
                    }
                    else
                    {
                        var preCheckRequest = new AutoCertificateRequest
                        {
                            AccountId = config.AccountId,
                            Domains = certificateDetails.Domains,
                            PreferredChallengeTypes = config.Settings.GetValueOrDefault("PreferredChallengeTypes") as List<string> ?? new List<string>(),
                            DnsCredentials = config.Settings.GetValueOrDefault("DnsCredentials") as Dictionary<string, Dictionary<string, object>>
                        };

                        var preCheckResult = await PreCheckCertificateRequestAsync(preCheckRequest, cancellationToken);
                        if (preCheckResult.CanProceed)
                        {
                            result.PassedTests.Add("预检查通过");
                        }
                        else
                        {
                            result.FailedTests.Add("预检查失败");
                            result.FailedTests.AddRange(preCheckResult.Errors);
                            result.CanAutoRenew = false;
                        }
                    }
                }

                result.TestCompletedAt = DateTime.UtcNow;
                result.Success = result.FailedTests.Count == 0;
                result.Message = result.Success ? "所有测试通过" : $"测试失败: {result.FailedTests.Count} 项失败";
                result.EstimatedRenewalTime = TimeSpan.FromMinutes(5); // 估算时间

                if (result.Success)
                {
                    result.Recommendations.Add("证书可以正常自动续期");
                }
                else
                {
                    result.Recommendations.Add("请修复失败的测试项");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "测试自动续期流程失败: {CertificateId}", certificateId);

                result.Success = false;
                result.Message = $"测试失败: {ex.Message}";
                result.TestCompletedAt = DateTime.UtcNow;
                result.FailedTests.Add(ex.Message);
                result.CanAutoRenew = false;

                return result;
            }
        }

        #region IHostedService Implementation

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("证书自动服务已启动");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("证书自动服务正在停止");
            _renewalCheckTimer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        #endregion

        #region Private Methods

        private async void CheckForRenewals(object? state)
        {
            try
            {
                if (!_globalConfig.GlobalAutoRenewalEnabled) return;

                _logger.LogDebug("执行定时续期检查");

                var expiringCertificates = await GetExpiringCertificatesAsync(_globalConfig.DefaultRenewalDaysBeforeExpiry);

                foreach (var certificate in expiringCertificates.Where(c => c.AutoRenewalEnabled))
                {
                    // 将续期任务加入基于 TinyDb 的持久化后台队列
                    using var scope = _scopeFactory.CreateScope();
                    var jobQueue = scope.ServiceProvider.GetRequiredService<AcmeJobQueueService>();
                    
                    await jobQueue.EnqueueAsync("AutoRenewal", new
                    {
                        CertificateId = certificate.CertificateId
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "定时续期检查失败");
            }
        }

        private GlobalAutoRenewalConfiguration LoadGlobalConfiguration()
        {
            return new GlobalAutoRenewalConfiguration
            {
                GlobalAutoRenewalEnabled = true,
                DefaultRenewalDaysBeforeExpiry = _certificateSettings.DefaultRenewalDaysBeforeExpiry,
                RenewalCheckInterval = TimeSpan.FromHours(_certificateSettings.RenewalCheckIntervalHours),
                MaxConcurrentRenewals = 5,
                RenewalTimeout = TimeSpan.FromHours(2),
                DefaultRetryAttempts = 3,
                DefaultRetryDelay = TimeSpan.FromMinutes(5),
                EnableRenewalNotifications = true,
                EnableFailureNotifications = true,
                EnableSuccessNotifications = false
            };
        }

        private List<string> ValidateRenewalConfiguration(AutoRenewalConfiguration configuration)
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(configuration.CertificateId))
            {
                errors.Add("证书ID不能为空");
            }

            if (string.IsNullOrEmpty(configuration.AccountId))
            {
                errors.Add("账户ID不能为空");
            }

            if (configuration.RenewalDaysBeforeExpiry < 1 || configuration.RenewalDaysBeforeExpiry > 90)
            {
                errors.Add("续期天数必须在1-90之间");
            }

            return errors;
        }

        private List<string> ValidateGlobalConfiguration(GlobalAutoRenewalConfiguration configuration)
        {
            var errors = new List<string>();

            if (configuration.DefaultRenewalDaysBeforeExpiry < 1 || configuration.DefaultRenewalDaysBeforeExpiry > 90)
            {
                errors.Add("默认续期天数必须在1-90之间");
            }

            if (configuration.MaxConcurrentRenewals < 1 || configuration.MaxConcurrentRenewals > 20)
            {
                errors.Add("最大并发续期数必须在1-20之间");
            }

            if (configuration.RenewalCheckInterval < TimeSpan.FromMinutes(1) || configuration.RenewalCheckInterval > TimeSpan.FromDays(1))
            {
                errors.Add("检查间隔必须在1分钟到1天之间");
            }

            return errors;
        }

        private string GenerateTaskId()
        {
            return $"task_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        }

        private async Task<DateTime?> CalculateNextRenewalDateAsync(string certificateId, int daysBeforeExpiry)
        {
            using var scope = _scopeFactory.CreateScope();
            var certificateManagementService = scope.ServiceProvider.GetRequiredService<ICertificateManagementService>();
            var details = await certificateManagementService.GetCertificateDetailsAsync(certificateId);
            return details?.ExpiresAt.AddDays(-daysBeforeExpiry);
        }

        private bool IsValidDomain(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain)) return false;

            // 简单的域名验证
            if (domain.Length > 253) return false;

            var parts = domain.Split('.');
            if (parts.Length < 2) return false;

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part) || part.Length > 63) return false;
                if (part.StartsWith("-") || part.EndsWith("-")) return false;
            }

            return true;
        }

        private string GetStatusMessage(string status)
        {
            return status switch
            {
                "pending" => "等待开始",
                "running" => "正在执行",
                "completed" => "已完成",
                "failed" => "执行失败",
                "cancelled" => "已取消",
                _ => "未知状态"
            };
        }

        #endregion
    }
}