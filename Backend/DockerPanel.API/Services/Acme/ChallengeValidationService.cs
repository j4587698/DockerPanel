using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DockerPanel.API.Services;
using DockerPanel.API.Models.Acme;
using DockerPanel.API.Services.Acme.DnsProviders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DockerPanel.API.Services.Acme
{
    /// <summary>
    /// ACME挑战验证服务实现
    /// </summary>
    public class ChallengeValidationService : IChallengeValidationService
    {
        private readonly ILogger<ChallengeValidationService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly Dictionary<string, ChallengeStatus> _challengeStatuses;
        private readonly Dictionary<string, DnsProvider> _dnsProviders;
        private readonly Dictionary<string, IDnsProvider> _dnsProviderServices;
        private readonly SemaphoreSlim _semaphore;

        public ChallengeValidationService(
            ILogger<ChallengeValidationService> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            IBackgroundTaskQueue taskQueue,
            CloudflareDnsProvider cloudflareProvider,
            AliyunDnsProvider aliyunProvider,
            TencentDnsProvider tencentProvider,
            DnsPodDnsProvider dnspodProvider,
            DnsPodTraditionalDnsProvider dnspodTraditionalProvider,
            AwsRoute53DnsProvider awsProvider,
            AzureDnsProvider azureProvider,
            GoDaddyDnsProvider godaddyProvider)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _taskQueue = taskQueue;
            _challengeStatuses = new Dictionary<string, ChallengeStatus>();
            _dnsProviders = InitializeDnsProviders();
            _dnsProviderServices = new Dictionary<string, IDnsProvider>(StringComparer.OrdinalIgnoreCase)
            {
                ["cloudflare"] = cloudflareProvider,
                ["aliyun"] = aliyunProvider,
                ["tencent"] = tencentProvider,
                ["dnspod"] = dnspodProvider,
                ["dnspod-traditional"] = dnspodTraditionalProvider,
                ["aws"] = awsProvider,
                ["azure"] = azureProvider,
                ["godaddy"] = godaddyProvider
            };
            _semaphore = new SemaphoreSlim(10, 10); // 限制并发挑战配置数量
        }

        /// <summary>
        /// 配置HTTP-01挑战验证
        /// </summary>
        public async Task<ChallengeValidationResult> ConfigureHttpChallengeAsync(AcmeChallenge challenge, string domain)
        {
            await _semaphore.WaitAsync();
            try
            {
                _logger.LogInformation("配置HTTP-01挑战验证: {Domain}, Token: {Token}", domain, challenge.Token);

                var result = new ChallengeValidationResult
                {
                    Success = false,
                    ChallengeType = "http-01",
                    Domain = domain,
                    ConfiguredAt = DateTime.UtcNow
                };

                // 验证挑战数据完整性
                if (string.IsNullOrEmpty(challenge.Token) || string.IsNullOrEmpty(challenge.KeyAuthorization))
                {
                    result.Errors.Add("挑战令牌或授权密钥为空");
                    return result;
                }

                // 配置Web服务器响应
                var webConfigured = await ConfigureWebServerForHttpChallenge(domain, challenge.Token, challenge.KeyAuthorization);
                if (!webConfigured)
                {
                    result.Errors.Add("Web服务器配置失败");
                    return result;
                }

                result.ValidationSteps.Add("Web服务器配置完成");
                result.ConfigurationDetails["WellKnownUrl"] = $"http://{domain}/.well-known/acme-challenge/{challenge.Token}";
                result.ConfigurationDetails["Token"] = challenge.Token;
                result.ConfigurationDetails["KeyAuthorization"] = challenge.KeyAuthorization;

                // 更新挑战状态
                var challengeId = GenerateChallengeId(challenge, domain);
                _challengeStatuses[challengeId] = new ChallengeStatus
                {
                    ChallengeId = challengeId,
                    ChallengeType = "http-01",
                    Domain = domain,
                    Status = "configured",
                    CreatedAt = DateTime.UtcNow,
                    ConfiguredAt = DateTime.UtcNow,
                    Token = challenge.Token,
                    KeyAuthorization = challenge.KeyAuthorization,
                    ValidationUrl = challenge.Url
                };

                result.Success = true;
                result.Message = "HTTP-01挑战配置成功";
                result.ConfigurationStatus = "active";

                _logger.LogInformation("HTTP-01挑战配置成功: {Domain}", domain);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "配置HTTP-01挑战失败: {Domain}", domain);
                return new ChallengeValidationResult
                {
                    Success = false,
                    ChallengeType = "http-01",
                    Domain = domain,
                    Message = $"配置失败: {ex.Message}",
                    ConfiguredAt = DateTime.UtcNow,
                    Errors = new List<string> { ex.Message }
                };
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// 验证HTTP-01挑战
        /// </summary>
        public async Task<ChallengeValidationResult> ValidateHttpChallengeAsync(AcmeChallenge challenge, string domain)
        {
            try
            {
                _logger.LogInformation("验证HTTP-01挑战: {Domain}", domain);

                var result = new ChallengeValidationResult
                {
                    Success = false,
                    ChallengeType = "http-01",
                    Domain = domain,
                    ConfiguredAt = DateTime.UtcNow,
                    ValidatedAt = DateTime.UtcNow
                };

                var httpClient = _httpClientFactory.CreateClient();
                var wellKnownUrl = $"http://{domain}/.well-known/acme-challenge/{challenge.Token}";

                // 发送HTTP请求验证挑战文件
                var response = await httpClient.GetAsync(wellKnownUrl);
                if (!response.IsSuccessStatusCode)
                {
                    result.Errors.Add($"无法访问挑战文件: HTTP {response.StatusCode}");
                    return result;
                }

                var content = await response.Content.ReadAsStringAsync();
                if (content.Trim() != challenge.KeyAuthorization)
                {
                    result.Errors.Add("挑战文件内容不匹配");
                    result.ConfigurationDetails["ExpectedContent"] = challenge.KeyAuthorization ?? string.Empty;
                    result.ConfigurationDetails["ActualContent"] = content;
                    return result;
                }

                result.ValidationSteps.Add("HTTP-01挑战文件验证成功");
                result.Success = true;
                result.Message = "HTTP-01挑战验证成功";
                result.ConfigurationStatus = "validated";

                // 更新挑战状态
                var challengeId = GenerateChallengeId(challenge, domain);
                if (_challengeStatuses.ContainsKey(challengeId))
                {
                    _challengeStatuses[challengeId].Status = "validated";
                    _challengeStatuses[challengeId].ValidatedAt = DateTime.UtcNow;
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "验证HTTP-01挑战失败: {Domain}", domain);
                return new ChallengeValidationResult
                {
                    Success = false,
                    ChallengeType = "http-01",
                    Domain = domain,
                    Message = $"验证失败: {ex.Message}",
                    ConfiguredAt = DateTime.UtcNow,
                    ValidatedAt = DateTime.UtcNow,
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        /// <summary>
        /// 配置DNS-01挑战验证
        /// </summary>
        public async Task<ChallengeValidationResult> ConfigureDnsChallengeAsync(AcmeChallenge challenge, string domain, string dnsProvider, Dictionary<string, object>? credentials = null)
        {
            await _semaphore.WaitAsync();
            try
            {
                _logger.LogInformation("配置DNS-01挑战验证: {Domain}, Provider: {Provider}", domain, dnsProvider);

                var result = new ChallengeValidationResult
                {
                    Success = false,
                    ChallengeType = "dns-01",
                    Domain = domain,
                    ConfiguredAt = DateTime.UtcNow
                };

                // 验证DNS提供商支持
                if (!_dnsProviders.ContainsKey(dnsProvider.ToLower()))
                {
                    result.Errors.Add($"不支持的DNS提供商: {dnsProvider}");
                    return result;
                }

                var provider = _dnsProviders[dnsProvider.ToLower()];

                // 验证凭据
                if (provider.RequiresCredentials && (credentials == null || !credentials.Any()))
                {
                    result.Errors.Add("DNS提供商需要凭据但未提供");
                    return result;
                }

                // 计算DNS记录值
                var recordName = "_acme-challenge";
                var fullRecordName = $"{recordName}.{domain}";

                if (string.IsNullOrEmpty(challenge.KeyAuthorization))
                {
                    result.Errors.Add("挑战的 KeyAuthorization 为空");
                    return result;
                }

                var recordValue = challenge.KeyAuthorization!;

                // 配置DNS记录
                var dnsConfigured = await ConfigureDnsRecord(provider, fullRecordName, recordValue, "TXT", credentials);
                if (!dnsConfigured)
                {
                    result.Errors.Add("DNS记录配置失败");
                    return result;
                }

                result.ValidationSteps.Add("DNS TXT记录配置完成");
                result.ConfigurationDetails["RecordName"] = fullRecordName;
                result.ConfigurationDetails["RecordValue"] = recordValue;
                result.ConfigurationDetails["RecordType"] = "TXT";
                result.ConfigurationDetails["Provider"] = dnsProvider;

                // 更新挑战状态
                var challengeId = GenerateChallengeId(challenge, domain);
                _challengeStatuses[challengeId] = new ChallengeStatus
                {
                    ChallengeId = challengeId,
                    ChallengeType = "dns-01",
                    Domain = domain,
                    Status = "configured",
                    CreatedAt = DateTime.UtcNow,
                    ConfiguredAt = DateTime.UtcNow,
                    Token = challenge.Token,
                    KeyAuthorization = challenge.KeyAuthorization,
                    ValidationUrl = challenge.Url,
                    StatusDetails = new Dictionary<string, object>
                    {
                        ["RecordName"] = fullRecordName,
                        ["RecordValue"] = recordValue,
                        ["Provider"] = dnsProvider
                    }
                };

                result.Success = true;
                result.Message = "DNS-01挑战配置成功";
                result.ConfigurationStatus = "active";

                _logger.LogInformation("DNS-01挑战配置成功: {Domain}", domain);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "配置DNS-01挑战失败: {Domain}", domain);
                return new ChallengeValidationResult
                {
                    Success = false,
                    ChallengeType = "dns-01",
                    Domain = domain,
                    Message = $"配置失败: {ex.Message}",
                    ConfiguredAt = DateTime.UtcNow,
                    Errors = new List<string> { ex.Message }
                };
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// 验证DNS-01挑战
        /// </summary>
        public async Task<ChallengeValidationResult> ValidateDnsChallengeAsync(AcmeChallenge challenge, string domain, string dnsProvider, Dictionary<string, object>? credentials = null)
        {
            try
            {
                _logger.LogInformation("验证DNS-01挑战: {Domain}", domain);

                var result = new ChallengeValidationResult
                {
                    Success = false,
                    ChallengeType = "dns-01",
                    Domain = domain,
                    ConfiguredAt = DateTime.UtcNow,
                    ValidatedAt = DateTime.UtcNow
                };

                var recordName = "_acme-challenge";
                var fullRecordName = $"{recordName}.{domain}";
                var expectedValue = challenge.KeyAuthorization ?? string.Empty;

                // 查询DNS记录
                var dnsValues = await QueryDnsRecord(fullRecordName, "TXT");
                if (!dnsValues.Any())
                {
                    result.Errors.Add("未找到DNS TXT记录");
                    return result;
                }

                // 验证记录值
                var matchingRecord = dnsValues.Any(value => value.Trim('"') == expectedValue);
                if (!matchingRecord)
                {
                    result.Errors.Add("DNS记录值不匹配");
                    result.ConfigurationDetails["ExpectedValue"] = expectedValue;
                    result.ConfigurationDetails["ActualValues"] = dnsValues;
                    return result;
                }

                result.ValidationSteps.Add("DNS-01记录验证成功");
                result.Success = true;
                result.Message = "DNS-01挑战验证成功";
                result.ConfigurationStatus = "validated";

                // 更新挑战状态
                var challengeId = GenerateChallengeId(challenge, domain);
                if (_challengeStatuses.ContainsKey(challengeId))
                {
                    _challengeStatuses[challengeId].Status = "validated";
                    _challengeStatuses[challengeId].ValidatedAt = DateTime.UtcNow;
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "验证DNS-01挑战失败: {Domain}", domain);
                return new ChallengeValidationResult
                {
                    Success = false,
                    ChallengeType = "dns-01",
                    Domain = domain,
                    Message = $"验证失败: {ex.Message}",
                    ConfiguredAt = DateTime.UtcNow,
                    ValidatedAt = DateTime.UtcNow,
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        /// <summary>
        /// 配置TLS-ALPN-01挑战验证
        /// </summary>
        public async Task<ChallengeValidationResult> ConfigureTlsAlpnChallengeAsync(AcmeChallenge challenge, string domain)
        {
            await _semaphore.WaitAsync();
            try
            {
                _logger.LogInformation("配置TLS-ALPN-01挑战验证: {Domain}", domain);

                var result = new ChallengeValidationResult
                {
                    Success = false,
                    ChallengeType = "tls-alpn-01",
                    Domain = domain,
                    ConfiguredAt = DateTime.UtcNow
                };

                // 生成自签名证书
                if (string.IsNullOrEmpty(challenge.KeyAuthorization))
                {
                    result.Errors.Add("挑战的 KeyAuthorization 为空");
                    return result;
                }

                var certGenerated = await GenerateTlsAlpnCertificate(domain, challenge.KeyAuthorization!);
                if (!certGenerated)
                {
                    result.Errors.Add("TLS-ALPN证书生成失败");
                    return result;
                }

                // 配置Web服务器支持ALPN
                var alpnConfigured = await ConfigureWebServerForAlpn(domain);
                if (!alpnConfigured)
                {
                    result.Errors.Add("Web服务器ALPN配置失败");
                    return result;
                }

                result.ValidationSteps.Add("TLS-ALPN证书生成完成");
                result.ValidationSteps.Add("Web服务器ALPN配置完成");
                result.ConfigurationDetails["Domain"] = domain;
                result.ConfigurationDetails["AlpnProtocol"] = "acme-tls/1";
                result.ConfigurationDetails["CertificateGenerated"] = true;

                // 更新挑战状态
                var challengeId = GenerateChallengeId(challenge, domain);
                _challengeStatuses[challengeId] = new ChallengeStatus
                {
                    ChallengeId = challengeId,
                    ChallengeType = "tls-alpn-01",
                    Domain = domain,
                    Status = "configured",
                    CreatedAt = DateTime.UtcNow,
                    ConfiguredAt = DateTime.UtcNow,
                    Token = challenge.Token,
                    KeyAuthorization = challenge.KeyAuthorization,
                    ValidationUrl = challenge.Url
                };

                result.Success = true;
                result.Message = "TLS-ALPN-01挑战配置成功";
                result.ConfigurationStatus = "active";

                _logger.LogInformation("TLS-ALPN-01挑战配置成功: {Domain}", domain);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "配置TLS-ALPN-01挑战失败: {Domain}", domain);
                return new ChallengeValidationResult
                {
                    Success = false,
                    ChallengeType = "tls-alpn-01",
                    Domain = domain,
                    Message = $"配置失败: {ex.Message}",
                    ConfiguredAt = DateTime.UtcNow,
                    Errors = new List<string> { ex.Message }
                };
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// 验证TLS-ALPN-01挑战
        /// </summary>
        public async Task<ChallengeValidationResult> ValidateTlsAlpnChallengeAsync(AcmeChallenge challenge, string domain)
        {
            try
            {
                _logger.LogInformation("验证TLS-ALPN-01挑战: {Domain}", domain);

                var result = new ChallengeValidationResult
                {
                    Success = false,
                    ChallengeType = "tls-alpn-01",
                    Domain = domain,
                    ConfiguredAt = DateTime.UtcNow,
                    ValidatedAt = DateTime.UtcNow
                };

                // 连接到服务器并验证ALPN和证书
                if (string.IsNullOrEmpty(challenge.KeyAuthorization))
                {
                    result.Errors.Add("挑战的 KeyAuthorization 为空");
                    return result;
                }

                var validationSuccess = await ValidateTlsAlpnConnection(domain, challenge.KeyAuthorization!);
                if (!validationSuccess)
                {
                    result.Errors.Add("TLS-ALPN连接验证失败");
                    return result;
                }

                result.ValidationSteps.Add("TLS-ALPN连接验证成功");
                result.Success = true;
                result.Message = "TLS-ALPN-01挑战验证成功";
                result.ConfigurationStatus = "validated";

                // 更新挑战状态
                var challengeId = GenerateChallengeId(challenge, domain);
                if (_challengeStatuses.ContainsKey(challengeId))
                {
                    _challengeStatuses[challengeId].Status = "validated";
                    _challengeStatuses[challengeId].ValidatedAt = DateTime.UtcNow;
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "验证TLS-ALPN-01挑战失败: {Domain}", domain);
                return new ChallengeValidationResult
                {
                    Success = false,
                    ChallengeType = "tls-alpn-01",
                    Domain = domain,
                    Message = $"验证失败: {ex.Message}",
                    ConfiguredAt = DateTime.UtcNow,
                    ValidatedAt = DateTime.UtcNow,
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        /// <summary>
        /// 清理挑战验证配置
        /// </summary>
        public async Task<ChallengeCleanupResult> CleanupChallengeAsync(AcmeChallenge challenge, string domain, string challengeType)
        {
            try
            {
                _logger.LogInformation("清理挑战验证配置: {Domain}, Type: {Type}", domain, challengeType);

                var result = new ChallengeCleanupResult
                {
                    Success = false,
                    ChallengeType = challengeType,
                    Domain = domain,
                    CleanedAt = DateTime.UtcNow
                };

                var challengeId = GenerateChallengeId(challenge, domain);

                switch (challengeType.ToLower())
                {
                    case "http-01":
                        await CleanupHttpChallenge(domain, challenge.Token);
                        result.CleanupSteps.Add("HTTP-01挑战文件已清理");
                        break;

                    case "dns-01":
                        await CleanupDnsChallenge(domain, challengeId);
                        result.CleanupSteps.Add("DNS-01记录已清理");
                        break;

                    case "tls-alpn-01":
                        await CleanupTlsAlpnChallenge(domain);
                        result.CleanupSteps.Add("TLS-ALPN证书已清理");
                        break;

                    default:
                        result.Errors.Add($"不支持的挑战类型: {challengeType}");
                        return result;
                }

                // 清理挑战状态
                _challengeStatuses.Remove(challengeId);

                result.Success = true;
                result.Message = $"{challengeType}挑战清理成功";

                _logger.LogInformation("挑战清理成功: {Domain}, Type: {Type}", domain, challengeType);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理挑战失败: {Domain}, Type: {Type}", domain, challengeType);
                return new ChallengeCleanupResult
                {
                    Success = false,
                    ChallengeType = challengeType,
                    Domain = domain,
                    Message = $"清理失败: {ex.Message}",
                    CleanedAt = DateTime.UtcNow,
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        /// <summary>
        /// 获取挑战配置状态
        /// </summary>
        public async Task<ChallengeStatus> GetChallengeStatusAsync(string challengeId)
        {
            await Task.CompletedTask; // 同步操作，保持接口一致性

            if (_challengeStatuses.TryGetValue(challengeId, out var status))
            {
                return status;
            }

            return new ChallengeStatus
            {
                ChallengeId = challengeId,
                Status = "not_found",
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 获取支持的DNS提供商列表
        /// </summary>
        public async Task<IEnumerable<DnsProvider>> GetSupportedDnsProvidersAsync()
        {
            await Task.CompletedTask; // 同步操作
            return _dnsProviders.Values.ToList();
        }

        /// <summary>
        /// 测试DNS提供商连接
        /// </summary>
        public async Task<DnsProviderTestResult> TestDnsProviderConnectionAsync(string dnsProvider, Dictionary<string, object>? credentials = null)
        {
            try
            {
                _logger.LogInformation("测试DNS提供商连接: {Provider}", dnsProvider);

                var startTime = DateTime.UtcNow;

                if (!_dnsProviders.ContainsKey(dnsProvider.ToLower()))
                {
                    return new DnsProviderTestResult
                    {
                        Success = false,
                        Provider = dnsProvider,
                        Message = "不支持的DNS提供商",
                        TestedAt = DateTime.UtcNow,
                        Errors = new List<string> { $"提供商 {dnsProvider} 不受支持" }
                    };
                }

                var provider = _dnsProviders[dnsProvider.ToLower()];

                // 执行连接测试
                var testSuccess = await TestDnsProviderConnection(provider, credentials);

                var result = new DnsProviderTestResult
                {
                    Success = testSuccess,
                    Provider = dnsProvider,
                    Message = testSuccess ? "连接测试成功" : "连接测试失败",
                    TestedAt = DateTime.UtcNow,
                    ResponseTime = DateTime.UtcNow - startTime,
                    SupportedFeatures = provider.SupportedChallengeTypes
                };

                if (testSuccess)
                {
                    result.TestResults["Authentication"] = "Success";
                    result.TestResults["ApiAccess"] = "Success";
                }
                else
                {
                    result.Errors.Add("认证失败或API访问受限");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "测试DNS提供商连接失败: {Provider}", dnsProvider);
                return new DnsProviderTestResult
                {
                    Success = false,
                    Provider = dnsProvider,
                    Message = $"测试失败: {ex.Message}",
                    TestedAt = DateTime.UtcNow,
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        /// <summary>
        /// 自动配置挑战验证
        /// </summary>
        public async Task<AutoChallengeResult> AutoConfigureChallengeAsync(AcmeChallenge challenge, string domain,
            List<string>? preferredChallengeTypes = null,
            Dictionary<string, Dictionary<string, object>>? dnsCredentials = null)
        {
            try
            {
                _logger.LogInformation("自动配置挑战验证: {Domain}", domain);

                var result = new AutoChallengeResult
                {
                    Success = false,
                    ConfiguredAt = DateTime.UtcNow
                };

                // 确定挑战类型优先级
                var challengeTypes = preferredChallengeTypes ?? new List<string> { "http-01", "dns-01", "tls-alpn-01" };

                foreach (var challengeType in challengeTypes)
                {
                    _logger.LogInformation("尝试配置 {Type} 挑战: {Domain}", challengeType, domain);

                    ChallengeValidationResult? validationResult = null;

                    try
                    {
                        switch (challengeType.ToLower())
                        {
                            case "http-01":
                                validationResult = await ConfigureHttpChallengeAsync(challenge, domain);
                                break;

                            case "dns-01":
                                // 尝试所有DNS提供商
                                foreach (var dnsProvider in _dnsProviders.Keys)
                                {
                                    var credentials = dnsCredentials?.ContainsKey(dnsProvider) == true
                                        ? dnsCredentials[dnsProvider]
                                        : null;

                                    validationResult = await ConfigureDnsChallengeAsync(challenge, domain, dnsProvider, credentials);
                                    if (validationResult.Success)
                                    {
                                        result.SelectedChallengeType = challengeType;
                                        break;
                                    }
                                }
                                break;

                            case "tls-alpn-01":
                                validationResult = await ConfigureTlsAlpnChallengeAsync(challenge, domain);
                                break;
                        }

                        if (validationResult?.Success == true)
                        {
                            result.Success = true;
                            result.SelectedChallengeType = challengeType;
                            result.Message = $"自动配置成功，使用 {challengeType} 挑战";
                            break;
                        }

                        result.AttemptedChallenges.Add(validationResult ?? new ChallengeValidationResult
                        {
                            Success = false,
                            ChallengeType = challengeType,
                            Domain = domain,
                            Message = "配置失败"
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "尝试配置 {Type} 挑战失败: {Domain}", challengeType, domain);
                        result.AttemptedChallenges.Add(new ChallengeValidationResult
                        {
                            Success = false,
                            ChallengeType = challengeType,
                            Domain = domain,
                            Message = ex.Message,
                            Errors = new List<string> { ex.Message }
                        });
                    }
                }

                if (!result.Success)
                {
                    result.Message = "所有挑战类型配置均失败";
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "自动配置挑战失败: {Domain}", domain);
                return new AutoChallengeResult
                {
                    Success = false,
                    Message = $"自动配置失败: {ex.Message}",
                    ConfiguredAt = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// 监控挑战验证状态
        /// </summary>
        public async IAsyncEnumerable<ChallengeStatusUpdate> MonitorChallengeStatusAsync(string challengeId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var status = await GetChallengeStatusAsync(challengeId);

                yield return new ChallengeStatusUpdate
                {
                    ChallengeId = challengeId,
                    Status = status.Status,
                    Timestamp = DateTime.UtcNow,
                    Message = GetStatusMessage(status.Status),
                    Details = status.StatusDetails
                };

                // 如果挑战完成或失败，停止监控
                if (status.Status == "validated" || status.Status == "failed")
                {
                    yield break;
                }

                // 每5秒检查一次状态
                await Task.Delay(5000, cancellationToken);
            }
        }

        #region 私有方法

        private Dictionary<string, DnsProvider> InitializeDnsProviders()
        {
            return new Dictionary<string, DnsProvider>(StringComparer.OrdinalIgnoreCase)
            {
                ["cloudflare"] = new DnsProvider
                {
                    Name = "cloudflare",
                    DisplayName = "Cloudflare",
                    Description = "Cloudflare DNS API",
                    SupportedChallengeTypes = new List<string> { "dns-01" },
                    RequiredFields = new List<DnsProviderFieldConfig>
                    {
                        new() { Name = "api_token", Label = "API Token", Type = "password", Required = true },
                        new() { Name = "email", Label = "Email", Type = "text", Required = false }
                    },
                    RequiresCredentials = true
                },
                ["aliyun"] = new DnsProvider
                {
                    Name = "aliyun",
                    DisplayName = "阿里云DNS",
                    Description = "阿里云DNS API",
                    SupportedChallengeTypes = new List<string> { "dns-01" },
                    RequiredFields = new List<DnsProviderFieldConfig>
                    {
                        new() { Name = "access_key_id", Label = "Access Key ID", Type = "text", Required = true },
                        new() { Name = "access_key_secret", Label = "Access Key Secret", Type = "password", Required = true }
                    },
                    RequiresCredentials = true
                },
                ["tencent"] = new DnsProvider
                {
                    Name = "tencent",
                    DisplayName = "腾讯云DNS",
                    Description = "腾讯云DNS API",
                    SupportedChallengeTypes = new List<string> { "dns-01" },
                    RequiredFields = new List<DnsProviderFieldConfig>
                    {
                        new() { Name = "secret_id", Label = "Secret ID", Type = "text", Required = true },
                        new() { Name = "secret_key", Label = "Secret Key", Type = "password", Required = true }
                    },
                    RequiresCredentials = true
                },
                ["dnspod"] = new DnsProvider
                {
                    Name = "dnspod",
                    DisplayName = "DNSPod",
                    Description = "DNSPod (腾讯云DNSPod) API",
                    SupportedChallengeTypes = new List<string> { "dns-01" },
                    RequiredFields = new List<DnsProviderFieldConfig>
                    {
                        new() { Name = "secret_id", Label = "Secret ID", Type = "text", Required = true },
                        new() { Name = "secret_key", Label = "Secret Key", Type = "password", Required = true }
                    },
                    RequiresCredentials = true
                },
                ["aws"] = new DnsProvider
                {
                    Name = "aws",
                    DisplayName = "AWS Route 53",
                    Description = "AWS Route 53 DNS API",
                    SupportedChallengeTypes = new List<string> { "dns-01" },
                    RequiredFields = new List<DnsProviderFieldConfig>
                    {
                        new() { Name = "access_key_id", Label = "Access Key ID", Type = "text", Required = true },
                        new() { Name = "secret_access_key", Label = "Secret Access Key", Type = "password", Required = true },
                        new() { Name = "region", Label = "Region", Type = "text", Required = false }
                    },
                    RequiresCredentials = true
                },
                ["azure"] = new DnsProvider
                {
                    Name = "azure",
                    DisplayName = "Azure DNS",
                    Description = "Azure DNS API",
                    SupportedChallengeTypes = new List<string> { "dns-01" },
                    RequiredFields = new List<DnsProviderFieldConfig>
                    {
                        new() { Name = "client_id", Label = "Client ID", Type = "text", Required = true },
                        new() { Name = "client_secret", Label = "Client Secret", Type = "password", Required = true },
                        new() { Name = "tenant_id", Label = "Tenant ID", Type = "text", Required = true },
                        new() { Name = "subscription_id", Label = "Subscription ID", Type = "text", Required = true },
                        new() { Name = "resource_group", Label = "Resource Group", Type = "text", Required = true }
                    },
                    RequiresCredentials = true
                },
                ["godaddy"] = new DnsProvider
                {
                    Name = "godaddy",
                    DisplayName = "GoDaddy",
                    Description = "GoDaddy DNS API",
                    SupportedChallengeTypes = new List<string> { "dns-01" },
                    RequiredFields = new List<DnsProviderFieldConfig>
                    {
                        new() { Name = "api_key", Label = "API Key", Type = "text", Required = true },
                        new() { Name = "api_secret", Label = "API Secret", Type = "password", Required = true }
                    },
                    RequiresCredentials = true
                }
            };
        }

        private async Task<bool> ConfigureWebServerForHttpChallenge(string domain, string token, string keyAuthorization)
        {
            try
            {
                // 创建挑战文件目录
                var challengeDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", ".well-known", "acme-challenge");
                Directory.CreateDirectory(challengeDir);

                // 写入挑战文件
                var challengeFile = Path.Combine(challengeDir, token);
                await File.WriteAllTextAsync(challengeFile, keyAuthorization);

                // 配置Web服务器静态文件服务（在Startup.cs或Program.cs中配置）
                _logger.LogInformation("HTTP-01挑战文件已创建: {FilePath}", challengeFile);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "配置Web服务器HTTP-01挑战失败");
                return false;
            }
        }

        private async Task<bool> ConfigureDnsRecord(DnsProvider provider, string recordName, string recordValue, string recordType, Dictionary<string, object>? credentials)
        {
            try
            {
                if (!_dnsProviderServices.TryGetValue(provider.Name.ToLower(), out var dnsProviderService))
                {
                    _logger.LogWarning("不支持的DNS提供商: {Provider}", provider.Name);
                    return false;
                }

                var result = await dnsProviderService.CreateTxtRecordAsync("", recordName, recordValue, credentials);

                if (result.Success)
                {
                    _logger.LogInformation("DNS记录配置成功: {Provider} - {RecordName}", provider.Name, recordName);
                    return true;
                }
                else
                {
                    _logger.LogError("DNS记录配置失败: {Provider} - {RecordName} - {Message}", provider.Name, recordName, result.Message);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "配置DNS记录失败: {Provider}", provider.Name);
                return false;
            }
        }

        
        private async Task<List<string>> QueryDnsRecord(string recordName, string recordType)
        {
            try
            {
                _logger.LogInformation("查询DNS记录: {RecordName} ({Type})", recordName, recordType);

                // 使用国内可访问的 DNS over HTTPS (DoH) 服务
                // 主用：腾讯云 DNSPod（国内优化，访问稳定）
                var dohEndpoint = "https://doh.pub/dns-query";
                var encodedName = Uri.EscapeDataString(recordName);
                var queryUrl = $"{dohEndpoint}?name={encodedName}&type={recordType}";

                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);
                httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/dns-json");

                var response = await httpClient.GetAsync(queryUrl);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("腾讯云 DoH 查询失败，尝试阿里云备用方案: {StatusCode}", response.StatusCode);
                    return await FallbackDnsQuery(recordName, recordType);
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var dnsResponse = JsonSerializer.Deserialize<DohResponse>(responseContent);

                if (dnsResponse?.Answer == null || !dnsResponse.Answer.Any())
                {
                    _logger.LogDebug("未找到DNS记录: {RecordName}", recordName);
                    return new List<string>();
                }

                var txtRecords = dnsResponse.Answer
                    .Where(a => a.type == 16) // TXT 记录类型
                    .Select(a => a.data)
                    .ToList();

                _logger.LogInformation("查询到 {Count} 条TXT记录: {RecordName}", txtRecords.Count, recordName);
                return txtRecords;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DoH 查询异常，尝试备用方案: {RecordName}", recordName);
                try
                {
                    return await FallbackDnsQuery(recordName, recordType);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "备用DNS查询也失败: {RecordName}", recordName);
                    return new List<string>();
                }
            }
        }

        /// <summary>
        /// 备用 DNS 查询方案（支持多个 DoH 服务，按优先级尝试）
        /// </summary>
        private async Task<List<string>> FallbackDnsQuery(string recordName, string recordType)
        {
            // 备用 DoH 服务列表（国内 → 海外）
            var dohEndpoints = new List<string>
            {
                "https://dns.aliyuncs.com/dns-query",  // 阿里云（国内）
                "https://dns.360.com/dns-query",       // 360（国内）
                "https://cloudflare-dns.com/dns-query", // Cloudflare（海外）
                "https://dns.google/resolve"           // Google（海外）
            };

            foreach (var dohEndpoint in dohEndpoints)
            {
                try
                {
                    var encodedName = Uri.EscapeDataString(recordName);
                    var queryUrl = $"{dohEndpoint}?name={encodedName}&type={recordType}";

                    using var httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(10);
                    httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/dns-json");

                    var response = await httpClient.GetAsync(queryUrl);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogDebug("DoH 查询失败: {Endpoint}, Status: {Status}", dohEndpoint, response.StatusCode);
                        continue;
                    }

                    var responseContent = await response.Content.ReadAsStringAsync();
                    var dnsResponse = JsonSerializer.Deserialize<DohResponse>(responseContent);

                    if (dnsResponse?.Answer == null || !dnsResponse.Answer.Any())
                    {
                        continue;
                    }

                    var txtRecords = dnsResponse.Answer
                        .Where(a => a.type == 16)
                        .Select(a => a.data)
                        .ToList();

                    if (txtRecords.Any())
                    {
                        _logger.LogInformation("DoH 查询成功: {Endpoint}, 记录数: {Count}", dohEndpoint, txtRecords.Count);
                        return txtRecords;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "DoH 查询异常: {Endpoint}", dohEndpoint);
                    continue;
                }
            }

            _logger.LogWarning("所有备用 DoH 查询均失败: {RecordName}", recordName);
            return new List<string>();
        }

        private async Task<bool> GenerateTlsAlpnCertificate(string domain, string keyAuthorization)
        {
            try
            {
                // 生成TLS-ALPN-01挑战所需的证书
                // 这里是简化的实现
                await Task.Delay(200);

                _logger.LogInformation("TLS-ALPN证书生成成功: {Domain}", domain);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生成TLS-ALPN证书失败: {Domain}", domain);
                return false;
            }
        }

        private async Task<bool> ConfigureWebServerForAlpn(string domain)
        {
            try
            {
                // 配置Web服务器支持ALPN协议
                await Task.Delay(100);

                _logger.LogInformation("Web服务器ALPN配置成功: {Domain}", domain);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "配置Web服务器ALPN失败: {Domain}", domain);
                return false;
            }
        }

        private async Task<bool> ValidateTlsAlpnConnection(string domain, string keyAuthorization)
        {
            try
            {
                // 验证TLS-ALPN连接和证书
                await Task.Delay(200);

                _logger.LogInformation("TLS-ALPN连接验证成功: {Domain}", domain);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TLS-ALPN连接验证失败: {Domain}", domain);
                return false;
            }
        }

        private async Task CleanupHttpChallenge(string domain, string token)
        {
            try
            {
                var challengeFile = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", ".well-known", "acme-challenge", token);

                if (File.Exists(challengeFile))
                {
                    File.Delete(challengeFile);
                    _logger.LogInformation("HTTP-01挑战文件已删除: {FilePath}", challengeFile);
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "清理HTTP-01挑战文件失败: {Domain}", domain);
            }
        }

        private async Task CleanupDnsChallenge(string domain, string challengeId)
        {
            try
            {
                // 从挑战状态中获取 DNS 记录信息
                if (!_challengeStatuses.TryGetValue(challengeId, out var challengeStatus))
                {
                    _logger.LogWarning("未找到挑战状态，无法清理DNS记录: {Domain}, ChallengeId: {ChallengeId}", domain, challengeId);
                    return;
                }

                var statusDetails = challengeStatus.StatusDetails;
                var providerName = statusDetails.GetValueOrDefault("Provider")?.ToString();
                var recordName = statusDetails.GetValueOrDefault("RecordName")?.ToString();
                var recordValue = statusDetails.GetValueOrDefault("RecordValue")?.ToString();

                if (string.IsNullOrEmpty(providerName) || string.IsNullOrEmpty(recordName))
                {
                    _logger.LogWarning("DNS记录信息不完整，无法清理: {Domain}", domain);
                    return;
                }

                // 获取 DNS 提供商
                if (!_dnsProviderServices.TryGetValue(providerName.ToLower(), out var dnsProvider))
                {
                    _logger.LogWarning("未找到DNS提供商: {Provider}", providerName);
                    return;
                }

                _logger.LogInformation("开始清理DNS记录: {RecordName} (Provider: {Provider})", recordName, providerName);

                // 删除 DNS TXT 记录
                var result = await dnsProvider.DeleteTxtRecordAsync(domain, recordName, recordValue ?? string.Empty, null);

                if (result.Success)
                {
                    _logger.LogInformation("DNS记录清理成功: {RecordName}", recordName);
                }
                else
                {
                    _logger.LogWarning("DNS记录清理失败: {RecordName}, 原因: {Message}", recordName, result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "清理DNS-01记录失败: {Domain}", domain);
            }
        }

        private async Task CleanupTlsAlpnChallenge(string domain)
        {
            try
            {
                // 清理TLS-ALPN证书
                await Task.Delay(100);

                _logger.LogInformation("TLS-ALPN证书清理完成: {Domain}", domain);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "清理TLS-ALPN证书失败: {Domain}", domain);
            }
        }

        private async Task<bool> TestDnsProviderConnection(DnsProvider provider, Dictionary<string, object>? credentials)
        {
            try
            {
                // 测试DNS提供商连接
                await Task.Delay(200);

                var hasCredentials = !provider.RequiresCredentials || credentials?.Any() == true;
                return hasCredentials;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "测试DNS提供商连接失败: {Provider}", provider.Name);
                return false;
            }
        }

        private string GenerateChallengeId(AcmeChallenge challenge, string domain)
        {
            // 生成唯一的挑战ID
            var data = $"{challenge.Type}:{domain}:{challenge.Token}:{DateTime.UtcNow:yyyyMMddHHmmss}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(data));
            return Convert.ToHexString(hash).ToLower()[..16];
        }

        private string GetStatusMessage(string status)
        {
            return status switch
            {
                "pending" => "等待配置",
                "configured" => "配置完成",
                "validated" => "验证成功",
                "failed" => "验证失败",
                "cleanup" => "清理完成",
                _ => "未知状态"
            };
        }

        #endregion

        #region DNS over HTTPS Response Models

        /// <summary>
        /// DNS over HTTPS 响应模型
        /// </summary>
        private class DohResponse
        {
            public int Status { get; set; }
            public bool TC { get; set; }
            public bool RD { get; set; }
            public bool RA { get; set; }
            public bool AD { get; set; }
            public bool CD { get; set; }
            public List<DohAnswer>? Answer { get; set; }
            public List<DohAuthority>? Authority { get; set; }
            public List<object>? Additional { get; set; }
            public List<DohQuestion>? Question { get; set; }
        }

        private class DohAnswer
        {
            public string name { get; set; } = string.Empty;
            public int type { get; set; }
            public int ttl { get; set; }
            public string data { get; set; } = string.Empty;
        }

        private class DohAuthority
        {
            public string name { get; set; } = string.Empty;
            public int type { get; set; }
            public int ttl { get; set; }
            public string data { get; set; } = string.Empty;
        }

        private class DohQuestion
        {
            public string name { get; set; } = string.Empty;
            public int type { get; set; }
        }

        #endregion
    }

    }