using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using DockerPanel.API.Models.Acme;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http;
using Microsoft.AspNetCore.SignalR;
using DockerPanel.API.Hubs;
using DockerPanel.API.Data;
using DockerPanel.API.Services.Acme.DnsProviders;
using TinyDb;
using TinyDb.Bson;
using TinyDb.Core;
using TinyDb.Collections;
using DnsClient;
using DockerPanel.API.Services;

namespace DockerPanel.API.Services.Acme
{
    /// <summary>
    /// 基于 Certes 库的真实 ACME 协议实现
    /// </summary>
    public class CertesAcmeService : IAcmeService
    {
        private readonly ILogger<CertesAcmeService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHubContext<DockerPanelHub> _hubContext;
        private readonly ICertificateProgressService _progressService;
        private readonly IAcmeChallengeStore _challengeStore;
        private readonly DataBaseService _dataBaseService;
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly Dictionary<string, IAcmeContext> _acmeContexts;
        private readonly Dictionary<string, IKey> _accountKeys;
        private readonly TinyDbContext _dbContext;
        private readonly Dictionary<string, IDnsProvider> _dnsProviders;
        private readonly TlsAlpnChallengeService _tlsAlpnChallengeService;
        private readonly SniCertificateSelector _sniCertificateSelector;

        // 使用静态字典来跨请求保持ACME上下文
        private static readonly ConcurrentDictionary<string, IAcmeContext> _staticAcmeContexts = new();
        private static readonly ConcurrentDictionary<string, IKey> _staticAccountKeys = new();

        public CertesAcmeService(
            ILogger<CertesAcmeService> logger,
            IHttpClientFactory httpClientFactory,
            IHubContext<DockerPanelHub> hubContext,
            ICertificateProgressService progressService,
            IAcmeChallengeStore challengeStore,
            DataBaseService dataBaseService,
            IBackgroundTaskQueue taskQueue,
            TinyDbContext dbContext,
            CloudflareDnsProvider cloudflareProvider,
            AliyunDnsProvider aliyunProvider,
            TencentDnsProvider tencentProvider,
            DnsPodDnsProvider dnspodProvider,
            DnsPodTraditionalDnsProvider dnspodTraditionalProvider,
            TlsAlpnChallengeService tlsAlpnChallengeService,
            SniCertificateSelector sniCertificateSelector)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _hubContext = hubContext;
            _progressService = progressService;
            _challengeStore = challengeStore;
            _dataBaseService = dataBaseService;
            _taskQueue = taskQueue;
            _dbContext = dbContext;
            _tlsAlpnChallengeService = tlsAlpnChallengeService;
            _sniCertificateSelector = sniCertificateSelector;

            // 初始化DNS提供商字典
            _dnsProviders = new Dictionary<string, IDnsProvider>(StringComparer.OrdinalIgnoreCase)
            {
                ["cloudflare"] = cloudflareProvider,
                ["aliyun"] = aliyunProvider,
                ["tencent"] = tencentProvider,
                ["dnspod"] = dnspodProvider,
                ["dnspod-traditional"] = dnspodTraditionalProvider
            };

            _acmeContexts = new Dictionary<string, IAcmeContext>();
            _accountKeys = new Dictionary<string, IKey>();
        }

        #region ACME Provider Management

        public async Task<IEnumerable<AcmeProvider>> GetProvidersAsync()
        {
            return await Task.FromResult(new List<AcmeProvider>
            {
                new AcmeProvider
                {
                    Name = "letsencrypt",
                    DisplayName = "Let's Encrypt",
                    DirectoryUrl = "https://acme-v02.api.letsencrypt.org/directory",
                    IsProduction = false,
                    IsStaging = false,
                    SupportedChallengeTypes = new List<string> { "http-01", "dns-01" },
                    Description = "免费、自动化、开放的证书颁发机构"
                },

                new AcmeProvider
                {
                    Name = "zerossl",
                    DisplayName = "ZeroSSL",
                    DirectoryUrl = "https://acme.zerossl.com/v2/DV90",
                    IsProduction = false,
                    IsStaging = false,
                    SupportedChallengeTypes = new List<string> { "http-01", "dns-01" },
                    Description = "提供免费 SSL 证书的服务商"
                }
            });
        }

        public async Task<AcmeConnectionTestResult> TestProviderConnectionAsync(string provider)
        {
            try
            {
                var directoryUrl = GetDirectoryUrl(provider);
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);
                using var response = await httpClient.GetAsync(directoryUrl);
                stopwatch.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    return new AcmeConnectionTestResult
                    {
                        Success = false,
                        Message = $"连接失败: {(int)response.StatusCode} {response.ReasonPhrase}",
                        Provider = provider,
                        DirectoryUrl = directoryUrl,
                        ResponseTime = stopwatch.Elapsed,
                        Version = "ACMEv2"
                    };
                }

                return new AcmeConnectionTestResult
                {
                    Success = true,
                    Message = "连接成功",
                    Provider = provider,
                    DirectoryUrl = directoryUrl,
                    ResponseTime = stopwatch.Elapsed,
                    Version = "ACMEv2",
                    SupportedChallengeTypes = new List<string> { "http-01", "dns-01" }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "测试 ACME 提供商连接失败: {Provider}", provider);
                return new AcmeConnectionTestResult
                {
                    Success = false,
                    Message = "连接失败: " + ex.Message,
                    Provider = provider,
                    DirectoryUrl = GetDirectoryUrl(provider),
                    ResponseTime = TimeSpan.Zero,
                    Version = "ACMEv2"
                };
            }
        }

        #endregion

        #region ACME Account Management

        public async Task<IEnumerable<AcmeAccount>> GetAccountsAsync()
        {
            try
            {
                var accountsCollection = _dbContext.GetCollection<AcmeAccount>("acme_accounts");
                var accounts = accountsCollection.FindAll().ToList();

                _logger.LogInformation("从数据库获取到 {Count} 个账户", accounts.Count);
                return accounts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取账户列表失败");
                return new List<AcmeAccount>();
            }
        }

        public async Task<AcmeAccount?> GetAccountAsync(string accountId)
        {
            try
            {
                var accountsCollection = _dbContext.GetCollection<AcmeAccount>("acme_accounts");
                var dbAccount = accountsCollection.FindById(accountId);

                if (dbAccount != null)
                {
                    _logger.LogInformation("成功从数据库获取账户: {AccountId}", accountId);

                    if (string.IsNullOrEmpty(dbAccount.AccountKey))
                    {
                        _logger.LogWarning("账户密钥为空: {AccountId}", accountId);
                        return null;
                    }
                }
                else
                {
                    _logger.LogWarning("未找到账户: {AccountId}", accountId);
                }

                return dbAccount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取账户信息失败: {AccountId}", accountId);
                return null;
            }
        }

        public async Task<AcmeAccount> CreateAccountAsync(CreateAcmeAccountRequest request)
        {
            try
            {
                var directoryUrl = GetDirectoryUrl(request.Provider);
                var accountKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
                var acme = new AcmeContext(new Uri(directoryUrl), accountKey);

                var contacts = new[] { $"mailto:{request.Email}" };

                // 检查是否需要 EAB（External Account Binding）
                var requiresEab = RequiresEab(request.Provider);

                IAccountContext accountContext;
                if (requiresEab)
                {
                    // 需要验证 EAB 凭据
                    if (string.IsNullOrEmpty(request.EabKid) || string.IsNullOrEmpty(request.EabHmacKey))
                    {
                        throw new ArgumentException($"提供商 {request.Provider} 需要 EAB 凭据（EAB Key ID 和 EAB HMAC Key）。请在对应平台获取 EAB 凭据后重新创建账户。");
                    }

                    _logger.LogInformation("使用 EAB 创建账户: {Email} @ {Provider}, KID: {Kid}", request.Email, request.Provider, request.EabKid);

                    // 使用 EAB 创建账户 (Certes API: NewAccount(contacts, termsOfServiceAgreed, eabKid, eabHmacKey))
                    accountContext = await acme.NewAccount(contacts, true, request.EabKid, request.EabHmacKey);
                }
                else
                {
                    // 不需要 EAB，直接创建账户
                    _logger.LogInformation("直接创建账户: {Email} @ {Provider}", request.Email, request.Provider);
                    accountContext = await acme.NewAccount(contacts, true);
                }

                var account = new AcmeAccount
                {
                    Id = Guid.NewGuid().ToString(),
                    Email = request.Email,
                    Provider = request.Provider,
                    AccountKey = accountKey.ToPem(),
                    AccountUri = accountContext.Location?.ToString() ?? string.Empty,
                    IsActive = accountContext != null,
                    CreatedAt = DateTime.UtcNow,
                    Metadata = new Dictionary<string, object>
                    {
                        ["KeyType"] = "ECDSA",
                        ["KeySize"] = "P256",
                        ["DirectoryUrl"] = directoryUrl
                    }
                };

                // 如果使用了 EAB，保存 EAB 信息到元数据
                if (requiresEab && !string.IsNullOrEmpty(request.EabKid))
                {
                    account.Metadata["EabKid"] = request.EabKid;
                }

                // 保存账户密钥以供后续使用
                _accountKeys[account.Id] = accountKey;
                _staticAccountKeys[account.Id] = accountKey;
                CacheAcmeContext(account.Id, request.Provider, acme);

                // 保存账户到数据库
                try
                {
                    _dbContext.BeginTrans();
                    var accountsCollection = _dbContext.GetCollection<AcmeAccount>("acme_accounts");
                    accountsCollection.Insert(account);
                    _dbContext.Commit();
                    _logger.LogInformation("账户已保存到数据库: {AccountId}", account.Id);

                    // 验证保存是否成功
                    var verifyAccount = accountsCollection.FindById(account.Id);
                    if (verifyAccount != null)
                    {
                        _logger.LogInformation("账户保存验证成功: {AccountId}", account.Id);
                    }
                    else
                    {
                        _logger.LogError("账户保存验证失败: {AccountId}", account.Id);
                    }
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "保存账户到数据库失败: {AccountId}", account.Id);
                    _dbContext.Rollback();
                    throw;
                }

                _logger.LogInformation("成功创建 ACME 账户: {Email} @ {Provider}", request.Email, request.Provider);
                return account;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建 ACME 账户失败: {Email} @ {Provider}", request.Email, request.Provider);
                throw;
            }
        }

        /// <summary>
        /// 检查提供商是否需要 EAB
        /// </summary>
        private bool RequiresEab(string provider)
        {
            return provider.ToLowerInvariant() switch
            {
                "zerossl" => true,
                "google" => true,
                "sslcom" => true,
                _ => false
            };
        }

        public async Task<bool> DeleteAccountAsync(string accountId)
        {
            try
            {
                // 检查是否有关联的证书订单
                var ordersCollection = _dbContext.GetCollection<AcmeCertificateOrder>("acme_orders");
                var relatedOrders = ordersCollection.Find(o => o.AccountId == accountId).ToList();

                if (relatedOrders.Count > 0)
                {
                    // 检查是否有有效或待处理的证书
                    var activeOrders = relatedOrders.Where(o => 
                        o.Status == "valid" || 
                        o.Status == "pending" || 
                        o.Status == "ready" || 
                        o.Status == "processing"
                    ).ToList();

                    if (activeOrders.Count > 0)
                    {
                        var domains = string.Join(", ", activeOrders.SelectMany(o => o.Domains).Distinct());
                        _logger.LogWarning("无法删除账户 {AccountId}，存在 {Count} 个关联的活跃证书订单，域名: {Domains}", 
                            accountId, activeOrders.Count, domains);
                        throw new InvalidOperationException($"无法删除账户，存在 {activeOrders.Count} 个关联的活跃证书订单。请先删除相关证书后再删除账户。关联域名: {domains}");
                    }

                    // 如果只有历史记录（invalid, expired等），允许删除但记录日志
                    _logger.LogInformation("删除账户 {AccountId}，将保留 {Count} 个历史证书订单记录", accountId, relatedOrders.Count);
                }

                // 删除内存缓存
                _accountKeys.Remove(accountId);
                _acmeContexts.Remove(accountId);
                _staticAccountKeys.TryRemove(accountId, out _);
                _staticAcmeContexts.TryRemove(accountId, out _);

                // 删除数据库中的账户记录
                var accountsCollection = _dbContext.GetCollection<AcmeAccount>("acme_accounts");
                var deleted = accountsCollection.Delete(accountId);

                if (deleted > 0)
                {
                    _logger.LogInformation("已删除 ACME 账户: {AccountId}", accountId);
                    return true;
                }
                else
                {
                    _logger.LogWarning("未找到要删除的 ACME 账户: {AccountId}", accountId);
                    return false;
                }
            }
            catch (InvalidOperationException)
            {
                throw; // 重新抛出业务异常
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除 ACME 账户失败: {AccountId}", accountId);
                return false;
            }
        }

        #endregion

        #region Certificate Order Management

        public async Task<IEnumerable<AcmeCertificateOrder>> GetPendingOrdersForDomainsAsync(IEnumerable<string> domains)
        {
            try
            {
                var ordersCollection = _dbContext.GetCollection<AcmeCertificateOrder>("acme_orders");
                var domainSet = domains.ToHashSet(StringComparer.OrdinalIgnoreCase);

                // 查找状态为pending且包含相同域名的订单
                var pendingOrders = ordersCollection.Find(o =>
                    o.Status == "pending" &&
                    o.Domains.Any(d => domainSet.Contains(d))
                ).ToList();

                _logger.LogInformation("找到 {Count} 个pending订单，域名: {Domains}", pendingOrders.Count, string.Join(",", domains));
                return pendingOrders;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取pending订单失败: {Domains}", string.Join(",", domains));
                return Enumerable.Empty<AcmeCertificateOrder>();
            }
        }

        public async Task<IEnumerable<AcmeCertificateOrder>> GetCertificateOrdersAsync(string? accountId = null)
        {
            var ordersCollection = _dbContext.GetCollection<AcmeCertificateOrder>("acme_orders");

            if (!string.IsNullOrEmpty(accountId))
            {
                return ordersCollection.Find(o => o.AccountId == accountId).ToList();
            }

            return ordersCollection.FindAll().ToList();
        }

        public async Task<AcmeCertificateOrder?> GetCertificateOrderAsync(string orderId)
        {
            try
            {
                var ordersCollection = _dbContext.GetCollection<AcmeCertificateOrder>("acme_orders");
                return ordersCollection.FindById(orderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取证书订单失败: {OrderId}", orderId);
                return null;
            }
        }

        public async Task<AcmeCertificateOrder?> OrderCertificateAsync(AcmeCertificateRequest request)
        {
            string? progressId = null;
            AcmeCertificateOrder? order = null;
            try
            {
                // 预先生成证书订单ID
                var orderId = Guid.NewGuid().ToString();

                // 创建进度跟踪
                var progressRequest = new ProgressTrackRequest
                {
                    CertificateId = orderId, // 使用证书订单的ID
                    ApplicationName = $"证书申请: {string.Join(",", request.Domains)}",
                    Domains = request.Domains,
                    Provider = request.AccountId,
                    ChallengeType = "http-01", // 默认挑战类型
                    Metadata = new Dictionary<string, object>
                    {
                        ["AccountId"] = request.AccountId,
                        ["RequestedAt"] = DateTime.UtcNow
                    }
                };

                progressId = await _progressService.CreateProgressAsync(progressRequest);

                // 初始化ACME客户端
                await _progressService.UpdateProgressStepAsync(progressId,
                    CertificateApplicationStep.InitializingAcmeClient,
                    "初始化ACME客户端");

                // 🔧 修复：为每个证书订单创建独立的ACME上下文，避免授权冲突
                var acme = await CreateIndependentAcmeContextAsync(request.AccountId, request.AccountKey, request.AcmeProvider, progressId);

                // 检查ACME上下文是否创建成功
                if (acme == null)
                {
                    _logger.LogError("无法创建ACME上下文，证书申请失败: {AccountId}", request.AccountId);
                    if (!string.IsNullOrEmpty(progressId))
                    {
                        await _progressService.AddErrorAsync(progressId, "无法创建ACME上下文");
                    }
                    return null;
                }

                // 创建ACME订单
                await _progressService.UpdateProgressStepAsync(progressId,
                    CertificateApplicationStep.CreatingOrder,
                    "创建ACME订单");

                var identifiers = request.Domains.ToArray();
                var orderContext = await acme.NewOrder(identifiers);

                order = new AcmeCertificateOrder
                {
                    Id = orderId, // 使用预先生成的ID
                    AccountId = request.AccountId,
                    OrderUri = orderContext.Location?.ToString() ?? string.Empty,
                    Domains = request.Domains,
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(1),
                    FinalizeUri = "",
                    CertificateUri = "",
                    ProgressId = progressId,
                    Authorizations = new List<AcmeAuthorization>(),
                    Metadata = new Dictionary<string, object>(request.Metadata ?? new Dictionary<string, object>())
                };

                // 获取授权
                await _progressService.UpdateProgressStepAsync(progressId,
                    CertificateApplicationStep.GettingAuthorizations,
                    "获取域名授权信息");

                var authorizations = await orderContext.Authorizations();

                // 处理每个授权
                foreach (var authContext in authorizations)
                {
                    await _progressService.UpdateProgressStepAsync(progressId,
                        CertificateApplicationStep.ValidatingDomains,
                        $"验证域名: {authContext.Location?.ToString()}");

                    var authorizationDetails = await authContext.Resource();
                    var domain = authorizationDetails.Identifier?.Value
                                 ?? order.Domains.FirstOrDefault()
                                 ?? "unknown";
                    var isWildcard = authorizationDetails.Wildcard == true
                                     || order.Domains.Any(d => d.Equals($"*.{domain}", StringComparison.OrdinalIgnoreCase));

                    var authorization = new AcmeAuthorization
                    {
                        Id = Guid.NewGuid().ToString(),
                        Domain = domain,
                        Status = "pending",
                        ExpiresAt = DateTime.UtcNow.AddHours(1),
                        IsWildcard = isWildcard,
                        Challenges = new List<AcmeChallenge>()
                    };

                    // 处理挑战
                    var challenges = await GetChallengesAsync(authContext, authorization.Domain, order);
                    authorization.Challenges = challenges;

                    order.Authorizations.Add(authorization);
                }

                await _progressService.CompleteCurrentStepAsync(progressId, "证书订单创建成功");

                await _progressService.MarkAsCompletedAsync(progressId);

                // 保存到数据库
                try
                {
                    var ordersCollection = _dbContext.GetCollection<AcmeCertificateOrder>("acme_orders");
                    _dbContext.BeginTrans();
                    ordersCollection.Insert(order);
                    _dbContext.Commit();

                    _logger.LogInformation("证书订单已保存到数据库: {OrderId} for Account {AccountId}",
                        order.Id, request.AccountId);
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "保存证书订单到数据库失败: {OrderId}", order.Id);
                    _dbContext.Rollback();
                    throw;
                }

                _logger.LogInformation("成功创建证书订单: {Domains} for Account {AccountId}",
                    string.Join(",", request.Domains), request.AccountId);

                // 🚀 将自动验证流程移至后台任务队列，避免阻塞请求导致前端超时
                _logger.LogInformation("将自动验证挑战加入后台队列: OrderId={OrderId}", order.Id);

                _taskQueue.QueueBackgroundWorkItem(async token =>
                {
                    try
                    {
                        _logger.LogInformation("后台任务开始处理订单验证: OrderId={OrderId}", order.Id);

                        if (!string.IsNullOrEmpty(progressId))
                        {
                            await _progressService.UpdateProgressStepAsync(progressId,
                                CertificateApplicationStep.ValidatingDomains,
                                "开始后台自动验证域名控制权");
                        }

                        // 执行自动验证
                        var autoValidationSuccess = await PerformAutoValidationAsync(order, progressId);

                        // 获取订单集合以更新状态
                        var ordersCollection = _dbContext.GetCollection<AcmeCertificateOrder>("acme_orders");

                        if (autoValidationSuccess)
                        {
                            _logger.LogInformation("后台自动验证成功: OrderId={OrderId}", order.Id);
                            if (!string.IsNullOrEmpty(progressId))
                            {
                                await _progressService.CompleteCurrentStepAsync(progressId, "所有域名验证成功");
                                await _progressService.MarkAsCompletedAsync(progressId);
                            }

                            // 注意：PerformAutoValidationAsync 内部的 FinalizeCertificateAsync 已经更新了 order.Status 为 valid
                        }
                        else
                        {
                            _logger.LogError("后台自动验证失败，订单标记为失败状态: OrderId={OrderId}", order.Id);

                            order.Status = "failed";
                            order.Error = "自动验证失败：无法完成域名验证。请检查网络连接、防火墙设置或DNS配置。";

                            if (!string.IsNullOrEmpty(progressId))
                            {
                                await _progressService.AddErrorAsync(progressId, "自动验证失败，请检查配置后重试。");
                                await _progressService.MarkAsFailedAsync(progressId, "验证失败");
                            }
                        }

                        // 💾 最终更新数据库中的订单状态
                        ordersCollection.Update(order);
                        _logger.LogInformation("订单状态已更新到数据库: OrderId={OrderId}, Status={Status}", order.Id, order.Status);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "执行后台证书验证任务时发生异常: OrderId={OrderId}", order.Id);

                        order.Status = "failed";
                        order.Error = $"后台处理异常: {ex.Message}";
                        _dbContext.GetCollection<AcmeCertificateOrder>("acme_orders").Update(order);

                        if (!string.IsNullOrEmpty(progressId))
                        {
                            await _progressService.AddErrorAsync(progressId, $"后台处理异常: {ex.Message}");
                            await _progressService.MarkAsFailedAsync(progressId, "后台处理异常");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建证书订单时发生异常: {Domains}", string.Join(",", request.Domains));
                if (!string.IsNullOrEmpty(progressId))
                {
                    await _progressService.AddErrorAsync(progressId, $"创建订单失败: {ex.Message}");
                }
                throw;
            }

            return order ?? throw new InvalidOperationException("订单创建失败");
        }

        /// <summary>
        /// 执行自动验证流程
        /// </summary>
        private async Task<bool> PerformAutoValidationAsync(AcmeCertificateOrder order, string? progressId)
        {
            // 获取挑战类型和DNS提供商信息
            var rawChallengeType = order.Metadata?.GetValueOrDefault("challengeType")?.ToString();
            var challengeType = string.IsNullOrWhiteSpace(rawChallengeType) ? "http-01" : rawChallengeType;

            _logger.LogInformation("自动验证开始: OrderId={OrderId}, ChallengeType={ChallengeType}", order.Id, challengeType);

            var dnsProvider = order.Metadata?.ContainsKey("dnsProvider") == true
                ? order.Metadata["dnsProvider"]?.ToString()
                : string.Empty;

            var dnsCredentials = order.Metadata?.ContainsKey("dnsCredentials") == true
                ? order.Metadata["dnsCredentials"] as Dictionary<string, object>
                : new Dictionary<string, object>();

            // 收集所有需要验证的授权域名和对应的记录值
            var validationTasks = new List<(IAuthorizationContext authContext, string domain, string recordName, string recordValue)>();

            try
            {
                // 获取ACME上下文
                var acme = await GetOrCreateAcmeContextAsync(order.AccountId, null, progressId);
                if (acme == null)
                {
                    _logger.LogError("无法获取ACME上下文进行自动验证: OrderId={OrderId}", order.Id);
                    return false;
                }

                // 获取订单上下文
                var orderContext = acme.Order(new Uri(order.OrderUri));
                if (orderContext == null)
                {
                    _logger.LogError("无法获取订单上下文进行自动验证: OrderId={OrderId}", order.Id);
                    return false;
                }

                _logger.LogInformation("开始自动验证: OrderId={OrderId}, ChallengeType={ChallengeType}, DnsProvider={DnsProvider}",
                    order.Id, challengeType, dnsProvider);

                // 获取所有授权
                var authorizations = await orderContext.Authorizations();
                bool allValidated = true;

                if (challengeType == "dns-01")
                {
                    // 预处理：收集所有授权信息
                    foreach (var authContext in authorizations)
                    {
                        var authResource = await authContext.Resource();
                        var domain = authResource.Identifier?.Value ?? "unknown";

                        var challenges = await authContext.Challenges();
                        var dnsChallenge = challenges.FirstOrDefault(c => c.Type == "dns-01");

                        if (dnsChallenge != null)
                        {
                            var recordName = $"_acme-challenge.{domain}";
                            // 🔧 修复：DNS-01 的 TXT 记录值必须是 KeyAuthz 的 SHA256 哈希的 base64url 编码
                            // 根据 ACME RFC-8555 规范
                            var keyAuthz = dnsChallenge.KeyAuthz;
                            using var sha256 = System.Security.Cryptography.SHA256.Create();
                            var hash = sha256.ComputeHash(System.Text.Encoding.ASCII.GetBytes(keyAuthz));
                            var recordValue = Convert.ToBase64String(hash)
                                .TrimEnd('=')        // 移除填充
                                .Replace('+', '-')   // 替换为 URL 安全字符
                                .Replace('/', '_');  // 替换为 URL 安全字符

                            validationTasks.Add((authContext, domain, recordName, recordValue));
                            _logger.LogInformation("收集DNS-01验证任务: Domain={Domain}, RecordName={RecordName}, RecordValue={RecordValue}", domain, recordName, recordValue);
                        }
                    }

                    // 顺序验证：先验证所有授权（使用相同的DNS记录名）
                    // 先创建所有DNS记录，然后等待传播，最后统一验证
                    var provider = string.IsNullOrEmpty(dnsProvider) ? null : GetDnsProvider(dnsProvider);
                    if (provider == null)
                    {
                        _logger.LogError("未找到DNS提供商: {DnsProvider}", dnsProvider);
                        return false;
                    }

                    // 步骤1: 为所有授权创建DNS记录
                    _logger.LogInformation("步骤1: 为所有授权创建DNS记录");
                    foreach (var (authContext, domain, recordName, recordValue) in validationTasks)
                    {
                        if (!string.IsNullOrEmpty(progressId))
                        {
                            await _progressService.UpdateProgressStepAsync(progressId,
                                CertificateApplicationStep.ValidatingDomains,
                                $"正在创建DNS TXT记录: {recordName}");
                        }

                        var createResult = await provider.CreateTxtRecordAsync(domain, recordName, recordValue, dnsCredentials);
                        if (!createResult.Success)
                        {
                            _logger.LogError("创建DNS TXT记录失败: {Domain}, {Message}", domain, createResult.Message);
                            allValidated = false;
                        }
                        else
                        {
                            _logger.LogInformation("DNS TXT记录创建成功: {Domain}, RecordId={RecordId}", domain, createResult.RecordId);
                        }
                    }

                    if (!allValidated)
                    {
                        _logger.LogError("创建DNS记录失败，终止验证");
                        return false;
                    }

                    // 步骤2: 等待所有DNS记录传播
                    if (!string.IsNullOrEmpty(progressId))
                    {
                        await _progressService.UpdateProgressStepAsync(progressId,
                            CertificateApplicationStep.ValidatingDomains,
                            "正在等待DNS传播");
                    }

                    // 使用第一个记录等待传播（因为所有记录名相同）
                    if (validationTasks.Count > 0)
                    {
                        var firstTask = validationTasks[0];
                        _logger.LogInformation("步骤2: 等待DNS传播: {RecordName}", firstTask.recordName);
                        var propagationSuccess = await WaitForDnsPropagationAsync(firstTask.recordName, firstTask.recordValue, progressId);
                        if (!propagationSuccess)
                        {
                            _logger.LogWarning("DNS传播超时，但继续尝试验证");
                        }
                    }

                    // 步骤3: 逐个验证授权
                    _logger.LogInformation("步骤3: 验证所有授权");
                    foreach (var (authContext, domain, recordName, recordValue) in validationTasks)
                    {
                        _logger.LogInformation("开始验证域名: {Domain}", domain);

                        if (!string.IsNullOrEmpty(progressId))
                        {
                            await _progressService.UpdateProgressStepAsync(progressId,
                                CertificateApplicationStep.ValidatingDomains,
                                $"正在验证域名: {domain}");
                        }

                        // 获取挑战并验证
                        var challenges = await authContext.Challenges();
                        var dnsChallenge = challenges.FirstOrDefault(c => c.Type == "dns-01");

                        if (dnsChallenge == null)
                        {
                            var availableTypes = string.Join(", ", challenges.Select(c => c.Type));
                            _logger.LogWarning("未找到DNS-01挑战: {Domain}，可用挑战类型: {AvailableTypes}", domain, availableTypes);
                            allValidated = false;
                            continue;
                        }

                        _logger.LogInformation("通知ACME服务器验证DNS-01挑战: {Domain}", domain);
                        await dnsChallenge.Validate();

                        // 🚀 增加轮询逻辑：等待 ACME 服务端完成验证
                        int maxPollAttempts = 30; // 最多检查30次
                        int pollAttempts = 0;
                        string? finalStatus = "pending";

                        while (pollAttempts < maxPollAttempts)
                        {
                            await Task.Delay(3000); // 每次间隔3秒
                            var challengeResource = await dnsChallenge.Resource();
                            finalStatus = challengeResource.Status?.ToString()?.ToLowerInvariant();

                            _logger.LogInformation("等待ACME服务端验证结果: Domain={Domain}, Status={Status} (尝试 {Attempt}/{Max})",
                                domain, finalStatus, pollAttempts + 1, maxPollAttempts);

                            if (finalStatus == "valid" || finalStatus == "invalid")
                                break;

                            pollAttempts++;
                        }

                        if (finalStatus == "valid")
                        {
                            _logger.LogInformation("DNS-01挑战验证成功: Domain={Domain}", domain);
                        }
                        else
                        {
                            _logger.LogError("DNS-01挑战验证失败或超时: Domain={Domain}, Status={Status}", domain, finalStatus);
                            allValidated = false;
                        }
                    }

                    // 步骤4: 验证完成后清理DNS记录
                    if (!string.IsNullOrEmpty(progressId))
                    {
                        await _progressService.UpdateProgressStepAsync(progressId,
                            CertificateApplicationStep.ValidatingDomains,
                            "正在清理DNS记录");
                    }

                    _logger.LogInformation("步骤4: 清理DNS记录");
                    foreach (var (authContext, domain, recordName, recordValue) in validationTasks)
                    {
                        try
                        {
                            var deleteResult = await provider.DeleteAllTxtRecordsByNameAsync(domain, recordName, dnsCredentials);
                            if (deleteResult.Success)
                            {
                                _logger.LogInformation("DNS记录清理成功: {RecordName}, {Message}", recordName, deleteResult.Message);
                            }
                            else
                            {
                                _logger.LogWarning("DNS记录清理失败: {RecordName}, {Message}", recordName, deleteResult.Message);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "清理DNS记录时发生错误: {RecordName}", recordName);
                        }
                    }
                }
                else if (challengeType == "http-01")
                {
                    // HTTP-01 验证
                    foreach (var authContext in authorizations)
                    {
                        var authResource = await authContext.Resource();
                        var domain = authResource.Identifier?.Value ?? authContext.Location?.ToString().Split('/').LastOrDefault() ?? "unknown";
                        _logger.LogInformation("开始验证域名: {Domain}", domain);

                        if (!string.IsNullOrEmpty(progressId))
                        {
                            await _progressService.UpdateProgressStepAsync(progressId,
                                CertificateApplicationStep.ValidatingDomains,
                                $"正在验证域名: {domain} ({challengeType})");
                        }

                        var validationSuccess = await ValidateHttpChallengeAsync(authContext, domain, progressId);

                        if (!validationSuccess)
                        {
                            allValidated = false;
                            _logger.LogWarning("域名验证失败: {Domain} ({ChallengeType})", domain, challengeType);
                        }
                        else
                        {
                            _logger.LogInformation("域名验证成功: {Domain} ({ChallengeType})", domain, challengeType);
                        }
                    }
                }
                else if (challengeType == "tls-alpn-01")
                {
                    // TLS-ALPN-01 验证
                    foreach (var authContext in authorizations)
                    {
                        var authResource = await authContext.Resource();
                        var domain = authResource.Identifier?.Value ?? authContext.Location?.ToString().Split('/').LastOrDefault() ?? "unknown";
                        _logger.LogInformation("开始验证域名: {Domain}", domain);

                        if (!string.IsNullOrEmpty(progressId))
                        {
                            await _progressService.UpdateProgressStepAsync(progressId,
                                CertificateApplicationStep.ValidatingDomains,
                                $"正在验证域名: {domain} ({challengeType})");
                        }

                        var validationSuccess = await ValidateTlsAlpnChallengeAsync(authContext, progressId);

                        if (!validationSuccess)
                        {
                            allValidated = false;
                            _logger.LogWarning("域名验证失败: {Domain} ({ChallengeType})", domain, challengeType);
                        }
                        else
                        {
                            _logger.LogInformation("域名验证成功: {Domain} ({ChallengeType})", domain, challengeType);
                        }
                    }
                }
                else
                {
                    _logger.LogError("不支持的挑战类型: {ChallengeType}", challengeType);
                    return false;
                }

                // 如果所有验证都成功，尝试完成订单
                if (allValidated)
                {
                    await _progressService.UpdateProgressStepAsync(progressId!,
                        CertificateApplicationStep.DownloadingCertificate,
                        "正在完成证书申请并下载证书");

                    var success = await FinalizeCertificateAsync(orderContext, order, progressId);

                    // 如果是DNS-01挑战，验证完成后清理DNS记录
                    if (success && challengeType == "dns-01")
                    {
                        await CleanupDnsRecordsAsync(order, dnsProvider, dnsCredentials, progressId);
                    }

                    return success;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "自动验证过程中发生异常: OrderId={OrderId}", order.Id);

                if (!string.IsNullOrEmpty(progressId))
                {
                    await _progressService.AddErrorAsync(progressId, $"自动验证异常: {ex.Message}");
                }

                // 🔧 修复：发生异常时也清理 DNS 记录
                if (challengeType == "dns-01" && validationTasks.Count > 0 && !string.IsNullOrEmpty(dnsProvider))
                {
                    _logger.LogInformation("异常处理：正在清理已创建的 DNS 记录");
                    var provider = GetDnsProvider(dnsProvider);
                    if (provider != null)
                    {
                        foreach (var (_, domain, recordName, _) in validationTasks)
                        {
                            try
                            {
                                await provider.DeleteAllTxtRecordsByNameAsync(domain, recordName, dnsCredentials);
                            }
                            catch (Exception cleanupEx)
                            {
                                _logger.LogWarning(cleanupEx, "异常清理过程中删除 DNS 记录失败: {RecordName}", recordName);
                            }
                        }
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// 验证HTTP-01挑战
        /// </summary>
        private async Task<bool> ValidateHttpChallengeAsync(IAuthorizationContext authContext, string domain, string? progressId)
        {
            try
            {
                // 获取挑战
                var challenges = await authContext.Challenges();
                var httpChallenge = challenges.FirstOrDefault(c => c.Type == "http-01");

                if (httpChallenge == null)
                {
                    var availableTypes = string.Join(", ", challenges.Select(c => c.Type));
                    _logger.LogWarning("未找到HTTP-01挑战，可用挑战类型: {AvailableTypes}", availableTypes);

                    if (!string.IsNullOrEmpty(progressId))
                    {
                        await _progressService.AddErrorAsync(progressId, 
                            $"HTTP-01 验证不可用。可用挑战类型: {availableTypes}");
                    }
                    return false;
                }

                _logger.LogInformation("找到HTTP-01挑战，开始验证: Token={Token}, Domain={Domain}", httpChallenge.Token, domain);

                // 🔍 预检查：确保挑战文件已正确存储并可访问
                var preCheckSuccess = await PreCheckHttpChallengeAsync(httpChallenge, domain, progressId);
                if (!preCheckSuccess)
                {
                    _logger.LogError("挑战文件预检查失败，停止验证流程");
                    return false;
                }

                // 🚀 主动触发验证
                _logger.LogInformation("通知Let's Encrypt验证HTTP-01挑战");
                try
                {
                    await httpChallenge.Validate();
                    _logger.LogInformation("已通知Let's Encrypt验证HTTP-01挑战");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "通知Let's Encrypt验证HTTP-01挑战失败");
                    return false;
                }

                // 🚀 主动检查验证结果，使用更短的等待间隔
                int maxAttempts = 60; // 最多检查60次，每次间隔3秒，总计3分钟
                int attempts = 0;

                while (attempts < maxAttempts)
                {
                    await Task.Delay(3000); // 等待3秒给Let's Encrypt足够时间

                    // 🎯 重新获取挑战状态
                    var challengeDetail = await httpChallenge.Resource();
                    var currentStatus = challengeDetail.Status?.ToString()?.ToLowerInvariant();

                    _logger.LogInformation("检查挑战状态: {Status} (尝试 {Attempt}/{MaxAttempts})",
                        currentStatus, attempts + 1, maxAttempts);

                    // 如果状态变为valid，跳出循环
                    if (currentStatus == "valid")
                    {
                        _logger.LogInformation("HTTP-01挑战验证成功");

                        if (!string.IsNullOrEmpty(progressId))
                        {
                            await _progressService.CompleteCurrentStepAsync(progressId, "HTTP-01挑战验证成功");
                        }

                        return true;
                    }

                    // 如果状态变为invalid，直接失败
                    if (currentStatus == "invalid")
                    {
                        var error = challengeDetail.Error;
                        var errorType = error?.Type ?? "unknown";
                        var errorDetail = error?.Detail ?? "挑战验证失败";
                        var errorStatus = error?.Status ?? 0;

                        // 尝试从授权上下文获取更详细的错误信息
                        try
                        {
                            var authResource = await authContext.Resource();
                            var authChallenges = await authContext.Challenges();
                            var httpChal = authChallenges.FirstOrDefault(c => c.Type == "http-01");
                            if (httpChal != null)
                            {
                                var chalDetail = await httpChal.Resource();
                                if (chalDetail.Error != null)
                                {
                                    error = chalDetail.Error;
                                    errorType = error.Type ?? errorType;
                                    errorDetail = error.Detail ?? errorDetail;
                                    errorStatus = error.Status > 0 ? error.Status : errorStatus;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "获取授权挑战详细错误失败");
                        }

                        var errorMessage = $"[{errorType}] {errorDetail} (HTTP {errorStatus})";

                        _logger.LogError("HTTP-01挑战验证失败: {Error}, 完整错误: {@ErrorDetail}", errorMessage, error);

                        if (!string.IsNullOrEmpty(progressId))
                        {
                            await _progressService.AddErrorAsync(progressId, $"HTTP-01挑战验证失败: {errorMessage}");
                        }

                        return false;
                    }

                    attempts++;
                }

                _logger.LogWarning("HTTP-01挑战验证超时");

                if (!string.IsNullOrEmpty(progressId))
                {
                    await _progressService.AddErrorAsync(progressId, "HTTP-01挑战验证超时");
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "验证HTTP-01挑战时发生异常");

                if (!string.IsNullOrEmpty(progressId))
                {
                    await _progressService.AddErrorAsync(progressId, $"HTTP-01挑战验证异常: {ex.Message}");
                }

                return false;
            }
        }

        /// <summary>
        /// 验证TLS-ALPN-01挑战
        /// </summary>
        private async Task<bool> ValidateTlsAlpnChallengeAsync(IAuthorizationContext authContext, string? progressId)
        {
            string domain = "unknown";
            try
            {
                // 获取挑战
                var challenges = await authContext.Challenges();
                var tlsAlpnChallenge = challenges.FirstOrDefault(c => c.Type == "tls-alpn-01");

                if (tlsAlpnChallenge == null)
                {
                    var availableTypes = string.Join(", ", challenges.Select(c => c.Type));
                    _logger.LogWarning("未找到TLS-ALPN-01挑战，可用挑战类型: {AvailableTypes}", availableTypes);

                    if (!string.IsNullOrEmpty(progressId))
                    {
                        await _progressService.AddErrorAsync(progressId, 
                            $"TLS-ALPN-01 验证不可用。可用挑战类型: {availableTypes}。" +
                            $"请使用 HTTP-01 或 DNS-01 验证方式。");
                    }
                    return false;
                }

                _logger.LogInformation("找到TLS-ALPN-01挑战，开始验证: Token={Token}", tlsAlpnChallenge.Token);

                // 获取授权信息以获取域名
                var auth = await authContext.Resource();
                domain = auth.Identifier?.Value ?? "unknown";
                var keyAuthorization = tlsAlpnChallenge.KeyAuthz;

                // 确保挑战证书已在内存中就绪
                var existingCert = _tlsAlpnChallengeService.GetChallengeCertificate(domain);
                if (existingCert == null)
                {
                    _logger.LogInformation("TLS-ALPN-01 挑战证书未就绪，现在生成: Domain={Domain}", domain);
                    try
                    {
                        _tlsAlpnChallengeService.PrepareChallengeCertificate(domain, keyAuthorization);
                        _logger.LogInformation("TLS-ALPN-01 证书已就绪: Domain={Domain}", domain);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "生成 TLS-ALPN-01 证书失败: Domain={Domain}", domain);
                        if (!string.IsNullOrEmpty(progressId))
                        {
                            await _progressService.AddErrorAsync(progressId, $"生成 TLS-ALPN-01 证书失败: {ex.Message}");
                        }
                        return false;
                    }
                }
                else
                {
                    _logger.LogInformation("TLS-ALPN-01 证书已就绪: Domain={Domain}", domain);
                }

                if (!string.IsNullOrEmpty(progressId))
                {
                    await _progressService.UpdateProgressStepAsync(progressId,
                        CertificateApplicationStep.ValidatingDomains,
                        $"TLS-ALPN-01 证书已配置，正在验证: {domain}");
                }

                // 等待一小段时间确保证书服务已就绪
                await Task.Delay(TimeSpan.FromSeconds(1));

                // 主动触发验证
                _logger.LogInformation("通知Let's Encrypt验证TLS-ALPN-01挑战");
                try
                {
                    await tlsAlpnChallenge.Validate();
                    _logger.LogInformation("已通知Let's Encrypt验证TLS-ALPN-01挑战");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "通知Let's Encrypt验证TLS-ALPN-01挑战失败");
                    _tlsAlpnChallengeService.CleanupChallenge(domain);
                    return false;
                }

                // 检查验证结果
                int maxAttempts = 60;
                int attempts = 0;

                while (attempts < maxAttempts)
                {
                    await Task.Delay(3000);
                    attempts++;

                    // 重新获取挑战状态
                    var challengeDetail = await tlsAlpnChallenge.Resource();
                    var currentStatus = challengeDetail.Status?.ToString()?.ToLowerInvariant();

                    _logger.LogInformation("检查TLS-ALPN-01挑战状态: {Status} (尝试 {Attempt}/{MaxAttempts})",
                        currentStatus, attempts, maxAttempts);

                    // 如果状态变为valid，跳出循环
                    if (currentStatus == "valid")
                    {
                        _logger.LogInformation("TLS-ALPN-01挑战验证成功");
                        // 验证成功，清理内存中的挑战证书
                        _tlsAlpnChallengeService.CleanupChallenge(domain);

                        if (!string.IsNullOrEmpty(progressId))
                        {
                            await _progressService.CompleteCurrentStepAsync(progressId, "TLS-ALPN-01挑战验证成功");
                        }

                        return true;
                    }

                    // 如果状态变为invalid，直接失败
                    if (currentStatus == "invalid")
                    {
                        var errorMessage = challengeDetail.Error?.Detail ?? "挑战验证失败";
                        _logger.LogWarning("TLS-ALPN-01挑战验证失败: {Error}", errorMessage);
                        // 验证失败，清理内存中的挑战证书
                        _tlsAlpnChallengeService.CleanupChallenge(domain);

                        if (!string.IsNullOrEmpty(progressId))
                        {
                            await _progressService.AddErrorAsync(progressId, $"TLS-ALPN-01挑战验证失败: {errorMessage}");
                        }

                        return false;
                    }
                }

                _logger.LogWarning("TLS-ALPN-01挑战验证超时");
                // 超时，清理内存中的挑战证书
                _tlsAlpnChallengeService.CleanupChallenge(domain);

                if (!string.IsNullOrEmpty(progressId))
                {
                    await _progressService.AddErrorAsync(progressId, "TLS-ALPN-01挑战验证超时");
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "验证TLS-ALPN-01挑战时发生异常");

                // 确保发生异常时也能清理挑战证书，防止 SNI 一直返回挑战证书
                if (domain != "unknown")
                {
                    _tlsAlpnChallengeService.CleanupChallenge(domain);
                }

                if (!string.IsNullOrEmpty(progressId))
                {
                    await _progressService.AddErrorAsync(progressId, $"TLS-ALPN-01挑战验证异常: {ex.Message}");
                }

                return false;
            }
        }

        /// <summary>
        /// 完成证书申请并下载证书
        /// </summary>
        private async Task<bool> FinalizeCertificateAsync(IOrderContext orderContext, AcmeCertificateOrder order, string? progressId)
        {
            try
            {
                // 生成私钥和 CSR
                _logger.LogInformation("生成 CSR 并提交 Finalize 请求: {Domains}", string.Join(", ", order.Domains));

                var privateKey = KeyFactory.NewKey(KeyAlgorithm.ES256);

                // 🔧 修复：实际调用 Finalize 提交 CSR
                await orderContext.Finalize(new CsrInfo
                {
                    CommonName = order.Domains.FirstOrDefault()
                }, privateKey);

                _logger.LogInformation("CSR 已提交，等待证书颁发");

                // 等待证书颁发
                int maxWaitAttempts = 30; // 最多等待30次，每次2秒，总计1分钟
                int waitAttempts = 0;

                while (waitAttempts < maxWaitAttempts)
                {
                    await Task.Delay(2000);

                    // 检查订单状态
                    var orderDetail = await orderContext.Resource();
                    var orderStatus = orderDetail.Status?.ToString();

                    waitAttempts++;

                    _logger.LogInformation("检查证书颁发状态: {Status} (尝试 {Attempt}/{MaxAttempts})",
                        orderStatus, waitAttempts, maxWaitAttempts);

                    // 🔧 修复：使用不区分大小写的比较（ACME 返回 "Valid" 而不是 "valid"）
                    var statusLower = orderStatus?.ToLowerInvariant() ?? "";

                    if (statusLower == "valid")
                    {
                        // 下载证书
                        _logger.LogInformation("证书已颁发，开始下载");
                        var certificateChain = await orderContext.Download();

                        // 转换为PEM格式
                        // 🔧 修复：Let's Encrypt Staging 的证书链可能有兼容性问题
                        string certificatePem;
                        try
                        {
                            // 先尝试标准方法
                            certificatePem = certificateChain.ToPem();
                        }
                        catch (Exception ex) when (ex.Message.Contains("Can not find issuer"))
                        {
                            // 如果是 Staging 环境的证书链问题，使用原始证书 DER 转 PEM
                            _logger.LogWarning("证书链构建失败，使用原始证书: {Error}", ex.Message);

                            // CertificateChain.Certificate 是 IEncodable，需要调用 ToDer()
                            var certDer = certificateChain.Certificate.ToDer();
                            certificatePem = "-----BEGIN CERTIFICATE-----\r\n" +
                                Convert.ToBase64String(certDer, Base64FormattingOptions.InsertLineBreaks) +
                                "\r\n-----END CERTIFICATE-----";

                            // 尝试添加中间证书（如果可用）
                            try
                            {
                                // 获取中间证书 - Issuers 是 IList<IEncodable>，需要调用 ToDer()
                                var issuers = certificateChain.Issuers;
                                if (issuers != null && issuers.Count > 0)
                                {
                                    foreach (var issuer in issuers)
                                    {
                                        var issuerDer = issuer.ToDer();
                                        certificatePem += "\r\n-----BEGIN CERTIFICATE-----\r\n" +
                                            Convert.ToBase64String(issuerDer, Base64FormattingOptions.InsertLineBreaks) +
                                            "\r\n-----END CERTIFICATE-----";
                                    }
                                }
                            }
                            catch (Exception issuerEx)
                            {
                                _logger.LogWarning("获取中间证书失败: {Error}", issuerEx.Message);
                            }
                        }

                        var privateKeyPem = privateKey.ToPem();

                        // 解析证书以获取详细信息
                        var cert = X509Certificate2.CreateFromPem(certificatePem);

                        // 从证书中读取密钥信息
                        var publicKey = cert.PublicKey;
                        var keyAlgorithm = publicKey.Oid?.FriendlyName ?? "Unknown";
                        int keySize = 0;

                        // 根据密钥类型计算密钥大小
                        if (publicKey.Oid?.Value == "1.2.840.113549.1.1.1") // RSA
                        {
                            keyAlgorithm = "RSA";
                            try
                            {
                                using var rsa = cert.GetRSAPublicKey();
                                keySize = rsa?.KeySize ?? 2048;
                            }
                            catch { keySize = 2048; }
                        }
                        else if (publicKey.Oid?.Value == "1.2.840.10045.2.1") // ECDSA
                        {
                            keyAlgorithm = "ECDSA";
                            try
                            {
                                using var ecdsa = cert.GetECDsaPublicKey();
                                keySize = ecdsa?.KeySize ?? 256;
                            }
                            catch { keySize = 256; }
                        }
                        else
                        {
                            // 其他类型，尝试获取
                            keySize = publicKey.EncodedKeyValue?.RawData?.Length * 8 ?? 0;
                        }

                        // 从订单 metadata 中读取 autoRenew 设置
                        var autoRenew = order.Metadata?.GetValueOrDefault("autoRenew") is bool b && b;

                        // 检查是否是续期（更新原证书而不是创建新证书）
                        var isRenewal = order.Metadata?.GetValueOrDefault("isRenewal") is bool r && r;
                        var originalCertificateId = order.Metadata?.GetValueOrDefault("originalCertificateId")?.ToString();

                        var certificatesCollection = _dbContext.GetCollection<DockerPanel.API.Services.Acme.CertificateRecord>("certificates");

                        DockerPanel.API.Services.Acme.CertificateRecord certificateRecord;
                        DockerPanel.API.Services.Acme.CertificateRecord? certificateRecordVar;

                        if (isRenewal && !string.IsNullOrEmpty(originalCertificateId))
                        {
                            // 续期：更新原证书记录
                            certificateRecordVar = certificatesCollection.FindById(originalCertificateId);
                            
                            // 记录原订单ID，续期完成后删除
                            var oldOrderId = certificateRecordVar?.OrderId;
                            
                            if (certificateRecordVar == null)
                            {
                                _logger.LogWarning("未找到原证书记录 {OriginalId}，创建新证书", originalCertificateId);
                                certificateRecordVar = new DockerPanel.API.Services.Acme.CertificateRecord
                                {
                                    Id = originalCertificateId,
                                    CreatedAt = DateTime.UtcNow
                                };
                            }

                            // 更新证书数据
                            certificateRecordVar.Name = order.Domains.FirstOrDefault() ?? certificateRecordVar.Name;
                            certificateRecordVar.Domains = order.Domains;
                            certificateRecordVar.Status = "Active";
                            certificateRecordVar.IssuedAt = cert.NotBefore.ToUniversalTime();
                            certificateRecordVar.ExpiresAt = cert.NotAfter.ToUniversalTime();
                            certificateRecordVar.Issuer = cert.Issuer ?? "Unknown";
                            certificateRecordVar.CertificateData = certificatePem;
                            certificateRecordVar.PrivateKeyData = privateKeyPem;
                            certificateRecordVar.CertificateChain = certificatePem;
                            certificateRecordVar.KeyAlgorithm = keyAlgorithm;
                            certificateRecordVar.KeySize = keySize;
                            certificateRecordVar.SignatureAlgorithm = cert.SignatureAlgorithm?.FriendlyName ?? "Unknown";
                            certificateRecordVar.SerialNumber = cert.SerialNumber ?? string.Empty;
                            certificateRecordVar.Fingerprint = cert.Thumbprint ?? string.Empty;
                            certificateRecordVar.OrderId = order.Id;
                            certificateRecordVar.AutoRenewalEnabled = autoRenew;
                            certificateRecordVar.Metadata = order.Metadata ?? new Dictionary<string, object>();
                            certificateRecordVar.UpdatedAt = DateTime.UtcNow;

                            certificatesCollection.Update(certificateRecordVar);
                            certificateRecord = certificateRecordVar;
                            _logger.LogInformation("证书续期已更新到数据库: OriginalCertificateId={OriginalId}, OrderId={OrderId}, ExpiresAt={ExpiresAt}",
                                originalCertificateId, order.Id, cert.NotAfter);

                            // 删除原订单（避免重复显示）
                            if (!string.IsNullOrEmpty(oldOrderId) && oldOrderId != order.Id)
                            {
                                try
                                {
                                    var ordersCol = _dbContext.GetCollection<AcmeCertificateOrder>("acme_orders");
                                    ordersCol.Delete(oldOrderId);
                                    _logger.LogInformation("续期已删除旧订单: OldOrderId={OldOrderId}", oldOrderId);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "删除旧订单失败: OldOrderId={OldOrderId}", oldOrderId);
                                }
                            }
                        }
                        else
                        {
                            // 新申请：创建新证书记录
                            certificateRecord = new DockerPanel.API.Services.Acme.CertificateRecord
                            {
                                Id = ObjectId.NewObjectId().ToString(),
                                Name = order.Domains.FirstOrDefault() ?? "Unknown",
                                Type = DetermineCertificateType(order.Domains),
                                Domains = order.Domains,
                                Status = "Active",
                                IssuedAt = cert.NotBefore.ToUniversalTime(),
                                ExpiresAt = cert.NotAfter.ToUniversalTime(),
                                Issuer = cert.Issuer ?? "Unknown",
                                CertificateData = certificatePem,
                                PrivateKeyData = privateKeyPem,
                                CertificateChain = certificatePem,
                                KeyAlgorithm = keyAlgorithm,
                                KeySize = keySize,
                                SignatureAlgorithm = cert.SignatureAlgorithm?.FriendlyName ?? "Unknown",
                                SerialNumber = cert.SerialNumber ?? string.Empty,
                                Fingerprint = cert.Thumbprint ?? string.Empty,
                                AccountId = order.AccountId,
                                OrderId = order.Id,
                                AutoRenewalEnabled = autoRenew,
                                Metadata = order.Metadata ?? new Dictionary<string, object>(),
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            };

                            certificatesCollection.Insert(certificateRecord);
                            _logger.LogInformation("证书已保存到数据库: OrderId={OrderId}, CertificateId={CertificateId}, KeyAlgorithm={KeyAlgorithm}, KeySize={KeySize}, ExpiresAt={ExpiresAt}, AutoRenew={AutoRenew}",
                                order.Id, certificateRecord.Id, keyAlgorithm, keySize, cert.NotAfter, autoRenew);
                        }

                        // 清除 SNI 证书缓存，使新证书立即生效
                        foreach (var domain in order.Domains)
                        {
                            _sniCertificateSelector.ClearCache(domain);
                        }
                        _logger.LogInformation("已清除 SNI 证书缓存，新证书立即生效: Domains={Domains}", string.Join(", ", order.Domains));

                        // 更新订单状态
                        order.Status = "valid";
                        order.CompletedAt = DateTime.UtcNow;
                        order.CertificateId = certificateRecord.Id;

                        if (!string.IsNullOrEmpty(progressId))
                        {
                            await _progressService.CompleteCurrentStepAsync(progressId, "证书下载并保存成功");
                            await _progressService.MarkAsCompletedAsync(progressId);
                        }

                        return true;
                    }
                    else if (statusLower == "invalid")
                    {
                        var errorMessage = orderDetail.Error?.ToString() ?? "证书申请失败";
                        _logger.LogError("证书申请失败: {Error}", errorMessage);

                        if (!string.IsNullOrEmpty(progressId))
                        {
                            await _progressService.AddErrorAsync(progressId, $"证书申请失败: {errorMessage}");
                        }

                        return false;
                    }
                }

                _logger.LogWarning("证书申请超时");

                if (!string.IsNullOrEmpty(progressId))
                {
                    await _progressService.AddErrorAsync(progressId, "证书申请超时");
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "完成证书申请时发生异常");

                if (!string.IsNullOrEmpty(progressId))
                {
                    await _progressService.AddErrorAsync(progressId, $"完成证书申请异常: {ex.Message}");
                }

                return false;
            }
        }

        #endregion

        #region Challenge Handling

        public async Task<IEnumerable<AcmeChallenge>> GetPendingChallengesAsync(string orderId)
        {
            try
            {
                var order = await GetCertificateOrderAsync(orderId);
                if (order?.Authorizations == null)
                {
                    return Enumerable.Empty<AcmeChallenge>();
                }

                return order.Authorizations
                    .SelectMany(a => a.Challenges)
                    .Where(c => string.IsNullOrEmpty(c.Status) ||
                                c.Status.Equals("pending", StringComparison.OrdinalIgnoreCase) ||
                                c.Status.Equals("processing", StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取待处理挑战失败: {OrderId}", orderId);
                throw;
            }
        }

        public async Task<AcmeChallengeResult> CompleteChallengeAsync(string orderId, string authorizationId, CompleteChallengeRequest request)
        {
            try
            {
                _logger.LogInformation("开始完成挑战验证: OrderId={OrderId}, AuthorizationId={AuthorizationId}", orderId, authorizationId);

                // 从数据库获取订单信息
                var order = await GetCertificateOrderAsync(orderId);
                if (order == null)
                {
                    return new AcmeChallengeResult
                    {
                        Success = false,
                        Message = "未找到订单信息",
                        ErrorType = "order_not_found"
                    };
                }

                // 获取 ACME 上下文 - 使用正确的账户密钥
                var directoryUrl = "https://acme-v02.api.letsencrypt.org/directory";
                IAcmeContext? acmeContextFinal;

                if (!TryGetCachedAcmeContext(order.AccountId, out acmeContextFinal))
                {
                    // 从数据库获取账户信息
                    var accountsCollection = _dbContext.GetCollection<AcmeAccount>("acme_accounts");
                    var account = accountsCollection.FindById(order.AccountId);

                    if (account != null && !string.IsNullOrEmpty(account.AccountKey))
                    {
                        _logger.LogInformation("使用数据库中的账户密钥创建ACME上下文: {AccountId}", order.AccountId);
                        var privateKey = Certes.KeyFactory.FromPem(account.AccountKey);
                        acmeContextFinal = new AcmeContext(new Uri(directoryUrl), privateKey);
                        CacheAcmeContext(order.AccountId, account.Provider ?? "letsencrypt", acmeContextFinal);
                    }
                    else
                    {
                        _logger.LogWarning("未找到账户密钥，创建新的ACME上下文（可能无法工作）");
                        acmeContextFinal = new AcmeContext(new Uri(directoryUrl));
                        CacheAcmeContext(order.AccountId, "letsencrypt", acmeContextFinal);
                    }
                }

                if (acmeContextFinal == null)
                {
                    return new AcmeChallengeResult
                    {
                        Success = false,
                        Message = "无法获取ACME上下文",
                        ErrorType = "acme_context_error"
                    };
                }

                // 获取订单上下文
                var orderContext = acmeContextFinal.Order(new Uri(order.OrderUri));
                if (orderContext == null)
                {
                    return new AcmeChallengeResult
                    {
                        Success = false,
                        Message = "未找到订单上下文",
                        ErrorType = "order_context_error"
                    };
                }

                // 获取授权列表和目标域名
                var authorizations = await orderContext.Authorizations();

                var orderAuthorizationRecord = order.Authorizations
                    ?.FirstOrDefault(a =>
                        a.Id == authorizationId ||
                        (!string.IsNullOrEmpty(a.Domain) &&
                         a.Domain.Equals(authorizationId, StringComparison.OrdinalIgnoreCase)));

                var targetDomain = orderAuthorizationRecord?.Domain ?? order.Domains.FirstOrDefault(); // 从订单获取目标域名
                var targetAuthorization = await ResolveAuthorizationContextAsync(authorizations, authorizationId, targetDomain);

                if (targetAuthorization == null)
                {
                    _logger.LogWarning("未找到域名 {Domain} 的授权信息", targetDomain);
                    return new AcmeChallengeResult
                    {
                        Success = false,
                        Message = "未找到授权信息",
                        ErrorType = "authorization_not_found",
                        ErrorDetails = $"域名: {targetDomain}, 授权数量: {authorizations?.Count() ?? 0}"
                    };
                }

                // 获取挑战列表
                var challenges = await targetAuthorization.Challenges();
                var targetChallenge = challenges.FirstOrDefault(c => c.Type == request.ChallengeType);

                if (targetChallenge == null)
                {
                    return new AcmeChallengeResult
                    {
                        Success = false,
                        Message = $"未找到 {request.ChallengeType} 类型的挑战",
                        ErrorType = "challenge_not_found"
                    };
                }

                // 等待挑战验证完成
                _logger.LogInformation("等待挑战验证完成: ChallengeType={ChallengeType}, Token={Token}",
                    targetChallenge.Type, targetChallenge.Token);

                var challengeResult = await targetChallenge.Validate();

                var statusString = challengeResult.Status?.ToString()?.ToLowerInvariant();
                _logger.LogInformation("挑战验证结果: ChallengeType={ChallengeType}, Status={Status}", targetChallenge.Type, statusString);

                if (statusString == "valid")
                {
                    _logger.LogInformation("挑战验证成功: ChallengeType={ChallengeType}", targetChallenge.Type);

                    return new AcmeChallengeResult
                    {
                        Success = true,
                        Message = "挑战验证成功",
                        ChallengeType = targetChallenge.Type,
                        Status = "valid",
                        Token = targetChallenge.Token,
                        ValidationUrl = targetChallenge.Location?.ToString(),
                        ValidatedAt = DateTime.UtcNow
                    };
                }
                else if (statusString == "pending")
                {
                    _logger.LogWarning("挑战验证仍在处理中: ChallengeType={ChallengeType}", targetChallenge.Type);

                    return new AcmeChallengeResult
                    {
                        Success = false,
                        Message = "挑战验证仍在处理中",
                        ChallengeType = targetChallenge.Type,
                        Status = "pending",
                        Token = targetChallenge.Token,
                        ErrorType = "challenge_pending"
                    };
                }
                else
                {
                    _logger.LogError("挑战验证失败: ChallengeType={ChallengeType}, Status={Status}, Error={Error}",
                        targetChallenge.Type, statusString, challengeResult.Error?.ToString());

                    return new AcmeChallengeResult
                    {
                        Success = false,
                        Message = $"挑战验证失败: {challengeResult.Error}",
                        ChallengeType = targetChallenge.Type,
                        Status = statusString ?? "unknown",
                        Token = targetChallenge.Token,
                        ErrorType = "challenge_failed",
                        ErrorDetails = challengeResult.Error?.ToString()
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "完成挑战验证失败: OrderId={OrderId}, AuthorizationId={AuthorizationId}", orderId, authorizationId);

                return new AcmeChallengeResult
                {
                    Success = false,
                    Message = $"挑战验证异常: {ex.Message}",
                    ErrorType = "exception",
                    ErrorDetails = ex.ToString()
                };
            }
        }

        /// <summary>
        /// 预检查HTTP-01挑战文件是否正确存储并可访问
        /// </summary>
        private async Task<bool> PreCheckHttpChallengeAsync(IChallengeContext httpChallenge, string domain, string? progressId)
        {
            try
            {
                var token = httpChallenge.Token;
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogError("HTTP-01挑战Token为空");
                    return false;
                }

                _logger.LogInformation("开始预检查HTTP-01挑战文件: Token={Token}, Domain={Domain}", token, domain);

                // 1. 检查挑战文件是否已存储到持久化存储中
                var storedKeyAuthorization = await _challengeStore.GetHttpChallengeAsync(token);
                if (string.IsNullOrEmpty(storedKeyAuthorization))
                {
                    _logger.LogError("挑战文件未在存储中找到: Token={Token}", token);
                    if (!string.IsNullOrEmpty(progressId))
                    {
                        await _progressService.AddErrorAsync(progressId, $"挑战文件未存储: Token={token}");
                    }
                    return false;
                }

                _logger.LogInformation("挑战文件已在存储中找到: Token={Token}", token);

                // 2. 重新计算并验证KeyAuthorization是否正确
                var expectedKeyAuthorization = httpChallenge.KeyAuthz;
                if (storedKeyAuthorization != expectedKeyAuthorization)
                {
                    _logger.LogError("挑战文件内容不匹配: Token={Token}, 期望={Expected}, 实际={Actual}",
                        token, expectedKeyAuthorization, storedKeyAuthorization);
                    if (!string.IsNullOrEmpty(progressId))
                    {
                        await _progressService.AddErrorAsync(progressId, $"挑战文件内容不匹配: Token={token}");
                    }
                    return false;
                }

                _logger.LogInformation("挑战文件内容验证通过: Token={Token}", token);

                // 3. 本地访问测试 - 确认服务本身能正确响应
                try
                {
                    using var httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(10);

                    // 使用 localhost 测试服务本身（避免 NAT 回环问题）
                    var localTestUrl = $"http://localhost/.well-known/acme-challenge/{token}";
                    _logger.LogInformation("本地预检查URL: {Url}", localTestUrl);

                    var response = await httpClient.GetAsync(localTestUrl);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError("本地挑战端点访问失败: Token={Token}, 状态码={StatusCode}",
                            token, response.StatusCode);
                        if (!string.IsNullOrEmpty(progressId))
                        {
                            await _progressService.AddErrorAsync(progressId,
                                $"本地挑战端点无法访问，状态码: {response.StatusCode}");
                        }
                        return false;
                    }

                    var responseContent = await response.Content.ReadAsStringAsync();
                    if (responseContent != expectedKeyAuthorization)
                    {
                        _logger.LogError("本地挑战端点返回内容不匹配: Token={Token}, 期望={Expected}, 实际={Actual}",
                            token, expectedKeyAuthorization, responseContent);
                        if (!string.IsNullOrEmpty(progressId))
                        {
                            await _progressService.AddErrorAsync(progressId,
                                $"本地挑战端点内容不匹配");
                        }
                        return false;
                    }

                    _logger.LogInformation("本地挑战端点验证通过: Token={Token}", token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "本地挑战端点测试异常: Token={Token}", token);
                    if (!string.IsNullOrEmpty(progressId))
                    {
                        await _progressService.AddErrorAsync(progressId,
                            $"本地挑战端点测试异常: {ex.Message}");
                    }
                    return false;
                }

                // 4. 域名访问测试（可选，仅警告，不影响流程 - 因为可能存在 NAT 回环问题）
                try
                {
                    using var httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(5);

                    var domainTestUrl = $"http://{domain}/.well-known/acme-challenge/{token}";
                    _logger.LogInformation("域名预检查URL: {Url}", domainTestUrl);

                    var response = await httpClient.GetAsync(domainTestUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        if (responseContent == expectedKeyAuthorization)
                        {
                            _logger.LogInformation("域名挑战端点验证通过: Token={Token}, Domain={Domain}", token, domain);
                        }
                        else
                        {
                            _logger.LogWarning("域名挑战端点内容不匹配（可能 Let's Encrypt 会验证失败）: Domain={Domain}", domain);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("域名挑战端点访问失败（可能是 NAT 回环问题）: Domain={Domain}, 状态码={StatusCode}", domain, response.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    // 域名测试失败不影响流程，只是警告
                    _logger.LogWarning(ex, "域名挑战端点测试失败（可能是 NAT 回环问题，不影响验证）: Domain={Domain}", domain);
                }

                // 5. 等待一小段时间确保挑战文件已完全写入并同步
                await Task.Delay(TimeSpan.FromSeconds(2));

                _logger.LogInformation("HTTP-01挑战文件预检查通过: Token={Token}, Domain={Domain}", token, domain);
                if (!string.IsNullOrEmpty(progressId))
                {
                    await _progressService.CompleteCurrentStepAsync(progressId,
                        $"挑战文件预检查通过: {domain}");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HTTP-01挑战文件预检查异常: Token={Token}", httpChallenge.Token);
                if (!string.IsNullOrEmpty(progressId))
                {
                    await _progressService.AddErrorAsync(progressId,
                        $"挑战文件预检查异常: Token={httpChallenge.Token}, 错误={ex.Message}");
                }
                return false;
            }
        }

        private async Task<List<AcmeChallenge>> GetChallengesAsync(IAuthorizationContext authContext, string domain, AcmeCertificateOrder order)
        {
            var challenges = new List<AcmeChallenge>();

            try
            {
                _logger.LogInformation("获取域名 {Domain} 的挑战信息", domain);

                var challengeContexts = await authContext.Challenges();

                foreach (var challengeContext in challengeContexts)
                {
                    var challengeType = challengeContext.Type;
                    var token = challengeContext.Token;

                    // 获取账户密钥用于生成 Key Authorization
                    // 使用正确的 ACME 协议生成 Key Authorization
                    var keyAuthorization = challengeContext.KeyAuthz;

                    _logger.LogInformation("挑战信息: Type={Type}, Token={Token}, KeyAuthz={KeyAuthz}",
                        challengeType, token, keyAuthorization);

                    if (challengeType == "http-01")
                    {
                        // 存储 HTTP-01 挑战数据到挑战存储中，供 YARP 转发使用
                        await _challengeStore.StoreHttpChallengeAsync(token, keyAuthorization, DateTime.UtcNow.AddHours(1));

                        challenges.Add(new AcmeChallenge
                        {
                            Type = "http-01",
                            Status = "pending",
                            Url = $"http://{domain}/.well-known/acme-challenge/{token}",
                            Token = token,
                            KeyAuthorization = keyAuthorization,
                            ChallengeData = new Dictionary<string, object>
                            {
                                ["Type"] = "http-01",
                                ["Token"] = token,
                                ["KeyAuthorization"] = keyAuthorization,
                                ["FilePath"] = $"/.well-known/acme-challenge/{token}",
                                ["ExpectedUrl"] = $"http://{domain}/.well-known/acme-challenge/{token}"
                            }
                        });

                        _logger.LogInformation("已存储 HTTP-01 挑战数据: Token={Token}, Domain={Domain}", token, domain);
                    }
                    else if (challengeType == "dns-01")
                    {
                        var recordName = $"_acme-challenge.{domain}";

                        // 🔧 修复：DNS-01 的 TXT 记录值必须是 keyAuthorization 的 SHA256 哈希的 base64url 编码
                        // 根据 ACME RFC-8555 规范，DNS-01 验证使用 base64url(SHA256(keyAuthorization))
                        using var sha256 = System.Security.Cryptography.SHA256.Create();
                        var hash = sha256.ComputeHash(System.Text.Encoding.ASCII.GetBytes(keyAuthorization));
                        var dnsRecordValue = Convert.ToBase64String(hash)
                            .TrimEnd('=')        // 移除填充
                            .Replace('+', '-')   // 替换为 URL 安全字符
                            .Replace('/', '_');  // 替换为 URL 安全字符

                        // 存储 DNS-01 挑战数据到挑战存储中 - 使用正确的 TXT 记录值
                        await _challengeStore.StoreDnsChallengeAsync(domain, recordName, dnsRecordValue, DateTime.UtcNow.AddHours(1));

                        challenges.Add(new AcmeChallenge
                        {
                            Type = "dns-01",
                            Status = "pending",
                            Url = challengeContext.Location?.ToString() ?? "",
                            Token = token,
                            KeyAuthorization = keyAuthorization,
                            ChallengeData = new Dictionary<string, object>
                            {
                                ["Type"] = "dns-01",
                                ["Token"] = token,
                                ["KeyAuthorization"] = keyAuthorization,
                                ["RecordName"] = recordName,
                                ["RecordValue"] = dnsRecordValue, // 🔧 使用正确的哈希值
                                ["RecordType"] = "TXT"
                            }
                        });

                        _logger.LogInformation("已存储 DNS-01 挑战数据: Token={Token}, Domain={Domain}, RecordName={RecordName}, RecordValue={RecordValue}",
                            token, domain, recordName, dnsRecordValue);
                    }
                    else if (challengeType == "tls-alpn-01")
                    {
                        // TLS-ALPN-01 挑战 - 需要在 443 端口提供特殊证书
                        // 直接在内存中生成并缓存验证证书，无需持久化
                        if (_tlsAlpnChallengeService != null)
                        {
                            try
                            {
                                _tlsAlpnChallengeService.PrepareChallengeCertificate(domain, keyAuthorization);
                                _logger.LogInformation("已生成 TLS-ALPN-01 验证证书: Domain={Domain}", domain);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "生成 TLS-ALPN-01 证书失败: Domain={Domain}", domain);
                            }
                        }

                        challenges.Add(new AcmeChallenge
                        {
                            Type = "tls-alpn-01",
                            Status = "pending",
                            Url = challengeContext.Location?.ToString() ?? "",
                            Token = token,
                            KeyAuthorization = keyAuthorization,
                            ChallengeData = new Dictionary<string, object>
                            {
                                ["Type"] = "tls-alpn-01",
                                ["Token"] = token,
                                ["KeyAuthorization"] = keyAuthorization,
                                ["Port"] = 443,
                                ["AlpnProtocol"] = "acme-tls/1"
                            }
                        });

                        _logger.LogInformation("已准备 TLS-ALPN-01 挑战: Token={Token}, Domain={Domain}", token, domain);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取挑战失败: Domain={Domain}", domain);
                throw;
            }

            return challenges;
        }

        #endregion

        #region Certificate Download and Management

        public async Task<AcmeCertificateData> DownloadCertificateAsync(string orderId)
        {
            try
            {
                _logger.LogInformation("开始下载证书: OrderId={OrderId}", orderId);

                // 从数据库获取订单信息
                var order = await GetCertificateOrderAsync(orderId);
                if (order == null)
                {
                    throw new KeyNotFoundException($"未找到订单: {orderId}");
                }

                // 获取 ACME 上下文
                var acmeContext = await GetAcmeContextAsync(order.AccountId);
                if (acmeContext == null)
                {
                    throw new InvalidOperationException($"无法获取 ACME 上下文");
                }

                // 获取订单上下文
                var orderContext = acmeContext.Order(new Uri(order.OrderUri));
                if (orderContext == null)
                {
                    throw new InvalidOperationException($"未找到订单上下文");
                }

                // 检查订单状态
                var orderDetails = await orderContext.Resource();
                _logger.LogInformation("订单状态: {Status}, 域名: {Domains}", orderDetails.Status, string.Join(",", order.Domains));

                if (orderDetails.Status?.ToString() != "Ready" && orderDetails.Status?.ToString() != "Valid")
                {
                    throw new InvalidOperationException($"订单尚未准备就绪，当前状态: {orderDetails.Status}");
                }

                // 声明变量
                CertificateChain? certificateChain = null;
                string certificatePem;
                string privateKeyPem;

                if (orderDetails.Status?.ToString() == "Valid")
                {
                    // 订单已完成，直接下载证书
                    _logger.LogInformation("订单已完成，直接下载证书...");
                    certificateChain = await orderContext.Download();
                    certificatePem = certificateChain.ToPem();

                    var certificatesCollection = _dbContext.GetCollection<DockerPanel.API.Services.Acme.CertificateRecord>("certificates");
                    var existingCertificate = certificatesCollection.FindAll()
                        .FirstOrDefault(c => c.OrderId == order.Id && !string.IsNullOrWhiteSpace(c.PrivateKeyData));

                    if (existingCertificate == null)
                    {
                        throw new InvalidOperationException("订单已完成，但未找到与该订单匹配的私钥记录。ACME 证书私钥无法从服务端重新生成，请使用已保存的证书记录导出，或重新申请证书。");
                    }

                    privateKeyPem = existingCertificate.PrivateKeyData!;
                }
                else
                {
                    // 订单准备就绪，需要完成订单
                    _logger.LogInformation("开始完成订单并下载证书...");
                    var privateKey = KeyFactory.NewKey(KeyAlgorithm.ES256);

                    // 完成订单并获取证书
                    certificateChain = await orderContext.Generate(new CsrInfo
                    {
                        CountryName = "CN",
                        State = "Beijing",
                        Locality = "Beijing",
                        Organization = "DockerPanel",
                        OrganizationUnit = "SSL",
                        CommonName = order.Domains.First()
                    }, privateKey);

                    // 转换为PEM格式
                    certificatePem = certificateChain.ToPem();
                    privateKeyPem = privateKey.ToPem();
                }

                // 从PEM中解析证书以获取真实的过期时间和其他信息
                var cert = X509Certificate2.CreateFromPem(certificatePem);

                var certData = new AcmeCertificateData
                {
                    Certificate = certificatePem,
                    CertificateChain = certificatePem, // certificateChain.ToPem() 已包含完整链
                    PrivateKey = privateKeyPem,
                    CertificateFingerprint = cert.Thumbprint,
                    SerialNumber = cert.SerialNumber,
                    IssuedAt = cert.NotBefore.ToUniversalTime(),
                    ExpiresAt = cert.NotAfter.ToUniversalTime(),
                    Domains = order.Domains,
                    Issuer = cert.Issuer,
                    Subject = cert.Subject
                };

                _logger.LogInformation("证书下载成功: OrderId={OrderId}, Subject={Subject}, IssuedAt={IssuedAt}, ExpiresAt={ExpiresAt}",
                    orderId, certData.Subject, certData.IssuedAt, certData.ExpiresAt);

                return certData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "下载证书失败: {OrderId}", orderId);
                throw;
            }
        }

        public async Task<AcmeCertificateOrder> RenewCertificateAsync(string certificateId)
        {
            try
            {
                _logger.LogInformation("开始续期证书: {CertificateId}", certificateId);

                // 从数据库获取原证书信息（先尝试 acme_orders，再尝试 certificates）
                var ordersCollection = _dbContext.GetCollection<AcmeCertificateOrder>("acme_orders");
                var existingOrder = ordersCollection.FindById(certificateId);

                // 如果在 acme_orders 中没找到，尝试从 certificates 集合获取
                if (existingOrder == null)
                {
                    var certificatesCollection = _dbContext.GetCollection<CertificateRecord>("certificates");
                    var certificateRecord = certificatesCollection.FindById(certificateId);

                    if (certificateRecord == null)
                    {
                        throw new KeyNotFoundException($"未找到证书: {certificateId}");
                    }

                    // 转换为 AcmeCertificateOrder
                    existingOrder = ConvertCertificateRecordToOrder(certificateRecord);
                }

                if (existingOrder == null)
                {
                    throw new KeyNotFoundException($"未找到证书: {certificateId}");
                }

                _logger.LogInformation("找到原证书: Domains={Domains}, ChallengeType={ChallengeType}",
                    string.Join(",", existingOrder.Domains), existingOrder.Metadata?.GetValueOrDefault("challengeType"));

                // 创建续期请求，标记为续期以更新原证书
                var renewalRequest = new AcmeCertificateRequest
                {
                    AccountId = existingOrder.AccountId,
                    AccountKey = existingOrder.Metadata?.GetValueOrDefault("accountKey")?.ToString(),
                    Domains = existingOrder.Domains,
                    Metadata = new Dictionary<string, object>(existingOrder.Metadata ?? new Dictionary<string, object>())
                    {
                        ["isRenewal"] = true,
                        ["originalCertificateId"] = certificateId,  // 使用原证书ID
                        ["renewedAt"] = DateTime.UtcNow
                    }
                };

                // 调用证书申请方法，这会自动处理 DNS-01 验证
                var newOrder = await OrderCertificateAsync(renewalRequest);

                if (newOrder == null)
                {
                    throw new InvalidOperationException("证书续期失败：无法创建新订单");
                }

                _logger.LogInformation("证书续期成功: OriginalCertificateId={OriginalId}, NewOrderId={NewOrderId}",
                    certificateId, newOrder.Id);

                // 返回更新后的原订单信息（保持原ID）
                existingOrder.Status = "valid";
                existingOrder.CompletedAt = DateTime.UtcNow;
                existingOrder.ExpiresAt = newOrder.ExpiresAt;
                existingOrder.CertificateId = certificateId;
                existingOrder.Metadata ??= new Dictionary<string, object>();
                existingOrder.Metadata["lastRenewedAt"] = DateTime.UtcNow;
                existingOrder.Metadata["renewalOrderId"] = newOrder.Id;

                return existingOrder;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "续期证书失败: {CertificateId}", certificateId);
                throw;
            }
        }

        /// <summary>
        /// 重试失败的证书申请
        /// </summary>
        public async Task<AcmeCertificateOrder> RetryCertificateOrderAsync(string certificateId)
        {
            try
            {
                _logger.LogInformation("开始重试证书申请: {CertificateId}", certificateId);

                // 从数据库获取失败的证书订单
                var ordersCollection = _dbContext.GetCollection<AcmeCertificateOrder>("acme_orders");
                var failedOrder = ordersCollection.FindById(certificateId);

                if (failedOrder == null)
                {
                    throw new KeyNotFoundException($"未找到证书订单: {certificateId}");
                }

                if (failedOrder.Status != "failed" && failedOrder.Status != "pending")
                {
                    throw new InvalidOperationException($"只能重试失败或待处理的证书申请，当前状态: {failedOrder.Status}");
                }

                _logger.LogInformation("重试失败的证书申请: Domains={Domains}, Account={AccountId}",
                    string.Join(",", failedOrder.Domains), failedOrder.AccountId);

                // 创建重试请求，保持原有的DNS提供商配置
                var retryRequest = new AcmeCertificateRequest
                {
                    AccountId = failedOrder.AccountId,
                    AccountKey = failedOrder.Metadata?.GetValueOrDefault("accountKey")?.ToString(),
                    Domains = failedOrder.Domains,
                    Metadata = new Dictionary<string, object>(failedOrder.Metadata ?? new Dictionary<string, object>())
                    {
                        ["isRetry"] = true,
                        ["originalOrderId"] = certificateId,
                        ["retriedAt"] = DateTime.UtcNow
                    }
                };

                // 清理之前的DNS记录（如果存在）
                try
                {
                    var dnsProvider = failedOrder.Metadata?.GetValueOrDefault("dnsProvider")?.ToString();
                    var dnsCredentials = new Dictionary<string, object>();

                    // 提取DNS凭据
                    if (failedOrder.Metadata?.GetValueOrDefault("dnsCredentials") is Dictionary<string, object> credentials)
                    {
                        dnsCredentials = credentials;
                    }

                    if (!string.IsNullOrEmpty(dnsProvider) && dnsCredentials.Count > 0)
                    {
                        _logger.LogInformation("清理之前的DNS记录: DNS Provider={DnsProvider}", dnsProvider);
                        await CleanupDnsRecordsAsync(failedOrder, dnsProvider, dnsCredentials, certificateId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "清理DNS记录失败，但继续重试流程");
                }

                // 重新触发证书申请流程
                var retryOrder = await OrderCertificateAsync(retryRequest);

                if (retryOrder == null)
                {
                    throw new InvalidOperationException("证书重试失败：无法创建新订单");
                }

                // 更新重试信息
                retryOrder.Metadata["originalOrderId"] = certificateId;
                retryOrder.Metadata["isRetry"] = true;
                retryOrder.Metadata["retriedAt"] = DateTime.UtcNow;

                _logger.LogInformation("证书重试成功: NewOrderId={NewId}, OriginalOrderId={OriginalId}",
                    retryOrder.Id, certificateId);

                return retryOrder;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重试证书申请失败: {CertificateId}", certificateId);
                throw;
            }
        }

        public async Task<bool> RevokeCertificateAsync(string certificateId, RevokeCertificateRequest request)
        {
            try
            {
                _logger.LogInformation("开始撤销证书: {CertificateId}, Reason={Reason}", certificateId, request.Reason);

                var certificatesCollection = _dbContext.GetCollection<CertificateRecord>("certificates");
                var ordersCollection = _dbContext.GetCollection<AcmeCertificateOrder>("acme_orders");

                var certificate = certificatesCollection.FindById(certificateId);
                if (certificate == null)
                {
                    foreach (var candidate in certificatesCollection.FindAll())
                    {
                        if (candidate.CertificateId == certificateId ||
                            candidate.OrderId == certificateId ||
                            candidate.Fingerprint == certificateId)
                        {
                            certificate = candidate;
                            break;
                        }
                    }
                }
                string? relatedOrderId = null;
                if (certificate != null)
                {
                    relatedOrderId = certificate.OrderId;
                }
                var order = ordersCollection.FindById(certificateId);
                if (order == null && !string.IsNullOrWhiteSpace(relatedOrderId))
                {
                    order = ordersCollection.FindById(relatedOrderId);
                }
                if (order == null)
                {
                    order = ordersCollection.FindAll()
                        .FirstOrDefault(o => o.CertificateId == certificateId);
                }

                if (certificate == null && order == null)
                {
                    _logger.LogWarning("撤销证书失败，未找到证书或订单: {CertificateId}", certificateId);
                    return false;
                }

                var shouldCallAcme = certificate != null &&
                                     !string.IsNullOrWhiteSpace(certificate.CertificateData) &&
                                     !string.IsNullOrWhiteSpace(certificate.AccountId);

                if (shouldCallAcme)
                {
                    var acmeContext = await GetAcmeContextAsync(certificate!.AccountId);
                    if (acmeContext is not AcmeContext concreteAcmeContext)
                    {
                        throw new InvalidOperationException("无法获取可用于撤销证书的 ACME 上下文");
                    }

                    using var x509 = X509Certificate2.CreateFromPem(certificate.CertificateData);
                    var certPrivateKey = !string.IsNullOrWhiteSpace(certificate.PrivateKeyData)
                        ? KeyFactory.FromPem(certificate.PrivateKeyData)
                        : null;
                    var reason = Enum.IsDefined(typeof(RevocationReason), request.Reason)
                        ? (RevocationReason)request.Reason
                        : RevocationReason.Unspecified;

                    await concreteAcmeContext.RevokeCertificate(x509.RawData, reason, certPrivateKey);
                    _logger.LogInformation("ACME 证书撤销请求已提交: {CertificateId}", certificate.Id);
                }
                else
                {
                    _logger.LogWarning("证书 {CertificateId} 缺少 ACME 账户或证书数据，仅更新本地撤销状态", certificateId);
                }

                var revokedAt = DateTime.UtcNow;
                var reasonText = request.Reason.ToString();

                if (certificate != null)
                {
                    certificate.Status = "revoked";
                    certificate.RevokedAt = revokedAt;
                    certificate.RevocationReason = reasonText;
                    certificate.UpdatedAt = revokedAt;
                    certificate.Metadata["RevokedAt"] = revokedAt;
                    certificate.Metadata["RevocationReason"] = request.Reason;
                    certificate.Metadata["LocalRevocationOnly"] = !shouldCallAcme;
                    certificatesCollection.Update(certificate);
                }

                if (order != null)
                {
                    order.Status = "revoked";
                    order.Metadata["RevokedAt"] = revokedAt;
                    order.Metadata["RevocationReason"] = request.Reason;
                    ordersCollection.Update(order);
                }

                var operationAccountId = certificate != null ? certificate.AccountId : order?.AccountId ?? string.Empty;
                var operationCertificateId = certificate != null ? certificate.Id : certificateId;
                var operationOrderId = order != null ? order.Id : null;
                await AddOperationLogAsync("revoke", operationAccountId, operationCertificateId, operationOrderId, true, "证书撤销成功");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "撤销证书失败: {CertificateId}", certificateId);
                await AddOperationLogAsync("revoke", string.Empty, certificateId, null, false, ex.Message);
                throw;
            }
        }

        #endregion

        #region Utilities and Helper Methods

        public async Task<int> CheckCertificateExpiryAsync(string certificateId)
        {
            var certificate = FindCertificateRecord(certificateId);
            if (certificate != null)
            {
                return (int)Math.Floor((certificate.ExpiresAt.ToUniversalTime() - DateTime.UtcNow).TotalDays);
            }

            var ordersCollection = _dbContext.GetCollection<AcmeCertificateOrder>("acme_orders");
            var order = ordersCollection.FindById(certificateId) ?? ordersCollection.FindOne(o => o.CertificateId == certificateId);
            if (order?.ExpiresAt != null)
            {
                return (int)Math.Floor((order.ExpiresAt.Value.ToUniversalTime() - DateTime.UtcNow).TotalDays);
            }

            throw new KeyNotFoundException($"未找到证书: {certificateId}");
        }

        public async Task<int> AutoRenewCertificatesAsync(int daysBeforeExpiry = 15)
        {
            if (daysBeforeExpiry < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(daysBeforeExpiry), "自动续期提前天数必须大于 0");
            }

            var threshold = DateTime.UtcNow.AddDays(daysBeforeExpiry);
            var certificatesCollection = _dbContext.GetCollection<CertificateRecord>("certificates");
            var candidates = certificatesCollection.Find(c =>
                    c.AutoRenewalEnabled &&
                    !string.Equals(c.Status, "revoked", StringComparison.OrdinalIgnoreCase) &&
                    c.ExpiresAt <= threshold)
                .ToList();

            var renewedCount = 0;
            foreach (var certificate in candidates)
            {
                try
                {
                    await RenewCertificateAsync(certificate.Id);
                    renewedCount++;
                    await AddOperationLogAsync("auto-renew", certificate.AccountId, certificate.Id, certificate.OrderId, true, "已提交自动续期任务");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "自动续期证书失败: {CertificateId}", certificate.Id);
                    await AddOperationLogAsync("auto-renew", certificate.AccountId, certificate.Id, certificate.OrderId, false, ex.Message);
                }
            }

            return renewedCount;
        }

        public async Task<AcmeChallengeResult> VerifyDomainOwnershipAsync(string domain, string challengeType)
        {
            if (string.IsNullOrWhiteSpace(domain))
            {
                throw new ArgumentException("域名不能为空", nameof(domain));
            }

            var normalizedType = string.IsNullOrWhiteSpace(challengeType)
                ? "http-01"
                : challengeType.Trim().ToLowerInvariant();

            try
            {
                if (normalizedType == "dns-01")
                {
                    var recordName = $"_acme-challenge.{domain.Trim().TrimEnd('.')}";
                    var lookup = new LookupClient();
                    var response = await lookup.QueryAsync(recordName, QueryType.TXT);
                    var records = response.Answers.TxtRecords().SelectMany(r => r.Text).ToList();

                    return new AcmeChallengeResult
                    {
                        Success = records.Count > 0,
                        Message = records.Count > 0 ? "DNS-01 TXT 记录存在" : "未找到 DNS-01 TXT 记录",
                        ChallengeType = normalizedType,
                        Status = records.Count > 0 ? "valid" : "pending",
                        ValidatedAt = DateTime.UtcNow,
                        Details = new Dictionary<string, object>
                        {
                            ["recordName"] = recordName,
                            ["recordCount"] = records.Count
                        }
                    };
                }

                if (normalizedType == "tls-alpn-01")
                {
                    using var tcpClient = new System.Net.Sockets.TcpClient();
                    await tcpClient.ConnectAsync(domain, 443).WaitAsync(TimeSpan.FromSeconds(5));
                    return new AcmeChallengeResult
                    {
                        Success = tcpClient.Connected,
                        Message = tcpClient.Connected ? "443 端口可访问" : "443 端口不可访问",
                        ChallengeType = normalizedType,
                        Status = tcpClient.Connected ? "reachable" : "unreachable",
                        ValidatedAt = DateTime.UtcNow
                    };
                }

                if (normalizedType != "http-01")
                {
                    throw new ArgumentException($"不支持的挑战类型: {challengeType}", nameof(challengeType));
                }

                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(8);
                var url = $"http://{domain.Trim().TrimEnd('/')}/.well-known/acme-challenge/";
                using var responseMessage = await httpClient.GetAsync(url);

                return new AcmeChallengeResult
                {
                    Success = responseMessage.StatusCode != System.Net.HttpStatusCode.NotFound,
                    Message = responseMessage.StatusCode != System.Net.HttpStatusCode.NotFound
                        ? "HTTP-01 挑战路径可访问"
                        : "HTTP-01 挑战路径返回 404，请先创建挑战文件或检查反向代理配置",
                    ChallengeType = normalizedType,
                    Status = responseMessage.StatusCode.ToString(),
                    ValidatedAt = DateTime.UtcNow,
                    ValidationUrl = url,
                    Details = new Dictionary<string, object>
                    {
                        ["statusCode"] = (int)responseMessage.StatusCode
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "域名所有权预检查失败: {Domain}, Type={ChallengeType}", domain, normalizedType);
                return new AcmeChallengeResult
                {
                    Success = false,
                    Message = $"域名所有权预检查失败: {ex.Message}",
                    ChallengeType = normalizedType,
                    Status = "error",
                    ErrorType = ex.GetType().Name,
                    ErrorDetails = ex.Message,
                    ValidatedAt = DateTime.UtcNow
                };
            }
        }

        public async Task<string> GenerateCsrAsync(IEnumerable<string> domains, string keyType = "rsa2048")
        {
            try
            {
                await Task.CompletedTask;
                return CreateCertificateSigningRequest(domains, keyType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生成 CSR 失败");
                throw;
            }
        }

        public async Task<AcmeCertificateValidationResult> ValidateCertificateAsync(string certificateData)
        {
            await Task.CompletedTask;
            return BuildCertificateValidationResult(certificateData, null);
        }

        public async Task<AcmeKeyInfo> GetAccountKeyInfoAsync(string accountId)
        {
            try
            {
                var accountKey = await ResolveAccountKeyAsync(accountId);
                if (accountKey != null)
                {
                    var account = await GetAccountAsync(accountId);
                    var keyType = GetKeyType(accountKey, account?.Metadata);
                    var fingerprint = ComputeSha256Fingerprint(accountKey.ToDer());
                    var publicKeyPem = ExtractPublicKeyPem(accountKey.ToPem());
                    var certificates = _dbContext.GetCollection<CertificateRecord>("certificates")
                        .Find(c => c.AccountId == accountId)
                        .Select(c => c.Id)
                        .ToList();

                    return new AcmeKeyInfo
                    {
                        KeyId = accountId,
                        KeyType = keyType.type,
                        KeySize = keyType.size,
                        PublicKey = publicKeyPem,
                        KeyAlgorithm = keyType.algorithm,
                        KeyFingerprint = fingerprint,
                        CreatedAt = account?.CreatedAt ?? DateTime.UtcNow,
                        LastUsedAt = account?.LastUsedAt,
                        IsActive = account?.IsActive ?? true,
                        AssociatedCertificates = certificates,
                        Metadata = account?.Metadata ?? new Dictionary<string, object>(),
                        PublicKeyPem = publicKeyPem
                    };
                }
                throw new KeyNotFoundException($"未找到账户 {accountId} 的密钥信息");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取账户密钥信息失败: {AccountId}", accountId);
                throw;
            }
        }

        public async Task<AcmeKeyPair> GenerateKeyPairAsync(string keyType = "rsa2048")
        {
            try
            {
                IKey key;
                var keyTypeStr = keyType.ToLower();

                if (keyTypeStr.Contains("ec") || keyTypeStr.Contains("p256"))
                {
                    key = KeyFactory.NewKey(KeyAlgorithm.ES256);
                }
                else if (keyTypeStr.Contains("384"))
                {
                    key = KeyFactory.NewKey(KeyAlgorithm.ES384);
                }
                else
                {
                    key = KeyFactory.NewKey(KeyAlgorithm.RS256);
                }

                return new AcmeKeyPair
                {
                    KeyId = Guid.NewGuid().ToString(),
                    PrivateKeyPem = key.ToPem(),
                    PublicKeyPem = ExtractPublicKeyPem(key.ToPem()),
                    KeyType = keyType,
                    KeySize = keyTypeStr.Contains("ec") ? 256 : 2048,
                    KeyAlgorithm = keyTypeStr.Contains("ec") ? "ECDSA" : "RSA",
                    CreatedAt = DateTime.UtcNow,
                    KeyFingerprint = ComputeSha256Fingerprint(key.ToDer())
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生成密钥对失败: {KeyType}", keyType);
                throw;
            }
        }

        public async Task<string> ExportAccountKeyAsync(string accountId, string format = "pem")
        {
            try
            {
                var accountKey = await ResolveAccountKeyAsync(accountId);
                if (accountKey != null)
                {
                    return format.ToLower() switch
                    {
                        "pem" => accountKey.ToPem(),
                        "der" => Convert.ToBase64String(accountKey.ToDer()),
                        _ => throw new ArgumentException($"不支持的导出格式: {format}")
                    };
                }
                throw new KeyNotFoundException($"未找到账户 {accountId} 的密钥");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导出账户密钥失败: {AccountId}", accountId);
                throw;
            }
        }

        public async Task<AcmeKeyInfo> ImportAccountKeyAsync(string keyData, string format = "pem")
        {
            try
            {
                IKey key;

                if (format.Equals("pem", StringComparison.OrdinalIgnoreCase))
                {
                    key = KeyFactory.FromPem(keyData);
                }
                else
                {
                    throw new ArgumentException($"不支持的导入格式: {format}");
                }

                return new AcmeKeyInfo
                {
                    KeyId = Guid.NewGuid().ToString(),
                    KeyType = GetKeyType(key).type,
                    KeySize = GetKeyType(key).size,
                    PublicKey = ExtractPublicKeyPem(key.ToPem()),
                    KeyAlgorithm = GetKeyType(key).algorithm,
                    KeyFingerprint = ComputeSha256Fingerprint(key.ToDer()),
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    PublicKeyPem = ExtractPublicKeyPem(key.ToPem())
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导入账户密钥失败");
                throw;
            }
        }

        public async Task<IEnumerable<AcmeOperationLog>> GetOperationLogsAsync(string? accountId = null, int limit = 100, int offset = 0)
        {
            await Task.CompletedTask;
            var logsCollection = _dbContext.GetCollection<AcmeOperationLog>("acme_operation_logs");
            var logs = string.IsNullOrWhiteSpace(accountId)
                ? logsCollection.FindAll()
                : logsCollection.Find(l => l.AccountId == accountId);

            return logs
                .OrderByDescending(l => l.Timestamp == default ? l.StartedAt : l.Timestamp)
                .Skip(Math.Max(0, offset))
                .Take(Math.Clamp(limit, 1, 500))
                .ToList();
        }

        #endregion

        #region Private Helper Methods

        private CertificateRecord? FindCertificateRecord(string certificateId)
        {
            if (string.IsNullOrWhiteSpace(certificateId))
            {
                return null;
            }

            var certificatesCollection = _dbContext.GetCollection<CertificateRecord>("certificates");
            return certificatesCollection.FindById(certificateId) ??
                   certificatesCollection.FindOne(c => c.CertificateId == certificateId ||
                                                       c.OrderId == certificateId ||
                                                       c.Fingerprint == certificateId ||
                                                       c.SerialNumber == certificateId);
        }

        private async Task<IKey?> ResolveAccountKeyAsync(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return null;
            }

            if (_accountKeys.TryGetValue(accountId, out var cachedKey) ||
                _staticAccountKeys.TryGetValue(accountId, out cachedKey))
            {
                return cachedKey;
            }

            var account = await GetAccountAsync(accountId);
            if (account == null || string.IsNullOrWhiteSpace(account.AccountKey))
            {
                return null;
            }

            var key = KeyFactory.FromPem(account.AccountKey);
            _accountKeys[accountId] = key;
            _staticAccountKeys[accountId] = key;
            return key;
        }

        private static string CreateCertificateSigningRequest(IEnumerable<string> domains, string keyType)
        {
            var domainList = domains
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => d.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (domainList.Count == 0)
            {
                throw new ArgumentException("CSR 至少需要一个域名", nameof(domains));
            }

            var subject = new X500DistinguishedName($"CN={EscapeDistinguishedNameValue(domainList[0])}");
            var keyTypeLower = string.IsNullOrWhiteSpace(keyType) ? "rsa2048" : keyType.ToLowerInvariant();

            if (keyTypeLower.Contains("ec") || keyTypeLower.Contains("ecdsa") || keyTypeLower.Contains("p256") || keyTypeLower.Contains("p384"))
            {
                using var ecdsa = ECDsa.Create(keyTypeLower.Contains("384")
                    ? ECCurve.NamedCurves.nistP384
                    : ECCurve.NamedCurves.nistP256);
                var request = new CertificateRequest(subject, ecdsa, HashAlgorithmName.SHA256);
                AddCertificateRequestExtensions(request, domainList, keyEncipherment: false);
                return request.CreateSigningRequestPem();
            }

            using var rsa = RSA.Create(keyTypeLower.Contains("4096") ? 4096 : 2048);
            var rsaRequest = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            AddCertificateRequestExtensions(rsaRequest, domainList, keyEncipherment: true);
            return rsaRequest.CreateSigningRequestPem();
        }

        private static void AddCertificateRequestExtensions(CertificateRequest request, IEnumerable<string> domains, bool keyEncipherment)
        {
            var sanBuilder = new SubjectAlternativeNameBuilder();
            foreach (var domain in domains)
            {
                sanBuilder.AddDnsName(domain);
            }

            request.CertificateExtensions.Add(sanBuilder.Build());
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));

            var keyUsage = X509KeyUsageFlags.DigitalSignature;
            if (keyEncipherment)
            {
                keyUsage |= X509KeyUsageFlags.KeyEncipherment;
            }

            request.CertificateExtensions.Add(new X509KeyUsageExtension(keyUsage, false));
        }

        private static string EscapeDistinguishedNameValue(string value)
        {
            return value.Replace("\\", "\\\\").Replace(",", "\\,").Replace("+", "\\+").Replace("\"", "\\\"");
        }

        private static AcmeCertificateValidationResult BuildCertificateValidationResult(string certificateData, string? certificateId)
        {
            var result = new AcmeCertificateValidationResult
            {
                CertificateId = certificateId ?? string.Empty,
                CertificateChain = certificateData ?? string.Empty,
                ValidatedAt = DateTime.UtcNow
            };

            if (string.IsNullOrWhiteSpace(certificateData))
            {
                result.Valid = false;
                result.Status = "invalid";
                result.Errors.Add("证书数据为空");
                result.ValidationErrors.Add("证书数据为空");
                result.ValidationStatus = new ValidationResult
                {
                    IsValid = false,
                    Status = "invalid",
                    Reason = "证书数据为空",
                    ValidatedAt = DateTime.UtcNow
                };
                return result;
            }

            try
            {
                using var cert = X509Certificate2.CreateFromPem(certificateData);
                var now = DateTime.UtcNow;
                var notBefore = cert.NotBefore.ToUniversalTime();
                var notAfter = cert.NotAfter.ToUniversalTime();
                var domains = ExtractCertificateDomains(cert);
                var selfSigned = cert.SubjectName.RawData.SequenceEqual(cert.IssuerName.RawData);

                result.CertificateFingerprint = cert.Thumbprint ?? string.Empty;
                result.SerialNumber = cert.SerialNumber ?? string.Empty;
                result.IssuedAt = notBefore;
                result.ExpiresAt = notAfter;
                result.Domains = domains;
                result.SubjectAlternativeNames = domains;
                result.Issuer = cert.Issuer ?? string.Empty;
                result.Subject = cert.Subject ?? string.Empty;
                result.SelfSigned = selfSigned;
                result.DomainMatch = domains.Count > 0;
                result.DaysUntilExpiry = (int)Math.Floor((notAfter - now).TotalDays);

                if (notBefore > now)
                {
                    result.Errors.Add("证书尚未生效");
                }

                if (notAfter <= now)
                {
                    result.Errors.Add("证书已过期");
                }

                if (selfSigned)
                {
                    result.Warnings.Add("证书为自签名证书");
                }

                using var chain = new X509Chain();
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                chain.ChainPolicy.VerificationTime = DateTime.Now;
                var chainValid = chain.Build(cert);
                if (!chainValid)
                {
                    foreach (var status in chain.ChainStatus)
                    {
                        result.Warnings.Add($"证书链警告: {status.StatusInformation.Trim()}");
                    }
                }

                result.Valid = result.Errors.Count == 0;
                result.Status = result.Valid ? "valid" : "invalid";
                result.ValidationErrors.AddRange(result.Errors);
                result.ValidationStatus = new ValidationResult
                {
                    IsValid = result.Valid,
                    Status = result.Status,
                    Reason = result.Valid ? null : string.Join("; ", result.Errors),
                    ValidatedAt = DateTime.UtcNow,
                    ExpiresAt = notAfter,
                    DomainValidations = domains.Select(d => new DomainValidation
                    {
                        Domain = d,
                        IsValid = result.Valid,
                        Status = result.Status,
                        ValidatedAt = DateTime.UtcNow
                    }).ToList()
                };
                result.Metadata["ChainValid"] = chainValid;
                return result;
            }
            catch (Exception ex)
            {
                result.Valid = false;
                result.Status = "invalid";
                result.Errors.Add($"证书解析失败: {ex.Message}");
                result.ValidationErrors.AddRange(result.Errors);
                result.ValidationStatus = new ValidationResult
                {
                    IsValid = false,
                    Status = "invalid",
                    Reason = ex.Message,
                    ValidatedAt = DateTime.UtcNow
                };
                return result;
            }
        }

        private static List<string> ExtractCertificateDomains(X509Certificate2 cert)
        {
            var domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var extension in cert.Extensions)
            {
                if (extension.Oid?.Value == "2.5.29.17")
                {
                    var formatted = new AsnEncodedData(extension.Oid, extension.RawData).Format(false);
                    foreach (var part in formatted.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var value = part.Trim();
                        const string dnsPrefix = "DNS Name=";
                        if (value.StartsWith(dnsPrefix, StringComparison.OrdinalIgnoreCase))
                        {
                            domains.Add(value[dnsPrefix.Length..].Trim());
                        }
                    }
                }
            }

            if (domains.Count == 0)
            {
                var commonName = cert.GetNameInfo(X509NameType.DnsName, false);
                if (!string.IsNullOrWhiteSpace(commonName))
                {
                    domains.Add(commonName);
                }
            }

            return domains.ToList();
        }

        private static (string type, int size, string algorithm) GetKeyType(IKey key, Dictionary<string, object>? metadata = null)
        {
            var metadataType = metadata?.GetValueOrDefault("KeyType")?.ToString();
            var metadataSize = metadata?.GetValueOrDefault("KeySize")?.ToString();

            if (!string.IsNullOrWhiteSpace(metadataType))
            {
                var normalizedType = metadataType.Contains("RSA", StringComparison.OrdinalIgnoreCase) ? "RSA" : "ECDSA";
                var size = int.TryParse(metadataSize?.Replace("P", string.Empty), out var parsedSize)
                    ? parsedSize
                    : normalizedType == "RSA" ? 2048 : 256;
                var algorithm = normalizedType == "RSA" ? "RS256" : size >= 384 ? "ES384" : "ES256";
                return (normalizedType, size, algorithm);
            }

            var pem = key.ToPem();
            if (pem.Contains("RSA", StringComparison.OrdinalIgnoreCase))
            {
                return ("RSA", 2048, "RS256");
            }

            if (pem.Contains("EC", StringComparison.OrdinalIgnoreCase))
            {
                return ("ECDSA", 256, "ES256");
            }

            return ("Unknown", 0, "Unknown");
        }

        private static string ComputeSha256Fingerprint(byte[] data)
        {
            return Convert.ToHexString(SHA256.HashData(data));
        }

        private static string ExtractPublicKeyPem(string privateKeyPem)
        {
            try
            {
                using var rsa = RSA.Create();
                rsa.ImportFromPem(privateKeyPem);
                return rsa.ExportSubjectPublicKeyInfoPem();
            }
            catch
            {
                // ignore and try ECDSA
            }

            try
            {
                using var ecdsa = ECDsa.Create();
                ecdsa.ImportFromPem(privateKeyPem);
                return ecdsa.ExportSubjectPublicKeyInfoPem();
            }
            catch
            {
                return string.Empty;
            }
        }

        private async Task AddOperationLogAsync(string operation, string accountId, string? certificateId, string? orderId, bool success, string? message)
        {
            try
            {
                await Task.CompletedTask;
                var logsCollection = _dbContext.GetCollection<AcmeOperationLog>("acme_operation_logs");
                var now = DateTime.UtcNow;
                logsCollection.Insert(new AcmeOperationLog
                {
                    Id = ObjectId.NewObjectId().ToString(),
                    Operation = operation,
                    OperationType = operation,
                    AccountId = accountId,
                    CertificateId = certificateId,
                    OrderId = orderId,
                    Status = success ? "completed" : "failed",
                    Success = success,
                    Message = message,
                    ErrorMessage = success ? null : message,
                    StartedAt = now,
                    Timestamp = now,
                    CompletedAt = now,
                    Duration = TimeSpan.Zero
                });
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "记录 ACME 操作日志失败: {Operation}", operation);
            }
        }

        private bool TryGetCachedAcmeContext(string key, out IAcmeContext? context)
        {
            context = null;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            // 直接命中
            if (_acmeContexts.TryGetValue(key, out var existing))
            {
                context = existing;
                return true;
            }

            // 使用小写 Provider 名称
            var lowerKey = key.ToLowerInvariant();
            if (_acmeContexts.TryGetValue(lowerKey, out existing))
            {
                context = existing;
                return true;
            }

            // 尝试从静态缓存恢复
            if (_staticAcmeContexts.TryGetValue(key, out existing) ||
                _staticAcmeContexts.TryGetValue(lowerKey, out existing))
            {
                _acmeContexts[key] = existing;
                context = existing;
                return true;
            }

            return false;
        }

        private void CacheAcmeContext(string accountId, string? provider, IAcmeContext context)
        {
            if (!string.IsNullOrWhiteSpace(accountId))
            {
                _acmeContexts[accountId] = context;
                _staticAcmeContexts[accountId] = context;
            }

            var providerKey = string.IsNullOrWhiteSpace(provider)
                ? "letsencrypt"
                : provider.ToLowerInvariant();

            _acmeContexts[providerKey] = context;
        }

        private async Task<IAuthorizationContext?> ResolveAuthorizationContextAsync(
            IEnumerable<IAuthorizationContext> authorizationContexts,
            string? authorizationId,
            string? domain)
        {
            if (!string.IsNullOrEmpty(authorizationId))
            {
                var match = authorizationContexts.FirstOrDefault(a =>
                    a.Location.ToString().Contains(authorizationId, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    return match;
                }
            }

            if (!string.IsNullOrEmpty(domain))
            {
                foreach (var authContext in authorizationContexts)
                {
                    try
                    {
                        var authResource = await authContext.Resource();
                        if (authResource?.Identifier?.Value != null &&
                            authResource.Identifier.Value.Equals(domain, StringComparison.OrdinalIgnoreCase))
                        {
                            return authContext;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "解析授权上下文失败: {Domain}", domain);
                    }
                }
            }

            return null;
        }

        private async Task<IAcmeContext?> GetAcmeContextAsync(string providerOrAccountId)
        {
            try
            {
                if (TryGetCachedAcmeContext(providerOrAccountId, out var cachedContext))
                {
                    return cachedContext;
                }

                // 如果是accountId，需要查找对应的账户获取provider
                IKey? accountKey = null;
                var cacheAccountKey = providerOrAccountId;

                if (Guid.TryParse(providerOrAccountId, out var accountGuid))
                {
                    var accountsCollection = _dbContext.GetCollection<AcmeAccount>("acme_accounts");
                    var account = accountsCollection.FindById(accountGuid.ToString());
                    if (account != null)
                    {
                        cacheAccountKey = account.Id;
                        providerOrAccountId = account.Provider;
                        // 获取账户密钥
                        if (!string.IsNullOrEmpty(account.AccountKey))
                        {
                            // 从存储的密钥字符串重建密钥
                            accountKey = KeyFactory.FromPem(account.AccountKey);
                        }
                    }
                    else
                    {
                        cacheAccountKey = accountGuid.ToString();
                    }
                }

                // 现在providerOrAccountId应该是provider名称
                var directoryUrl = GetDirectoryUrl(providerOrAccountId);
                var acmeContext = accountKey != null
                    ? new AcmeContext(new Uri(directoryUrl), accountKey)
                    : new AcmeContext(new Uri(directoryUrl));

                CacheAcmeContext(cacheAccountKey, providerOrAccountId, acmeContext);
                return acmeContext;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取 ACME 上下文失败: {ProviderOrAccountId}", providerOrAccountId);
                return null;
            }
        }

        private async Task<IKey> GetAccountKeyAsync(string keyIdentifier)
        {
            try
            {
                // 如果缓存中有，直接返回
                if (_accountKeys.TryGetValue(keyIdentifier, out var existingKey))
                {
                    return existingKey;
                }

                // 从数据库获取账户密钥
                var accountsCollection = _dbContext.GetCollection<AcmeAccount>("acme_accounts");
                var account = accountsCollection.FindById(keyIdentifier);

                if (account != null)
                {
                    if (!string.IsNullOrEmpty(account.AccountKey))
                    {
                        _logger.LogInformation("从数据库加载账户密钥: {AccountId}", keyIdentifier);
                        var privateKey = KeyFactory.FromPem(account.AccountKey);
                        _accountKeys[keyIdentifier] = privateKey;
                        _staticAccountKeys[keyIdentifier] = privateKey;
                        return privateKey;
                    }
                    else
                    {
                        _logger.LogWarning("账户密钥为空: {AccountId}", keyIdentifier);
                    }
                }
                else
                {
                    _logger.LogWarning("未找到账户: {AccountId}", keyIdentifier);
                }

                // 如果数据库中没有，生成新的密钥
                _logger.LogWarning("未找到账户密钥，生成新密钥: {AccountId}", keyIdentifier);
                var newKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
                _accountKeys[keyIdentifier] = newKey;
                _staticAccountKeys[keyIdentifier] = newKey;

                return newKey;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取账户密钥失败: {KeyIdentifier}", keyIdentifier);
                throw;
            }
        }

        private string GetDirectoryUrl(string? provider)
        {
            if (string.IsNullOrEmpty(provider)) return "https://acme-v02.api.letsencrypt.org/directory";

            return provider.ToLowerInvariant() switch
            {
                "letsencrypt" => "https://acme-v02.api.letsencrypt.org/directory",
                "letsencrypt-staging" => "https://acme-staging-v02.api.letsencrypt.org/directory",
                "zerossl" => "https://acme.zerossl.com/v2/DV90",
                "buypass" => "https://api.buypass.com/acme/directory",
                "google" => "https://dv.acme-v02.api.pki.goog/directory",
                "sslcom" => "https://acme.ssl.com/sslcom-dv-rsa",
                _ => "https://acme-v02.api.letsencrypt.org/directory"
            };
        }


        /// <summary>
        /// 查询挑战验证结果
        /// </summary>
        public async Task<AcmeChallengeResult> CheckChallengeStatusAsync(string orderId, string authorizationId, string challengeType = "http-01")
        {
            try
            {
                _logger.LogInformation("查询挑战状态: OrderId={OrderId}, AuthorizationId={AuthorizationId}, ChallengeType={ChallengeType}",
                    orderId, authorizationId, challengeType);

                // 从数据库获取订单信息
                var order = await GetCertificateOrderAsync(orderId);
                if (order == null)
                {
                    return new AcmeChallengeResult
                    {
                        Success = false,
                        Message = "未找到订单信息",
                        ErrorType = "order_not_found"
                    };
                }

                // 获取 ACME 上下文 - 复用之前的逻辑
                var directoryUrl = "https://acme-v02.api.letsencrypt.org/directory";
                var acmeContext = _acmeContexts.TryGetValue("letsencrypt", out var existingContext)
                    ? existingContext
                    : await CreateAcmeContextWithAccountAsync(order.AccountId, directoryUrl);

                if (acmeContext == null)
                {
                    return new AcmeChallengeResult
                    {
                        Success = false,
                        Message = "无法创建ACME上下文",
                        ErrorType = "acme_context_error"
                    };
                }

                // 获取订单上下文
                var orderContext = acmeContext.Order(new Uri(order.OrderUri));
                if (orderContext == null)
                {
                    return new AcmeChallengeResult
                    {
                        Success = false,
                        Message = "未找到订单上下文",
                        ErrorType = "order_context_error"
                    };
                }

                // 获取授权列表
                var authorizations = await orderContext.Authorizations();
                _logger.LogInformation("获取到 {Count} 个授权", authorizations?.Count() ?? 0);

                // 根据域名查找对应的授权（更可靠的方法）
                var existingOrder = await GetCertificateOrderAsync(orderId);
                var targetDomain = existingOrder?.Domains.FirstOrDefault();
                _logger.LogInformation("目标域名: {TargetDomain}, 订单状态: {OrderStatus}", targetDomain, existingOrder?.Status);
                IAuthorizationContext? targetAuthorization = null;

                if (authorizations != null)
                {
                    _logger.LogInformation("开始查找域名 {Domain} 的授权", targetDomain);
                    foreach (var auth in authorizations)
                    {
                        var authResource = await auth.Resource();
                        var authDomain = authResource.Identifier.Value;
                        _logger.LogInformation("检查授权: 域名={AuthDomain}, 位置={Location}", authDomain, auth.Location);

                        if (authDomain == targetDomain)
                        {
                            targetAuthorization = auth;
                            _logger.LogInformation("找到匹配的授权: {Domain}", targetDomain);
                            break;
                        }
                    }
                }

                if (targetAuthorization == null)
                {
                    _logger.LogWarning("未找到域名 {Domain} 的授权信息", targetDomain);
                    return new AcmeChallengeResult
                    {
                        Success = false,
                        Message = "未找到授权信息",
                        ErrorType = "authorization_not_found",
                        ErrorDetails = $"域名: {targetDomain}, 授权数量: {authorizations?.Count() ?? 0}"
                    };
                }

                // 获取挑战列表
                var challenges = await targetAuthorization.Challenges();
                var targetChallenge = challenges.FirstOrDefault(c => c.Type == challengeType);

                if (targetChallenge == null)
                {
                    _logger.LogWarning("未找到 {ChallengeType} 类型的挑战，可用类型: {Types}",
                        challengeType, string.Join(", ", challenges.Select(c => c.Type)));
                    return new AcmeChallengeResult
                    {
                        Success = false,
                        Message = $"未找到 {challengeType} 类型的挑战",
                        ErrorType = "challenge_not_found"
                    };
                }

                // 获取挑战详细信息 - 仅检查状态，不触发验证
                _logger.LogInformation("检查挑战状态: Type={Type}, Token={Token}",
                    targetChallenge.Type, targetChallenge.Token);
                var challengeDetail = await targetChallenge.Resource();
                var challengeStatus = challengeDetail.Status?.ToString()?.ToLowerInvariant() ?? "pending";
                var validatedAt = challengeStatus == "valid" ? DateTime.UtcNow : (DateTime?)null;

                _logger.LogInformation("挑战状态查询结果: Type={Type}, Status={Status}, Token={Token}",
                    targetChallenge.Type, challengeStatus, targetChallenge.Token);

                if (challengeStatus == "valid")
                {
                    return new AcmeChallengeResult
                    {
                        Success = true,
                        Message = "挑战验证成功",
                        ChallengeType = targetChallenge.Type,
                        Status = challengeStatus,
                        Token = targetChallenge.Token,
                        ValidatedAt = validatedAt,
                        ErrorType = "success"
                    };
                }
                else if (challengeStatus == "pending")
                {
                    return new AcmeChallengeResult
                    {
                        Success = false,
                        Message = "挑战验证中，请稍后再次查询",
                        ChallengeType = targetChallenge.Type,
                        Status = challengeStatus,
                        Token = targetChallenge.Token,
                        ErrorType = "challenge_pending"
                    };
                }
                else if (challengeStatus == "invalid")
                {
                    var errorDetails = challengeDetail.Error?.ToString() ?? "未知错误";
                    return new AcmeChallengeResult
                    {
                        Success = false,
                        Message = $"挑战验证失败: {errorDetails}",
                        ChallengeType = targetChallenge.Type,
                        Status = challengeStatus,
                        Token = targetChallenge.Token,
                        ErrorType = "challenge_failed",
                        ErrorDetails = errorDetails
                    };
                }
                else
                {
                    return new AcmeChallengeResult
                    {
                        Success = false,
                        Message = $"未知挑战状态: {challengeStatus}",
                        ChallengeType = targetChallenge.Type,
                        Status = challengeStatus,
                        Token = targetChallenge.Token,
                        ErrorType = "unknown_status"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查询挑战状态失败: OrderId={OrderId}, AuthorizationId={AuthorizationId}", orderId, authorizationId);

                return new AcmeChallengeResult
                {
                    Success = false,
                    Message = $"查询挑战状态失败: {ex.Message}",
                    ErrorType = "exception",
                    ErrorDetails = ex.ToString()
                };
            }
        }

        /// <summary>
        /// 创建独立的ACME上下文（不使用缓存，避免授权冲突）
        /// </summary>
        private async Task<AcmeContext?> CreateIndependentAcmeContextAsync(string accountId, string? accountKey, string acmeProvider, string progressId)
        {
            _logger.LogInformation("为证书订单创建独立的 ACME 上下文: {AccountId}, Provider: {Provider}", accountId, acmeProvider);

            // 优先使用直接提供的账户密钥
            if (!string.IsNullOrEmpty(accountKey))
            {
                _logger.LogInformation("使用提供的账户密钥创建独立 ACME 上下文: {AccountId}", accountId);
                try
                {
                    var privateKey = Certes.KeyFactory.FromPem(accountKey);

                    // 尝试从数据库获取账户信息以获取正确的 DirectoryUrl
                    var accountsCollection = _dbContext.GetCollection<AcmeAccount>("acme_accounts");
                    var dbAccount = accountsCollection.FindById(accountId);

                    // 使用账户元数据中的 DirectoryUrl（账户跟环境绑定）
                    // 如果不存在则使用 acmeProvider 参数
                    string directoryUrl;
                    if (dbAccount?.Metadata != null && dbAccount.Metadata.ContainsKey("DirectoryUrl"))
                    {
                        directoryUrl = dbAccount.Metadata["DirectoryUrl"].ToString() ?? GetDirectoryUrl(acmeProvider);
                        _logger.LogInformation("从账户元数据获取 DirectoryUrl: {Url}", directoryUrl);
                    }
                    else
                    {
                        directoryUrl = GetDirectoryUrl(acmeProvider);
                        _logger.LogInformation("使用 acmeProvider 获取 DirectoryUrl: {Url}", directoryUrl);
                    }

                    var acme = new AcmeContext(new Uri(directoryUrl), privateKey);

                    // 验证账户有效性
                    var account = await acme.Account();
                    if (account != null)
                    {
                        _logger.LogInformation("独立 ACME 上下文创建成功: {AccountId}", accountId);
                        return acme;
                    }
                    else
                    {
                        _logger.LogWarning("独立 ACME 上下文创建失败，账户无效: {AccountId}", accountId);
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "使用提供的账户密钥创建独立 ACME 上下文时发生错误: {AccountId}", accountId);
                    if (!string.IsNullOrEmpty(progressId))
                    {
                        await _progressService.AddErrorAsync(progressId, $"ACME上下文创建错误: {ex.Message}");
                    }
                    return null;
                }
            }

            // 尝试从数据库获取账户密钥
            try
            {
                // 直接从数据库获取账户信息
                var accountsCollection = _dbContext.GetCollection<AcmeAccount>("acme_accounts");
                var dbAccount = accountsCollection.FindById(accountId);
                if (dbAccount != null && !string.IsNullOrEmpty(dbAccount.AccountKey))
                {
                    // 使用账户元数据中的 DirectoryUrl（账户跟环境绑定）
                    string directoryUrl;
                    if (dbAccount.Metadata != null && dbAccount.Metadata.ContainsKey("DirectoryUrl"))
                    {
                        directoryUrl = dbAccount.Metadata["DirectoryUrl"]?.ToString() ?? GetDirectoryUrl(dbAccount.Provider);
                        _logger.LogInformation("使用账户元数据中的 DirectoryUrl: {Url}", directoryUrl);
                    }
                    else
                    {
                        directoryUrl = GetDirectoryUrl(dbAccount.Provider);
                        _logger.LogInformation("使用 Provider 获取 DirectoryUrl: {Url}", directoryUrl);
                    }

                    _logger.LogInformation("使用数据库中的账户密钥创建独立 ACME 上下文: {AccountId}, Provider: {Provider}, DirectoryUrl: {Url}", accountId, dbAccount.Provider, directoryUrl);
                    var privateKey = Certes.KeyFactory.FromPem(dbAccount.AccountKey);
                    var acme = new AcmeContext(new Uri(directoryUrl), privateKey);

                    // 验证账户有效性
                    var accountInfo = await acme.Account();
                    if (accountInfo != null)
                    {
                        _logger.LogInformation("独立 ACME 上下文创建成功: {AccountId}", accountId);
                        return acme;
                    }
                    else
                    {
                        _logger.LogWarning("独立 ACME 上下文创建失败，账户无效: {AccountId}", accountId);
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从数据库获取账户创建独立 ACME 上下文时发生错误: {AccountId}", accountId);
                if (!string.IsNullOrEmpty(progressId))
                {
                    await _progressService.AddErrorAsync(progressId, $"ACME上下文创建错误: {ex.Message}");
                }
                return null;
            }

            _logger.LogError("无法创建独立的 ACME 上下文，未找到有效的账户密钥: {AccountId}", accountId);
            if (!string.IsNullOrEmpty(progressId))
            {
                await _progressService.AddErrorAsync(progressId, "未找到有效的ACME账户密钥");
            }
            return null;
        }

        /// <summary>
        /// 获取或创建ACME上下文
        /// </summary>
        private async Task<AcmeContext?> GetOrCreateAcmeContextAsync(string accountId, string? accountKey, string? progressId)
        {
            // 尝试从缓存获取
            if (TryGetCachedAcmeContext(accountId, out var cachedContext))
            {
                _logger.LogInformation("找到现有的 ACME 上下文: {AccountId}", accountId);
                return (AcmeContext?)cachedContext;
            }

            _logger.LogInformation("内存中未找到账户 {AccountId} 的 ACME 上下文，尝试重新创建", accountId);

            // 优先使用直接提供的账户密钥
            if (!string.IsNullOrEmpty(accountKey))
            {
                _logger.LogInformation("使用提供的账户密钥创建 ACME 上下文: {AccountId}", accountId);
                try
                {
                    var privateKey = Certes.KeyFactory.FromPem(accountKey);
                    var directoryUrl = GetDirectoryUrl("letsencrypt");
                    var acme = new AcmeContext(new Uri(directoryUrl), privateKey);

                    CacheAcmeContext(accountId, "letsencrypt", acme);
                    _logger.LogInformation("成功使用提供的密钥创建 ACME 上下文: {AccountId}", accountId);
                    return (AcmeContext?)acme;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "使用提供的账户密钥创建 ACME 上下文失败: {AccountId}", accountId);
                    if (!string.IsNullOrEmpty(progressId))
                    {
                        await _progressService.AddErrorAsync(progressId, $"使用提供的账户密钥创建 ACME 上下文失败: {accountId}");
                    }
                    throw new InvalidOperationException($"使用提供的账户密钥创建 ACME 上下文失败: {accountId}", ex);
                }
            }

            // 从数据库获取账户信息并重新创建ACME上下文
            var accountsCollection = _dbContext.GetCollection<AcmeAccount>("acme_accounts");
            var dbAccount = accountsCollection.FindById(accountId);

            if (dbAccount != null && !string.IsNullOrEmpty(dbAccount.AccountKey))
            {
                // 使用账户元数据中的 DirectoryUrl（账户跟环境绑定）
                var directoryUrl = dbAccount.Metadata?.ContainsKey("DirectoryUrl") == true
                    ? dbAccount.Metadata["DirectoryUrl"].ToString()
                    : GetDirectoryUrl(dbAccount.Provider);

                var context = await CreateAcmeContextWithAccountAsync(accountId, directoryUrl);
                if (context == null)
                {
                    if (!string.IsNullOrEmpty(progressId))
                    {
                        await _progressService.AddErrorAsync(progressId, $"无法为账户 {accountId} 创建 ACME 上下文");
                    }
                    throw new InvalidOperationException($"无法为账户 {accountId} 创建 ACME 上下文");
                }
                _logger.LogInformation("成功为账户 {AccountId} 重新创建 ACME 上下文", accountId);
                return context;
            }
            else
            {
                if (!string.IsNullOrEmpty(progressId))
                {
                    await _progressService.AddErrorAsync(progressId, $"未找到账户 {accountId} 或账户密钥为空");
                }
                throw new InvalidOperationException($"未找到账户 {accountId} 或账户密钥为空");
            }
        }

        /// <summary>
        /// 使用账户信息创建ACME上下文
        /// </summary>
        private async Task<AcmeContext?> CreateAcmeContextWithAccountAsync(string accountId, string? directoryUrl)
        {
            try
            {
                // 从数据库获取账户信息
                var accountsCollection = _dbContext.GetCollection<AcmeAccount>("acme_accounts");
                var dbAccount = accountsCollection.FindById(accountId);

                if (dbAccount != null && !string.IsNullOrEmpty(dbAccount.AccountKey))
                {
                    _logger.LogInformation("使用数据库中的账户密钥创建ACME上下文: {AccountId}", accountId);
                    var privateKey = Certes.KeyFactory.FromPem(dbAccount.AccountKey);
                    var url = directoryUrl ?? GetDirectoryUrl(dbAccount.Provider);
                    var acmeContext = new AcmeContext(new Uri(url), privateKey);
                    _acmeContexts["letsencrypt"] = acmeContext;
                    // 同时保存到静态字典
                    _staticAcmeContexts[accountId] = acmeContext;
                    return acmeContext;
                }
                else
                {
                    _logger.LogWarning("未找到账户密钥，账户ID: {AccountId}", accountId);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建ACME上下文失败: {AccountId}", accountId);
                return null;
            }
        }



        /// <summary>
        /// 清理DNS记录
        /// </summary>
        private async Task CleanupDnsRecordsAsync(
            AcmeCertificateOrder order,
            string? dnsProvider,
            Dictionary<string, object>? dnsCredentials,
            string? progressId)
        {
            try
            {
                if (string.IsNullOrEmpty(dnsProvider))
                {
                    _logger.LogWarning("DNS提供商为空，跳过DNS记录清理");
                    return;
                }

                _logger.LogInformation("开始清理DNS记录: OrderId={OrderId}, DnsProvider={DnsProvider}",
                    order.Id, dnsProvider);

                var provider = GetDnsProvider(dnsProvider);
                if (provider == null)
                {
                    _logger.LogWarning("未找到DNS提供商，跳过DNS记录清理: {DnsProvider}", dnsProvider);
                    return;
                }

                if (!string.IsNullOrEmpty(progressId))
                {
                    await _progressService.UpdateProgressStepAsync(progressId,
                        CertificateApplicationStep.DownloadingCertificate,
                        "正在清理DNS记录");
                }

                var credentials = dnsCredentials ?? new Dictionary<string, object>();

                // 从挑战存储中获取DNS-01挑战数据
                foreach (var authorization in order.Authorizations ?? new List<AcmeAuthorization>())
                {
                    var dnsChallenge = authorization.Challenges?.FirstOrDefault(c => c.Type == "dns-01");
                    if (dnsChallenge?.ChallengeData != null)
                    {
                        var domain = authorization.Domain;
                        var recordName = dnsChallenge.ChallengeData.ContainsKey("RecordName")
                            ? dnsChallenge.ChallengeData["RecordName"]?.ToString()
                            : $"_acme-challenge.{domain}";
                        var recordValue = dnsChallenge.ChallengeData.ContainsKey("RecordValue")
                            ? dnsChallenge.ChallengeData["RecordValue"]?.ToString()
                            : string.Empty;

                        if (!string.IsNullOrEmpty(domain) && !string.IsNullOrEmpty(recordName) && !string.IsNullOrEmpty(recordValue))
                        {
                            await CleanupDnsRecordAsync(provider, domain, recordName, recordValue, credentials, null);
                        }
                    }
                }

                _logger.LogInformation("DNS记录清理完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理DNS记录异常: OrderId={OrderId}", order.Id);
                if (!string.IsNullOrEmpty(progressId))
                {
                    await _progressService.AddWarningAsync(progressId, $"清理DNS记录异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 清理单个DNS记录
        /// </summary>
        private async Task CleanupDnsRecordAsync(
            IDnsProvider provider,
            string domain,
            string recordName,
            string recordValue,
            Dictionary<string, object> dnsCredentials,
            string? recordId)
        {
            try
            {
                _logger.LogInformation("清理DNS记录: {RecordName} = {RecordValue}", recordName, recordValue);

                var deleteResult = await provider.DeleteTxtRecordAsync(domain, recordName, recordValue, dnsCredentials);
                if (deleteResult.Success)
                {
                    _logger.LogInformation("DNS记录清理成功: {RecordName}", recordName);
                }
                else
                {
                    _logger.LogWarning("DNS记录清理失败: {RecordName}, Message: {Message}",
                        recordName, deleteResult.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理DNS记录异常: {RecordName}", recordName);
            }
        }

        /// <summary>
        /// 获取DNS提供商实例
        /// </summary>
        private IDnsProvider? GetDnsProvider(string providerName)
        {
            if (string.IsNullOrEmpty(providerName))
            {
                return null;
            }

            // 添加调试日志
            _logger.LogInformation("查找DNS提供商: {ProviderName}", providerName);
            _logger.LogInformation("可用的DNS提供商数量: {Count}", _dnsProviders.Count);
            foreach (var kvp in _dnsProviders)
            {
                _logger.LogInformation("可用DNS提供商: Key={Key}, Name={Name}, DisplayName={DisplayName}",
                    kvp.Key, kvp.Value.Name, kvp.Value.DisplayName);
            }

            // 🔧 修复：使用不区分大小写的查找
            var normalizedName = providerName.ToLowerInvariant();
            if (_dnsProviders.TryGetValue(normalizedName, out var provider))
            {
                _logger.LogInformation("找到DNS提供商: {ProviderName}", providerName);
                return provider;
            }

            _logger.LogWarning("未找到DNS提供商: {ProviderName}", providerName);
            return null;
        }

        /// <summary>
        /// 等待DNS传播
        /// </summary>
        private async Task<bool> WaitForDnsPropagationAsync(string recordName, string expectedValue, string? progressId)
        {
            try
            {
                _logger.LogInformation("等待DNS传播: {RecordName}, 期望值: {ExpectedValue}", recordName, expectedValue);

                int maxAttempts = 90; // 最多等待90次，每次间隔5秒，总计7.5分钟
                int attempts = 0;

                // 使用多个DNS服务器进行检查，提高在中国大陆的成功率
                var dnsServers = new[]
                {
                    System.Net.IPAddress.Parse("8.8.8.8"),
                    System.Net.IPAddress.Parse("223.5.5.5"),
                    System.Net.IPAddress.Parse("1.1.1.1")
                };

                while (attempts < maxAttempts)
                {
                    bool verified = false;

                    foreach (var dnsServerIp in dnsServers)
                    {
                        try
                        {
                            var lookup = new LookupClient(dnsServerIp);
                            var result = await lookup.QueryAsync(recordName, QueryType.TXT);

                            if (result.HasError)
                            {
                                _logger.LogDebug("DNS查询返回错误 (Server: {Server}): {Error}", dnsServerIp, result.ErrorMessage);
                                continue;
                            }

                            foreach (var txtRecord in result.Answers.TxtRecords())
                            {
                                foreach (var txt in txtRecord.Text)
                                {
                                    // 检查是否包含期望的值
                                    if (txt == expectedValue)
                                    {
                                        _logger.LogInformation("DNS传播成功 (Server: {Server}): {RecordName} = {ActualValue}", dnsServerIp, recordName, txt);
                                        if (!string.IsNullOrEmpty(progressId))
                                        {
                                            await _progressService.CompleteCurrentStepAsync(progressId, "DNS传播成功");
                                        }
                                        verified = true;
                                        break;
                                    }
                                }
                                if (verified) break;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug("DNS查询异常 (Server: {Server}): {RecordName}, Error: {Error}", dnsServerIp, recordName, ex.Message);
                        }

                        if (verified) break;
                    }

                    if (verified) return true;

                    attempts++;
                    _logger.LogInformation("等待DNS传播中: {RecordName} ({Attempts}/{MaxAttempts})", recordName, attempts, maxAttempts);

                    if (attempts >= maxAttempts)
                    {
                        break;
                    }

                    await Task.Delay(5000); // 等待5秒
                }

                _logger.LogWarning("DNS传播超时: {RecordName}", recordName);
                if (!string.IsNullOrEmpty(progressId))
                {
                    await _progressService.AddErrorAsync(progressId, $"DNS传播超时: {recordName}");
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "等待DNS传播异常: {RecordName}", recordName);
                if (!string.IsNullOrEmpty(progressId))
                {
                    await _progressService.AddErrorAsync(progressId, $"DNS传播异常: {ex.Message}");
                }
                return false;
            }
        }

        /// <summary>
        /// 将 CertificateRecord 转换为 AcmeCertificateOrder
        /// </summary>
        private AcmeCertificateOrder ConvertCertificateRecordToOrder(CertificateRecord record)
        {
            var order = new AcmeCertificateOrder
            {
                Id = record.Id,
                AccountId = record.AccountId ?? string.Empty,
                OrderUri = record.Metadata?.GetValueOrDefault("orderUri")?.ToString() ?? string.Empty,
                Domains = record.Domains ?? new List<string>(),
                Status = record.Status ?? "unknown",
                CreatedAt = record.CreatedAt,
                ExpiresAt = record.ExpiresAt,
                CompletedAt = record.IssuedAt != default ? record.IssuedAt : (DateTime?)null,
                CertificateId = record.CertificateId,
                ProgressId = record.Metadata?.GetValueOrDefault("progressId")?.ToString(),
                FinalizeUri = record.Metadata?.GetValueOrDefault("finalizeUri")?.ToString(),
                CertificateUri = record.Metadata?.GetValueOrDefault("certificateUri")?.ToString(),
                Error = record.Metadata?.GetValueOrDefault("error")?.ToString(),
                Metadata = new Dictionary<string, object>(record.Metadata ?? new Dictionary<string, object>())
            };

            // 添加挑战类型信息
            if (!order.Metadata.ContainsKey("challengeType") && !string.IsNullOrEmpty(record.Type))
            {
                order.Metadata["challengeType"] = record.Type;
            }

            return order;
        }

        /// <summary>
        /// 将证书文档转换为 AcmeCertificateOrder
        /// </summary>
        private AcmeCertificateOrder ConvertCertificateToOrder(BsonDocument certificateDoc)
        {
            var order = new AcmeCertificateOrder
            {
                Id = certificateDoc["_id"].As<string>(),
                AccountId = certificateDoc.ContainsKey("accountId") ? certificateDoc["accountId"].As<string>() : string.Empty,
                OrderUri = certificateDoc.ContainsKey("orderUri") ? certificateDoc["orderUri"].As<string>() : string.Empty,
                Domains = new List<string>(),
                Status = certificateDoc.ContainsKey("status") ? certificateDoc["status"].As<string>() : "unknown",
                CreatedAt = certificateDoc.ContainsKey("createdAt") ? certificateDoc["createdAt"].As<DateTime>() : DateTime.UtcNow,
                ExpiresAt = certificateDoc.ContainsKey("expiresAt") && certificateDoc["expiresAt"] != BsonValue.Null ?
                           certificateDoc["expiresAt"].As<DateTime>() : (DateTime?)null,
                CompletedAt = certificateDoc.ContainsKey("issuedAt") && certificateDoc["issuedAt"] != BsonValue.Null ?
                             certificateDoc["issuedAt"].As<DateTime>() : (DateTime?)null,
                CertificateId = certificateDoc.ContainsKey("id") ? certificateDoc["id"].As<string>() : null,
                ProgressId = certificateDoc.ContainsKey("progressId") ? certificateDoc["progressId"].As<string>() : null,
                FinalizeUri = certificateDoc.ContainsKey("finalizeUri") ? certificateDoc["finalizeUri"].As<string>() : null,
                CertificateUri = certificateDoc.ContainsKey("certificateUri") ? certificateDoc["certificateUri"].As<string>() : null,
                Error = certificateDoc.ContainsKey("error") ? certificateDoc["error"].As<string>() : null,
                Metadata = new Dictionary<string, object>()
            };

            // 处理域名
            if (certificateDoc.ContainsKey("domains") && certificateDoc["domains"].IsArray)
            {
                var domainsArray = certificateDoc["domains"].As<BsonArray>();
                foreach (var domain in domainsArray)
                {
                    order.Domains.Add(domain.As<string>());
                }
            }
            else if (certificateDoc.ContainsKey("domain"))
            {
                order.Domains.Add(certificateDoc["domain"].As<string>());
                if (certificateDoc.ContainsKey("alternativeNames") && certificateDoc["alternativeNames"].IsArray)
                {
                    var altNamesArray = certificateDoc["alternativeNames"].As<BsonArray>();
                    foreach (var altName in altNamesArray)
                    {
                        order.Domains.Add(altName.As<string>());
                    }
                }
            }

            // 处理元数据
            if (certificateDoc.ContainsKey("metadata") && certificateDoc["metadata"].IsDocument)
            {
                var metadataDoc = certificateDoc["metadata"].As<BsonDocument>();
                foreach (var item in metadataDoc)
                {
                    order.Metadata[item.Key] = item.Value;
                }
            }

            // 添加一些有用的元数据
            if (certificateDoc.ContainsKey("challengeType"))
            {
                order.Metadata["challengeType"] = certificateDoc["challengeType"].As<string>();
            }
            if (certificateDoc.ContainsKey("dnsProvider"))
            {
                order.Metadata["dnsProvider"] = certificateDoc["dnsProvider"].As<string>();
            }
            if (certificateDoc.ContainsKey("dnsCredentials"))
            {
                order.Metadata["dnsCredentials"] = certificateDoc["dnsCredentials"];
            }

            return order;
        }

        /// <summary>
        /// 根据域名列表确定证书类型
        /// </summary>
        private string DetermineCertificateType(List<string> domains)
        {
            if (domains == null || domains.Count == 0)
                return "single";

            if (domains.Count == 1)
            {
                return domains[0].StartsWith("*.") ? "wildcard" : "single";
            }
            return domains.Any(d => d.StartsWith("*.")) ? "wildcard" : "multi-domain";
        }

        #endregion
    }
}
