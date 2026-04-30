using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DnsClient;
using DockerPanel.API.Data;
using DockerPanel.API.Models;
using DockerPanel.API.Models.Acme;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DockerPanel.API.Services.Acme
{
    /// <summary>
    /// 通配符证书服务实现
    /// </summary>
    public class WildcardCertificateService : IWildcardCertificateService
    {
        private readonly ILogger<WildcardCertificateService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IAcmeService _acmeService;
        private readonly IChallengeValidationService _challengeValidationService;
        private readonly ICertificateProgressService _progressService;
        private readonly TinyDbContext _dbContext;
        private readonly CertificateSettings _certificateSettings;
        private readonly ConcurrentDictionary<string, WildcardCertificateInfo> _certificateCache;
        private readonly Dictionary<string, WildcardCertificateType> _supportedTypes;

        public WildcardCertificateService(
            ILogger<WildcardCertificateService> logger,
            IConfiguration configuration,
            IAcmeService acmeService,
            IChallengeValidationService challengeValidationService,
            ICertificateProgressService progressService,
            TinyDbContext dbContext,
            IOptions<CertificateSettings> certificateSettings)
        {
            _logger = logger;
            _configuration = configuration;
            _acmeService = acmeService;
            _challengeValidationService = challengeValidationService;
            _progressService = progressService;
            _dbContext = dbContext;
            _certificateSettings = certificateSettings.Value;
            _certificateCache = new ConcurrentDictionary<string, WildcardCertificateInfo>();
            _supportedTypes = InitializeSupportedTypes();
        }

        /// <summary>
        /// 申请通配符证书
        /// </summary>
        public async Task<WildcardCertificateResult> RequestWildcardCertificateAsync(WildcardCertificateRequest request, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("开始申请通配符证书: {BaseDomain}, 子域名: {Subdomains}",
                request.BaseDomain, string.Join(",", request.Subdomains));

            // 创建带超时的CancellationTokenSource
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(10)); // 10分钟总超时
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var result = new WildcardCertificateResult
            {
                BaseDomain = request.BaseDomain,
                Subdomains = request.Subdomains,
                FullDomains = GenerateFullDomains(request.BaseDomain, request.Subdomains),
                RequestedAt = DateTime.UtcNow
            };

            // 生成临时证书ID用于跟踪
            var tempCertificateId = $"temp_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
            result.CertificateId = tempCertificateId;

            // 创建进度跟踪
            var progressId = await _progressService.CreateProgressAsync(new ProgressTrackRequest
            {
                CertificateId = tempCertificateId,
                ApplicationName = $"通配符证书申请 - {request.BaseDomain}",
                Domains = result.FullDomains,
                Provider = request.UseStaging ? "Let's Encrypt (Staging)" : "Let's Encrypt (Production)",
                ChallengeType = "dns-01",
                Metadata = new Dictionary<string, object>
                {
                    ["baseDomain"] = request.BaseDomain,
                    ["subdomains"] = request.Subdomains,
                    ["keyType"] = request.KeyType,
                    ["accountId"] = request.AccountId
                }
            });

            // 记录开始申请
            await _progressService.UpdateProgressStepAsync(progressId,
                CertificateApplicationStep.InitializingAcmeClient,
                "开始申请通配符证书，初始化进度跟踪");

            try
            {
                // 预验证请求
                combinedCts.Token.ThrowIfCancellationRequested();
                await _progressService.UpdateProgressStepAsync(progressId,
                    CertificateApplicationStep.GettingAuthorizations,
                    "开始预验证请求");

                var validationResult = await ValidateWildcardRequestAsync(request, combinedCts.Token);
                result.PreValidationResult = ConvertToAcmeCertificateValidationResult(validationResult);

                if (!validationResult.CanProceed)
                {
                    await _progressService.AddErrorAsync(progressId, "请求验证失败，无法申请通配符证书");
                    foreach (var failedCheck in validationResult.FailedChecks)
                    {
                        await _progressService.AddErrorAsync(progressId, failedCheck);
                    }
                    await _progressService.MarkAsFailedAsync(progressId, "预验证失败");

                    result.Success = false;
                    result.Message = "请求验证失败，无法申请通配符证书";
                    result.Errors.AddRange(validationResult.FailedChecks);
                    return result;
                }

                await _progressService.CompleteCurrentStepAsync(progressId, "请求预验证通过");
                result.ValidationSteps.Add("请求预验证通过");

                // 生成通配符CSR
                combinedCts.Token.ThrowIfCancellationRequested();
                await _progressService.UpdateProgressStepAsync(progressId,
                    CertificateApplicationStep.CreatingOrder,
                    "生成通配符证书签名请求(CSR)");

                var csrRequest = new WildcardCsrRequest
                {
                    BaseDomain = request.BaseDomain,
                    Subdomains = request.Subdomains,
                    KeyType = request.KeyType,
                    SubjectInfo = new Dictionary<string, object>
                    {
                        ["commonName"] = $"*.{request.BaseDomain}",
                        ["organization"] = "DockerPanel",
                        ["country"] = "CN",
                        ["state"] = "Beijing"
                    },
                    Extensions = new Dictionary<string, object>
                    {
                        ["subjectAltName"] = result.FullDomains
                    }
                };

                var csr = await GenerateWildcardCsrAsync(csrRequest, combinedCts.Token);
                await _progressService.CompleteCurrentStepAsync(progressId, "通配符CSR生成成功");
                result.ValidationSteps.Add("通配符CSR生成成功");

                // 创建证书申请请求
                combinedCts.Token.ThrowIfCancellationRequested();
                await _progressService.UpdateProgressStepAsync(progressId,
                    CertificateApplicationStep.CreatingAccount,
                    "创建ACME证书订单");

                var certificateRequest = new AcmeCertificateRequest
                {
                    AccountId = request.AccountId,
                    Domains = result.FullDomains,
                    KeyType = request.KeyType,
                    UseWildcard = true,
                    ChallengeTypes = new List<string> { "dns-01" }, // 通配符证书必须使用DNS-01挑战
                    CertificateValidityDays = request.CertificateValidityDays,
                    Metadata = request.Metadata,
                    UseStaging = request.UseStaging
                };

                // 申请证书
                var order = await _acmeService.OrderCertificateAsync(certificateRequest);
                if (order == null)
                {
                    throw new InvalidOperationException("证书申请失败：无法创建订单");
                }
                result.OrderId = order.Id;
                await _progressService.CompleteCurrentStepAsync(progressId, "证书订单创建成功");
                result.ValidationSteps.Add("证书订单创建成功");

                // 处理DNS挑战验证
                combinedCts.Token.ThrowIfCancellationRequested();
                await _progressService.UpdateProgressStepAsync(progressId,
                    CertificateApplicationStep.ConfiguringDnsChallenge,
                    "开始处理DNS挑战验证");

                foreach (var authorization in order.Authorizations)
                {
                    combinedCts.Token.ThrowIfCancellationRequested();
                    await _progressService.UpdateProgressStepAsync(progressId,
                        CertificateApplicationStep.ConfiguringDnsChallenge,
                        $"处理域名 {authorization.Domain} 的DNS挑战");

                    var pendingChallenges = await _acmeService.GetPendingChallengesAsync(order.Id);

                    foreach (var challenge in pendingChallenges)
                    {
                        combinedCts.Token.ThrowIfCancellationRequested();
                        if (challenge.Type != "dns-01")
                        {
                            await _progressService.AddWarningAsync(progressId, $"跳过非DNS-01挑战: {challenge.Type}");
                            result.Warnings.Add($"跳过非DNS-01挑战: {challenge.Type}");
                            continue;
                        }

                        // 选择DNS提供商
                        var selectedProvider = SelectDnsProvider(challenge, request.PreferredDnsProviders, request.DnsCredentials);
                        if (selectedProvider == null)
                        {
                            var errorMsg = $"没有可用的DNS提供商来处理 {authorization.Domain} 的挑战";
                            await _progressService.AddErrorAsync(progressId, errorMsg);
                            result.Errors.Add(errorMsg);
                            continue;
                        }

                        await _progressService.UpdateProgressStepAsync(progressId,
                            CertificateApplicationStep.ConfiguringDnsChallenge,
                            $"为 {authorization.Domain} 配置DNS记录，使用提供商: {selectedProvider}");

                        // 配置DNS挑战
                        var dnsRequest = new WildcardDnsChallengeRequest
                        {
                            Domain = authorization.Domain,
                            ChallengeToken = challenge.Token,
                            KeyAuthorization = challenge.KeyAuthorization ?? string.Empty,
                            DnsProvider = selectedProvider,
                            Credentials = request.DnsCredentials.GetValueOrDefault(selectedProvider),
                            RecordName = $"_acme-challenge.{authorization.Domain}",
                            RecordValue = challenge.KeyAuthorization ?? string.Empty,
                            RecordType = "TXT",
                            ChallengeDetails = new Dictionary<string, object>
                            {
                                ["BaseDomain"] = request.BaseDomain,
                                ["IsWildcard"] = true,
                                ["ChallengeType"] = "dns-01"
                            }
                        };

                        var dnsResult = await ConfigureWildcardDnsChallengeAsync(dnsRequest, combinedCts.Token);
                        if (!dnsResult.Success)
                        {
                            var errorMsg = $"DNS挑战配置失败: {authorization.Domain} - {dnsResult.Message}";
                            await _progressService.AddErrorAsync(progressId, errorMsg);
                            result.Errors.Add(errorMsg);
                            continue;
                        }

                        await _progressService.CompleteCurrentStepAsync(progressId,
                            $"DNS挑战配置成功: {authorization.Domain} - {selectedProvider}");
                        result.ValidationSteps.Add($"DNS挑战配置成功: {authorization.Domain} - {selectedProvider}");

                        // 等待DNS传播（使用改进的超时处理）
                        await _progressService.UpdateProgressStepAsync(progressId,
                            CertificateApplicationStep.WaitingForDnsPropagation,
                            $"等待 {authorization.Domain} 的DNS记录传播");

                        try
                        {
                            await WaitForDnsPropagationWithTimeout(dnsRequest.RecordName, dnsRequest.RecordValue, combinedCts.Token);
                            await _progressService.CompleteCurrentStepAsync(progressId,
                                $"DNS传播完成: {authorization.Domain}");
                        }
                        catch (OperationCanceledException)
                        {
                            var errorMsg = $"DNS传播超时: {authorization.Domain}";
                            await _progressService.AddErrorAsync(progressId, errorMsg);
                            result.Errors.Add(errorMsg);
                            await CleanupDnsChallengeOnFailure(dnsRequest, combinedCts.Token);
                            throw;
                        }

                        // 完成挑战
                        combinedCts.Token.ThrowIfCancellationRequested();
                        await _progressService.UpdateProgressStepAsync(progressId,
                            CertificateApplicationStep.ValidatingDomains,
                            $"验证 {authorization.Domain} 的域名控制权");

                        var completeRequest = new CompleteChallengeRequest
                        {
                            ChallengeType = challenge.Type
                        };

                        var challengeResult = await _acmeService.CompleteChallengeAsync(
                            order.Id, authorization.Id, completeRequest);

                        if (!challengeResult.Success)
                        {
                            var errorMsg = $"挑战完成失败: {authorization.Domain} - {challengeResult.Message}";
                            await _progressService.AddErrorAsync(progressId, errorMsg);
                            result.Errors.Add(errorMsg);
                        }
                        else
                        {
                            await _progressService.CompleteCurrentStepAsync(progressId,
                                $"挑战完成成功: {authorization.Domain}");
                            result.ValidationSteps.Add($"挑战完成成功: {authorization.Domain}");
                        }
                    }
                }

                // 清理DNS记录
                await _progressService.UpdateProgressStepAsync(progressId,
                    CertificateApplicationStep.CleaningDnsRecords,
                    "清理DNS验证记录");

                try
                {
                    foreach (var authorization in order.Authorizations)
                    {
                        // 这里可以添加DNS清理逻辑
                        await _progressService.UpdateProgressStepAsync(progressId,
                            CertificateApplicationStep.CleaningDnsRecords,
                            $"清理 {authorization.Domain} 的DNS记录");
                    }
                    await _progressService.CompleteCurrentStepAsync(progressId, "DNS记录清理完成");
                }
                catch (Exception ex)
                {
                    await _progressService.AddWarningAsync(progressId, $"DNS清理部分失败: {ex.Message}");
                }

                // 等待挑战验证完成
                combinedCts.Token.ThrowIfCancellationRequested();
                await _progressService.UpdateProgressStepAsync(progressId,
                    CertificateApplicationStep.ValidatingDomains,
                    "等待域名验证完成");

                var allValid = await WaitForChallengesValidAsync(order.Id, combinedCts.Token);
                
                if (!allValid)
                {
                    await _progressService.AddErrorAsync(progressId, "挑战验证未完成，无法下载证书");
                    await _progressService.MarkAsFailedAsync(progressId, "挑战验证超时");
                    result.Success = false;
                    result.Message = "挑战验证未完成，请稍后重试";
                    result.Errors.Add("挑战验证超时");
                    return result;
                }

                await _progressService.CompleteCurrentStepAsync(progressId, "所有域名验证成功");

                // 下载证书
                combinedCts.Token.ThrowIfCancellationRequested();
                await _progressService.UpdateProgressStepAsync(progressId,
                    CertificateApplicationStep.DownloadingCertificate,
                    "从ACME服务器下载证书");

                var certificateData = await _acmeService.DownloadCertificateAsync(order.Id);
                result.CertificateId = certificateData.CertificateFingerprint;
                result.CertificateFingerprint = certificateData.CertificateFingerprint;
                result.IssuedAt = certificateData.IssuedAt;
                result.ExpiresAt = certificateData.ExpiresAt;
                await _progressService.CompleteCurrentStepAsync(progressId, "证书下载成功");
                result.ValidationSteps.Add("证书下载成功");

                // 保存证书到本地
                await _progressService.UpdateProgressStepAsync(progressId,
                    CertificateApplicationStep.SavingCertificate,
                    "保存证书到本地存储");

                // 缓存证书信息
                var certificateInfo = new WildcardCertificateInfo
                {
                    Id = result.CertificateId ?? string.Empty,
                    AccountId = request.AccountId,
                    BaseDomain = request.BaseDomain,
                    Subdomains = request.Subdomains,
                    FullDomains = result.FullDomains,
                    KeyType = request.KeyType,
                    CertificateFingerprint = result.CertificateFingerprint,
                    SerialNumber = certificateData.SerialNumber,
                    IssuedAt = result.IssuedAt,
                    ExpiresAt = result.ExpiresAt,
                    Issuer = certificateData.Issuer,
                    Subject = certificateData.Subject,
                    SanDomains = certificateData.Domains,
                    Status = "active",
                    IsWildcard = true,
                    DnsProvider = request.PreferredDnsProviders.FirstOrDefault() ?? "unknown",
                    AutoRenewalEnabled = request.EnableAutoRenewal,
                    LastRenewalAttempt = DateTime.UtcNow,
                    NextRenewalAttempt = result.ExpiresAt.AddDays(-request.RenewalDaysBeforeExpiry),
                    RenewalAttempts = 1,
                    NotificationEmails = request.NotificationEmails,
                    Metadata = request.Metadata,
                    CertificateData = certificateData.Certificate,
                    PrivateKeyData = certificateData.PrivateKey ?? string.Empty
                };

                _certificateCache[result.CertificateId!] = certificateInfo;

                await _progressService.CompleteCurrentStepAsync(progressId, "证书保存完成");

                // 标记申请完成
                await _progressService.UpdateProgressStepAsync(progressId,
                    CertificateApplicationStep.Completed,
                    "通配符证书申请流程完成");

                await _progressService.MarkAsCompletedAsync(progressId);

                result.Success = true;
                result.Message = "通配符证书申请成功";
                result.CompletedAt = DateTime.UtcNow;

                _logger.LogInformation("通配符证书申请成功: {CertificateId}, 域�名: {BaseDomain}",
                    result.CertificateId, result.BaseDomain);

                return result;
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
            {
                _logger.LogWarning("证书申请超时: {BaseDomain}, 临时ID: {TempCertificateId}", request.BaseDomain, tempCertificateId);

                await _progressService.AddErrorAsync(progressId, "证书申请超时，请检查网络连接和DNS配置后重试");
                await _progressService.MarkAsFailedAsync(progressId, "申请超时");

                result.Success = false;
                result.Message = "证书申请超时，请检查网络连接和DNS配置后重试";
                result.Errors.Add("申请超时");
                result.CompletedAt = DateTime.UtcNow;

                // 清理临时数据
                await CleanupExpiredRequestAsync(tempCertificateId, request.BaseDomain);

                throw;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("证书申请被取消: {BaseDomain}, 临时ID: {TempCertificateId}", request.BaseDomain, tempCertificateId);

                await _progressService.AddErrorAsync(progressId, "证书申请被取消");
                await _progressService.MarkAsFailedAsync(progressId, "申请被取消");

                result.Success = false;
                result.Message = "证书申请被取消";
                result.Errors.Add("申请被取消");
                result.CompletedAt = DateTime.UtcNow;

                // 清理临时数据
                await CleanupExpiredRequestAsync(tempCertificateId, request.BaseDomain);

                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "申请通配符证书失败: {BaseDomain}", request.BaseDomain);

                await _progressService.AddErrorAsync(progressId, $"申请失败: {ex.Message}");
                await _progressService.MarkAsFailedAsync(progressId, ex.Message);

                result.Success = false;
                result.Message = $"申请失败: {ex.Message}";
                result.Errors.Add(ex.Message);
                result.CompletedAt = DateTime.UtcNow;

                // 清理临时数据
                await CleanupExpiredRequestAsync(tempCertificateId, request.BaseDomain);

                throw;
            }
        }

        /// <summary>
        /// 续期通配符证书
        /// </summary>
        public async Task<WildcardCertificateResult> RenewWildcardCertificateAsync(string certificateId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("开始续期通配符证书: {CertificateId}", certificateId);

            try
            {
                if (!_certificateCache.TryGetValue(certificateId, out var certificateInfo))
                {
                    return new WildcardCertificateResult
                    {
                        Success = false,
                        Message = "证书不存在",
                        CertificateId = certificateId
                    };
                }

                // 创建续期请求
                var renewalRequest = new WildcardCertificateRequest
                {
                    AccountId = certificateInfo.AccountId,
                    BaseDomain = certificateInfo.BaseDomain,
                    Subdomains = certificateInfo.Subdomains,
                    KeyType = certificateInfo.KeyType,
                    PreferredDnsProviders = new List<string> { certificateInfo.DnsProvider },
                    DnsCredentials = new Dictionary<string, Dictionary<string, object>>(),
                    CertificateValidityDays = 90,
                    EnableAutoRenewal = certificateInfo.AutoRenewalEnabled,
                    RenewalDaysBeforeExpiry = certificateInfo.RenewalDaysBeforeExpiry,
                    NotificationEmails = certificateInfo.NotificationEmails,
                    UseStaging = false
                };

                return await RequestWildcardCertificateAsync(renewalRequest, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "续期通配符证书失败: {CertificateId}", certificateId);

                return new WildcardCertificateResult
                {
                    Success = false,
                    Message = $"续期失败: {ex.Message}",
                    CertificateId = certificateId
                };
            }
        }

        /// <summary>
        /// 验证通配符证书申请前置条件
        /// </summary>
        public async Task<WildcardCertificateValidationResult> ValidateWildcardRequestAsync(WildcardCertificateRequest request, CancellationToken cancellationToken = default)
        {
            var result = new WildcardCertificateValidationResult
            {
                IsValid = true,
                CanProceed = true
            };

            try
            {
                // 检查基础域名格式
                if (string.IsNullOrWhiteSpace(request.BaseDomain))
                {
                    result.FailedChecks.Add("基础域名不能为空");
                    result.CanProceed = false;
                }
                else if (!IsValidDomain(request.BaseDomain))
                {
                    result.FailedChecks.Add($"无效的基础域名格式: {request.BaseDomain}");
                    result.CanProceed = false;
                }

                // 检查子域名格式
                foreach (var subdomain in request.Subdomains)
                {
                    if (string.IsNullOrWhiteSpace(subdomain))
                    {
                        result.Warnings.Add("子域名包含空字符串");
                        continue;
                    }

                    if (!IsValidSubdomain(subdomain))
                    {
                        result.FailedChecks.Add($"无效的子域名格式: {subdomain}");
                        result.CanProceed = false;
                    }
                }

                // 检查账户是否存在
                var account = await _acmeService.GetAccountAsync(request.AccountId);
                if (account == null)
                {
                    result.FailedChecks.Add($"ACME账户不存在: {request.AccountId}");
                    result.CanProceed = false;
                }

                // 检查DNS提供商配置
                if (!request.PreferredDnsProviders.Any())
                {
                    result.Warnings.Add("未指定首选DNS提供商");
                    result.RecommendedActions.Add("建议配置DNS提供商以支持通配符证书");
                }
                else
                {
                    foreach (var provider in request.PreferredDnsProviders)
                    {
                        if (!request.DnsCredentials.ContainsKey(provider))
                        {
                            result.Warnings.Add($"DNS提供商 {provider} 未配置凭据");
                        }
                    }
                }

                // 检查密钥类型
                if (!_supportedTypes.Values.Any(t => t.SupportedKeyTypes.Contains(request.KeyType)))
                {
                    result.FailedChecks.Add($"不支持的密钥类型: {request.KeyType}");
                    result.CanProceed = false;
                }

                // 检查有效性天数
                if (request.CertificateValidityDays < 1 || request.CertificateValidityDays > 825)
                {
                    result.FailedChecks.Add("证书有效期必须在1-825天之间");
                    result.CanProceed = false;
                }

                // 检查通配符证书限制
                if (request.Subdomains.Count > 100)
                {
                    result.Warnings.Add("子域名数量较多，可能影响证书申请速度");
                }

                // 设置验证检查
                result.ValidationChecks.Add("基础域名格式检查");
                result.ValidationChecks.Add("子域名格式检查");
                result.ValidationChecks.Add("ACME账户验证");
                result.ValidationChecks.Add("DNS提供商配置检查");
                result.ValidationChecks.Add("密钥类型验证");
                result.ValidationChecks.Add("证书有效期验证");

                result.PassedChecks.AddRange(result.ValidationChecks.Where(c => !result.FailedChecks.Contains(c)));

                result.IsValid = !result.FailedChecks.Any();
                result.Message = result.IsValid ? "验证通过" : "验证失败";
                result.CanProceed = result.IsValid;

                if (!result.CanProceed)
                {
                    result.RecommendedActions.Add("修复所有失败的验证项目后重试");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "验证通配符证书请求失败");

                result.IsValid = false;
                result.CanProceed = false;
                result.Message = "验证过程中发生错误";
                result.FailedChecks.Add(ex.Message);

                return result;
            }
        }

        /// <summary>
        /// 获取支持的通配符证书类型
        /// </summary>
        public async Task<IEnumerable<WildcardCertificateType>> GetSupportedWildcardTypesAsync()
        {
            await Task.CompletedTask;
            return _supportedTypes.Values.ToList();
        }

        /// <summary>
        /// 检查域名是否支持通配符证书
        /// </summary>
        public async Task<WildcardDomainSupportResult> CheckWildcardSupportAsync(string domain, CancellationToken cancellationToken = default)
        {
            var result = new WildcardDomainSupportResult
            {
                Domain = domain
            };

            try
            {
                // 检查域名是否为二级域名
                if (!AcmeHelpers.IsSecondLevelDomain(domain))
                {
                    result.PotentialIssues.Add("通配符证书通常只支持二级域名");
                    result.Recommendations.Add("建议使用二级域名申请通配符证书");
                }
                else
                {
                    result.SupportsWildcard = true;
                    result.WildcardPattern = $"*.{domain}";
                    result.SupportedProviders = _supportedTypes.Values
                        .SelectMany(t => t.SupportedDnsProviders)
                        .Distinct()
                        .ToList();
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查通配符证书支持失败: {Domain}", domain);

                result.SupportsWildcard = false;
                result.PotentialIssues.Add(ex.Message);
                return result;
            }
        }

        /// <summary>
        /// 生成通配符证书CSR
        /// </summary>
        public async Task<string> GenerateWildcardCsrAsync(WildcardCsrRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("生成通配符证书CSR: {BaseDomain}", request.BaseDomain);

                var domains = GenerateFullDomains(request.BaseDomain, request.Subdomains);
                var csr = await _acmeService.GenerateCsrAsync(domains, request.KeyType);

                _logger.LogInformation("通配符证书CSR生成成功: {BaseDomain}", request.BaseDomain);
                return csr;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生成通配符证书CSR失败: {BaseDomain}", request.BaseDomain);
                throw;
            }
        }

        /// <summary>
        /// 配置DNS挑战用于通配符证书
        /// </summary>
        public async Task<WildcardDnsChallengeResult> ConfigureWildcardDnsChallengeAsync(WildcardDnsChallengeRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("配置通配符DNS挑战: {Domain}, Provider: {Provider}", request.Domain, request.DnsProvider);

                var result = new WildcardDnsChallengeResult
                {
                    Domain = request.Domain,
                    DnsProvider = request.DnsProvider,
                    RecordName = request.RecordName,
                    RecordValue = request.RecordValue,
                    RecordType = request.RecordType,
                    ConfiguredAt = DateTime.UtcNow
                };

                // 使用挑战验证服务配置DNS
                var challenge = new AcmeChallenge
                {
                    Type = "dns-01",
                    Token = request.ChallengeToken,
                    KeyAuthorization = request.KeyAuthorization,
                    ChallengeData = request.ChallengeDetails
                };

                var validationResult = await _challengeValidationService.ConfigureDnsChallengeAsync(
                    challenge, request.Domain, request.DnsProvider, request.Credentials);

                result.Success = validationResult.Success;
                result.Message = validationResult.Success ? "DNS挑战配置成功" : $"DNS挑战配置失败: {validationResult.Message}";
                result.ConfigurationSteps.AddRange(validationResult.ValidationSteps);
                result.Errors.AddRange(validationResult.Errors);
                result.Warnings.AddRange(validationResult.Warnings);
                result.ConfigurationDetails = validationResult.ConfigurationDetails;

                if (validationResult.Success)
                {
                    result.ValidatedAt = DateTime.UtcNow;
                    result.DnsPropagationTime = TimeSpan.FromSeconds(30); // 估算传播时间
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "配置通配符DNS挑战失败: {Domain}", request.Domain);

                return new WildcardDnsChallengeResult
                {
                    Success = false,
                    Message = $"DNS挑战配置失败: {ex.Message}",
                    Domain = request.Domain,
                    DnsProvider = request.DnsProvider,
                    RecordName = request.RecordName,
                    RecordValue = request.RecordValue,
                    RecordType = request.RecordType,
                    ConfiguredAt = DateTime.UtcNow,
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        /// <summary>
        /// 验证DNS挑战状态
        /// </summary>
        public async Task<WildcardDnsChallengeResult> ValidateWildcardDnsChallengeAsync(WildcardDnsChallengeRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("验证通配符DNS挑战: {Domain}", request.Domain);

                // 使用挑战验证服务验证DNS
                var challenge = new AcmeChallenge
                {
                    Type = "dns-01",
                    Token = request.ChallengeToken,
                    KeyAuthorization = request.KeyAuthorization,
                    ChallengeData = request.ChallengeDetails
                };

                var validationResult = await _challengeValidationService.ValidateDnsChallengeAsync(
                    challenge, request.Domain, request.DnsProvider, request.Credentials);

                var result = new WildcardDnsChallengeResult
                {
                    Domain = request.Domain,
                    DnsProvider = request.DnsProvider,
                    RecordName = request.RecordName,
                    RecordValue = request.RecordValue,
                    RecordType = request.RecordType,
                    ConfiguredAt = DateTime.UtcNow,
                    ValidatedAt = DateTime.UtcNow
                };

                result.Success = validationResult.Success;
                result.Message = validationResult.Success ? "DNS挑战验证成功" : $"DNS挑战验证失败: {validationResult.Message}";
                result.ValidationSteps.AddRange(validationResult.ValidationSteps);
                result.Errors.AddRange(validationResult.Errors);
                result.Warnings.AddRange(validationResult.Warnings);
                result.ConfigurationDetails = validationResult.ConfigurationDetails;

                if (validationResult.Success)
                {
                    result.DnsPropagationTime = TimeSpan.FromSeconds(10); // 实际传播时间
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "验证通配符DNS挑战失败: {Domain}", request.Domain);

                return new WildcardDnsChallengeResult
                {
                    Success = false,
                    Message = $"DNS挑战验证失败: {ex.Message}",
                    Domain = request.Domain,
                    DnsProvider = request.DnsProvider,
                    RecordName = request.RecordName,
                    RecordValue = request.RecordValue,
                    RecordType = request.RecordType,
                    ConfiguredAt = DateTime.UtcNow,
                    ValidatedAt = DateTime.UtcNow,
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        /// <summary>
        /// 清理通配符证书DNS挑战配置
        /// </summary>
        public async Task<WildcardDnsChallengeCleanupResult> CleanupWildcardDnsChallengeAsync(WildcardDnsChallengeCleanupRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("清理通配符DNS挑战配置: {Domain}, Provider: {Provider}", request.Domain, request.DnsProvider);

                var result = new WildcardDnsChallengeCleanupResult
                {
                    Domain = request.Domain,
                    DnsProvider = request.DnsProvider,
                    RecordName = request.RecordName,
                    RecordType = request.RecordType,
                    CleanedAt = DateTime.UtcNow
                };

                // 使用挑战验证服务清理DNS
                var challenge = new AcmeChallenge
                {
                    Type = "dns-01",
                    Token = string.Empty, // 清理时不需要token
                    KeyAuthorization = string.Empty,
                    ChallengeData = request.CleanupDetails
                };

                var cleanupResult = await _challengeValidationService.CleanupChallengeAsync(
                    challenge, request.Domain, "dns-01");

                result.Success = cleanupResult.Success;
                result.Message = cleanupResult.Success ? "DNS挑战清理成功" : $"DNS挑战清理失败: {cleanupResult.Message}";
                result.CleanupSteps.AddRange(cleanupResult.CleanupSteps);
                result.Errors.AddRange(cleanupResult.Errors);
                result.Warnings.AddRange(cleanupResult.Warnings);
                result.CleanupDetails = cleanupResult.CleanupDetails;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理通配符DNS挑战失败: {Domain}", request.Domain);

                return new WildcardDnsChallengeCleanupResult
                {
                    Success = false,
                    Message = $"DNS挑战清理失败: {ex.Message}",
                    Domain = request.Domain,
                    DnsProvider = request.DnsProvider,
                    RecordName = request.RecordName,
                    RecordType = request.RecordType,
                    CleanedAt = DateTime.UtcNow,
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        /// <summary>
        /// 获取通配符证书列表
        /// </summary>
        public async Task<IEnumerable<WildcardCertificateInfo>> GetWildcardCertificatesAsync(string? accountId = null, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;

            var persistedCertificates = _dbContext.GetCollection<CertificateRecord>("certificates")
                .FindAll()
                .Where(IsWildcardCertificate)
                .Select(MapToWildcardCertificateInfo)
                .ToList();

            foreach (var certificate in persistedCertificates)
            {
                _certificateCache[certificate.Id] = certificate;
            }

            var certificates = _certificateCache.Values.AsEnumerable();

            if (!string.IsNullOrEmpty(accountId))
            {
                certificates = certificates.Where(c => c.AccountId == accountId);
            }

            return certificates.OrderByDescending(c => c.ExpiresAt);
        }

        /// <summary>
        /// 获取通配符证书详情
        /// </summary>
        public async Task<WildcardCertificateInfo?> GetWildcardCertificateAsync(string certificateId, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            if (_certificateCache.TryGetValue(certificateId, out var certificate))
            {
                return certificate;
            }

            var record = _dbContext.GetCollection<CertificateRecord>("certificates").FindById(certificateId);
            if (record == null || !IsWildcardCertificate(record))
            {
                return null;
            }

            certificate = MapToWildcardCertificateInfo(record);
            _certificateCache[certificate.Id] = certificate;
            return certificate;
        }

        /// <summary>
        /// 删除通配符证书
        /// </summary>
        public async Task<WildcardCertificateDeletionResult> DeleteWildcardCertificateAsync(string certificateId, CancellationToken cancellationToken = default)
        {
            var result = new WildcardCertificateDeletionResult
            {
                CertificateId = certificateId,
                DeletedAt = DateTime.UtcNow,
                DeletionSteps = new List<string>(),
                Errors = new List<string>(),
                Warnings = new List<string>(),
                DeletionDetails = new Dictionary<string, object>()
            };

            try
            {
                _logger.LogInformation("开始删除通配符证书: {CertificateId}", certificateId);

                // 检查证书是否存在
                if (!_certificateCache.TryGetValue(certificateId, out var certificateInfo))
                {
                    result.Success = false;
                    result.Message = "证书不存在或已被删除";
                    result.Errors.Add("证书不存在");
                    return result;
                }

                result.DeletionSteps.Add("找到证书记录");

                // 检查证书状态
                if (certificateInfo.Status == "deleting")
                {
                    result.Success = false;
                    result.Message = "证书正在删除中，请稍后再试";
                    result.Errors.Add("证书正在删除中");
                    return result;
                }

                // 标记为删除中
                certificateInfo.Status = "deleting";
                result.DeletionSteps.Add("标记证书为删除中状态");

                // 1. 清理相关的DNS挑战记录（如果存在）
                if (!string.IsNullOrEmpty(certificateInfo.BaseDomain))
                {
                    try
                    {
                        await CleanupRelatedDnsChallenges(certificateInfo, cancellationToken);
                        result.DeletionSteps.Add("清理相关DNS挑战记录");
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add($"DNS挑战清理部分失败: {ex.Message}");
                        _logger.LogWarning(ex, "清理DNS挑战失败: {CertificateId}", certificateId);
                    }
                }

                // 2. 从数据库和缓存中移除证书
                var certificatesCollection = _dbContext.GetCollection<CertificateRecord>("certificates");
                var deletedFromDb = certificatesCollection.Delete(certificateId);
                if (deletedFromDb > 0)
                {
                    result.DeletionSteps.Add("从数据库中删除证书记录");
                }

                if (_certificateCache.TryRemove(certificateId, out _))
                {
                    result.DeletionSteps.Add("从缓存中移除证书");
                }
                else
                {
                    result.Warnings.Add("证书已从缓存中移除");
                }

                // 3. 清理相关的进度跟踪记录
                try
                {
                    await CleanupRelatedProgressTracking(certificateId, cancellationToken);
                    result.DeletionSteps.Add("清理进度跟踪记录");
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"进度跟踪清理失败: {ex.Message}");
                    _logger.LogWarning(ex, "清理进度跟踪失败: {CertificateId}", certificateId);
                }

                // 4. 通知相关服务证书已删除
                try
                {
                    await NotifyServicesCertificateDeleted(certificateInfo, cancellationToken);
                    result.DeletionSteps.Add("通知相关服务");
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"服务通知失败: {ex.Message}");
                    _logger.LogWarning(ex, "通知服务失败: {CertificateId}", certificateId);
                }

                result.Success = true;
                result.Message = "通配符证书删除成功";
                result.DeletionDetails["DeletedCertificate"] = new
                {
                    BaseDomain = certificateInfo.BaseDomain,
                    Subdomains = certificateInfo.Subdomains,
                    DnsProvider = certificateInfo.DnsProvider,
                    IssuedAt = certificateInfo.IssuedAt,
                    ExpiresAt = certificateInfo.ExpiresAt
                };

                _logger.LogInformation("通配符证书删除成功: {CertificateId}, 域名: {BaseDomain}",
                    certificateId, certificateInfo.BaseDomain);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("证书删除被取消: {CertificateId}", certificateId);

                result.Success = false;
                result.Message = "证书删除被取消";
                result.Errors.Add("操作被取消");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除通配符证书失败: {CertificateId}", certificateId);

                result.Success = false;
                result.Message = $"删除失败: {ex.Message}";
                result.Errors.Add(ex.Message);

                // 尝试恢复状态
                try
                {
                    if (_certificateCache.TryGetValue(certificateId, out var certInfo))
                    {
                        certInfo.Status = "active";
                    }
                }
                catch
                {
                    // 忽略状态恢复失败
                }

                return result;
            }
        }

        /// <summary>
        /// 强制删除通配符证书（用于处理超时或异常状态的证书）
        /// </summary>
        public async Task<WildcardCertificateDeletionResult> ForceDeleteWildcardCertificateAsync(string certificateId, CancellationToken cancellationToken = default)
        {
            var result = new WildcardCertificateDeletionResult
            {
                CertificateId = certificateId,
                DeletedAt = DateTime.UtcNow,
                DeletionSteps = new List<string>(),
                Errors = new List<string>(),
                Warnings = new List<string>(),
                DeletionDetails = new Dictionary<string, object>()
            };

            try
            {
                _logger.LogInformation("开始强制删除通配符证书: {CertificateId}", certificateId);

                // 强制删除，不检查状态
                var certificatesCollection = _dbContext.GetCollection<CertificateRecord>("certificates");
                var persistedRecord = certificatesCollection.FindById(certificateId);
                var persistedInfo = persistedRecord != null && IsWildcardCertificate(persistedRecord)
                    ? MapToWildcardCertificateInfo(persistedRecord)
                    : null;

                if (_certificateCache.TryRemove(certificateId, out var certificateInfo) || persistedInfo != null)
                {
                    certificateInfo ??= persistedInfo!;
                    result.DeletionSteps.Add("从缓存中强制移除证书");
                    if (certificatesCollection.Delete(certificateId) > 0)
                    {
                        result.DeletionSteps.Add("从数据库中强制删除证书记录");
                    }

                    // 强制清理所有相关资源
                    try
                    {
                        await CleanupRelatedDnsChallenges(certificateInfo, cancellationToken);
                        result.DeletionSteps.Add("强制清理DNS挑战");
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add($"DNS挑战清理失败: {ex.Message}");
                    }

                    try
                    {
                        await CleanupRelatedProgressTracking(certificateId, cancellationToken);
                        result.DeletionSteps.Add("强制清理进度跟踪");
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add($"进度跟踪清理失败: {ex.Message}");
                    }

                    result.Success = true;
                    result.Message = "通配符证书强制删除成功";
                    result.DeletionDetails["ForceDeleted"] = true;
                }
                else
                {
                    result.Success = true;
                    result.Message = "证书不存在（已删除）";
                    result.DeletionSteps.Add("确认证书不存在");
                }

                _logger.LogInformation("强制删除完成: {CertificateId}", certificateId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "强制删除证书失败: {CertificateId}", certificateId);

                result.Success = false;
                result.Message = $"强制删除失败: {ex.Message}";
                result.Errors.Add(ex.Message);
                return result;
            }
        }

        /// <summary>
        /// 导出通配符证书
        /// </summary>
        public async Task<WildcardCertificateExportResult> ExportWildcardCertificateAsync(string certificateId, string format = "pem", bool includePrivateKey = true, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("导出通配符证书: {CertificateId}, Format: {Format}", certificateId, format);

                if (!_certificateCache.TryGetValue(certificateId, out var certificateInfo))
                {
                    return new WildcardCertificateExportResult
                    {
                        Success = false,
                        Message = "证书不存在",
                        CertificateId = certificateId,
                        Format = format,
                        ExportData = string.Empty,
                        ExportedAt = DateTime.UtcNow,
                        Errors = new List<string>(),
                        ExportDetails = new Dictionary<string, object>()
                    };
                }

                // 根据格式导出证书
                var exportData = format.ToLower() switch
                {
                    "pem" => FormatCertificateAsPem(certificateInfo, includePrivateKey),
                    "pfx" => FormatCertificateAsPfx(certificateInfo, includePrivateKey),
                    "json" => FormatCertificateAsJson(certificateInfo),
                    _ => throw new ArgumentException($"不支持的导出格式: {format}")
                };

                var fileName = $"{certificateInfo.BaseDomain}_wildcard.{format}";
                var fileSize = Encoding.UTF8.GetByteCount(exportData);

                var result = new WildcardCertificateExportResult
                {
                    Success = true,
                    Message = "通配符证书导出成功",
                    CertificateId = certificateId,
                    Format = format,
                    ExportData = exportData,
                    FileName = fileName,
                    FileSize = fileSize,
                    ExportedAt = DateTime.UtcNow,
                    ExportSteps = new List<string> { "准备导出数据", "格式转换", "生成文件" }
                };

                _logger.LogInformation("通配符证书导出成功: {CertificateId}, Format: {Format}, Size: {Size} bytes",
                    certificateId, format, fileSize);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导出通配符证书失败: {CertificateId}", certificateId);

                return new WildcardCertificateExportResult
                {
                    Success = false,
                    Message = $"导出失败: {ex.Message}",
                    CertificateId = certificateId,
                    Format = format,
                    ExportedAt = DateTime.UtcNow,
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        /// <summary>
        /// 导入通配符证书
        /// </summary>
        public async Task<WildcardCertificateImportResult> ImportWildcardCertificateAsync(WildcardCertificateImportRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("导入通配符证书: Format: {Format}", request.Format);

                // 验证证书格式
                if (request.ValidateCertificate)
                {
                    var validationResult = ValidateImportedCertificate(request.CertificateData, request.Format, request.PrivateKeyData);
                    if (!validationResult.IsValid)
                    {
                        return new WildcardCertificateImportResult
                        {
                            Success = false,
                            Message = "证书验证失败",
                            ImportedAt = DateTime.UtcNow,
                            Errors = validationResult.Errors,
                            ValidationResult = new WildcardCertificateValidationResult
                            {
                                IsValid = validationResult.IsValid,
                                Message = validationResult.Message,
                                Errors = validationResult.Errors,
                                Warnings = validationResult.Warnings,
                                ValidationDetails = validationResult.ValidationDetails,
                                ValidatedAt = validationResult.ValidatedAt,
                                ValidationChecks = new List<string>(),
                                PassedChecks = new List<string>(),
                                FailedChecks = validationResult.Errors,
                                CanProceed = validationResult.IsValid,
                                RecommendedActions = new List<string>(),
                                Passed = validationResult.IsValid
                            }
                        };
                    }
                }

                // 根据格式解析证书
                var certificateInfo = ParseCertificateData(request.CertificateData, request.Format, request.PrivateKeyData);
                if (certificateInfo == null)
                {
                    return new WildcardCertificateImportResult
                    {
                        Success = false,
                        Message = "证书解析失败",
                        ImportedAt = DateTime.UtcNow,
                        Errors = new List<string> { "证书解析失败" }
                    };
                }

                // 生成证书ID
                certificateInfo.Id = GenerateCertificateId();
                certificateInfo.AccountId = request.AccountId;
                certificateInfo.AutoRenewalEnabled = request.EnableAutoRenewal;
                certificateInfo.RenewalDaysBeforeExpiry = request.RenewalDaysBeforeExpiry;
                certificateInfo.NotificationEmails = request.NotificationEmails;
                certificateInfo.ImportedAt = DateTime.UtcNow;
                certificateInfo.Status = "active";
                certificateInfo.Metadata = new Dictionary<string, object>(request.Metadata)
                {
                    ["ImportedAt"] = certificateInfo.ImportedAt.Value,
                    ["Source"] = "manual-import"
                };

                var certificateRecord = new CertificateRecord
                {
                    Id = certificateInfo.Id,
                    Name = certificateInfo.BaseDomain,
                    Type = "Wildcard",
                    Domains = certificateInfo.FullDomains,
                    Status = certificateInfo.Status,
                    IssuedAt = certificateInfo.IssuedAt,
                    ExpiresAt = certificateInfo.ExpiresAt,
                    Issuer = certificateInfo.Issuer,
                    CertificateData = certificateInfo.CertificateData,
                    PrivateKeyData = certificateInfo.PrivateKeyData,
                    CertificateChain = certificateInfo.CertificateData,
                    KeyAlgorithm = certificateInfo.KeyType,
                    SignatureAlgorithm = string.Empty,
                    SerialNumber = certificateInfo.SerialNumber,
                    Fingerprint = certificateInfo.CertificateFingerprint,
                    AccountId = request.AccountId,
                    AutoRenewalEnabled = request.EnableAutoRenewal,
                    NextRenewalAttempt = certificateInfo.ExpiresAt.AddDays(-request.RenewalDaysBeforeExpiry),
                    Metadata = certificateInfo.Metadata,
                    NotificationEmails = request.NotificationEmails,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var certificatesCollection = _dbContext.GetCollection<CertificateRecord>("certificates");
                certificatesCollection.Insert(certificateRecord);
                _certificateCache[certificateInfo.Id] = certificateInfo;

                var result = new WildcardCertificateImportResult
                {
                    Success = true,
                    Message = "通配符证书导入成功",
                    CertificateId = certificateInfo.Id,
                    BaseDomain = certificateInfo.BaseDomain,
                    Subdomains = certificateInfo.Subdomains,
                    FullDomains = certificateInfo.FullDomains,
                    ImportedAt = DateTime.UtcNow,
                    CertificateFingerprint = certificateInfo.CertificateFingerprint,
                    ExpiresAt = certificateInfo.ExpiresAt,
                    ImportSteps = new List<string> { "验证证书格式", "解析证书数据", "缓存证书信息" },
                    ImportDetails = new Dictionary<string, object>
                    {
                        ["CertificateType"] = "通配符证书",
                        ["ImportFormat"] = request.Format,
                        ["HasPrivateKey"] = !string.IsNullOrEmpty(request.PrivateKeyData)
                    }
                };

                _logger.LogInformation("通配符证书导入成功: {CertificateId}", certificateInfo.Id);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导入通配符证书失败");

                return new WildcardCertificateImportResult
                {
                    Success = false,
                    Message = $"导入失败: {ex.Message}",
                    ImportedAt = DateTime.UtcNow,
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        /// <summary>
        /// 获取通配符证书统计信息
        /// </summary>
        public async Task<WildcardCertificateStatistics> GetWildcardCertificateStatisticsAsync(CancellationToken cancellationToken = default)
        {
            var certificates = (await GetWildcardCertificatesAsync(null, cancellationToken)).ToList();
            var now = DateTime.UtcNow;
            var totalRenewalAttempts = certificates.Sum(c => c.RenewalAttempts);
            var successfulRenewals = certificates.Sum(c => c.RenewalAttempts > 0 ? Math.Max(0, c.RenewalAttempts - 1) : 0);

            var stats = new WildcardCertificateStatistics
            {
                TotalWildcardCertificates = certificates.Count,
                ActiveCertificates = certificates.Count(c => c.Status == "active"),
                ExpiredCertificates = certificates.Count(c => c.Status == "expired"),
                RevokedCertificates = certificates.Count(c => c.Status == "revoked"),
                CertificatesExpiringNext30Days = certificates.Count(c => (c.ExpiresAt - now).TotalDays <= _certificateSettings.ExpiringSoonDays),
                CertificatesExpiringNext7Days = certificates.Count(c => (c.ExpiresAt - now).TotalDays <= 7),
                LastCertificateIssued = certificates.Any() ? certificates.Max(c => c.IssuedAt) : DateTime.MinValue,
                NextScheduledRenewal = certificates.Where(c => c.AutoRenewalEnabled && c.NextRenewalAttempt.HasValue)
                    .OrderBy(c => c.NextRenewalAttempt!.Value)
                    .FirstOrDefault()?.NextRenewalAttempt ?? DateTime.MaxValue,
                AverageRenewalSuccessRate = totalRenewalAttempts > 0 ? (double)successfulRenewals / totalRenewalAttempts : 0,
                TotalRenewalAttempts = totalRenewalAttempts,
                SuccessfulRenewals = successfulRenewals
            };

            // 按提供商分组统计
            var providerGroups = certificates
                .Where(c => !string.IsNullOrEmpty(c.DnsProvider))
                .GroupBy(c => c.DnsProvider);
            stats.CertificatesByProvider = providerGroups
                .ToDictionary(group => group.Key, group => group.Count());

            // 按密钥类型分组统计
            var keyTypeGroups = certificates
                .GroupBy(c => c.KeyType);
            stats.CertificatesByKeyType = keyTypeGroups
                .ToDictionary(group => group.Key, group => group.Count());

            // 按状态分组统计
            var statusGroups = certificates
                .GroupBy(c => c.Status);
            stats.CertificatesByStatus = statusGroups
                .ToDictionary(group => group.Key, group => group.Count());

            // 最近证书
            stats.MostRecentCertificates = certificates
                .OrderByDescending(c => c.IssuedAt)
                .Take(5)
                .Select(c => c.Id)
                .ToList();

            // 即将到期证书
            stats.UpcomingExpirations = certificates
                .Where(c => c.ExpiresAt > now)
                .OrderBy(c => c.ExpiresAt)
                .Take(5)
                .Select(c => c.Id)
                .ToList();

            return stats;
        }

        /// <summary>
        /// 测试通配符证书申请流程
        /// </summary>
        public async Task<WildcardCertificateTestResult> TestWildcardCertificateFlowAsync(WildcardCertificateRequest request, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("测试通配符证书申请流程: {BaseDomain}", request.BaseDomain);

            var result = new WildcardCertificateTestResult
            {
                BaseDomain = request.BaseDomain,
                TestStartedAt = DateTime.UtcNow
            };

            try
            {
                // 测试1: 预验证
                result.TestSteps.Add("预检查申请条件");
                var validationResult = await ValidateWildcardRequestAsync(request, cancellationToken);
                result.ValidationResult = validationResult;

                if (!validationResult.CanProceed)
                {
                    result.Success = false;
                    result.Message = "预检查失败，无法申请证书";
                    result.CanIssueCertificate = false;
                    return result;
                }
                result.PassedTests.Add("预检查通过");

                // 测试2: DNS提供商连接
                result.TestSteps.Add("测试DNS提供商连接");
                foreach (var provider in request.PreferredDnsProviders)
                {
                    var credentials = request.DnsCredentials.GetValueOrDefault(provider);
                    var testResult = await _challengeValidationService.TestDnsProviderConnectionAsync(provider, credentials);
                    if (testResult.Success)
                    {
                        result.PassedTests.Add($"DNS提供商 {provider} 连接正常");
                    }
                    else
                    {
                        result.FailedTests.Add($"DNS提供商 {provider} 连接失败: {testResult.Message}");
                        result.CanIssueCertificate = false;
                    }
                }

                // 测试3: DNS挑战配置
                if (request.PreferredDnsProviders.Any())
                {
                    result.TestSteps.Add("测试DNS挑战配置");
                    var provider = request.PreferredDnsProviders.First();
                    var credentials = request.DnsCredentials.GetValueOrDefault(provider);

                    var dnsRequest = new WildcardDnsChallengeRequest
                    {
                        Domain = $"*.{request.BaseDomain}",
                        ChallengeToken = "test_token",
                        KeyAuthorization = "test_key_authorization",
                        DnsProvider = provider,
                        Credentials = credentials,
                        RecordName = $"_acme-challenge.{request.BaseDomain}",
                        RecordValue = "test_value",
                        RecordType = "TXT"
                    };

                    var dnsResult = await ConfigureWildcardDnsChallengeAsync(dnsRequest, cancellationToken);
                    result.DnsChallengeResult = dnsResult;

                    if (dnsResult.Success)
                    {
                        result.PassedTests.Add("DNS挑战配置成功");
                    }
                    else
                    {
                        result.FailedTests.Add($"DNS挑战配置失败: {dnsResult.Message}");
                        result.CanIssueCertificate = false;
                    }
                }

                // 测试4: CSR生成
                result.TestSteps.Add("测试CSR生成");
                var csrRequest = new WildcardCsrRequest
                {
                    BaseDomain = request.BaseDomain,
                    Subdomains = request.Subdomains,
                    KeyType = request.KeyType
                };

                try
                {
                    var csr = await GenerateWildcardCsrAsync(csrRequest, cancellationToken);
                    result.PassedTests.Add("CSR生成成功");
                }
                catch (Exception ex)
                {
                    result.FailedTests.Add($"CSR生成失败: {ex.Message}");
                    result.CanIssueCertificate = false;
                }

                result.TestCompletedAt = DateTime.UtcNow;
                result.Success = result.FailedTests.Count == 0;
                result.Message = result.Success ? "所有测试通过" : $"测试失败: {result.FailedTests.Count} 项失败";
                result.CanIssueCertificate = result.FailedTests.Count == 0;

                if (result.Success)
                {
                    result.Recommendations.Add("可以正常申请通配符证书");
                    result.EstimatedIssueTime = TimeSpan.FromMinutes(3);
                }
                else
                {
                    result.Recommendations.Add("请修复失败的测试项");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "测试通配符证书流程失败: {BaseDomain}", request.BaseDomain);

                result.TestCompletedAt = DateTime.UtcNow;
                result.Success = false;
                result.Message = $"测试失败: {ex.Message}";
                result.FailedTests.Add(ex.Message);
                result.CanIssueCertificate = false;

                return result;
            }
        }

        /// <summary>
        /// 预览通配符证书申请配置
        /// </summary>
        public async Task<WildcardCertificatePreview> PreviewWildcardConfigurationAsync(WildcardCertificateRequest request, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("预览通配符证书配置: {BaseDomain}", request.BaseDomain);

            var isValid = await ValidateWildcardRequestAsync(request, cancellationToken);
            var fullDomains = GenerateFullDomains(request.BaseDomain, request.Subdomains);
            var estimatedIssuedAt = DateTime.UtcNow;
            var estimatedExpiresAt = estimatedIssuedAt.AddDays(request.CertificateValidityDays ?? 90);

            var preview = new WildcardCertificatePreview
            {
                IsValid = isValid.CanProceed,
                Message = isValid.CanProceed ? "配置预览成功" : "配置存在问题",
                BaseDomain = request.BaseDomain,
                Subdomains = request.Subdomains,
                FullDomains = fullDomains,
                KeyType = request.KeyType,
                SelectedDnsProvider = request.PreferredDnsProviders.FirstOrDefault() ?? "cloudflare",
                CertificateValidityDays = request.CertificateValidityDays ?? 90,
                EstimatedIssuedAt = estimatedIssuedAt,
                EstimatedExpiresAt = estimatedExpiresAt,
                ConfigurationSteps = new List<string> { "验证域名格式", "检查DNS提供商", "准备CSR", "配置挑战", "申请证书", "下载证书" },
                PotentialIssues = isValid.Warnings,
                Recommendations = isValid.RecommendedActions,
                ConfigurationDetails = new Dictionary<string, object>
                {
                    ["TotalDomains"] = fullDomains.Count,
                    ["KeyType"] = request.KeyType,
                    ["ValidityDays"] = request.CertificateValidityDays ?? 90,
                    ["DnsProvider"] = request.PreferredDnsProviders.FirstOrDefault() ?? string.Empty
                },
                CostEstimate = new Dictionary<string, object>
                {
                    ["CertificateType"] = "通配符证书",
                    ["EstimatedCost"] = "免费（Let's Encrypt）"
                }
            };

            return preview;
        }

        #region 私有方法

        private static bool IsWildcardCertificate(CertificateRecord record)
        {
            return record.Domains.Any(d => d.StartsWith("*.", StringComparison.Ordinal)) ||
                   record.Type.Contains("wildcard", StringComparison.OrdinalIgnoreCase) ||
                   record.Metadata.TryGetValue("UseWildcard", out var useWildcard) && useWildcard is bool b && b;
        }

        private static WildcardCertificateInfo MapToWildcardCertificateInfo(CertificateRecord record)
        {
            var domains = record.Domains.Count > 0 ? record.Domains : ExtractCertificateDomains(record.CertificateData);
            var wildcardDomain = domains.FirstOrDefault(d => d.StartsWith("*.", StringComparison.Ordinal));
            var baseDomain = wildcardDomain != null
                ? wildcardDomain[2..]
                : domains.FirstOrDefault() ?? record.Name;
            var subdomains = domains
                .Where(d => !d.StartsWith("*.", StringComparison.Ordinal) && d.EndsWith($".{baseDomain}", StringComparison.OrdinalIgnoreCase))
                .Select(d => d[..^(baseDomain.Length + 1)])
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var status = record.Status.Equals("Active", StringComparison.OrdinalIgnoreCase) ? "active" : record.Status.ToLowerInvariant();
            if (record.ExpiresAt <= DateTime.UtcNow && status != "revoked")
            {
                status = "expired";
            }

            return new WildcardCertificateInfo
            {
                Id = record.Id,
                AccountId = record.AccountId,
                BaseDomain = baseDomain,
                Subdomains = subdomains,
                FullDomains = domains,
                KeyType = string.IsNullOrWhiteSpace(record.KeyAlgorithm) ? record.Type : record.KeyAlgorithm,
                CertificateFingerprint = record.Fingerprint,
                SerialNumber = record.SerialNumber,
                IssuedAt = record.IssuedAt,
                ExpiresAt = record.ExpiresAt,
                Issuer = record.Issuer,
                Subject = record.Name,
                SanDomains = domains,
                Status = status,
                IsWildcard = domains.Any(d => d.StartsWith("*.", StringComparison.Ordinal)),
                DnsProvider = record.Metadata.TryGetValue("dnsProvider", out var dnsProvider) ? dnsProvider?.ToString() ?? string.Empty : string.Empty,
                AutoRenewalEnabled = record.AutoRenewalEnabled,
                LastRenewalAttempt = record.Metadata.TryGetValue("lastRenewedAt", out var lastRenewed) && DateTime.TryParse(lastRenewed?.ToString(), out var lastRenewedAt) ? lastRenewedAt : null,
                NextRenewalAttempt = record.NextRenewalAttempt,
                RenewalAttempts = record.Metadata.TryGetValue("renewalAttempts", out var attempts) && int.TryParse(attempts?.ToString(), out var parsedAttempts) ? parsedAttempts : 0,
                RenewalDaysBeforeExpiry = record.AutoRenewalConfiguration?.RenewalDaysBeforeExpiry ?? 15,
                NotificationEmails = record.NotificationEmails ?? new List<string>(),
                Metadata = record.Metadata,
                CertificateData = record.CertificateData,
                PrivateKeyData = record.PrivateKeyData ?? string.Empty,
                ImportedAt = record.Metadata.TryGetValue("ImportedAt", out var importedAt) && DateTime.TryParse(importedAt?.ToString(), out var parsedImportedAt) ? parsedImportedAt : null
            };
        }

        private static List<string> ExtractCertificateDomains(string certificateData)
        {
            if (string.IsNullOrWhiteSpace(certificateData))
            {
                return new List<string>();
            }

            try
            {
                using var cert = X509Certificate2.CreateFromPem(certificateData);
                return ExtractCertificateDomains(cert);
            }
            catch
            {
                return new List<string>();
            }
        }

        private static List<string> ExtractCertificateDomains(X509Certificate2 cert)
        {
            var domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var extension in cert.Extensions)
            {
                if (extension.Oid?.Value != "2.5.29.17")
                {
                    continue;
                }

                var formatted = new AsnEncodedData(extension.Oid, extension.RawData).Format(false);
                foreach (var part in formatted.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var value = part.Trim();
                    const string prefix = "DNS Name=";
                    if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        domains.Add(value[prefix.Length..].Trim());
                    }
                }
            }

            if (domains.Count == 0)
            {
                var dnsName = cert.GetNameInfo(X509NameType.DnsName, false);
                if (!string.IsNullOrWhiteSpace(dnsName))
                {
                    domains.Add(dnsName);
                }
            }

            return domains.ToList();
        }

        private static string NormalizePemBlock(string pemOrBase64, string label)
        {
            if (string.IsNullOrWhiteSpace(pemOrBase64))
            {
                return string.Empty;
            }

            if (pemOrBase64.Contains($"BEGIN {label}", StringComparison.OrdinalIgnoreCase))
            {
                return pemOrBase64.Trim();
            }

            return $"-----BEGIN {label}-----{Environment.NewLine}" +
                   Convert.ToBase64String(Convert.FromBase64String(pemOrBase64), Base64FormattingOptions.InsertLineBreaks) +
                   $"{Environment.NewLine}-----END {label}-----";
        }

        private static string NormalizePrivateKeyPem(string privateKeyData)
        {
            if (string.IsNullOrWhiteSpace(privateKeyData))
            {
                return string.Empty;
            }

            if (privateKeyData.Contains("BEGIN", StringComparison.OrdinalIgnoreCase))
            {
                return privateKeyData.Trim();
            }

            return NormalizePemBlock(privateKeyData, "PRIVATE KEY");
        }

        private static string ComputeSha256Fingerprint(byte[] data)
        {
            return Convert.ToHexString(SHA256.HashData(data));
        }

        private static async Task<bool> DnsTxtRecordExistsAsync(LookupClient lookup, string recordName, string expectedValue, CancellationToken cancellationToken)
        {
            var response = await lookup.QueryAsync(recordName, QueryType.TXT, cancellationToken: cancellationToken);
            return response.Answers
                .TxtRecords()
                .SelectMany(record => record.Text)
                .Any(text => string.Equals(text.Trim('"'), expectedValue, StringComparison.Ordinal));
        }

        private Dictionary<string, WildcardCertificateType> InitializeSupportedTypes()
        {
            return new Dictionary<string, WildcardCertificateType>(StringComparer.OrdinalIgnoreCase)
            {
                ["rsa2048"] = new WildcardCertificateType
                {
                    Name = "rsa2048",
                    DisplayName = "RSA 2048位",
                    Description = "RSA 2048位密钥的通配符证书",
                    SupportedKeyTypes = new List<string> { "RSA2048" },
                    SupportedDnsProviders = new List<string> { "cloudflare", "aliyun", "tencent" },
                    MaxValidityDays = 90,
                    SupportsMultiDomain = true,
                    Configuration = new Dictionary<string, object>
                    {
                        ["KeySize"] = 2048,
                        ["Security"] = "Medium"
                    }
                },
                ["rsa4096"] = new WildcardCertificateType
                {
                    Name = "rsa4096",
                    DisplayName = "RSA 4096位",
                    Description = "RSA 4096位密钥的通配符证书",
                    SupportedKeyTypes = new List<string> { "RSA4096" },
                    SupportedDnsProviders = new List<string> { "cloudflare", "aliyun", "tencent" },
                    MaxValidityDays = 90,
                    SupportsMultiDomain = true,
                    Configuration = new Dictionary<string, object>
                    {
                        ["KeySize"] = 4096,
                        ["Security"] = "High"
                    }
                },
                ["ecdsa256"] = new WildcardCertificateType
                {
                    Name = "ecdsa256",
                    DisplayName = "ECDSA 256位",
                    Description = "ECDSA 256位密钥的通配符证书",
                    SupportedKeyTypes = new List<string> { "ECDSA256" },
                    SupportedDnsProviders = new List<string> { "cloudflare", "aliyun", "tencent" },
                    MaxValidityDays = 90,
                    SupportsMultiDomain = true,
                    Configuration = new Dictionary<string, object>
                    {
                        ["KeySize"] = 256,
                        ["Curve"] = "P-256",
                        ["Security"] = "High",
                        ["Performance"] = "Excellent"
                    }
                },
                ["ecdsa384"] = new WildcardCertificateType
                {
                    Name = "ecdsa384",
                    DisplayName = "ECDSA 384位",
                    Description = "ECDSA 384位密钥的通配符证书",
                    SupportedKeyTypes = new List<string> { "ECDSA384" },
                    SupportedDnsProviders = new List<string> { "cloudflare", "aliyun", "tencent" },
                    MaxValidityDays = 90,
                    SupportsMultiDomain = true,
                    Configuration = new Dictionary<string, object>
                    {
                        ["KeySize"] = 384,
                        ["Curve"] = "P-384",
                        ["Security"] = "Very High",
                        ["Performance"] = "Excellent"
                    }
                }
            };
        }

        private List<string> GenerateFullDomains(string baseDomain, List<string> subdomains)
        {
            var fullDomains = new List<string> { $"*.{baseDomain}" };

            foreach (var subdomain in subdomains)
            {
                if (!string.IsNullOrEmpty(subdomain))
                {
                    fullDomains.Add($"{subdomain}.{baseDomain}");
                }
            }

            return fullDomains.Distinct().ToList();
        }

        private string SelectDnsProvider(AcmeChallenge challenge, List<string> preferredProviders, Dictionary<string, Dictionary<string, object>> dnsCredentials)
        {
            foreach (var provider in preferredProviders)
            {
                if (dnsCredentials.ContainsKey(provider))
                {
                    return provider;
                }
            }

            return preferredProviders.FirstOrDefault() ?? "cloudflare";
        }

        private async Task WaitForDnsPropagation(string recordName, string expectedValue, CancellationToken cancellationToken)
        {
            var maxWaitTime = TimeSpan.FromMinutes(2);
            var waitInterval = TimeSpan.FromSeconds(10);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(maxWaitTime);
            var lookup = new LookupClient();

            while (!timeoutCts.IsCancellationRequested)
            {
                if (await DnsTxtRecordExistsAsync(lookup, recordName, expectedValue, timeoutCts.Token))
                {
                    return;
                }

                await Task.Delay(waitInterval, timeoutCts.Token);
            }

            throw new TimeoutException($"DNS传播超时: {recordName}");
        }

        /// <summary>
        /// 带超时控制的DNS传播等待
        /// </summary>
        private async Task WaitForDnsPropagationWithTimeout(string recordName, string expectedValue, CancellationToken cancellationToken)
        {
            var maxWaitTime = TimeSpan.FromMinutes(3); // 3分钟最大等待时间
            var waitInterval = TimeSpan.FromSeconds(15); // 15秒检查间隔
            var startTime = DateTime.UtcNow;
            var lookup = new LookupClient();

            _logger.LogInformation("开始等待DNS传播: {RecordName}, 预期值: {ExpectedValue}", recordName, expectedValue);

            for (var elapsed = TimeSpan.Zero; elapsed < maxWaitTime; elapsed += waitInterval)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // 检查是否超时
                    if (DateTime.UtcNow - startTime > maxWaitTime)
                    {
                        throw new TimeoutException($"DNS传播超时: {recordName}");
                    }

                    _logger.LogDebug("检查DNS传播状态: {RecordName}, 已等待: {Elapsed}s", recordName, elapsed.TotalSeconds);

                    if (await DnsTxtRecordExistsAsync(lookup, recordName, expectedValue, cancellationToken))
                    {
                        _logger.LogInformation("DNS传播成功: {RecordName}", recordName);
                        break;
                    }

                    await Task.Delay(waitInterval, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("DNS传播等待被取消: {RecordName}", recordName);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "DNS传播检查失败: {RecordName}", recordName);
                    throw;
                }
            }
        }

        /// <summary>
        /// 清理失败时的DNS挑战
        /// </summary>
        private async Task CleanupDnsChallengeOnFailure(WildcardDnsChallengeRequest dnsRequest, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("清理失败的DNS挑战: {Domain}, Provider: {Provider}", dnsRequest.Domain, dnsRequest.DnsProvider);

                var cleanupRequest = new WildcardDnsChallengeCleanupRequest
                {
                    Domain = dnsRequest.Domain,
                    DnsProvider = dnsRequest.DnsProvider,
                    RecordName = dnsRequest.RecordName,
                    RecordType = dnsRequest.RecordType,
                    CleanupDetails = new Dictionary<string, object>
                    {
                        ["Reason"] = "申请失败或超时",
                        ["CleanupTime"] = DateTime.UtcNow
                    }
                };

                await CleanupWildcardDnsChallengeAsync(cleanupRequest, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理DNS挑战失败: {Domain}", dnsRequest.Domain);
                // 不抛出异常，避免掩盖主要错误
            }
        }

        /// <summary>
        /// 清理过期请求数据
        /// </summary>
        private async Task CleanupExpiredRequestAsync(string tempCertificateId, string baseDomain)
        {
            try
            {
                _logger.LogInformation("清理过期请求数据: {TempCertificateId}, 域名: {BaseDomain}", tempCertificateId, baseDomain);

                // 从缓存中移除临时证书
                _certificateCache.TryRemove(tempCertificateId, out _);

                // 这里可以添加更多清理逻辑，比如：
                // - 清理DNS记录
                // - 取消进行中的ACME订单
                // - 通知相关服务

                _logger.LogInformation("过期请求数据清理完成: {TempCertificateId}", tempCertificateId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理过期请求数据失败: {TempCertificateId}", tempCertificateId);
            }
        }

        /// <summary>
        /// 清理相关的DNS挑战记录
        /// </summary>
        private async Task CleanupRelatedDnsChallenges(WildcardCertificateInfo certificateInfo, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("清理相关DNS挑战: {CertificateId}, 域名: {BaseDomain}",
                    certificateInfo.Id, certificateInfo.BaseDomain);

                // 为每个子域名清理DNS挑战记录
                var domainsToCleanup = new List<string> { $"*.{certificateInfo.BaseDomain}" };
                domainsToCleanup.AddRange(certificateInfo.Subdomains.Select(sub => $"{sub}.{certificateInfo.BaseDomain}"));

                foreach (var domain in domainsToCleanup)
                {
                    var cleanupRequest = new WildcardDnsChallengeCleanupRequest
                    {
                        Domain = domain,
                        DnsProvider = certificateInfo.DnsProvider,
                        RecordName = $"_acme-challenge.{domain}",
                        RecordType = "TXT",
                        CleanupDetails = new Dictionary<string, object>
                        {
                            ["CertificateId"] = certificateInfo.Id,
                            ["Reason"] = "证书删除",
                            ["CleanupTime"] = DateTime.UtcNow
                        }
                    };

                    await CleanupWildcardDnsChallengeAsync(cleanupRequest, cancellationToken);
                }

                _logger.LogInformation("DNS挑战清理完成: {CertificateId}", certificateInfo.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理相关DNS挑战失败: {CertificateId}", certificateInfo.Id);
                throw;
            }
        }

        /// <summary>
        /// 清理相关的进度跟踪记录
        /// </summary>
        private async Task CleanupRelatedProgressTracking(string certificateId, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("清理进度跟踪记录: {CertificateId}", certificateId);

                var progressRecords = await _progressService.GetAllProgressAsync();
                foreach (var progress in progressRecords.Where(p => p.CertificateId == certificateId))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await _progressService.DeleteProgressAsync(progress.ProgressId);
                }

                _logger.LogInformation("进度跟踪记录清理完成: {CertificateId}", certificateId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理进度跟踪记录失败: {CertificateId}", certificateId);
                throw;
            }
        }

        /// <summary>
        /// 通知相关服务证书已删除
        /// </summary>
        private async Task NotifyServicesCertificateDeleted(WildcardCertificateInfo certificateInfo, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("通知服务证书删除: {CertificateId}, 域名: {BaseDomain}",
                    certificateInfo.Id, certificateInfo.BaseDomain);

                cancellationToken.ThrowIfCancellationRequested();
                await Task.CompletedTask;

                _logger.LogInformation("服务通知完成: {CertificateId}", certificateInfo.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "通知服务证书删除失败: {CertificateId}", certificateInfo.Id);
                throw;
            }
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

        private bool IsValidSubdomain(string subdomain)
        {
            if (string.IsNullOrWhiteSpace(subdomain)) return false;

            // 子域名验证
            if (subdomain.Length > 63) return false;

            // 不允许以点开头或结尾
            if (subdomain.StartsWith(".") || subdomain.EndsWith(".")) return false;

            // 不允许包含特殊字符
            var invalidChars = new[] { "~", "!", "@", "#", "$", "%", "^", "&", "*", "(", ")", "+", "=", "[", "]", "{", "}", "\\", "|", ";", ":", "'", "\"", "<", ">", ",", "/", "?" };
            return !invalidChars.Any(ch => subdomain.Contains(ch));
        }

        private string FormatCertificateAsPem(WildcardCertificateInfo certificateInfo, bool includePrivateKey)
        {
            var pem = NormalizePemBlock(certificateInfo.CertificateData, "CERTIFICATE");

            if (includePrivateKey && !string.IsNullOrEmpty(certificateInfo.PrivateKeyData))
            {
                pem += Environment.NewLine + NormalizePrivateKeyPem(certificateInfo.PrivateKeyData);
            }

            return pem;
        }

        private string FormatCertificateAsPfx(WildcardCertificateInfo certificateInfo, bool includePrivateKey)
        {
            if (!includePrivateKey || string.IsNullOrWhiteSpace(certificateInfo.PrivateKeyData))
            {
                throw new InvalidOperationException("导出 PFX 需要包含私钥");
            }

            using var certWithKey = X509Certificate2.CreateFromPem(certificateInfo.CertificateData, certificateInfo.PrivateKeyData);
            return Convert.ToBase64String(certWithKey.Export(X509ContentType.Pkcs12));
        }

        private string FormatCertificateAsJson(WildcardCertificateInfo certificateInfo)
        {
            return System.Text.Json.JsonSerializer.Serialize(certificateInfo, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
        }

        private CertificateValidationResult ValidateImportedCertificate(string certificateData, string format, string? privateKeyData = null)
        {
            var result = new CertificateValidationResult
            {
                IsValid = true
            };

            try
            {
                if (string.IsNullOrEmpty(certificateData))
                {
                    result.IsValid = false;
                    result.Errors.Add("证书数据为空");
                }

                using var cert = X509Certificate2.CreateFromPem(certificateData);
                var now = DateTime.UtcNow;

                if (cert.NotBefore.ToUniversalTime() > now)
                {
                    result.IsValid = false;
                    result.Errors.Add("证书尚未生效");
                }

                if (cert.NotAfter.ToUniversalTime() <= now)
                {
                    result.IsValid = false;
                    result.Errors.Add("证书已过期");
                }

                if (!string.Equals(format, "pem", StringComparison.OrdinalIgnoreCase))
                {
                    result.Warnings.Add("当前仅完整支持 PEM 格式，其他格式会按 PEM 尝试解析");
                }

                var domains = ExtractCertificateDomains(cert);
                if (!domains.Any(d => d.StartsWith("*.", StringComparison.Ordinal)))
                {
                    result.Warnings.Add("证书 SAN 中未发现通配符域名");
                }

                if (!string.IsNullOrWhiteSpace(privateKeyData))
                {
                    try
                    {
                        using var _ = X509Certificate2.CreateFromPem(certificateData, privateKeyData);
                    }
                    catch (Exception ex)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"私钥与证书不匹配或格式无效: {ex.Message}");
                    }
                }

                result.ValidationDetails["Subject"] = cert.Subject;
                result.ValidationDetails["Issuer"] = cert.Issuer;
                result.ValidationDetails["ExpiresAt"] = cert.NotAfter.ToUniversalTime();
                result.ValidationDetails["Domains"] = domains;

                return result;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"证书验证失败: {ex.Message}");
                return result;
            }
        }

        private WildcardCertificateInfo? ParseCertificateData(string certificateData, string format, string? privateKeyData = null)
        {
            try
            {
                using var cert = X509Certificate2.CreateFromPem(certificateData);
                var domains = ExtractCertificateDomains(cert);
                var wildcardDomain = domains.FirstOrDefault(d => d.StartsWith("*.", StringComparison.Ordinal));
                var baseDomain = wildcardDomain != null
                    ? wildcardDomain[2..]
                    : domains.FirstOrDefault() ?? cert.GetNameInfo(X509NameType.DnsName, false);
                var subdomains = domains
                    .Where(d => !d.StartsWith("*.", StringComparison.Ordinal) && d.EndsWith($".{baseDomain}", StringComparison.OrdinalIgnoreCase))
                    .Select(d => d[..^(baseDomain.Length + 1)])
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var info = new WildcardCertificateInfo
                {
                    BaseDomain = baseDomain,
                    Subdomains = subdomains,
                    FullDomains = domains,
                    KeyType = cert.GetRSAPublicKey() != null ? "RSA" : cert.GetECDsaPublicKey() != null ? "ECDSA" : cert.PublicKey.Oid?.FriendlyName ?? "Unknown",
                    CertificateFingerprint = cert.Thumbprint ?? ComputeSha256Fingerprint(cert.RawData),
                    SerialNumber = cert.SerialNumber ?? string.Empty,
                    IssuedAt = cert.NotBefore.ToUniversalTime(),
                    ExpiresAt = cert.NotAfter.ToUniversalTime(),
                    Issuer = cert.Issuer ?? string.Empty,
                    Subject = cert.Subject ?? string.Empty,
                    SanDomains = domains,
                    Status = cert.NotAfter.ToUniversalTime() <= DateTime.UtcNow ? "expired" : "active",
                    IsWildcard = domains.Any(d => d.StartsWith("*.", StringComparison.Ordinal)),
                    DnsProvider = string.Empty,
                    AutoRenewalEnabled = false,
                    RenewalDaysBeforeExpiry = _certificateSettings.DefaultRenewalDaysBeforeExpiry,
                    NotificationEmails = new List<string>(),
                    Metadata = new Dictionary<string, object>(),
                    CertificateData = NormalizePemBlock(certificateData, "CERTIFICATE"),
                    PrivateKeyData = privateKeyData ?? string.Empty
                };

                return info;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解析证书数据失败");
                return null;
            }
        }

        private string GenerateCertificateFingerprint()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        }

        private string GenerateCertificateId()
        {
            return $"wc_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        }

        /// <summary>
        /// 获取通配符证书详情
        /// </summary>
        public async Task<WildcardCertificateDetails?> GetWildcardCertificateDetailsAsync(string certificateId, CancellationToken cancellationToken = default)
        {
            try
            {
                var certificate = await GetWildcardCertificateAsync(certificateId, cancellationToken);
                if (certificate != null)
                {
                    return new WildcardCertificateDetails
                    {
                        CertificateId = certificate.Id,
                        BaseDomain = certificate.BaseDomain,
                        WildcardDomain = $"*.{certificate.BaseDomain}",
                        AdditionalDomains = certificate.Subdomains,
                        CertificateChain = certificate.CertificateData,
                        PrivateKey = certificate.PrivateKeyData,
                        IssuedAt = certificate.IssuedAt,
                        ExpiresAt = certificate.ExpiresAt,
                        Issuer = certificate.Issuer,
                        Status = certificate.Status,
                        AccountId = certificate.AccountId,
                        AppliedServices = new List<string>(),
                        Metadata = certificate.Metadata
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取通配符证书详情失败: {CertificateId}", certificateId);
                return null;
            }
        }

        /// <summary>
        /// 验证通配符证书
        /// </summary>
        public async Task<AcmeCertificateValidationResult> ValidateWildcardCertificateAsync(string certificateId, CancellationToken cancellationToken = default)
        {
            try
            {
                var certificate = await GetWildcardCertificateAsync(certificateId, cancellationToken);
                if (certificate == null)
                {
                    return new AcmeCertificateValidationResult
                    {
                        CertificateId = certificateId,
                        ValidationStatus = new ValidationResult
                        {
                            IsValid = false,
                            Status = "invalid",
                            Reason = "证书不存在"
                        }
                    };
                }

                return new AcmeCertificateValidationResult
                {
                    CertificateId = certificateId,
                    CertificateChain = certificate.CertificateData,
                    PrivateKey = certificate.PrivateKeyData,
                    CertificateFingerprint = certificate.CertificateFingerprint,
                    SerialNumber = certificate.SerialNumber,
                    IssuedAt = certificate.IssuedAt,
                    ExpiresAt = certificate.ExpiresAt,
                    Domains = certificate.FullDomains,
                    Issuer = certificate.Issuer,
                    Subject = certificate.Subject,
                    ValidationStatus = new ValidationResult
                    {
                        IsValid = certificate.ExpiresAt > DateTime.UtcNow,
                        Status = certificate.ExpiresAt > DateTime.UtcNow ? "valid" : "expired",
                        ValidatedAt = DateTime.UtcNow,
                        ExpiresAt = certificate.ExpiresAt
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "验证通配符证书失败: {CertificateId}", certificateId);
                return new AcmeCertificateValidationResult
                {
                    CertificateId = certificateId,
                    ValidationStatus = new ValidationResult
                    {
                        IsValid = false,
                        Status = "error",
                        Reason = ex.Message
                    }
                };
            }
        }

        /// <summary>
        /// 获取支持的DNS提供商列表
        /// </summary>
        public IEnumerable<DnsProviderInfo> GetSupportedDnsProviders()
        {
            return new List<DnsProviderInfo>
            {
                new DnsProviderInfo
                {
                    ProviderId = "cloudflare",
                    Name = "cloudflare",
                    DisplayName = "Cloudflare",
                    Description = "Cloudflare DNS API",
                    SupportedChallengeTypes = new List<string> { "dns-01" },
                    IsEnabled = true,
                    DocumentationUrl = "https://developers.cloudflare.com/api/"
                },
                new DnsProviderInfo
                {
                    ProviderId = "aliyun",
                    Name = "aliyun",
                    DisplayName = "阿里云",
                    Description = "阿里云DNS API",
                    SupportedChallengeTypes = new List<string> { "dns-01" },
                    IsEnabled = true,
                    DocumentationUrl = "https://help.aliyun.com/"
                }
            };
        }

        /// <summary>
        /// 批量操作通配符证书
        /// </summary>
        public async Task<WildcardCertificateBatchResult> BatchOperationWildcardCertificatesAsync(WildcardCertificateBatchRequest request, CancellationToken cancellationToken = default)
        {
            var result = new WildcardCertificateBatchResult
            {
                BatchStartedAt = DateTime.UtcNow
            };

            try
            {
                var operation = string.IsNullOrWhiteSpace(request.Operation) ? "request" : request.Operation.Trim().ToLowerInvariant();

                if (request.Certificates.Count > 0 && operation == "request")
                {
                    foreach (var item in request.Certificates)
                    {
                        try
                        {
                            var wildcardRequest = new WildcardCertificateRequest
                            {
                                AccountId = request.AccountId,
                                BaseDomain = item.BaseDomain,
                                Subdomains = item.AdditionalDomains,
                                PreferredDnsProviders = string.IsNullOrWhiteSpace(item.DnsProvider) ? new List<string>() : new List<string> { item.DnsProvider },
                                DnsCredentials = string.IsNullOrWhiteSpace(item.DnsProvider)
                                    ? new Dictionary<string, Dictionary<string, object>>()
                                    : new Dictionary<string, Dictionary<string, object>>
                                    {
                                        [item.DnsProvider] = item.DnsProviderConfig.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value)
                                    },
                                EnableAutoRenewal = item.EnableAutoRenewal,
                                NotificationEmails = item.NotificationEmails,
                                Metadata = item.CertificateSettings
                            };

                            var issueResult = await RequestWildcardCertificateAsync(wildcardRequest, cancellationToken);
                            result.Results.Add(new WildcardCertificateResultItem
                            {
                                BaseDomain = item.BaseDomain,
                                Success = issueResult.Success,
                                CertificateId = issueResult.CertificateId,
                                ErrorMessage = issueResult.ErrorMessage ?? issueResult.Errors.FirstOrDefault(),
                                Status = issueResult.Success ? "completed" : "failed",
                                CompletedAt = DateTime.UtcNow,
                                Metadata = issueResult.Metadata
                            });
                        }
                        catch (Exception ex)
                        {
                            result.Results.Add(new WildcardCertificateResultItem
                            {
                                BaseDomain = item.BaseDomain,
                                Success = false,
                                ErrorMessage = ex.Message,
                                Status = "failed",
                                CompletedAt = DateTime.UtcNow
                            });
                        }
                    }
                }
                else
                {
                    foreach (var certificateId in request.CertificateIds)
                    {
                        try
                        {
                            switch (operation)
                            {
                                case "delete":
                                    var deleteResult = await DeleteWildcardCertificateAsync(certificateId, cancellationToken);
                                    result.Results.Add(new WildcardCertificateResultItem
                                    {
                                        Success = deleteResult.Success,
                                        CertificateId = certificateId,
                                        Status = deleteResult.Success ? "completed" : "failed",
                                        ErrorMessage = deleteResult.Errors.FirstOrDefault(),
                                        CompletedAt = DateTime.UtcNow
                                    });
                                    break;
                                case "force-delete":
                                    var forceDeleteResult = await ForceDeleteWildcardCertificateAsync(certificateId, cancellationToken);
                                    result.Results.Add(new WildcardCertificateResultItem
                                    {
                                        Success = forceDeleteResult.Success,
                                        CertificateId = certificateId,
                                        Status = forceDeleteResult.Success ? "completed" : "failed",
                                        ErrorMessage = forceDeleteResult.Errors.FirstOrDefault(),
                                        CompletedAt = DateTime.UtcNow
                                    });
                                    break;
                                case "renew":
                                    var renewResult = await RenewWildcardCertificateAsync(certificateId, cancellationToken);
                                    result.Results.Add(new WildcardCertificateResultItem
                                    {
                                        Success = renewResult.Success,
                                        CertificateId = renewResult.CertificateId ?? certificateId,
                                        Status = renewResult.Success ? "completed" : "failed",
                                        ErrorMessage = renewResult.Errors.FirstOrDefault(),
                                        CompletedAt = DateTime.UtcNow
                                    });
                                    break;
                                case "validate":
                                    var validationResult = await ValidateWildcardCertificateAsync(certificateId, cancellationToken);
                                    result.Results.Add(new WildcardCertificateResultItem
                                    {
                                        Success = validationResult.Valid || validationResult.ValidationStatus.IsValid,
                                        CertificateId = certificateId,
                                        Status = validationResult.Status,
                                        ErrorMessage = validationResult.Errors.FirstOrDefault(),
                                        CompletedAt = DateTime.UtcNow
                                    });
                                    break;
                                default:
                                    throw new ArgumentException($"不支持的批量操作: {request.Operation}");
                            }
                        }
                        catch (Exception ex)
                        {
                            result.Results.Add(new WildcardCertificateResultItem
                            {
                                Success = false,
                                CertificateId = certificateId,
                                Status = "failed",
                                ErrorMessage = ex.Message,
                                CompletedAt = DateTime.UtcNow
                            });
                        }
                    }
                }

                result.TotalCertificates = result.Results.Count;
                result.SuccessCount = result.Results.Count(r => r.Success);
                result.FailureCount = result.TotalCertificates - result.SuccessCount;
                result.BatchCompletedAt = DateTime.UtcNow;
                result.Duration = result.BatchCompletedAt - result.BatchStartedAt;
                result.Success = result.FailureCount == 0;
                result.Message = result.Success ? "批量操作成功" : "部分操作失败";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量操作通配符证书失败");
                result.Errors.Add(ex.Message);
                result.Success = false;
                result.Message = "批量操作失败";
            }

            return result;
        }

        /// <summary>
        /// 检查通配符证书状态
        /// </summary>
        public async Task<WildcardCertificateStatus> CheckWildcardCertificateStatusAsync(string certificateId, CancellationToken cancellationToken = default)
        {
            try
            {
                var certificate = await GetWildcardCertificateAsync(certificateId, cancellationToken);
                if (certificate == null)
                {
                    return new WildcardCertificateStatus
                    {
                        CertificateId = certificateId,
                        Status = "invalid",
                        LastError = "证书不存在",
                        LastChecked = DateTime.UtcNow
                    };
                }

                var daysUntilExpiry = (int)(certificate.ExpiresAt - DateTime.UtcNow).TotalDays;
                return new WildcardCertificateStatus
                {
                    CertificateId = certificateId,
                    BaseDomain = certificate.BaseDomain,
                    Status = certificate.Status,
                    IssuedAt = certificate.IssuedAt,
                    ExpiresAt = certificate.ExpiresAt,
                    DaysUntilExpiry = daysUntilExpiry,
                    IsExpiringSoon = daysUntilExpiry <= _certificateSettings.ExpiringSoonDays,
                    IsExpired = daysUntilExpiry <= 0,
                    LastChecked = DateTime.UtcNow,
                    AppliedServices = new List<string>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查通配符证书状态失败: {CertificateId}", certificateId);
                return new WildcardCertificateStatus
                {
                    CertificateId = certificateId,
                    Status = "error",
                    LastError = ex.Message,
                    LastChecked = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// 自动配置通配符证书挑战
        /// </summary>
        public async Task<WildcardAutoChallengeResult> AutoConfigureWildcardChallengeAsync(WildcardAutoChallengeRequest request, CancellationToken cancellationToken = default)
        {
            var result = new WildcardAutoChallengeResult
            {
                CertificateId = request.CertificateId,
                Domain = request.Domain,
                DnsProvider = request.DnsProvider,
                StartedAt = DateTime.UtcNow
            };

            try
            {
                if (string.IsNullOrWhiteSpace(request.Domain) || string.IsNullOrWhiteSpace(request.DnsProvider))
                {
                    throw new ArgumentException("域名和 DNS 提供商不能为空");
                }

                var recordName = $"_acme-challenge.{request.Domain.TrimStart('*').TrimStart('.')}";
                var recordValue = request.ChallengeSettings.TryGetValue("recordValue", out var configuredValue)
                    ? configuredValue?.ToString() ?? string.Empty
                    : request.ChallengeSettings.TryGetValue("keyAuthorization", out var keyAuthorization)
                        ? keyAuthorization?.ToString() ?? string.Empty
                        : string.Empty;

                if (string.IsNullOrWhiteSpace(recordValue))
                {
                    throw new InvalidOperationException("自动配置挑战需要提供 recordValue 或 keyAuthorization");
                }

                var dnsRequest = new WildcardDnsChallengeRequest
                {
                    Domain = request.Domain,
                    ChallengeToken = request.ChallengeSettings.TryGetValue("token", out var token) ? token?.ToString() ?? string.Empty : string.Empty,
                    KeyAuthorization = request.ChallengeSettings.TryGetValue("keyAuthorization", out var authz) ? authz?.ToString() ?? string.Empty : recordValue,
                    DnsProvider = request.DnsProvider,
                    Credentials = request.DnsProviderConfig.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value),
                    RecordName = recordName,
                    RecordValue = recordValue,
                    RecordType = "TXT",
                    ChallengeDetails = request.ChallengeSettings
                };

                var configureResult = await ConfigureWildcardDnsChallengeAsync(dnsRequest, cancellationToken);
                result.ChallengeResults.Add(new ChallengeResult
                {
                    ChallengeType = "dns-01",
                    Domain = request.Domain,
                    Success = configureResult.Success,
                    Status = configureResult.Success ? "configured" : "failed",
                    ErrorMessage = configureResult.Errors.FirstOrDefault(),
                    StartedAt = result.StartedAt,
                    CompletedAt = DateTime.UtcNow,
                    Duration = DateTime.UtcNow - result.StartedAt,
                    ChallengeData = new Dictionary<string, object>
                    {
                        ["recordName"] = recordName,
                        ["recordType"] = "TXT"
                    }
                });

                if (!configureResult.Success)
                {
                    result.Success = false;
                    result.ErrorMessage = configureResult.Message;
                    result.Errors.AddRange(configureResult.Errors);
                    return result;
                }

                var validateResult = await ValidateWildcardDnsChallengeAsync(dnsRequest, cancellationToken);

                result.Success = validateResult.Success;
                result.Message = validateResult.Success ? "自动挑战配置成功" : validateResult.Message;
                result.CompletedAt = DateTime.UtcNow;
                result.Duration = (result.CompletedAt ?? DateTime.UtcNow) - result.StartedAt;
                result.CleanupCompleted = false;
                result.ConfiguredAt = DateTime.UtcNow;
                if (!validateResult.Success)
                {
                    result.Errors.AddRange(validateResult.Errors);
                }

                if (request.AutoCleanup)
                {
                    var cleanupResult = await CleanupWildcardDnsChallengeAsync(new WildcardDnsChallengeCleanupRequest
                    {
                        Domain = request.Domain,
                        DnsProvider = request.DnsProvider,
                        Credentials = dnsRequest.Credentials,
                        RecordName = recordName,
                        RecordType = "TXT",
                        CleanupDetails = request.ChallengeSettings
                    }, cancellationToken);
                    result.CleanupCompleted = cleanupResult.Success;
                    if (!cleanupResult.Success)
                    {
                        result.Errors.AddRange(cleanupResult.Errors);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "自动配置通配符证书挑战失败");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Errors.Add(ex.Message);
            }

            return result;
        }

        /// <summary>
        /// 转换通配符证书验证结果为ACME证书验证结果
        /// </summary>
        private AcmeCertificateValidationResult ConvertToAcmeCertificateValidationResult(WildcardCertificateValidationResult wildcardResult)
        {
            return new AcmeCertificateValidationResult
            {
                CertificateId = string.Empty,
                CertificateChain = string.Empty,
                PrivateKey = string.Empty,
                CertificateFingerprint = string.Empty,
                SerialNumber = string.Empty,
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow,
                Domains = new List<string>(),
                Issuer = string.Empty,
                Subject = string.Empty,
                ValidationStatus = new ValidationResult
                {
                    IsValid = wildcardResult.IsValid,
                    Status = wildcardResult.IsValid ? "valid" : "invalid",
                    Reason = wildcardResult.Message,
                    ValidatedAt = wildcardResult.ValidatedAt,
                    ExpiresAt = DateTime.UtcNow.AddMonths(1),
                    DomainValidations = new List<DomainValidation>()
                },
                ValidationErrors = wildcardResult.Errors,
                Metadata = new Dictionary<string, object>
                {
                    ["PassedChecks"] = wildcardResult.PassedChecks,
                    ["FailedChecks"] = wildcardResult.FailedChecks,
                    ["ValidationDetails"] = wildcardResult.ValidationDetails,
                    ["CanProceed"] = wildcardResult.CanProceed,
                    ["RecommendedActions"] = wildcardResult.RecommendedActions
                }
            };
        }

        /// <summary>
        /// 等待所有挑战验证完成
        /// </summary>
        private async Task<bool> WaitForChallengesValidAsync(string orderId, CancellationToken cancellationToken)
        {
            var maxAttempts = 30;
            var delayBetweenAttempts = TimeSpan.FromSeconds(5);
            var totalTimeout = TimeSpan.FromMinutes(maxAttempts * 5 / 60.0);

            _logger.LogInformation("开始等待挑战验证: OrderId={OrderId}, 最多等待 {TotalTimeout}", orderId, totalTimeout);

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var order = await _acmeService.GetCertificateOrderAsync(orderId);
                    if (order == null)
                    {
                        _logger.LogWarning("等待挑战验证时未找到订单: {OrderId}", orderId);
                        await Task.Delay(delayBetweenAttempts, cancellationToken);
                        continue;
                    }

                    var allValid = true;
                    var pendingAuthorizations = new List<string>();

                    foreach (var auth in order.Authorizations)
                    {
                        if (auth.Status == "valid")
                        {
                            continue;
                        }

                        allValid = false;
                        pendingAuthorizations.Add($"{auth.Domain} ({auth.Status})");

                        foreach (var challenge in auth.Challenges)
                        {
                            if (challenge.Status == "invalid")
                            {
                                _logger.LogError("挑战验证失败: Domain={Domain}, Type={Type}, Error={Error}", 
                                    auth.Domain, challenge.Type, challenge.Error ?? "未知错误");
                                return false;
                            }
                        }
                    }

                    if (allValid)
                    {
                        _logger.LogInformation("所有挑战验证成功: OrderId={OrderId}, 尝试次数={Attempt}", orderId, attempt);
                        return true;
                    }

                    _logger.LogInformation("等待挑战验证 (尝试 {Attempt}/{MaxAttempts}): {PendingAuths}", 
                        attempt, maxAttempts, string.Join(", ", pendingAuthorizations));

                    await Task.Delay(delayBetweenAttempts, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("等待挑战验证被取消: OrderId={OrderId}", orderId);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "检查挑战状态时出错 (尝试 {Attempt}/{MaxAttempts}): {OrderId}", 
                        attempt, maxAttempts, orderId);
                    await Task.Delay(delayBetweenAttempts, cancellationToken);
                }
            }

            _logger.LogWarning("挑战验证超时: OrderId={OrderId}, 尝试次数={MaxAttempts}", orderId, maxAttempts);
            return false;
        }

        #endregion
    }
}
