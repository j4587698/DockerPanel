using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using DockerPanel.API.Models;
using DockerPanel.API.Models.Acme;
using DockerPanel.API.Services;
using DockerPanel.API.Services.Acme;
using DockerPanel.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TinyDb;
using TinyDb.Bson;
using TinyDb.Core;
using TinyDb.Collections;

namespace DockerPanel.API.Controllers
{
    /// <summary>
    /// ACME证书管理控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AcmeController : ControllerBase
    {
        private readonly IAcmeService _acmeService;
        private readonly ICertificateManagementService _certificateManagementService;
        private readonly IAcmeChallengeStore _challengeStore;
        private readonly TinyDbContext _dbContext;
        private readonly ILogger<AcmeController> _logger;
        private readonly CertificateSettings _certificateSettings;
        private readonly ILocalizationService _localization;

        public AcmeController(
            IAcmeService acmeService, 
            ICertificateManagementService certificateManagementService, 
            IAcmeChallengeStore challengeStore, 
            TinyDbContext dbContext, 
            ILogger<AcmeController> logger,
            IOptions<CertificateSettings> certificateSettings,
            ILocalizationService localization)
        {
            _acmeService = acmeService;
            _certificateManagementService = certificateManagementService;
            _challengeStore = challengeStore;
            _dbContext = dbContext;
            _logger = logger;
            _certificateSettings = certificateSettings.Value;
            _localization = localization;
        }

        /// <summary>
        /// 获取ACME统计信息
        /// </summary>
        [HttpGet("statistics")]
        public async Task<ActionResult<object>> GetAcmeStatistics()
        {
            try
            {
                // 获取所有订单
                var ordersCollection = _dbContext.GetCollection<AcmeCertificateOrder>("acme_orders");
                var allOrders = ordersCollection.FindAll().ToList();
                
                // 获取所有已完成证书
                var certificatesCollection = _dbContext.GetCollection<DockerPanel.API.Services.Acme.CertificateRecord>("certificates");
                var allCertificates = certificatesCollection.FindAll().ToList();
                
                // 获取账户信息
                var accounts = await _acmeService.GetAccountsAsync();

                // 排除已存在于certificates集合中的订单
                var completedOrderIds = allCertificates.Select(c => c.OrderId).Where(id => !string.IsNullOrEmpty(id)).ToHashSet();
                var incompleteOrders = allOrders.Where(o => !completedOrderIds.Contains(o.Id)).ToList();

                var now = DateTime.UtcNow;
                var renewalThreshold = now.AddDays(30);

                // 计算证书状态（与列表返回逻辑一致）
                int validCount = 0;
                int expiringCount = 0;
                int expiredCount = 0;
                int pendingCount = 0;
                int invalidCount = 0;

                // 处理已完成证书
                foreach (var cert in allCertificates)
                {
                    if (cert.ExpiresAt <= now)
                    {
                        expiredCount++;
                    }
                    else if (cert.ExpiresAt <= renewalThreshold)
                    {
                        expiringCount++;
                        validCount++; // 即将过期但仍然有效
                    }
                    else
                    {
                        validCount++;
                    }
                }

                // 处理未完成订单（根据订单状态和过期时间计算）
                foreach (var order in incompleteOrders)
                {
                    var baseStatus = order.Status?.ToLower() ?? "pending";
                    
                    // 先检查是否是pending/processing状态
                    if (baseStatus == "pending" || baseStatus == "processing" || baseStatus == "ready")
                    {
                        pendingCount++;
                    }
                    else if (baseStatus == "invalid" || baseStatus == "failed")
                    {
                        invalidCount++;
                    }
                    else if (baseStatus == "cancelled")
                    {
                        // 取消的订单不计入统计
                    }
                    else if (order.ExpiresAt.HasValue && order.ExpiresAt.Value <= now)
                    {
                        // 已过期
                        expiredCount++;
                    }
                    else if (baseStatus == "valid")
                    {
                        // 有效订单，检查是否即将过期
                        var daysUntilExpiry = order.ExpiresAt.HasValue ? (order.ExpiresAt.Value - now).Days : 999;
                        if (daysUntilExpiry <= _certificateSettings.ExpiringSoonDays)
                        {
                            expiringCount++;
                            validCount++;
                        }
                        else
                        {
                            validCount++;
                        }
                    }
                    else
                    {
                        pendingCount++;
                    }
                }

                var statistics = new
                {
                    // 总数 = 未完成订单 + 已完成证书
                    totalCertificates = incompleteOrders.Count + allCertificates.Count,
                    
                    // 有效证书（未过期）
                    validCertificates = validCount,
                    
                    // 即将过期（30天内）
                    expiringCertificates = expiringCount,
                    
                    // 已过期
                    expiredCertificates = expiredCount,
                    
                    // 处理中
                    pendingCertificates = pendingCount,
                    
                    // 失败/无效
                    invalidCertificates = invalidCount,
                    
                    totalAccounts = accounts.Count(),
                    activeAccounts = accounts.Count(a => a.IsActive),
                    
                    lastUpdated = now,
                    renewalThresholdDays = _certificateSettings.ExpiringSoonDays,
                    
                    // 即将到期续期数
                    upcomingRenewals = expiringCount
                };

                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取ACME统计信息失败");
                return StatusCode(500, new { error = _localization.GetMessage("acme.statsFailed"), message = ex.Message });
            }
        }

        /// <summary>
        /// 获取ACME提供商列表
        /// </summary>
        [HttpGet("providers")]
        public async Task<ActionResult<IEnumerable<AcmeProvider>>> GetProviders()
        {
            try
            {
                var providers = await _acmeService.GetProvidersAsync();
                return Ok(providers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取ACME提供商列表失败");
                return StatusCode(500, new { message = _localization.GetMessage("acme.providersFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 测试ACME提供商连接
        /// </summary>
        [HttpPost("providers/{provider}/test")]
        public async Task<ActionResult<AcmeConnectionTestResult>> TestProviderConnection(string provider)
        {
            try
            {
                var result = await _acmeService.TestProviderConnectionAsync(provider);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "测试ACME提供商连接失败: {Provider}", provider);
                return StatusCode(500, new { message = _localization.GetMessage("acme.testConnectionFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 获取ACME账户列表
        /// </summary>
        [HttpGet("accounts")]
        public async Task<ActionResult<IEnumerable<AcmeAccount>>> GetAccounts()
        {
            try
            {
                var accounts = await _acmeService.GetAccountsAsync();
                return Ok(accounts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取ACME账户列表失败");
                return StatusCode(500, new { message = _localization.GetMessage("acme.accountsFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 根据ID获取ACME账户
        /// </summary>
        [HttpGet("accounts/{accountId}")]
        public async Task<ActionResult<AcmeAccount>> GetAccount(string accountId)
        {
            try
            {
                var account = await _acmeService.GetAccountAsync(accountId);
                if (account == null)
                {
                    return NotFound(new { message = _localization.GetMessage("certificate.accountNotFound") });
                }
                return Ok(account);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取ACME账户失败: {AccountId}", accountId);
                return StatusCode(500, new { message = _localization.GetMessage("acme.getAccountFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 创建ACME账户
        /// </summary>
        [HttpPost("accounts")]
        public async Task<ActionResult<AcmeAccount>> CreateAccount([FromBody] CreateAcmeAccountRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var account = await _acmeService.CreateAccountAsync(request);
                return CreatedAtAction(nameof(GetAccount), new { accountId = account.Id }, account);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建ACME账户失败: {Email}, {Provider}", request.Email, request.Provider);
                return StatusCode(500, new { message = _localization.GetMessage("acme.accountCreateFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 删除ACME账户
        /// </summary>
        [HttpDelete("accounts/{accountId}")]
        public async Task<ActionResult> DeleteAccount(string accountId)
        {
            try
            {
                var result = await _acmeService.DeleteAccountAsync(accountId);
                if (!result)
                {
                    return NotFound(new { message = _localization.GetMessage("certificate.accountNotFound") });
                }
                return Ok(new { message = _localization.GetMessage("certificate.accountDeleted") });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除ACME账户失败: {AccountId}", accountId);
                return StatusCode(500, new { message = _localization.GetMessage("acme.accountDeleteFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 申请证书
        /// </summary>
        [HttpPost("certificates/order")]
        public async Task<ActionResult<AcmeCertificateOrder>> OrderCertificate([FromBody] CertificateOrderRequest request)
        {
            try
            {
                // 添加调试日志
                _logger.LogInformation("收到证书申请请求: Domain={Domain}, AlternativeNames={AlternativeNames}, Email={Email}",
                    request.Domain,
                    request.AlternativeNames != null ? string.Join(",", request.AlternativeNames) : "null",
                    request.Email);

                // 构建域名列表
                var domains = new List<string>();
                if (!string.IsNullOrEmpty(request.Domain))
                {
                    domains.Add(request.Domain);
                    _logger.LogInformation("添加主域名: {Domain}", request.Domain);
                }
                if (request.AlternativeNames != null && request.AlternativeNames.Count > 0)
                {
                    domains.AddRange(request.AlternativeNames);
                    _logger.LogInformation("添加别名域名: {AlternativeNames}", string.Join(",", request.AlternativeNames));
                }

                _logger.LogInformation("最终域名列表数量: {Count}, 域名: {Domains}", domains.Count, string.Join(",", domains));

                if (domains.Count == 0)
                {
                    _logger.LogWarning("域名列表为空，返回400错误");
                    return BadRequest(new { message = _localization.GetMessage("certificate.domainsEmpty") });
                }

                // 🔧 修复：检查是否已有相同域名的pending订单
                var existingOrders = await _acmeService.GetPendingOrdersForDomainsAsync(domains);
                if (existingOrders.Any())
                {
                    var existingOrder = existingOrders.First();
                    _logger.LogWarning("发现相同域名的pending订单，返回现有订单: Domain={Domain}, OrderId={OrderId}", 
                        string.Join(",", existingOrder.Domains), existingOrder.Id);
                    
                    return Conflict(new { 
                        message = _localization.GetMessage("acme.pendingOrderExists"), 
                        existingOrderId = existingOrder.Id,
                        status = existingOrder.Status,
                        createdAt = existingOrder.CreatedAt
                    });
                }

                // 创建ACME证书请求
                var acmeRequest = new AcmeCertificateRequest
                {
                    AccountId = request.AccountId ?? await GetOrCreateAccountIdAsync(request.Email, request.AcmeProvider ?? "letsencrypt"),
                    Domains = domains,
                    KeyType = "RSA2048",
                    UseStaging = request.AcmeProvider != "buypass", // Buypass 通常使用生产环境
                    ChallengeTypes = new List<string> { request.ChallengeType ?? "http-01" },
                    CertificateValidityDays = 90,
                    AccountKey = request.AccountKey, // 传递账户密钥
                    Metadata = new Dictionary<string, object>
                    {
                        ["autoRenew"] = request.AutoRenew,
                        ["email"] = request.Email,
                        ["challengeType"] = request.ChallengeType ?? string.Empty,
                        ["dnsProvider"] = request.DnsProvider ?? string.Empty,
                        ["dnsCredentials"] = ConvertDnsCredentialsForSave(request.DnsCredentials),
                        ["acmeProvider"] = request.AcmeProvider ?? "letsencrypt"
                    }
                };

                // 使用 CertesAcmeService 进行实际的ACME申请和自动验证
                var order = await _acmeService.OrderCertificateAsync(acmeRequest);
                if (order == null)
                {
                    return StatusCode(500, new { error = _localization.GetMessage("acme.orderCreateFailed") });
                }
                return CreatedAtAction(nameof(GetCertificateOrder), new { orderId = order.Id }, order);
            }
            catch (Exception ex)
            {
                var domains = new List<string>();
                if (!string.IsNullOrEmpty(request.Domain))
                    domains.Add(request.Domain);
                if (request.AlternativeNames != null)
                    domains.AddRange(request.AlternativeNames);

                _logger.LogError(ex, "申请证书失败: {Domains}", string.Join(",", domains));
                return StatusCode(500, new { message = _localization.GetMessage("acme.applyCertificateFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 获取证书列表（兼容前端API调用）
        /// </summary>
        [HttpGet("certificates")]
        public async Task<ActionResult<object>> GetCertificates(
            [FromQuery] string? accountId = null, 
            [FromQuery] string? status = null, 
            [FromQuery] string? domain = null,
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 20)
        {
            try
            {
                _logger.LogDebug("GetCertificates: accountId={AccountId}, status={Status}, domain={Domain}, page={Page}, pageSize={PageSize}", 
                    accountId, status, domain, page, pageSize);

                // 获取关联的账户信息以提取Email和Provider
                var accounts = await _acmeService.GetAccountsAsync();
                var accountDict = accounts.ToDictionary(a => a.Id, a => a);

                var certificates = new List<object>();

                // 1. 从证书订单集合获取未完成的证书订单
                _logger.LogInformation("从证书订单集合获取未完成的证书订单");
                var ordersCollection = _dbContext.GetCollection<AcmeCertificateOrder>("acme_orders");
                var allOrders = ordersCollection.FindAll().ToList();
                _logger.LogInformation("数据库中查询到 {count} 个证书订单", allOrders.Count);

                // 过滤出没有对应证书记录的订单（即未成功下载的订单）
                var certificatesCollection = _dbContext.GetCollection<DockerPanel.API.Services.Acme.CertificateRecord>("certificates");
                var allCertificates = certificatesCollection.FindAll().ToList();
                var completedOrderIds = allCertificates.Select(c => c.OrderId).Where(id => !string.IsNullOrEmpty(id)).ToHashSet();

                var incompleteOrders = allOrders.Where(o => !completedOrderIds.Contains(o.Id)).ToList();

                // 如果有账户ID过滤条件，应用过滤
                if (!string.IsNullOrEmpty(accountId))
                {
                    incompleteOrders = incompleteOrders.Where(o => o.AccountId == accountId).ToList();
                }

                _logger.LogInformation("过滤后返回 {count} 个未完成订单", incompleteOrders.Count);

                // 转换未完成订单为证书格式
                var orderCertificates = incompleteOrders.Select(order => {
                    var account = accountDict.TryGetValue(order.AccountId, out var acc) ? acc : null;

                    // 安全处理：只返回DNS配置字段名，不返回敏感值
                    var dnsCredentials = order.Metadata != null && order.Metadata.TryGetValue("dnsCredentials", out var dnsCreds) ? dnsCreds as Dictionary<string, object> : null;
                    var dnsConfigFields = dnsCredentials?.Keys.ToList() ?? new List<string>();

                    // 计算状态：先检查订单状态，再检查过期时间
                    var statusNow = DateTime.UtcNow;
                    var daysUntilExpiry = order.ExpiresAt.HasValue ? (int)(order.ExpiresAt.Value - statusNow).TotalDays : 0;
                    
                    string orderStatus;
                    var baseStatus = order.Status?.ToLower() ?? "pending";
                    
                    if (baseStatus == "pending" || baseStatus == "processing" || baseStatus == "ready")
                    {
                        orderStatus = "pending";
                    }
                    else if (baseStatus == "invalid" || baseStatus == "failed")
                    {
                        orderStatus = "failed";
                    }
                    else if (baseStatus == "cancelled")
                    {
                        orderStatus = "cancelled";
                    }
                    else if (order.ExpiresAt.HasValue && order.ExpiresAt.Value <= statusNow)
                    {
                        orderStatus = "expired";
                    }
                    else if (baseStatus == "valid")
                    {
                        orderStatus = daysUntilExpiry <= _certificateSettings.ExpiringSoonDays ? "expiring" : "valid";
                    }
                    else
                    {
                        orderStatus = "pending";
                    }

                    return new
                    {
                        id = order.Id,
                        name = order.Domains?.FirstOrDefault() ?? "Unknown",
                        domain = order.Domains?.FirstOrDefault() ?? "Unknown",
                        domains = order.Domains ?? new List<string>(),
                        status = orderStatus,
                        issuer = "Let's Encrypt",
                        subject = order.Domains?.FirstOrDefault() ?? "Unknown",
                        acmeProvider = account?.Provider ?? "letsencrypt",
                        provider = account?.Provider ?? "letsencrypt",
                        challengeType = order.Metadata != null && order.Metadata.TryGetValue("challengeType", out var ct) ? ct?.ToString() ?? "http-01" : "http-01",
                        dnsProvider = order.Metadata != null && order.Metadata.TryGetValue("dnsProvider", out var dp) ? dp?.ToString() : null,
                        dnsConfigFields = dnsConfigFields,
                        autoRenew = false,
                        isAutoRenewal = false,
                        createdAt = order.CreatedAt,
                        updatedAt = order.CreatedAt,
                        issuedAt = order.CompletedAt,
                        expiresAt = order.ExpiresAt,
                        daysUntilExpiry = order.ExpiresAt.HasValue ?
                            (int)((order.ExpiresAt.Value - DateTime.UtcNow).TotalDays) : 0,
                        serialNumber = string.Empty,
                        fingerprint = string.Empty,
                        email = account?.Email,
                        description = $"Certificate for {string.Join(", ", order.Domains ?? new List<string>())}",
                        logs = new object[0],
                        error = order.Error
                    };
                }).ToList();

                certificates.AddRange(orderCertificates);

                // 2. 从证书记录集合获取已成功下载的证书
                _logger.LogInformation("从证书记录集合获取已成功下载的证书");
                _logger.LogInformation("数据库中查询到 {count} 个证书记录", allCertificates.Count);

                var completedCertificates = allCertificates;
                if (!string.IsNullOrEmpty(accountId))
                {
                    completedCertificates = completedCertificates.Where(c => c.AccountId == accountId).ToList();
                }

                var finalCertificates = completedCertificates.Select(cert => {
                    var account = accountDict.TryGetValue(cert.AccountId, out var acc2) ? acc2 : null;
                    
                    // 根据证书过期时间计算状态（优先检查过期时间，而不是依赖存储的状态）
                    var statusNow = DateTime.UtcNow;
                    var daysUntilExpiry = (int)(cert.ExpiresAt - statusNow).TotalDays;
                    
                    string certStatus;
                    if (cert.Status?.ToLower() == "revoked")
                    {
                        certStatus = "revoked";
                    }
                    else if (cert.ExpiresAt <= statusNow)
                    {
                        certStatus = "expired";
                    }
                    else if (daysUntilExpiry <= _certificateSettings.ExpiringSoonDays)
                    {
                        certStatus = "expiring"; // 即将过期
                    }
                    else
                    {
                        certStatus = "valid";
                    }

                    // 安全处理：只返回DNS配置字段名，不返回敏感值
                    var dnsCredentials = cert.Metadata != null && cert.Metadata.TryGetValue("dnsCredentials", out var dc) ? dc as Dictionary<string, object> : null;
                    var dnsConfigFields = dnsCredentials?.Keys.ToList() ?? new List<string>();

                    return new
                    {
                        id = cert.Id.ToString(),
                        name = cert.Name,
                        domain = cert.Domains?.FirstOrDefault() ?? "Unknown",
                        domains = cert.Domains ?? new List<string>(),
                        status = certStatus,
                        issuer = cert.Issuer,
                        subject = cert.Domains?.FirstOrDefault() ?? "Unknown",
                        acmeProvider = account?.Provider ?? "letsencrypt",
                        provider = account?.Provider ?? "letsencrypt",
                        challengeType = cert.Metadata != null && cert.Metadata.TryGetValue("challengeType", out var ct2) ? ct2?.ToString() ?? "http-01" : "http-01",
                        dnsProvider = cert.Metadata != null && cert.Metadata.TryGetValue("dnsProvider", out var dp2) ? dp2?.ToString() : null,
                        dnsConfigFields = dnsConfigFields,
                        autoRenew = cert.AutoRenewalEnabled,
                        isAutoRenewal = cert.AutoRenewalEnabled,
                        createdAt = cert.CreatedAt,
                        updatedAt = cert.CreatedAt,
                        issuedAt = cert.IssuedAt,
                        expiresAt = cert.ExpiresAt,
                        daysUntilExpiry = daysUntilExpiry,
                        serialNumber = cert.SerialNumber,
                        fingerprint = cert.Fingerprint,
                        email = account?.Email,
                        description = $"Certificate for {string.Join(", ", cert.Domains ?? new List<string>())}",
                        logs = new object[0],
                        error = string.Empty
                    };
                });

                certificates.AddRange(finalCertificates);

                _logger.LogInformation("总共返回 {count} 个证书记录（{orderCount} 个订单 + {certCount} 个已完成证书）",
                    certificates.Count, orderCertificates.Count(), finalCertificates.Count());

                // 按创建时间倒序排列
                certificates = certificates.OrderByDescending(c => ((dynamic)c).createdAt).ToList();

                // 应用高级过滤
                if (!string.IsNullOrEmpty(status))
                {
                    _logger.LogInformation("应用状态过滤: {Status}", status);
                    certificates = certificates.Where(c => ((dynamic)c).status == status).ToList();
                }

                if (!string.IsNullOrEmpty(domain))
                {
                    _logger.LogInformation("应用域名过滤: {Domain}", domain);
                    certificates = certificates.Where(c => 
                    {
                        var domains = ((dynamic)c).domains as List<string>;
                        return domains != null && domains.Any(d => d.Contains(domain, StringComparison.OrdinalIgnoreCase));
                    }).ToList();
                }

                // 应用分页
                var totalItems = certificates.Count;
                var pagedCertificates = certificates.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                return Ok(new
                {
                    items = pagedCertificates,
                    total = totalItems,
                    page,
                    pageSize
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取证书列表失败");
                return StatusCode(500, new { message = _localization.GetMessage("acme.certificatesFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 获取证书订单列表
        /// </summary>
        [HttpGet("certificates/orders")]
        public async Task<ActionResult<IEnumerable<AcmeCertificateOrder>>> GetCertificateOrders([FromQuery] string? accountId = null)
        {
            try
            {
                var orders = await _acmeService.GetCertificateOrdersAsync(accountId);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取证书订单列表失败");
                return StatusCode(500, new { message = _localization.GetMessage("acme.ordersFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 根据ID获取证书订单
        /// </summary>
        [HttpGet("certificates/orders/{orderId}")]
        public async Task<ActionResult<AcmeCertificateOrder>> GetCertificateOrder(string orderId)
        {
            try
            {
                var order = await _acmeService.GetCertificateOrderAsync(orderId);
                if (order == null)
                {
                    return NotFound(new { message = _localization.GetMessage("certificate.orderNotFound") });
                }
                return Ok(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取证书订单失败: {OrderId}", orderId);
                return StatusCode(500, new { message = _localization.GetMessage("acme.getOrderFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 取消证书申请
        /// </summary>
        [HttpPost("certificates/orders/{orderId}/cancel")]
        public async Task<ActionResult> CancelCertificateOrder(string orderId)
        {
            try
            {
                // 获取证书订单 - 同时支持证书ID和订单ID
                var ordersCollection = _dbContext.GetCollection<AcmeCertificateOrder>("acme_orders");
                var order = ordersCollection.FindOne(x => x.Id == orderId || x.CertificateId == orderId);

                if (order == null)
                {
                    return NotFound(new { message = _localization.GetMessage("certificate.orderNotFound") });
                }

                // 更新订单状态为已取消
                order.Status = "cancelled";
                order.Error = _localization.GetMessage("acme.userCancelled");
                ordersCollection.Update(order);

                // 取消相关的进度跟踪
                var progressService = HttpContext.RequestServices.GetRequiredService<ICertificateProgressService>();
                var progress = await progressService.GetProgressByCertificateIdAsync(order.Id);
                if (progress != null)
                {
                    await progressService.MarkAsFailedAsync(progress.ProgressId, _localization.GetMessage("acme.userCancelled"));
                }

                _logger.LogInformation("用户取消证书申请: {OrderId}", order.Id);

                return Ok(new { message = _localization.GetMessage("certificate.cancelled") });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取消证书申请失败: {OrderId}", orderId);
                return StatusCode(500, new { message = _localization.GetMessage("acme.orderCancelFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 完成域名验证挑战
        /// </summary>
        [HttpPost("certificates/orders/{orderId}/challenges/{authorizationId}/complete")]
        public async Task<ActionResult<AcmeChallengeResult>> CompleteChallenge(
            string orderId,
            string authorizationId,
            [FromBody] CompleteChallengeRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _acmeService.CompleteChallengeAsync(orderId, authorizationId, request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "完成挑战验证失败: OrderId: {OrderId}, AuthId: {AuthId}", orderId, authorizationId);
                return StatusCode(500, new { message = _localization.GetMessage("acme.challengeCompleteFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 查询挑战验证状态
        /// </summary>
        [HttpGet("certificates/orders/{orderId}/challenges/{authorizationId}/status")]
        public async Task<ActionResult<AcmeChallengeResult>> CheckChallengeStatus(
            string orderId,
            string authorizationId,
            [FromQuery] string challengeType = "http-01")
        {
            try
            {
                _logger.LogInformation("查询挑战状态: OrderId={OrderId}, AuthorizationId={AuthorizationId}, ChallengeType={ChallengeType}",
                    orderId, authorizationId, challengeType);

                var result = await _acmeService.CheckChallengeStatusAsync(orderId, authorizationId, challengeType);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查询挑战状态失败: OrderId={OrderId}, AuthorizationId={AuthorizationId}", orderId, authorizationId);
                return StatusCode(500, new { message = _localization.GetMessage("acme.challengeStatusFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 下载证书（从ACME服务器下载并保存）
        /// </summary>
        [HttpPost("certificates/orders/{orderId}/download")]
        public async Task<ActionResult<AcmeCertificateData>> DownloadCertificateFromAcme(string orderId)
        {
            try
            {
                var certificateData = await _acmeService.DownloadCertificateAsync(orderId);

                // 保存证书到数据库
                await SaveCertificateToDatabase(orderId, certificateData);

                return Ok(certificateData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "下载证书失败: {OrderId}", orderId);
                return StatusCode(500, new { message = _localization.GetMessage("acme.downloadFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 下载已保存的证书（ZIP包：cert.pem、privkey.pem、fullchain.pem）
        /// </summary>
        [HttpGet("certificates/orders/{orderId}/download")]
        public async Task<IActionResult> DownloadCertificateZip(string orderId)
        {
            try
            {
                _logger.LogInformation("下载证书ZIP包请求: {OrderId}", orderId);

                // 从数据库获取已保存的证书
                var certificatesCollection = _dbContext.GetCollection<DockerPanel.API.Services.Acme.CertificateRecord>("certificates");
                var certificate = certificatesCollection.FindOne(c => c.OrderId == orderId);

                if (certificate == null)
                {
                    _logger.LogWarning("证书未找到: {OrderId}，尝试通过证书ID查询", orderId);

                    // 尝试直接作为证书ID查询
                    certificate = certificatesCollection.FindById(orderId);

                    if (certificate == null)
                    {
                        return NotFound(new { message = _localization.GetMessage("certificate.notFoundConfirm") });
                    }
                }

                // 检查证书数据
                if (string.IsNullOrEmpty(certificate.CertificateData))
                {
                    _logger.LogWarning("证书数据为空: {CertificateId}", certificate.Id);
                    return BadRequest(new { message = _localization.GetMessage("certificate.dataEmpty") });
                }

                // 获取域名作为文件名前缀
                var domainName = certificate.Domains?.FirstOrDefault()?.Replace("*.", "wildcard_") ?? "certificate";
                var fileNamePrefix = $"{domainName}_{DateTime.UtcNow:yyyyMMdd}";

                // 创建 ZIP 包
                using var memoryStream = new MemoryStream();
                using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
                {
                    // cert.pem - 证书
                    var certEntry = archive.CreateEntry("cert.pem");
                    using (var entryStream = certEntry.Open())
                    using (var writer = new StreamWriter(entryStream))
                    {
                        await writer.WriteAsync(certificate.CertificateData);
                        await writer.FlushAsync();
                    }

                    // privkey.pem - 私钥
                    if (!string.IsNullOrEmpty(certificate.PrivateKeyData))
                    {
                        var keyEntry = archive.CreateEntry("privkey.pem");
                        using (var keyEntryStream = keyEntry.Open())
                        using (var keyWriter = new StreamWriter(keyEntryStream))
                        {
                            await keyWriter.WriteAsync(certificate.PrivateKeyData);
                            await keyWriter.FlushAsync();
                        }
                    }

                    // fullchain.pem - 完整证书链
                    if (!string.IsNullOrEmpty(certificate.CertificateChain))
                    {
                        var chainEntry = archive.CreateEntry("fullchain.pem");
                        using (var chainEntryStream = chainEntry.Open())
                        using (var chainWriter = new StreamWriter(chainEntryStream))
                        {
                            await chainWriter.WriteAsync(certificate.CertificateChain);
                            await chainWriter.FlushAsync();
                        }
                    }
                    else
                    {
                        // 如果没有单独的 chain，用 cert 作为 fullchain
                        var chainEntry = archive.CreateEntry("fullchain.pem");
                        using (var chainEntryStream = chainEntry.Open())
                        using (var chainWriter = new StreamWriter(chainEntryStream))
                        {
                            await chainWriter.WriteAsync(certificate.CertificateData);
                            await chainWriter.FlushAsync();
                        }
                    }
                }

                memoryStream.Position = 0;
                var zipBytes = memoryStream.ToArray();
                var zipFileName = $"{fileNamePrefix}.zip";

                _logger.LogInformation("证书下载成功: {OrderId}, 文件大小: {Size} bytes", orderId, zipBytes.Length);
                return File(zipBytes, "application/zip", zipFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "下载证书ZIP包失败: {OrderId}", orderId);
                return StatusCode(500, new { message = _localization.GetMessage("acme.downloadFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 通过证书ID或订单ID下载证书（ZIP包：cert.pem、privkey.pem、fullchain.pem）
        /// </summary>
        [HttpGet("certificates/{id}/download")]
        public async Task<IActionResult> DownloadCertificateById(string id)
        {
            try
            {
                _logger.LogInformation("通过证书ID下载证书ZIP包请求: {CertificateId}", id);

                // 从数据库获取证书（支持证书ID或订单ID）
                var certificatesCollection = _dbContext.GetCollection<DockerPanel.API.Services.Acme.CertificateRecord>("certificates");
                var certificate = certificatesCollection.FindOne(c => c.Id == id || c.OrderId == id);

                if (certificate == null)
                {
                    _logger.LogWarning("证书未找到: {CertificateId}", id);
                    return NotFound(new { message = _localization.GetMessage("certificate.notFound") });
                }

                // 检查证书数据
                if (string.IsNullOrEmpty(certificate.CertificateData))
                {
                    _logger.LogWarning("证书数据为空: {CertificateId}", id);
                    return BadRequest(new { message = _localization.GetMessage("certificate.dataEmpty") });
                }

                // 获取域名作为文件名前缀
                var domainName = certificate.Domains?.FirstOrDefault()?.Replace("*.", "wildcard_") ?? "certificate";
                var fileNamePrefix = $"{domainName}_{DateTime.UtcNow:yyyyMMdd}";

                // 创建 ZIP 包
                using var memoryStream = new MemoryStream();
                using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
                {
                    // cert.pem - 证书
                    var certEntry = archive.CreateEntry("cert.pem");
                    using (var entryStream = certEntry.Open())
                    using (var writer = new StreamWriter(entryStream))
                    {
                        await writer.WriteAsync(certificate.CertificateData);
                        await writer.FlushAsync();
                    }

                    // privkey.pem - 私钥
                    if (!string.IsNullOrEmpty(certificate.PrivateKeyData))
                    {
                        var keyEntry = archive.CreateEntry("privkey.pem");
                        using (var keyEntryStream = keyEntry.Open())
                        using (var keyWriter = new StreamWriter(keyEntryStream))
                        {
                            await keyWriter.WriteAsync(certificate.PrivateKeyData);
                            await keyWriter.FlushAsync();
                        }
                    }

                    // fullchain.pem - 完整证书链
                    if (!string.IsNullOrEmpty(certificate.CertificateChain))
                    {
                        var chainEntry = archive.CreateEntry("fullchain.pem");
                        using (var chainEntryStream = chainEntry.Open())
                        using (var chainWriter = new StreamWriter(chainEntryStream))
                        {
                            await chainWriter.WriteAsync(certificate.CertificateChain);
                            await chainWriter.FlushAsync();
                        }
                    }
                    else
                    {
                        // 如果没有单独的 chain，用 cert 作为 fullchain
                        var chainEntry = archive.CreateEntry("fullchain.pem");
                        using (var chainEntryStream = chainEntry.Open())
                        using (var chainWriter = new StreamWriter(chainEntryStream))
                        {
                            await chainWriter.WriteAsync(certificate.CertificateData);
                            await chainWriter.FlushAsync();
                        }
                    }
                }

                memoryStream.Position = 0;
                var zipBytes = memoryStream.ToArray();
                var zipFileName = $"{fileNamePrefix}.zip";

                _logger.LogInformation("证书下载成功: {CertificateId}, 文件大小: {Size} bytes", id, zipBytes.Length);
                return File(zipBytes, "application/zip", zipFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "下载证书ZIP包失败: {CertificateId}", id);
                return StatusCode(500, new { message = _localization.GetMessage("acme.downloadFailed"), error = ex.Message });
            }
        }

        private async Task SaveCertificateToDatabase(string orderId, AcmeCertificateData certificateData)
        {
            try
            {
                // 获取订单信息
                var order = await _acmeService.GetCertificateOrderAsync(orderId);
                if (order == null)
                {
                    _logger.LogWarning("无法找到订单信息: {OrderId}", orderId);
                    return;
                }

                // 解析证书以获取详细信息
                var certBytes = System.Text.Encoding.UTF8.GetBytes(certificateData.Certificate);
                var cert = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificate(certBytes);

                // 从订单 metadata 中读取 autoRenew 设置
                var autoRenew = order.Metadata != null && order.Metadata.TryGetValue("autoRenew", out var ar) && ar is bool b && b;

                var certificatesCollection = _dbContext.GetCollection<DockerPanel.API.Services.Acme.CertificateRecord>("certificates");
                var existingCertificate = certificatesCollection.FindAll().FirstOrDefault(c => c.OrderId == orderId);

                // 创建或更新证书记录（使用证书实际的过期时间）
                var certificateRecord = new DockerPanel.API.Services.Acme.CertificateRecord
                {
                    Id = existingCertificate?.Id ?? string.Empty,
                    Name = certificateData.Domains.FirstOrDefault() ?? "Unknown",
                    Type = "SSL",
                    Domains = certificateData.Domains,
                    Status = "Active",
                    IssuedAt = cert.NotBefore, // 使用证书实际时间
                    ExpiresAt = cert.NotAfter,  // 使用证书实际过期时间（90天）
                    Issuer = certificateData.Issuer ?? string.Empty,
                    CertificateData = certificateData.Certificate ?? string.Empty,
                    PrivateKeyData = certificateData.PrivateKey ?? string.Empty,
                    CertificateChain = certificateData.CertificateChain ?? string.Empty,
                    KeyAlgorithm = "ECDSA",
                    KeySize = 256,
                    SignatureAlgorithm = cert.SignatureAlgorithm?.FriendlyName ?? "SHA256withECDSA",
                    SerialNumber = certificateData.SerialNumber ?? string.Empty,
                    Fingerprint = certificateData.CertificateFingerprint ?? string.Empty,
                    AccountId = order.AccountId,
                    OrderId = orderId,
                    AutoRenewalEnabled = autoRenew, // 从订单 metadata 读取
                    Metadata = order.Metadata ?? new Dictionary<string, object>(), // 保存完整的 metadata
                    CreatedAt = existingCertificate?.CreatedAt ?? DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // 保存到数据库
                if (existingCertificate == null)
                {
                    certificatesCollection.Insert(certificateRecord);
                }
                else
                {
                    certificatesCollection.Update(certificateRecord);
                }

                // 更新订单状态
                order.Status = "completed";
                order.CompletedAt = DateTime.UtcNow;
                order.CertificateId = certificateRecord.Id.ToString();

                var ordersCollection = _dbContext.GetCollection<AcmeCertificateOrder>("acme_orders");
                ordersCollection.Update(order);

                _logger.LogInformation("证书已保存到数据库: OrderId={OrderId}, CertificateId={CertificateId}, Subject={Subject}, AutoRenew={AutoRenew}, ExpiresAt={ExpiresAt}",
                    orderId, certificateRecord.Id, certificateData.Subject, autoRenew, cert.NotAfter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存证书到数据库失败: OrderId={OrderId}", orderId);
                throw;
            }
        }

        /// <summary>
        /// 获取待处理的验证挑战
        /// </summary>
        [HttpGet("certificates/orders/{orderId}/challenges/pending")]
        public async Task<ActionResult<IEnumerable<AcmeChallenge>>> GetPendingChallenges(string orderId)
        {
            try
            {
                var challenges = await _acmeService.GetPendingChallengesAsync(orderId);
                return Ok(challenges);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取待处理挑战失败: {OrderId}", orderId);
                return StatusCode(500, new { message = _localization.GetMessage("acme.pendingChallengesFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 续期证书
        /// </summary>
        [HttpPost("certificates/{certificateId}/renew")]
        public async Task<ActionResult<AcmeCertificateOrder>> RenewCertificate(string certificateId)
        {
            _logger.LogInformation("收到续期请求，证书ID: {CertificateId}", certificateId);
            try
            {
                // 直接使用 CertesAcmeService 进行真正的续期
                var renewalOrder = await _acmeService.RenewCertificateAsync(certificateId);
                _logger.LogInformation("续期成功，新订单ID: {OrderId}", renewalOrder.Id);
                return CreatedAtAction(nameof(GetCertificateOrder), new { orderId = renewalOrder.Id }, renewalOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "续期证书失败: {CertificateId}", certificateId);
                return StatusCode(500, new { message = _localization.GetMessage("acme.renewFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 启用证书自动续期
        /// </summary>
        [HttpPost("certificates/{certificateId}/auto-renewal/enable")]
        public async Task<ActionResult> EnableAutoRenewal(string certificateId)
        {
            _logger.LogInformation("启用证书自动续期: {CertificateId}", certificateId);
            try
            {
                // 查找证书记录
                var certificatesCollection = _dbContext.GetCollection<DockerPanel.API.Services.Acme.CertificateRecord>("certificates");
                var certificate = certificatesCollection.FindOne(c => c.Id == certificateId || c.OrderId == certificateId);
                
                if (certificate != null)
                {
                    certificate.AutoRenewalEnabled = true;
                    certificate.UpdatedAt = DateTime.UtcNow;
                    certificatesCollection.Update(certificate);
                    _logger.LogInformation("已启用证书自动续期: {CertificateId}", certificateId);
                    return Ok(new { success = true, message = _localization.GetMessage("certificate.autoRenewEnabled") });
                }

                // 检查是否是订单ID
                var ordersCollection = _dbContext.GetCollection<AcmeCertificateOrder>("acme_orders");
                var order = ordersCollection.FindOne(o => o.Id == certificateId);
                
                if (order != null)
                {
                    // 更新订单的 metadata
                    order.Metadata ??= new Dictionary<string, object>();
                    order.Metadata["autoRenew"] = true;
                    ordersCollection.Update(order);
                    _logger.LogInformation("已启用订单自动续期: {OrderId}", certificateId);
                    return Ok(new { success = true, message = _localization.GetMessage("certificate.autoRenewEnabled") });
                }

                return NotFound(new { message = _localization.GetMessage("certificate.notFound") });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启用自动续期失败: {CertificateId}", certificateId);
                return StatusCode(500, new { message = _localization.GetMessage("acme.autoRenewEnableFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 禁用证书自动续期
        /// </summary>
        [HttpPost("certificates/{certificateId}/auto-renewal/disable")]
        public async Task<ActionResult> DisableAutoRenewal(string certificateId)
        {
            _logger.LogInformation("禁用证书自动续期: {CertificateId}", certificateId);
            try
            {
                // 查找证书记录
                var certificatesCollection = _dbContext.GetCollection<DockerPanel.API.Services.Acme.CertificateRecord>("certificates");
                var certificate = certificatesCollection.FindOne(c => c.Id == certificateId || c.OrderId == certificateId);
                
                if (certificate != null)
                {
                    certificate.AutoRenewalEnabled = false;
                    certificate.UpdatedAt = DateTime.UtcNow;
                    certificatesCollection.Update(certificate);
                    _logger.LogInformation("已禁用证书自动续期: {CertificateId}", certificateId);
                    return Ok(new { success = true, message = _localization.GetMessage("certificate.autoRenewDisabled") });
                }

                // 检查是否是订单ID
                var ordersCollection = _dbContext.GetCollection<AcmeCertificateOrder>("acme_orders");
                var order = ordersCollection.FindOne(o => o.Id == certificateId);
                
                if (order != null)
                {
                    // 更新订单的 metadata
                    order.Metadata ??= new Dictionary<string, object>();
                    order.Metadata["autoRenew"] = false;
                    ordersCollection.Update(order);
                    _logger.LogInformation("已禁用订单自动续期: {OrderId}", certificateId);
                    return Ok(new { success = true, message = _localization.GetMessage("certificate.autoRenewDisabled") });
                }

                return NotFound(new { message = _localization.GetMessage("certificate.notFound") });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "禁用自动续期失败: {CertificateId}", certificateId);
                return StatusCode(500, new { message = _localization.GetMessage("acme.autoRenewDisableFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 重试失败的证书申请
        /// </summary>
        [HttpPost("certificates/{certificateId}/retry")]
        public async Task<ActionResult<AcmeCertificateOrder>> RetryCertificate(string certificateId)
        {
            _logger.LogInformation("收到重试请求，证书ID: {CertificateId}", certificateId);
            try
            {
                // 获取失败的证书订单
                var ordersCollection = _dbContext.GetCollection<AcmeCertificateOrder>("acme_orders");
                var failedOrder = ordersCollection.FindOne(x => x.Id == certificateId);

                if (failedOrder == null)
                {
                    return NotFound(new { message = _localization.GetMessage("acme.orderNotFound") });
                }

                if (failedOrder.Status != "failed" && failedOrder.Status != "pending")
                {
                    return BadRequest(new { message = _localization.GetMessage("certificate.canOnlyRetryFailed") });
                }

                _logger.LogInformation("重试证书申请: {Domains}, 账户: {AccountId}",
                    string.Join(", ", failedOrder.Domains ?? new List<string>()), failedOrder.AccountId);

                // 重置订单状态为pending
                failedOrder.Status = "pending";
                failedOrder.Error = null;
                ordersCollection.Update(failedOrder);

                // 异步触发证书申请流程，不等待完成
                _ = Task.Run(async () =>
                {
                    try
                    {
                        _logger.LogInformation("开始异步重试证书申请流程: {CertificateId}", certificateId);
                        var retryOrder = await _acmeService.RetryCertificateOrderAsync(certificateId);
                        _logger.LogInformation("异步重试成功，订单ID: {OrderId}", retryOrder.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "异步重试证书申请失败: {CertificateId}", certificateId);
                    }
                });

                _logger.LogInformation("重试请求已提交，将在后台处理: {CertificateId}", certificateId);
                return Ok(new { message = _localization.GetMessage("certificate.retrySubmitted"), certificateId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重试证书申请失败: {CertificateId}", certificateId);
                return StatusCode(500, new { message = _localization.GetMessage("acme.retryFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 撤销证书
        /// </summary>
        [HttpPost("certificates/{certificateId}/revoke")]
        public async Task<ActionResult> RevokeCertificate(string certificateId, [FromBody] RevokeCertificateRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _acmeService.RevokeCertificateAsync(certificateId, request);
                if (!result)
                {
                    return NotFound(new { message = _localization.GetMessage("certificate.revokeFailed") });
                }
                return Ok(new { message = _localization.GetMessage("certificate.revokeSuccess") });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "撤销证书失败: {CertificateId}", certificateId);
                return StatusCode(500, new { message = _localization.GetMessage("acme.revokeFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 删除证书
        /// </summary>
        [HttpDelete("certificates/{certificateId}")]
        public ActionResult<CertificateDeletionResult> DeleteCertificate(string certificateId, [FromQuery] bool force = false)
        {
            try
            {
                _logger.LogInformation("删除证书请求: {CertificateId}, Force: {Force}", certificateId, force);

                var deletionSteps = new List<string>();
                var errors = new List<string>();
                var warnings = new List<string>();

                // 与GetCertificates保持一致，查询两个集合
                // 1. 检查acme_orders集合（未完成订单）
                var ordersCollection = _dbContext.GetCollection<AcmeCertificateOrder>("acme_orders");
                var certificate = ordersCollection.FindOne(x => x.Id == certificateId);

                // 2. 检查certificates集合（已完成证书）
                var certificatesCollection = _dbContext.GetCollection<DockerPanel.API.Services.Acme.CertificateRecord>("certificates");
                var completedCertificate = certificatesCollection.FindOne(x => x.Id == certificateId);

                _logger.LogInformation("查找结果: OrdersFound={OrdersFound}, CertificatesFound={CertificatesFound}",
                    certificate != null, completedCertificate != null);

                if (certificate == null && completedCertificate == null)
                {
                    var message = force ? _localization.GetMessage("acme.certificateForceDeleted") : _localization.GetMessage("certificate.notFound");
                    deletionSteps.Add(force ? _localization.GetMessage("acme.forceModeSkipCheck") : _localization.GetMessage("acme.checkCertificateExists"));

                    if (force)
                    {
                        return Ok(new CertificateDeletionResult
                        {
                            Success = true,
                            Message = message,
                            CertificateId = certificateId,
                            DeletedAt = DateTime.UtcNow,
                            DeletionSteps = deletionSteps,
                            Errors = errors,
                            Warnings = warnings,
                            DeletionDetails = new Dictionary<string, object>
                            {
                                ["ForceDeleted"] = true,
                                ["CertificateExisted"] = false
                            }
                        });
                    }
                    else
                    {
                        // 证书不存在且非强制删除，返回404
                        return NotFound(new CertificateDeletionResult
                        {
                            Success = false,
                            Message = message,
                            CertificateId = certificateId,
                            DeletedAt = DateTime.UtcNow,
                            DeletionSteps = deletionSteps,
                            Errors = new List<string> { _localization.GetMessage("certificate.notFound") },
                            Warnings = warnings,
                            DeletionDetails = new Dictionary<string, object>
                            {
                                ["ForceDeleted"] = false,
                                ["CertificateExisted"] = false
                            }
                        });
                    }
                }

                // 检查证书是否正在使用中
                if (!force)
                {
                    // 检查是否有域名映射正在使用此证书
                    var domainMappingsCollection = _dbContext.GetCollection<DomainMapping>("domain_mappings");
                    var usingMappings = domainMappingsCollection.Find(m => m.CertificateId == certificateId).ToList();

                    if (usingMappings.Count > 0)
                    {
                        var usedByDomains = string.Join(", ", usingMappings.Select(m => m.Domain));
                        _logger.LogWarning("无法删除证书 {CertificateId}，被 {Count} 个域名映射使用: {Domains}",
                            certificateId, usingMappings.Count, usedByDomains);

                        return BadRequest(new CertificateDeletionResult
                        {
                            Success = false,
                            Message = _localization.GetMessage("acme.certificateInUse", $"无法删除证书，正在被 {usingMappings.Count} 个域名映射使用。请先解除域名绑定后再删除证书。"),
                            CertificateId = certificateId,
                            DeletedAt = DateTime.UtcNow,
                            DeletionSteps = deletionSteps,
                            Errors = new List<string> { _localization.GetMessage("acme.certificateUsedByDomains", $"证书正在被以下域名使用: {usedByDomains}") },
                            Warnings = warnings,
                            DeletionDetails = new Dictionary<string, object>
                            {
                                ["InUseByDomains"] = usingMappings.Select(m => m.Domain).ToList(),
                                ["MappingIds"] = usingMappings.Select(m => m.Id).ToList()
                            }
                        });
                    }

                    deletionSteps.Add(_localization.GetMessage("acme.checkUsageStatus"));
                }
                else
                {
                    // 强制删除时，先解除域名映射的证书绑定
                    var domainMappingsCollection = _dbContext.GetCollection<DomainMapping>("domain_mappings");
                    var usingMappings = domainMappingsCollection.Find(m => m.CertificateId == certificateId).ToList();

                    if (usingMappings.Count > 0)
                    {
                        foreach (var mapping in usingMappings)
                        {
                            mapping.CertificateId = null;
                            mapping.EnableSsl = false;
                            domainMappingsCollection.Update(mapping);
                            warnings.Add(_localization.GetMessage("acme.mappingCertificateUnbound", $"已解除域名映射 {mapping.Domain} 的证书绑定"));
                        }
                        _logger.LogInformation("强制删除：已解除 {Count} 个域名映射的证书绑定", usingMappings.Count);
                        deletionSteps.Add(_localization.GetMessage("acme.forceModeUnbindMappings", $"强制模式：解除 {usingMappings.Count} 个域名映射的证书绑定"));
                    }
                    else
                    {
                        deletionSteps.Add(_localization.GetMessage("acme.forceModeSkipCheck"));
                    }
                }

                // 执行删除操作
                var deletedFromOrders = 0;
                var deletedFromCertificates = 0;
                var deletedCollections = new List<string>();

                // 删除acme_orders中的记录（如果存在）
                if (certificate != null)
                {
                    deletedFromOrders = ordersCollection.DeleteMany(x => x.Id == certificateId);
                    if (deletedFromOrders > 0)
                    {
                        deletedCollections.Add("acme_orders");
                        deletionSteps.Add(_localization.GetMessage("acme.deleteFromOrdersCollection"));
                    }
                }

                // 删除certificates中的记录（如果存在）
                if (completedCertificate != null)
                {
                    deletedFromCertificates = certificatesCollection.DeleteMany(x => x.Id == certificateId);
                    if (deletedFromCertificates > 0)
                    {
                        deletedCollections.Add("certificates");
                        deletionSteps.Add(_localization.GetMessage("acme.deleteFromCertificatesCollection"));
                    }
                }

                if (deletedFromOrders > 0 || deletedFromCertificates > 0)
                {
                    deletionSteps.Add(_localization.GetMessage("acme.cleanOperationHistory"));
                    deletionSteps.Add(_localization.GetMessage("acme.cleanUsageStats"));

                    _logger.LogInformation("证书删除成功: {CertificateId}, Orders: {Orders}, Certificates: {Certificates}",
                        certificateId, deletedFromOrders, deletedFromCertificates);

                    return Ok(new CertificateDeletionResult
                    {
                        Success = true,
                        Message = force ? _localization.GetMessage("acme.certificateForceDeleted") : _localization.GetMessage("acme.certificateDeleted"),
                        CertificateId = certificateId,
                        DeletedAt = DateTime.UtcNow,
                        DeletionSteps = deletionSteps,
                        Errors = errors,
                        Warnings = warnings,
                        DeletionDetails = new Dictionary<string, object>
                        {
                            ["DeletedFromCollection"] = string.Join(", ", deletedCollections),
                            ["ForceDeleted"] = force,
                            ["CertificateExisted"] = true,
                            ["DeletedFromOrders"] = deletedFromOrders,
                            ["DeletedFromCertificates"] = deletedFromCertificates
                        }
                    });
                }
                else
                {
                    errors.Add(_localization.GetMessage("acme.dbDeleteFailed"));
                    _logger.LogWarning("证书删除失败: {CertificateId} - 数据库操作失败", certificateId);

                    return BadRequest(new CertificateDeletionResult
                    {
                        Success = false,
                        Message = _localization.GetMessage("acme.deleteFailed"),
                        CertificateId = certificateId,
                        DeletedAt = DateTime.UtcNow,
                        DeletionSteps = deletionSteps,
                        Errors = errors,
                        Warnings = warnings
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除证书失败: {CertificateId}", certificateId);
                return StatusCode(500, new CertificateDeletionResult
                {
                    Success = false,
                    Message = _localization.GetMessage("error.serverError"),
                    CertificateId = certificateId,
                    DeletedAt = DateTime.UtcNow,
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// 获取ACME操作日志
        /// </summary>
        [HttpGet("logs")]
        public async Task<ActionResult<IEnumerable<AcmeOperationLog>>> GetOperationLogs(
            [FromQuery] string? accountId = null, [FromQuery] int limit = 100, [FromQuery] int offset = 0)
        {
            try
            {
                var logs = await _acmeService.GetOperationLogsAsync(accountId, limit, offset);
                return Ok(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取ACME操作日志失败");
                return StatusCode(500, new { message = _localization.GetMessage("acme.logsFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 检查证书到期时间
        /// </summary>
        [HttpGet("certificates/{certificateId}/expiry")]
        public async Task<ActionResult<int>> CheckCertificateExpiry(string certificateId)
        {
            try
            {
                var daysUntilExpiry = await _acmeService.CheckCertificateExpiryAsync(certificateId);
                return Ok(new { certificateId, daysUntilExpiry });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查证书到期时间失败: {CertificateId}", certificateId);
                return StatusCode(500, new { message = _localization.GetMessage("acme.expiryCheckFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 自动续期即将到期的证书
        /// </summary>
        [HttpPost("certificates/auto-renew")]
        public async Task<ActionResult<int>> AutoRenewCertificates([FromQuery] int daysBeforeExpiry = 15)
        {
            try
            {
                var renewedCount = await _acmeService.AutoRenewCertificatesAsync(daysBeforeExpiry);
                return Ok(new { renewedCount, message = _localization.GetMessage("acme.renewedCount", $"成功续期了 {renewedCount} 个证书").Replace("{0}", renewedCount.ToString()) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "自动续期证书失败");
                return StatusCode(500, new { message = _localization.GetMessage("acme.autoRenewRunFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 修复证书状态 - 清理过期的pending状态
        /// </summary>
        [HttpPost("certificates/fix-status")]
        public async Task<ActionResult<object>> FixCertificateStatus([FromQuery] string? certificateId = null)
        {
            try
            {
                var orders = await _acmeService.GetCertificateOrdersAsync();
                var ordersToUpdate = new List<string>();
                var debugInfo = new Dictionary<string, object>();

                // 如果指定了证书ID，只处理该证书
                if (!string.IsNullOrEmpty(certificateId))
                {
                    orders = orders.Where(o => o.Id == certificateId).ToList();
                    debugInfo["targetCertificateId"] = certificateId;
                    debugInfo["foundOrders"] = orders.ToList().Count;
                }

                // 获取所有进度记录用于调试
                var progressCollection = _dbContext.GetCollection<CertificateApplicationProgress>("progress_tracks");
                var allProgressRecords = progressCollection.FindAll().ToList();
                debugInfo["totalProgressRecords"] = allProgressRecords.Count;
                debugInfo["progressCertificateIds"] = allProgressRecords.Select(p => p.CertificateId).ToList();

                // 获取所有订单记录用于调试
                var orderCollection = _dbContext.GetCollection<AcmeCertificateOrder>("acme_orders");
                var allOrders = orderCollection.FindAll().ToList();
                debugInfo["totalOrdersInDb"] = allOrders.Count;
                debugInfo["allOrderIdsInDb"] = allOrders.Select(o => new {
                    id = o.Id,
                    status = o.Status,
                    domains = o.Domains,
                    // 检查数据库中的其他可能字段
                    _id = o.GetType().GetProperty("_id")?.GetValue(o)?.ToString()
                }).ToList();

                foreach (var order in orders)
                {
                    debugInfo["checkingOrderId"] = order.Id;
                    debugInfo["orderStatus"] = order.Status ?? string.Empty;

                    // 如果证书状态是pending且没有进度记录，则标记为失败
                    if (order.Status?.ToLower() == "pending")
                    {
                        // 检查是否有进度记录
                        var progressRecords = progressCollection
                            .Find(p => p.CertificateId == order.Id).ToList();

                        debugInfo["progressRecordsForOrder"] = progressRecords.Count;

                        if (!progressRecords.Any())
                        {
                            ordersToUpdate.Add(order.Id);
                            debugInfo["willUpdateOrder"] = order.Id;
                        }
                    }
                }

                var updatedCount = 0;
                foreach (var orderId in ordersToUpdate)
                {
                    // 使用LINQ查询来查找记录
                    var order = orderCollection.FindAll().FirstOrDefault(o => o.Id == orderId);

                    debugInfo[$"orderFound_{orderId}"] = order != null;

                    if (order != null)
                    {
                        debugInfo[$"originalStatus_{orderId}"] = order.Status;

                        order.Status = "failed";
                        order.Error = _localization.GetMessage("acme.orderTimeoutMarkedFailed");
                        order.Metadata["UpdatedAt"] = DateTime.UtcNow;

                        var updateResult = orderCollection.Update(order);
                        debugInfo[$"updateResult_{orderId}"] = updateResult;

                        if (updateResult > 0)
                        {
                            updatedCount++;
                            _logger.LogInformation("已将证书 {OrderId} 状态更新为失败", orderId);
                        }
                        else
                        {
                            _logger.LogWarning("更新证书 {OrderId} 状态失败", orderId);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("未找到证书订单: {OrderId}", orderId);
                    }
                }

                return Ok(new {
                    updatedCount,
                    message = _localization.GetMessage("acme.statusFixed", $"成功修复了 {updatedCount} 个证书状态").Replace("{0}", updatedCount.ToString()),
                    updatedOrders = ordersToUpdate,
                    debug = debugInfo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "修复证书状态失败");
                return StatusCode(500, new { message = _localization.GetMessage("acme.fixStatusFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 验证域名所有权
        /// </summary>
        [HttpPost("domains/verify")]
        public async Task<ActionResult<AcmeChallengeResult>> VerifyDomainOwnership(
            [FromQuery] string domain, [FromQuery] string challengeType)
        {
            try
            {
                var result = await _acmeService.VerifyDomainOwnershipAsync(domain, challengeType);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "验证域名所有权失败: {Domain}, Type: {Type}", domain, challengeType);
                return StatusCode(500, new { message = _localization.GetMessage("acme.domainValidateFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 生成CSR（证书签名请求）
        /// </summary>
        [HttpPost("csr/generate")]
        public async Task<ActionResult<string>> GenerateCsr([FromBody] GenerateCsrRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var csr = await _acmeService.GenerateCsrAsync(request.Domains, request.KeyType);
                return Ok(new { csr });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生成CSR失败");
                return StatusCode(500, new { message = _localization.GetMessage("acme.csrGenerateFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 验证证书有效性
        /// </summary>
        [HttpPost("certificates/validate")]
        public async Task<ActionResult<AcmeCertificateValidationResult>> ValidateCertificate([FromBody] ValidateCertificateRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _acmeService.ValidateCertificateAsync(request.CertificateData);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "验证证书失败");
                return StatusCode(500, new { message = _localization.GetMessage("acme.certificateValidateFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 获取账户密钥信息
        /// </summary>
        [HttpGet("accounts/{accountId}/key")]
        public async Task<ActionResult<AcmeKeyInfo>> GetAccountKeyInfo(string accountId)
        {
            try
            {
                var keyInfo = await _acmeService.GetAccountKeyInfoAsync(accountId);
                return Ok(keyInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取账户密钥信息失败: {AccountId}", accountId);
                return StatusCode(500, new { message = _localization.GetMessage("acme.keyInfoFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 生成新的账户密钥对
        /// </summary>
        [HttpPost("keys/generate")]
        public async Task<ActionResult<AcmeKeyPair>> GenerateKeyPair([FromQuery] string keyType = "rsa2048")
        {
            try
            {
                var keyPair = await _acmeService.GenerateKeyPairAsync(keyType);
                return Ok(keyPair);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生成密钥对失败");
                return StatusCode(500, new { message = _localization.GetMessage("acme.keyGenerateFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 导出账户密钥
        /// </summary>
        [HttpGet("accounts/{accountId}/key/export")]
        public async Task<ActionResult<string>> ExportAccountKey(string accountId, [FromQuery] string format = "pem")
        {
            try
            {
                var keyData = await _acmeService.ExportAccountKeyAsync(accountId, format);
                return Ok(new { keyData, format });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导出账户密钥失败: {AccountId}", accountId);
                return StatusCode(500, new { message = _localization.GetMessage("acme.keyExportFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 导入账户密钥
        /// </summary>
        [HttpPost("keys/import")]
        public async Task<ActionResult<AcmeKeyInfo>> ImportAccountKey([FromBody] ImportKeyRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var keyInfo = await _acmeService.ImportAccountKeyAsync(request.KeyData, request.Format);
                return Ok(keyInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导入账户密钥失败");
                return StatusCode(500, new { message = _localization.GetMessage("acme.keyImportFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 处理 HTTP-01 ACME 挑战请求
        /// 通过 YARP 转发的 .well-known/acme-challenge/ 请求
        /// </summary>
        [HttpGet(".well-known/acme-challenge/{token}")]
        [HttpGet("acme-challenge/{token}")]
        [AllowAnonymous]
        public async Task<ActionResult<string>> GetHttpChallenge(string token)
        {
            try
            {
                _logger.LogInformation("收到 ACME HTTP-01 挑战请求，Token: {Token}", token);

                // 从挑战存储中获取 key authorization
                var keyAuthorization = await _challengeStore.GetHttpChallengeAsync(token);

                if (!string.IsNullOrEmpty(keyAuthorization))
                {
                    _logger.LogInformation("成功返回 ACME 挑战响应，Token: {Token}", token);
                    return Content(keyAuthorization, "text/plain");
                }

                _logger.LogWarning("未找到 Token 对应的挑战数据: {Token}", token);
                return NotFound(new { error = _localization.GetMessage("acme.challengeNotFound"), token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理 ACME 挑战请求失败: {Token}", token);
                return StatusCode(500, new { error = _localization.GetMessage("error.serverError") });
            }
        }

        /// <summary>
        /// 测试端点：存储 ACME 挑战
        /// </summary>
        [HttpPost("test/store-challenge")]
        public async Task<ActionResult> StoreTestChallenge([FromBody] StoreChallengeRequest request)
        {
            try
            {
                _logger.LogInformation("存储测试挑战，Token: {Token}", request.Token);

                await _challengeStore.StoreHttpChallengeAsync(request.Token, request.KeyAuthorization, DateTime.UtcNow.AddHours(1));

                return Ok(new { message = _localization.GetMessage("acme.challengeStored"), token = request.Token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "存储测试挑战失败: {Token}", request.Token);
                return StatusCode(500, new { error = _localization.GetMessage("error.serverError") });
            }
        }

        // 辅助方法
        private async Task<string> GetOrCreateAccountIdAsync(string email, string provider)
        {
            try
            {
                // 获取现有账户
                var accounts = await _acmeService.GetAccountsAsync();
                var existingAccount = accounts.FirstOrDefault(a => a.Email == email && a.Provider == provider);

                if (existingAccount != null)
                {
                    return existingAccount.Id;
                }

                // 创建新账户
                var createRequest = new CreateAcmeAccountRequest
                {
                    Email = email,
                    Provider = provider
                };

                var newAccount = await _acmeService.CreateAccountAsync(createRequest);
                return newAccount.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取或创建ACME账户失败: {Email}, {Provider}", email, provider);
                throw;
            }
        }

        /// <summary>
        /// 转换DNS凭据为保存格式 - 确保JsonElement被正确转换为字符串值
        /// 增强版本：处理更多数据类型并确保敏感信息的安全性
        /// 修复：将嵌套字典的凭据合并到外层，使DNS提供商可以直接获取secretId和secretKey
        /// </summary>
        private Dictionary<string, object> ConvertDnsCredentialsForSave(Dictionary<string, object>? dnsCredentials)
        {
            if (dnsCredentials == null)
            {
                _logger.LogDebug("ConvertDnsCredentialsForSave: dnsCredentials 为 null");
                return new Dictionary<string, object>();
            }

            try
            {
                _logger.LogInformation("ConvertDnsCredentialsForSave: 输入类型={0}, 键数量={1}",
                    dnsCredentials.GetType().Name, dnsCredentials.Count);

                var result = new Dictionary<string, object>();

                foreach (var kvp in dnsCredentials)
                {
                    if (kvp.Value == null)
                    {
                        result[kvp.Key] = string.Empty;
                        _logger.LogDebug("ConvertDnsCredentialsForSave: 键 {Key} 的值为 null，设置为空字符串", kvp.Key);
                        continue;
                    }

                    // 如果值是字典，递归处理并将子键合并到外层字典
                    // 这样DNS提供商可以直接获取secretId和secretKey
                    ProcessDnsCredentialValue(kvp.Value, result, kvp.Key);

                    _logger.LogDebug("ConvertDnsCredentialsForSave: 处理键 {Key}，值类型: {ValueType}",
                        kvp.Key, kvp.Value.GetType().Name);
                }

                _logger.LogInformation("ConvertDnsCredentialsForSave: 转换完成，结果键数量={0}", result.Count);

                // 验证结果
                if (result.Count == 0 && dnsCredentials.Count > 0)
                {
                    _logger.LogWarning("ConvertDnsCredentialsForSave: 警告 - 输入有 {InputCount} 个键，但输出为空",
                        dnsCredentials.Count);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ConvertDnsCredentialsForSave: 转换失败，输入键数量: {Count}",
                    dnsCredentials?.Count ?? 0);

                // 返回空字典而不是 null，以避免后续处理错误
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// 处理单个DNS凭据值，递归处理嵌套字典
        /// 对于嵌套字典，将其子键直接合并到外层result字典中
        /// </summary>
        /// <param name="value">要处理的值</param>
        /// <param name="result">目标字典，嵌套字典的子键将合并到这里</param>
        /// <param name="providerKey">外层字典的键（用于日志记录）</param>
        private void ProcessDnsCredentialValue(object value, Dictionary<string, object> result, string providerKey)
        {
            try
            {
                // 如果值是JsonElement，提取其字符串值
                if (value is System.Text.Json.JsonElement jsonElement)
                {
                    switch (jsonElement.ValueKind)
                    {
                        case System.Text.Json.JsonValueKind.String:
                            var stringValue = jsonElement.GetString();
                            _logger.LogDebug("JsonElement(String): 值长度={0}", stringValue?.Length ?? 0);
                            result[providerKey] = stringValue ?? string.Empty;
                            break;

                        case System.Text.Json.JsonValueKind.Number:
                            if (jsonElement.TryGetInt32(out var intValue))
                                result[providerKey] = intValue;
                            else if (jsonElement.TryGetInt64(out var longValue))
                                result[providerKey] = longValue;
                            else if (jsonElement.TryGetDouble(out var doubleValue))
                                result[providerKey] = doubleValue;
                            else
                                result[providerKey] = jsonElement.GetDecimal();
                            break;

                        case System.Text.Json.JsonValueKind.True:
                        case System.Text.Json.JsonValueKind.False:
                            result[providerKey] = jsonElement.GetBoolean();
                            break;

                        case System.Text.Json.JsonValueKind.Object:
                            // 对象类型，递归处理每个属性
                            ProcessJsonObject(jsonElement, result);
                            break;

                        case System.Text.Json.JsonValueKind.Array:
                            // 数组类型，序列化为JSON字符串
                            try
                            {
                                var serialized = System.Text.Json.JsonSerializer.Serialize(jsonElement);
                                _logger.LogDebug("JsonElement(Array): 序列化长度={0}", serialized.Length);
                                result[providerKey] = serialized;
                            }
                            catch (Exception serializeEx)
                            {
                                _logger.LogWarning(serializeEx, "序列化JsonElement失败，使用ToString");
                                result[providerKey] = jsonElement.ToString();
                            }
                            break;

                        default:
                            result[providerKey] = jsonElement.ToString();
                            break;
                    }
                }
                else
                {
                    // 处理其他类型
                    switch (value)
                    {
                        case string str:
                            result[providerKey] = str;
                            break;
                        case int intVal:
                        case long longVal:
                        case double doubleVal:
                        case float floatVal:
                        case decimal decimalVal:
                        case bool boolVal:
                            result[providerKey] = value;
                            break;
                        case Dictionary<string, object> dict:
                            // 递归处理嵌套字典，直接合并子键到result
                            foreach (var nestedKvp in dict)
                            {
                                ProcessDnsCredentialValue(nestedKvp.Value, result, nestedKvp.Key);
                            }
                            break;
                        case System.Collections.IEnumerable enumerable and not string:
                            // 处理数组/列表
                            var list = new List<object>();
                            foreach (var item in enumerable)
                            {
                                list.Add(ProcessListItemValue(item));
                            }
                            result[providerKey] = System.Text.Json.JsonSerializer.Serialize(list);
                            break;
                        default:
                            // 其他类型转换为字符串
                            var stringValue = value.ToString();
                            _logger.LogDebug("其他类型 {0} 转换为字符串，长度: {1}",
                                value.GetType().Name, stringValue?.Length ?? 0);
                            result[providerKey] = stringValue ?? string.Empty;
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ProcessDnsCredentialValue: 处理值失败，类型: {Type}", value.GetType().Name);
                result[providerKey] = string.Empty;
            }
        }

        /// <summary>
        /// 递归处理JsonElement对象
        /// </summary>
        private void ProcessJsonObject(System.Text.Json.JsonElement jsonElement, Dictionary<string, object> result)
        {
            foreach (var property in jsonElement.EnumerateObject())
            {
                var key = property.Name;
                var value = property.Value;

                switch (value.ValueKind)
                {
                    case System.Text.Json.JsonValueKind.String:
                        result[key] = value.GetString() ?? string.Empty;
                        break;
                    case System.Text.Json.JsonValueKind.Number:
                        if (value.TryGetInt32(out var intVal))
                            result[key] = intVal;
                        else if (value.TryGetInt64(out var longVal))
                            result[key] = longVal;
                        else if (value.TryGetDouble(out var doubleVal))
                            result[key] = doubleVal;
                        else
                            result[key] = value.GetDecimal();
                        break;
                    case System.Text.Json.JsonValueKind.True:
                    case System.Text.Json.JsonValueKind.False:
                        result[key] = value.GetBoolean();
                        break;
                    case System.Text.Json.JsonValueKind.Object:
                        // 递归处理嵌套对象
                        ProcessJsonObject(value, result);
                        break;
                    case System.Text.Json.JsonValueKind.Array:
                        var list = new List<object>();
                        foreach (var item in value.EnumerateArray())
                        {
                            list.Add(ProcessListItemValue(item));
                        }
                        result[key] = System.Text.Json.JsonSerializer.Serialize(list);
                        break;
                    default:
                        result[key] = value.ToString();
                        break;
                }
            }
        }

        /// <summary>
        /// 处理列表项值
        /// </summary>
        private object ProcessListItemValue(object item)
        {
            if (item is System.Text.Json.JsonElement jsonElement)
            {
                switch (jsonElement.ValueKind)
                {
                    case System.Text.Json.JsonValueKind.String:
                        return jsonElement.GetString() ?? string.Empty;
                    case System.Text.Json.JsonValueKind.Number:
                        if (jsonElement.TryGetInt32(out var intVal))
                            return intVal;
                        if (jsonElement.TryGetInt64(out var longVal))
                            return longVal;
                        if (jsonElement.TryGetDouble(out var doubleVal))
                            return doubleVal;
                        return jsonElement.GetDecimal();
                    case System.Text.Json.JsonValueKind.True:
                    case System.Text.Json.JsonValueKind.False:
                        return jsonElement.GetBoolean();
                    case System.Text.Json.JsonValueKind.Object:
                        var dict = new Dictionary<string, object>();
                        ProcessJsonObject(jsonElement, dict);
                        return dict;
                    case System.Text.Json.JsonValueKind.Array:
                        var list = new List<object>();
                        foreach (var arrayItem in jsonElement.EnumerateArray())
                        {
                            list.Add(ProcessListItemValue(arrayItem));
                        }
                        return list;
                    default:
                        return jsonElement.ToString();
                }
            }
            return item;
        }

        
        /// <summary>
        /// 批量续期证书（即将到期的证书）
        /// </summary>
        [HttpPost("certificates/renew-batch")]
        public async Task<ActionResult<object>> RenewBatchCertificates([FromBody] BatchRenewRequest request)
        {
            try
            {
                _logger.LogDebug("RenewBatchCertificates: CertificateIds={Ids}, DaysBeforeExpiry={Days}",
                    string.Join(",", request.CertificateIds), request.DaysBeforeExpiry);

                var results = new List<RenewResult>();

                foreach (var certificateId in request.CertificateIds)
                {
                    try
                    {
                        var renewedCertificate = await _acmeService.RenewCertificateAsync(certificateId);
                        results.Add(new RenewResult
                        {
                            CertificateId = certificateId,
                            Success = true,
                            NewCertificateId = renewedCertificate.Id,
                            Message = _localization.GetMessage("acme.renewSuccess")
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "批量续期失败: {CertificateId}", certificateId);
                        results.Add(new RenewResult
                        {
                            CertificateId = certificateId,
                            Success = false,
                            Message = ex.Message
                        });
                    }
                }

                var successCount = results.Count(r => r.Success);
                var totalCount = results.Count;

                return Ok(new
                {
                    message = _localization.GetMessage("acme.batchRenewComplete", $"批量续期完成: {successCount}/{totalCount} 成功").Replace("{0}", successCount.ToString()).Replace("{1}", totalCount.ToString()),
                    results = results,
                    summary = new
                    {
                        total = totalCount,
                        success = successCount,
                        failed = totalCount - successCount
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量续期证书失败");
                return StatusCode(500, new { message = _localization.GetMessage("acme.batchRenewFailed"), error = ex.Message });
            }
        }

    // 请求模型
    public class GenerateCsrRequest
    {
        public List<string> Domains { get; set; } = new();
        public string KeyType { get; set; } = "rsa2048";
    }

    public class ValidateCertificateRequest
    {
        public string CertificateData { get; set; } = string.Empty;
    }

    public class ImportKeyRequest
    {
        public string KeyData { get; set; } = string.Empty;
        public string Format { get; set; } = "pem";
    }

    /// <summary>
    /// 证书订单请求（兼容前端格式）
    /// </summary>
    public class CertificateOrderRequest
    {
        public string Domain { get; set; } = string.Empty;
        public List<string>? AlternativeNames { get; set; }
        public string ChallengeType { get; set; } = "http-01";
        public string AcmeProvider { get; set; } = "letsencrypt";
        public string Email { get; set; } = string.Empty;
        public string? DnsProvider { get; set; }
        public Dictionary<string, object>? DnsCredentials { get; set; }
        public bool AutoRenew { get; set; } = true;
        public string? AccountId { get; set; }
        public string? AccountKey { get; set; } // 可选的直接传递账户密钥
    }

    
    /// <summary>
    /// 测试挑战存储请求
    /// </summary>
    public class StoreChallengeRequest
    {
        public string Token { get; set; } = string.Empty;
        public string KeyAuthorization { get; set; } = string.Empty;
    }

    /// <summary>
    /// 批量续期请求
    /// </summary>
    public class BatchRenewRequest
    {
        public List<string> CertificateIds { get; set; } = new();
        public int DaysBeforeExpiry { get; set; } = 30;
    }

    /// <summary>
    /// 续期结果
    /// </summary>
    public class RenewResult
    {
        public string CertificateId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? NewCertificateId { get; set; }
        public string Message { get; set; } = string.Empty;
    }
    }
}