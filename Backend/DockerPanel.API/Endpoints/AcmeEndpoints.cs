using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using DockerPanel.API.Data;
using DockerPanel.API.Models;
using DockerPanel.API.Models.Acme;
using DockerPanel.API.Services;
using DockerPanel.API.Services.Acme;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using TinyDb;

namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// ACME 证书管理 Minimal API 端点（原 AcmeController）。
    /// </summary>
    public static class AcmeEndpoints
    {
        /// <summary>
        /// 日志类别标记（静态类不能作泛型参数）。
        /// </summary>
        public sealed class LoggingTag
        {
        }

        /// <summary>
        /// 映射 ACME 相关路由。
        /// </summary>
        public static IEndpointRouteBuilder MapAcmeEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/acme").RequireAuthorization();

            group.MapGet("statistics", GetAcmeStatistics);
            group.MapGet("providers", GetProviders);
            group.MapPost("providers/{provider}/test", TestProviderConnection);
            group.MapGet("accounts", GetAccounts);
            group.MapGet("accounts/{accountId}", GetAccount);
            group.MapPost("accounts", CreateAccount);
            group.MapDelete("accounts/{accountId}", DeleteAccount);
            group.MapPost("certificates/order", OrderCertificate);
            group.MapGet("certificates", GetCertificates);
            group.MapGet("certificates/orders", GetCertificateOrders);
            group.MapGet("certificates/orders/{orderId}", GetCertificateOrder);
            group.MapPost("certificates/orders/{orderId}/cancel", CancelCertificateOrder);
            group.MapPost("certificates/orders/{orderId}/challenges/{authorizationId}/complete", CompleteChallenge);
            group.MapGet("certificates/orders/{orderId}/challenges/{authorizationId}/status", CheckChallengeStatus);
            group.MapPost("certificates/orders/{orderId}/download", DownloadCertificateFromAcme);
            group.MapGet("certificates/orders/{orderId}/download", DownloadCertificateZip);
            group.MapGet("certificates/{id}/download", DownloadCertificateById);
            group.MapGet("certificates/orders/{orderId}/challenges/pending", GetPendingChallenges);
            group.MapPost("certificates/{certificateId}/renew", RenewCertificate);
            group.MapPost("certificates/{certificateId}/auto-renewal/enable", EnableAutoRenewal);
            group.MapPost("certificates/{certificateId}/auto-renewal/disable", DisableAutoRenewal);
            group.MapPost("certificates/{certificateId}/retry", RetryCertificate);
            group.MapPost("certificates/{certificateId}/revoke", RevokeCertificate);
            group.MapDelete("certificates/{certificateId}", DeleteCertificate);
            group.MapGet("logs", GetOperationLogs);
            group.MapGet("certificates/{certificateId}/expiry", CheckCertificateExpiry);
            group.MapPost("certificates/auto-renew", AutoRenewCertificates);
            group.MapPost("certificates/fix-status", FixCertificateStatus);
            group.MapPost("domains/verify", VerifyDomainOwnership);
            group.MapPost("csr/generate", GenerateCsr);
            group.MapPost("certificates/validate", ValidateCertificate);
            group.MapGet("accounts/{accountId}/key", GetAccountKeyInfo);
            group.MapPost("keys/generate", GenerateKeyPair);
            group.MapGet("accounts/{accountId}/key/export", ExportAccountKey);
            group.MapPost("keys/import", ImportAccountKey);
            group.MapGet(".well-known/acme-challenge/{token}", GetHttpChallenge).AllowAnonymous();
            group.MapGet("acme-challenge/{token}", GetHttpChallenge).AllowAnonymous();
            group.MapPost("test/store-challenge", StoreTestChallenge);
            group.MapPost("certificates/renew-batch", RenewBatchCertificates);

            return app;
        }

        private static async Task<IResult> GetAcmeStatistics(
            IAcmeService acmeService,
            TinyDbContext dbContext,
            IOptions<CertificateSettings> certificateSettings,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                var ordersCollection = dbContext.GetCollection<AcmeCertificateOrder>("acme_orders");
                var allOrders = ordersCollection.FindAll().ToList();

                var certificatesCollection = dbContext.GetCollection<CertificateRecord>("certificates");
                var allCertificates = certificatesCollection.FindAll().ToList();

                var accounts = await acmeService.GetAccountsAsync();

                var completedOrderIds = allCertificates.Select(c => c.OrderId).Where(id => !string.IsNullOrEmpty(id)).ToHashSet();
                var incompleteOrders = allOrders.Where(o => !completedOrderIds.Contains(o.Id)).ToList();

                var now = DateTime.UtcNow;
                var renewalThreshold = now.AddDays(30);
                var settings = certificateSettings.Value;

                int validCount = 0;
                int expiringCount = 0;
                int expiredCount = 0;
                int pendingCount = 0;
                int invalidCount = 0;

                foreach (var cert in allCertificates)
                {
                    if (cert.ExpiresAt <= now)
                    {
                        expiredCount++;
                    }
                    else if (cert.ExpiresAt <= renewalThreshold)
                    {
                        expiringCount++;
                        validCount++;
                    }
                    else
                    {
                        validCount++;
                    }
                }

                foreach (var order in incompleteOrders)
                {
                    var baseStatus = order.Status?.ToLower() ?? "pending";

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
                    }
                    else if (order.ExpiresAt.HasValue && order.ExpiresAt.Value <= now)
                    {
                        expiredCount++;
                    }
                    else if (baseStatus == "valid")
                    {
                        var daysUntilExpiry = order.ExpiresAt.HasValue ? (order.ExpiresAt.Value - now).Days : 999;
                        if (daysUntilExpiry <= settings.ExpiringSoonDays)
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

                return TypedResults.Ok(new AcmeStatisticsResponse
                {
                    TotalCertificates = incompleteOrders.Count + allCertificates.Count,
                    ValidCertificates = validCount,
                    ExpiringCertificates = expiringCount,
                    ExpiredCertificates = expiredCount,
                    PendingCertificates = pendingCount,
                    InvalidCertificates = invalidCount,
                    TotalAccounts = accounts.Count(),
                    ActiveAccounts = accounts.Count(a => a.IsActive),
                    LastUpdated = now,
                    RenewalThresholdDays = settings.ExpiringSoonDays,
                    UpcomingRenewals = expiringCount
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取ACME统计信息失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("acme.statsFailed"), Message = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> GetProviders(IAcmeService acmeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var providers = await acmeService.GetProvidersAsync();
                return TypedResults.Ok(providers);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取ACME提供商列表失败");
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.providersFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> TestProviderConnection(string provider, IAcmeService acmeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var result = await acmeService.TestProviderConnectionAsync(provider);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "测试ACME提供商连接失败: {Provider}", provider);
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.testConnectionFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> GetAccounts(IAcmeService acmeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var accounts = await acmeService.GetAccountsAsync();
                return TypedResults.Ok(accounts);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取ACME账户列表失败");
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.accountsFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> GetAccount(string accountId, IAcmeService acmeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var account = await acmeService.GetAccountAsync(accountId);
                if (account == null)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Message = localization.GetMessage("certificate.accountNotFound") });
                }
                return TypedResults.Ok(account);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取ACME账户失败: {AccountId}", accountId);
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.getAccountFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> CreateAccount(CreateAcmeAccountRequest request, IAcmeService acmeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var account = await acmeService.CreateAccountAsync(request);
                return TypedResults.Created($"/api/acme/accounts/{account.Id}", account);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "创建ACME账户失败: {Email}, {Provider}", request.Email, request.Provider);
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.accountCreateFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> DeleteAccount(string accountId, IAcmeService acmeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var result = await acmeService.DeleteAccountAsync(accountId);
                if (!result)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Message = localization.GetMessage("certificate.accountNotFound") });
                }
                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("certificate.accountDeleted") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "删除ACME账户失败: {AccountId}", accountId);
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.accountDeleteFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> OrderCertificate(
            CertificateOrderRequest request,
            IAcmeService acmeService,
            TinyDbContext dbContext,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("收到证书申请请求: Domain={Domain}, AlternativeNames={AlternativeNames}, Email={Email}",
                    request.Domain,
                    request.AlternativeNames != null ? string.Join(",", request.AlternativeNames) : "null",
                    request.Email);

                var domains = new List<string>();
                if (!string.IsNullOrEmpty(request.Domain))
                {
                    domains.Add(request.Domain);
                    logger.LogInformation("添加主域名: {Domain}", request.Domain);
                }
                if (request.AlternativeNames != null && request.AlternativeNames.Count > 0)
                {
                    domains.AddRange(request.AlternativeNames);
                    logger.LogInformation("添加别名域名: {AlternativeNames}", string.Join(",", request.AlternativeNames));
                }

                logger.LogInformation("最终域名列表数量: {Count}, 域名: {Domains}", domains.Count, string.Join(",", domains));

                if (domains.Count == 0)
                {
                    logger.LogWarning("域名列表为空，返回400错误");
                    return TypedResults.BadRequest(new ApiErrorResponse { Message = localization.GetMessage("certificate.domainsEmpty") });
                }

                var existingOrders = await acmeService.GetPendingOrdersForDomainsAsync(domains);
                if (existingOrders.Any())
                {
                    var existingOrder = existingOrders.First();
                    logger.LogWarning("发现相同域名的pending订单，返回现有订单: Domain={Domain}, OrderId={OrderId}",
                        string.Join(",", existingOrder.Domains), existingOrder.Id);

                    return TypedResults.Conflict(new PendingOrderResponse
                    {
                        Message = localization.GetMessage("acme.pendingOrderExists"),
                        ExistingOrderId = existingOrder.Id,
                        Status = existingOrder.Status,
                        CreatedAt = existingOrder.CreatedAt
                    });
                }

                var acmeRequest = new AcmeCertificateRequest
                {
                    AccountId = request.AccountId ?? await GetOrCreateAccountIdAsync(acmeService, request.Email, request.AcmeProvider ?? "letsencrypt", logger),
                    Domains = domains,
                    KeyType = "RSA2048",
                    UseStaging = request.AcmeProvider != "buypass",
                    ChallengeTypes = new List<string> { request.ChallengeType ?? "http-01" },
                    CertificateValidityDays = 90,
                    AccountKey = request.AccountKey,
                    Metadata = new Dictionary<string, object>
                    {
                        ["autoRenew"] = request.AutoRenew,
                        ["email"] = request.Email,
                        ["challengeType"] = request.ChallengeType ?? string.Empty,
                        ["dnsProvider"] = request.DnsProvider ?? string.Empty,
                        ["dnsCredentials"] = ConvertDnsCredentialsForSave(request.DnsCredentials, logger),
                        ["acmeProvider"] = request.AcmeProvider ?? "letsencrypt"
                    }
                };

                var order = await acmeService.OrderCertificateAsync(acmeRequest);
                if (order == null)
                {
                    return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("acme.orderCreateFailed") }, statusCode: 500);
                }
                return TypedResults.Created($"/api/acme/certificates/orders/{order.Id}", order);
            }
            catch (Exception ex)
            {
                var domains = new List<string>();
                if (!string.IsNullOrEmpty(request.Domain))
                    domains.Add(request.Domain);
                if (request.AlternativeNames != null)
                    domains.AddRange(request.AlternativeNames);

                logger.LogError(ex, "申请证书失败: {Domains}", string.Join(",", domains));
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.applyCertificateFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> GetCertificates(
            string? accountId,
            string? status,
            string? domain,
            int page,
            int pageSize,
            IAcmeService acmeService,
            TinyDbContext dbContext,
            IOptions<CertificateSettings> certificateSettings,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogDebug("GetCertificates: accountId={AccountId}, status={Status}, domain={Domain}, page={Page}, pageSize={PageSize}",
                    accountId, status, domain, page, pageSize);

                var accounts = await acmeService.GetAccountsAsync();
                var accountDict = accounts.ToDictionary(a => a.Id, a => a);

                var certificates = new List<CertificateListItemDto>();

                logger.LogInformation("从证书订单集合获取未完成的证书订单");
                var ordersCollection = dbContext.GetCollection<AcmeCertificateOrder>("acme_orders");
                var allOrders = ordersCollection.FindAll().ToList();
                logger.LogInformation("数据库中查询到 {count} 个证书订单", allOrders.Count);

                var certificatesCollection = dbContext.GetCollection<CertificateRecord>("certificates");
                var allCertificates = certificatesCollection.FindAll().ToList();
                var completedOrderIds = allCertificates.Select(c => c.OrderId).Where(id => !string.IsNullOrEmpty(id)).ToHashSet();

                var incompleteOrders = allOrders.Where(o => !completedOrderIds.Contains(o.Id)).ToList();

                if (!string.IsNullOrEmpty(accountId))
                {
                    incompleteOrders = incompleteOrders.Where(o => o.AccountId == accountId).ToList();
                }

                logger.LogInformation("过滤后返回 {count} 个未完成订单", incompleteOrders.Count);

                var settings = certificateSettings.Value;

                var orderCertificates = incompleteOrders.Select(order => {
                    var account = accountDict.TryGetValue(order.AccountId, out var acc) ? acc : null;

                    var dnsCredentials = order.Metadata != null && order.Metadata.TryGetValue("dnsCredentials", out var dnsCreds) ? dnsCreds as Dictionary<string, object> : null;
                    var dnsConfigFields = dnsCredentials?.Keys.ToList() ?? new List<string>();

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
                        orderStatus = daysUntilExpiry <= settings.ExpiringSoonDays ? "expiring" : "valid";
                    }
                    else
                    {
                        orderStatus = "pending";
                    }

                    return new CertificateListItemDto
                    {
                        Id = order.Id,
                        Name = order.Domains?.FirstOrDefault() ?? "Unknown",
                        Domain = order.Domains?.FirstOrDefault() ?? "Unknown",
                        Domains = order.Domains ?? new List<string>(),
                        Status = orderStatus,
                        Issuer = "Let's Encrypt",
                        Subject = order.Domains?.FirstOrDefault() ?? "Unknown",
                        AcmeProvider = account?.Provider ?? "letsencrypt",
                        Provider = account?.Provider ?? "letsencrypt",
                        ChallengeType = order.Metadata != null && order.Metadata.TryGetValue("challengeType", out var ct) ? ct?.ToString() ?? "http-01" : "http-01",
                        DnsProvider = order.Metadata != null && order.Metadata.TryGetValue("dnsProvider", out var dp) ? dp?.ToString() : null,
                        DnsConfigFields = dnsConfigFields,
                        AutoRenew = order.Metadata != null && order.Metadata.TryGetValue("autoRenew", out var ar) && ar is bool b && b,
                        IsAutoRenewal = order.Metadata != null && order.Metadata.TryGetValue("autoRenew", out var ar2) && ar2 is bool b2 && b2,
                        CreatedAt = order.CreatedAt,
                        UpdatedAt = order.CreatedAt,
                        IssuedAt = order.CompletedAt,
                        ExpiresAt = order.ExpiresAt,
                        DaysUntilExpiry = order.ExpiresAt.HasValue ?
                            (int)((order.ExpiresAt.Value - DateTime.UtcNow).TotalDays) : 0,
                        SerialNumber = string.Empty,
                        Fingerprint = string.Empty,
                        Email = account?.Email,
                        Description = $"Certificate for {string.Join(", ", order.Domains ?? new List<string>())}",
                        Logs = new List<object>(),
                        Error = order.Error
                    };
                }).ToList();

                certificates.AddRange(orderCertificates);

                logger.LogInformation("从证书记录集合获取已成功下载的证书");
                logger.LogInformation("数据库中查询到 {count} 个证书记录", allCertificates.Count);

                var completedCertificates = allCertificates;
                if (!string.IsNullOrEmpty(accountId))
                {
                    completedCertificates = completedCertificates.Where(c => c.AccountId == accountId).ToList();
                }

                var finalCertificates = completedCertificates.Select(cert => {
                    var account = accountDict.TryGetValue(cert.AccountId, out var acc2) ? acc2 : null;

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
                    else if (daysUntilExpiry <= settings.ExpiringSoonDays)
                    {
                        certStatus = "expiring";
                    }
                    else
                    {
                        certStatus = "valid";
                    }

                    var dnsCredentials = cert.Metadata != null && cert.Metadata.TryGetValue("dnsCredentials", out var dc) ? dc as Dictionary<string, object> : null;
                    var dnsConfigFields = dnsCredentials?.Keys.ToList() ?? new List<string>();

                    return new CertificateListItemDto
                    {
                        Id = cert.Id.ToString(),
                        Name = cert.Name,
                        Domain = cert.Domains?.FirstOrDefault() ?? "Unknown",
                        Domains = cert.Domains ?? new List<string>(),
                        Status = certStatus,
                        Issuer = cert.Issuer,
                        Subject = cert.Domains?.FirstOrDefault() ?? "Unknown",
                        AcmeProvider = account?.Provider ?? "letsencrypt",
                        Provider = account?.Provider ?? "letsencrypt",
                        ChallengeType = cert.Metadata != null && cert.Metadata.TryGetValue("challengeType", out var ct2) ? ct2?.ToString() ?? "http-01" : "http-01",
                        DnsProvider = cert.Metadata != null && cert.Metadata.TryGetValue("dnsProvider", out var dp2) ? dp2?.ToString() : null,
                        DnsConfigFields = dnsConfigFields,
                        AutoRenew = cert.AutoRenewalEnabled,
                        IsAutoRenewal = cert.AutoRenewalEnabled,
                        CreatedAt = cert.CreatedAt,
                        UpdatedAt = cert.CreatedAt,
                        IssuedAt = cert.IssuedAt,
                        ExpiresAt = cert.ExpiresAt,
                        DaysUntilExpiry = daysUntilExpiry,
                        SerialNumber = cert.SerialNumber,
                        Fingerprint = cert.Fingerprint,
                        Email = account?.Email,
                        Description = $"Certificate for {string.Join(", ", cert.Domains ?? new List<string>())}",
                        Logs = new List<object>(),
                        Error = string.Empty
                    };
                });

                certificates.AddRange(finalCertificates);

                logger.LogInformation("总共返回 {count} 个证书记录（{orderCount} 个订单 + {certCount} 个已完成证书）",
                    certificates.Count, orderCertificates.Count(), finalCertificates.Count());

                certificates = certificates.OrderByDescending(c => c.CreatedAt).ToList();

                if (!string.IsNullOrEmpty(status))
                {
                    logger.LogInformation("应用状态过滤: {Status}", status);
                    certificates = certificates.Where(c => c.Status == status).ToList();
                }

                if (!string.IsNullOrEmpty(domain))
                {
                    logger.LogInformation("应用域名过滤: {Domain}", domain);
                    certificates = certificates.Where(c =>
                        c.Domains.Any(d => d.Contains(domain, StringComparison.OrdinalIgnoreCase))
                    ).ToList();
                }

                var totalItems = certificates.Count;
                var pagedCertificates = certificates.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                return TypedResults.Ok(new CertificateListResponse
                {
                    Items = pagedCertificates,
                    Total = totalItems,
                    Page = page,
                    PageSize = pageSize
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取证书列表失败");
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.certificatesFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> GetCertificateOrders(string? accountId, IAcmeService acmeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var orders = await acmeService.GetCertificateOrdersAsync(accountId);
                return TypedResults.Ok(orders);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取证书订单列表失败");
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.ordersFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> GetCertificateOrder(string orderId, IAcmeService acmeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var order = await acmeService.GetCertificateOrderAsync(orderId);
                if (order == null)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Message = localization.GetMessage("certificate.orderNotFound") });
                }
                return TypedResults.Ok(order);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取证书订单失败: {OrderId}", orderId);
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.getOrderFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> CancelCertificateOrder(
            string orderId,
            TinyDbContext dbContext,
            ICertificateProgressService progressService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                var ordersCollection = dbContext.GetCollection<AcmeCertificateOrder>("acme_orders");
                var order = ordersCollection.FindOne(x => x.Id == orderId || x.CertificateId == orderId);

                if (order == null)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Message = localization.GetMessage("certificate.orderNotFound") });
                }

                order.Status = "cancelled";
                order.Error = localization.GetMessage("acme.userCancelled");
                ordersCollection.Update(order);

                var progress = await progressService.GetProgressByCertificateIdAsync(order.Id);
                if (progress != null)
                {
                    await progressService.MarkAsFailedAsync(progress.ProgressId, localization.GetMessage("acme.userCancelled"));
                }

                logger.LogInformation("用户取消证书申请: {OrderId}", order.Id);

                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("certificate.cancelled") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "取消证书申请失败: {OrderId}", orderId);
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.orderCancelFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> CompleteChallenge(
            string orderId,
            string authorizationId,
            CompleteChallengeRequest request,
            IAcmeService acmeService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                var result = await acmeService.CompleteChallengeAsync(orderId, authorizationId, request);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "完成挑战验证失败: OrderId: {OrderId}, AuthId: {AuthId}", orderId, authorizationId);
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.challengeCompleteFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> CheckChallengeStatus(
            string orderId,
            string authorizationId,
            string challengeType,
            IAcmeService acmeService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("查询挑战状态: OrderId={OrderId}, AuthorizationId={AuthorizationId}, ChallengeType={ChallengeType}",
                    orderId, authorizationId, challengeType);

                var result = await acmeService.CheckChallengeStatusAsync(orderId, authorizationId, challengeType);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "查询挑战状态失败: OrderId={OrderId}, AuthorizationId={AuthorizationId}", orderId, authorizationId);
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.challengeStatusFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> DownloadCertificateFromAcme(
            string orderId,
            IAcmeService acmeService,
            TinyDbContext dbContext,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                var certificateData = await acmeService.DownloadCertificateAsync(orderId);

                await SaveCertificateToDatabase(acmeService, dbContext, orderId, certificateData, logger);

                return TypedResults.Ok(certificateData);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "下载证书失败: {OrderId}", orderId);
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.downloadFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> DownloadCertificateZip(
            string orderId,
            TinyDbContext dbContext,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("下载证书ZIP包请求: {OrderId}", orderId);

                var certificatesCollection = dbContext.GetCollection<CertificateRecord>("certificates");
                var certificate = certificatesCollection.FindOne(c => c.OrderId == orderId);

                if (certificate == null)
                {
                    logger.LogWarning("证书未找到: {OrderId}，尝试通过证书ID查询", orderId);

                    certificate = certificatesCollection.FindById(orderId);

                    if (certificate == null)
                    {
                        return TypedResults.NotFound(new ApiErrorResponse { Message = localization.GetMessage("certificate.notFoundConfirm") });
                    }
                }

                if (string.IsNullOrEmpty(certificate.CertificateData))
                {
                    logger.LogWarning("证书数据为空: {CertificateId}", certificate.Id);
                    return TypedResults.BadRequest(new ApiErrorResponse { Message = localization.GetMessage("certificate.dataEmpty") });
                }

                var domainName = certificate.Domains?.FirstOrDefault()?.Replace("*.", "wildcard_") ?? "certificate";
                var fileNamePrefix = $"{domainName}_{DateTime.UtcNow:yyyyMMdd}";

                using var memoryStream = new MemoryStream();
                using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
                {
                    var certEntry = archive.CreateEntry("cert.pem");
                    using (var entryStream = certEntry.Open())
                    using (var writer = new StreamWriter(entryStream))
                    {
                        await writer.WriteAsync(certificate.CertificateData);
                        await writer.FlushAsync();
                    }

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

                logger.LogInformation("证书下载成功: {OrderId}, 文件大小: {Size} bytes", orderId, zipBytes.Length);
                return TypedResults.File(zipBytes, "application/zip", zipFileName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "下载证书ZIP包失败: {OrderId}", orderId);
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.downloadFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> DownloadCertificateById(
            string id,
            TinyDbContext dbContext,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("通过证书ID下载证书ZIP包请求: {CertificateId}", id);

                var certificatesCollection = dbContext.GetCollection<CertificateRecord>("certificates");
                var certificate = certificatesCollection.FindOne(c => c.Id == id || c.OrderId == id);

                if (certificate == null)
                {
                    logger.LogWarning("证书未找到: {CertificateId}", id);
                    return TypedResults.NotFound(new ApiErrorResponse { Message = localization.GetMessage("certificate.notFound") });
                }

                if (string.IsNullOrEmpty(certificate.CertificateData))
                {
                    logger.LogWarning("证书数据为空: {CertificateId}", id);
                    return TypedResults.BadRequest(new ApiErrorResponse { Message = localization.GetMessage("certificate.dataEmpty") });
                }

                var domainName = certificate.Domains?.FirstOrDefault()?.Replace("*.", "wildcard_") ?? "certificate";
                var fileNamePrefix = $"{domainName}_{DateTime.UtcNow:yyyyMMdd}";

                using var memoryStream = new MemoryStream();
                using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
                {
                    var certEntry = archive.CreateEntry("cert.pem");
                    using (var entryStream = certEntry.Open())
                    using (var writer = new StreamWriter(entryStream))
                    {
                        await writer.WriteAsync(certificate.CertificateData);
                        await writer.FlushAsync();
                    }

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

                logger.LogInformation("证书下载成功: {CertificateId}, 文件大小: {Size} bytes", id, zipBytes.Length);
                return TypedResults.File(zipBytes, "application/zip", zipFileName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "下载证书ZIP包失败: {CertificateId}", id);
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.downloadFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> GetPendingChallenges(string orderId, IAcmeService acmeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var challenges = await acmeService.GetPendingChallengesAsync(orderId);
                return TypedResults.Ok(challenges);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取待处理挑战失败: {OrderId}", orderId);
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.pendingChallengesFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> RenewCertificate(string certificateId, IAcmeService acmeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            logger.LogInformation("收到续期请求，证书ID: {CertificateId}", certificateId);
            try
            {
                var renewalOrder = await acmeService.RenewCertificateAsync(certificateId);
                logger.LogInformation("续期成功，新订单ID: {OrderId}", renewalOrder.Id);
                return TypedResults.Created($"/api/acme/certificates/orders/{renewalOrder.Id}", renewalOrder);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "续期证书失败: {CertificateId}", certificateId);
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.renewFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static IResult EnableAutoRenewal(string certificateId, TinyDbContext dbContext, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            logger.LogInformation("启用证书自动续期: {CertificateId}", certificateId);
            try
            {
                var certificatesCollection = dbContext.GetCollection<CertificateRecord>("certificates");
                var certificate = certificatesCollection.FindOne(c => c.Id == certificateId || c.OrderId == certificateId);

                if (certificate != null)
                {
                    certificate.AutoRenewalEnabled = true;
                    certificate.UpdatedAt = DateTime.UtcNow;
                    certificatesCollection.Update(certificate);
                    logger.LogInformation("已启用证书自动续期: {CertificateId}", certificateId);
                    return TypedResults.Ok(new ActionBooleanResponse { Success = true, Message = localization.GetMessage("certificate.autoRenewEnabled") });
                }

                var ordersCollection = dbContext.GetCollection<AcmeCertificateOrder>("acme_orders");
                var order = ordersCollection.FindOne(o => o.Id == certificateId);

                if (order != null)
                {
                    order.Metadata ??= new Dictionary<string, object>();
                    order.Metadata["autoRenew"] = true;
                    ordersCollection.Update(order);
                    logger.LogInformation("已启用订单自动续期: {OrderId}", certificateId);
                    return TypedResults.Ok(new ActionBooleanResponse { Success = true, Message = localization.GetMessage("certificate.autoRenewEnabled") });
                }

                return TypedResults.NotFound(new ApiErrorResponse { Message = localization.GetMessage("certificate.notFound") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "启用自动续期失败: {CertificateId}", certificateId);
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.autoRenewEnableFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static IResult DisableAutoRenewal(string certificateId, TinyDbContext dbContext, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            logger.LogInformation("禁用证书自动续期: {CertificateId}", certificateId);
            try
            {
                var certificatesCollection = dbContext.GetCollection<CertificateRecord>("certificates");
                var certificate = certificatesCollection.FindOne(c => c.Id == certificateId || c.OrderId == certificateId);

                if (certificate != null)
                {
                    certificate.AutoRenewalEnabled = false;
                    certificate.UpdatedAt = DateTime.UtcNow;
                    certificatesCollection.Update(certificate);
                    logger.LogInformation("已禁用证书自动续期: {CertificateId}", certificateId);
                    return TypedResults.Ok(new ActionBooleanResponse { Success = true, Message = localization.GetMessage("certificate.autoRenewDisabled") });
                }

                var ordersCollection = dbContext.GetCollection<AcmeCertificateOrder>("acme_orders");
                var order = ordersCollection.FindOne(o => o.Id == certificateId);

                if (order != null)
                {
                    order.Metadata ??= new Dictionary<string, object>();
                    order.Metadata["autoRenew"] = false;
                    ordersCollection.Update(order);
                    logger.LogInformation("已禁用订单自动续期: {OrderId}", certificateId);
                    return TypedResults.Ok(new ActionBooleanResponse { Success = true, Message = localization.GetMessage("certificate.autoRenewDisabled") });
                }

                return TypedResults.NotFound(new ApiErrorResponse { Message = localization.GetMessage("certificate.notFound") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "禁用自动续期失败: {CertificateId}", certificateId);
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.autoRenewDisableFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static IResult RetryCertificate(string certificateId, IAcmeService acmeService, TinyDbContext dbContext, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            logger.LogInformation("收到重试请求，证书ID: {CertificateId}", certificateId);
            try
            {
                var ordersCollection = dbContext.GetCollection<AcmeCertificateOrder>("acme_orders");
                var failedOrder = ordersCollection.FindOne(x => x.Id == certificateId);

                if (failedOrder == null)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Message = localization.GetMessage("acme.orderNotFound") });
                }

                if (failedOrder.Status != "failed" && failedOrder.Status != "pending")
                {
                    return TypedResults.BadRequest(new ApiErrorResponse { Message = localization.GetMessage("certificate.canOnlyRetryFailed") });
                }

                logger.LogInformation("重试证书申请: {Domains}, 账户: {AccountId}",
                    string.Join(", ", failedOrder.Domains ?? new List<string>()), failedOrder.AccountId);

                failedOrder.Status = "pending";
                failedOrder.Error = null;
                ordersCollection.Update(failedOrder);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        logger.LogInformation("开始异步重试证书申请流程: {CertificateId}", certificateId);
                        var retryOrder = await acmeService.RetryCertificateOrderAsync(certificateId);
                        logger.LogInformation("异步重试成功，订单ID: {OrderId}", retryOrder.Id);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "异步重试证书申请失败: {CertificateId}", certificateId);
                    }
                });

                logger.LogInformation("重试请求已提交，将在后台处理: {CertificateId}", certificateId);
                return TypedResults.Ok(new RetryResponse { Message = localization.GetMessage("certificate.retrySubmitted"), CertificateId = certificateId });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "重试证书申请失败: {CertificateId}", certificateId);
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.retryFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> RevokeCertificate(string certificateId, RevokeCertificateRequest request, IAcmeService acmeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var result = await acmeService.RevokeCertificateAsync(certificateId, request);
                if (!result)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Message = localization.GetMessage("certificate.revokeFailed") });
                }
                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("certificate.revokeSuccess") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "撤销证书失败: {CertificateId}", certificateId);
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.revokeFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static IResult DeleteCertificate(string certificateId, bool force, TinyDbContext dbContext, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("删除证书请求: {CertificateId}, Force: {Force}", certificateId, force);

                var deletionSteps = new List<string>();
                var errors = new List<string>();
                var warnings = new List<string>();

                var ordersCollection = dbContext.GetCollection<AcmeCertificateOrder>("acme_orders");
                var certificate = ordersCollection.FindOne(x => x.Id == certificateId);

                var certificatesCollection = dbContext.GetCollection<CertificateRecord>("certificates");
                var completedCertificate = certificatesCollection.FindOne(x => x.Id == certificateId);

                logger.LogInformation("查找结果: OrdersFound={OrdersFound}, CertificatesFound={CertificatesFound}",
                    certificate != null, completedCertificate != null);

                if (certificate == null && completedCertificate == null)
                {
                    var message = force ? localization.GetMessage("acme.certificateForceDeleted") : localization.GetMessage("certificate.notFound");
                    deletionSteps.Add(force ? localization.GetMessage("acme.forceModeSkipCheck") : localization.GetMessage("acme.checkCertificateExists"));

                    if (force)
                    {
                        return TypedResults.Ok(new CertificateDeletionResult
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
                        return TypedResults.NotFound(new CertificateDeletionResult
                        {
                            Success = false,
                            Message = message,
                            CertificateId = certificateId,
                            DeletedAt = DateTime.UtcNow,
                            DeletionSteps = deletionSteps,
                            Errors = new List<string> { localization.GetMessage("certificate.notFound") },
                            Warnings = warnings,
                            DeletionDetails = new Dictionary<string, object>
                            {
                                ["ForceDeleted"] = false,
                                ["CertificateExisted"] = false
                            }
                        });
                    }
                }

                if (!force)
                {
                    var domainMappingsCollection = dbContext.GetCollection<DomainMapping>("domain_mappings");
                    var usingMappings = domainMappingsCollection.Find(m => m.CertificateId == certificateId).ToList();

                    if (usingMappings.Count > 0)
                    {
                        var usedByDomains = string.Join(", ", usingMappings.Select(m => m.Domain));
                        logger.LogWarning("无法删除证书 {CertificateId}，被 {Count} 个域名映射使用: {Domains}",
                            certificateId, usingMappings.Count, usedByDomains);

                        return TypedResults.BadRequest(new CertificateDeletionResult
                        {
                            Success = false,
                            Message = localization.GetMessage("acme.certificateInUse", $"无法删除证书，正在被 {usingMappings.Count} 个域名映射使用。请先解除域名绑定后再删除证书。"),
                            CertificateId = certificateId,
                            DeletedAt = DateTime.UtcNow,
                            DeletionSteps = deletionSteps,
                            Errors = new List<string> { localization.GetMessage("acme.certificateUsedByDomains", $"证书正在被以下域名使用: {usedByDomains}") },
                            Warnings = warnings,
                            DeletionDetails = new Dictionary<string, object>
                            {
                                ["InUseByDomains"] = usingMappings.Select(m => m.Domain).ToList(),
                                ["MappingIds"] = usingMappings.Select(m => m.Id).ToList()
                            }
                        });
                    }

                    deletionSteps.Add(localization.GetMessage("acme.checkUsageStatus"));
                }
                else
                {
                    var domainMappingsCollection = dbContext.GetCollection<DomainMapping>("domain_mappings");
                    var usingMappings = domainMappingsCollection.Find(m => m.CertificateId == certificateId).ToList();

                    if (usingMappings.Count > 0)
                    {
                        foreach (var mapping in usingMappings)
                        {
                            mapping.CertificateId = null;
                            mapping.EnableSsl = false;
                            domainMappingsCollection.Update(mapping);
                            warnings.Add(localization.GetMessage("acme.mappingCertificateUnbound", $"已解除域名映射 {mapping.Domain} 的证书绑定"));
                        }
                        logger.LogInformation("强制删除：已解除 {Count} 个域名映射的证书绑定", usingMappings.Count);
                        deletionSteps.Add(localization.GetMessage("acme.forceModeUnbindMappings", $"强制模式：解除 {usingMappings.Count} 个域名映射的证书绑定"));
                    }
                    else
                    {
                        deletionSteps.Add(localization.GetMessage("acme.forceModeSkipCheck"));
                    }
                }

                var deletedFromOrders = 0;
                var deletedFromCertificates = 0;
                var deletedCollections = new List<string>();

                if (certificate != null)
                {
                    deletedFromOrders = ordersCollection.DeleteMany(x => x.Id == certificateId);
                    if (deletedFromOrders > 0)
                    {
                        deletedCollections.Add("acme_orders");
                        deletionSteps.Add(localization.GetMessage("acme.deleteFromOrdersCollection"));
                    }
                }

                if (completedCertificate != null)
                {
                    deletedFromCertificates = certificatesCollection.DeleteMany(x => x.Id == certificateId);
                    if (deletedFromCertificates > 0)
                    {
                        deletedCollections.Add("certificates");
                        deletionSteps.Add(localization.GetMessage("acme.deleteFromCertificatesCollection"));
                    }
                }

                if (deletedFromOrders > 0 || deletedFromCertificates > 0)
                {
                    deletionSteps.Add(localization.GetMessage("acme.cleanOperationHistory"));
                    deletionSteps.Add(localization.GetMessage("acme.cleanUsageStats"));

                    logger.LogInformation("证书删除成功: {CertificateId}, Orders: {Orders}, Certificates: {Certificates}",
                        certificateId, deletedFromOrders, deletedFromCertificates);

                    return TypedResults.Ok(new CertificateDeletionResult
                    {
                        Success = true,
                        Message = force ? localization.GetMessage("acme.certificateForceDeleted") : localization.GetMessage("acme.certificateDeleted"),
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
                    errors.Add(localization.GetMessage("acme.dbDeleteFailed"));
                    logger.LogWarning("证书删除失败: {CertificateId} - 数据库操作失败", certificateId);

                    return TypedResults.BadRequest(new CertificateDeletionResult
                    {
                        Success = false,
                        Message = localization.GetMessage("acme.deleteFailed"),
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
                logger.LogError(ex, "删除证书失败: {CertificateId}", certificateId);
                return TypedResults.Json(new CertificateDeletionResult
                {
                    Success = false,
                    Message = localization.GetMessage("error.serverError"),
                    CertificateId = certificateId,
                    DeletedAt = DateTime.UtcNow,
                    Errors = new List<string> { ex.Message }
                }, statusCode: 500);
            }
        }

        private static async Task<IResult> GetOperationLogs(string? accountId, int limit, int offset, IAcmeService acmeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var logs = await acmeService.GetOperationLogsAsync(accountId, limit, offset);
                return TypedResults.Ok(logs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取ACME操作日志失败");
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.logsFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> CheckCertificateExpiry(string certificateId, IAcmeService acmeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var daysUntilExpiry = await acmeService.CheckCertificateExpiryAsync(certificateId);
                return TypedResults.Ok(new CertificateExpiryResponse { CertificateId = certificateId, DaysUntilExpiry = daysUntilExpiry });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "检查证书到期时间失败: {CertificateId}", certificateId);
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.expiryCheckFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> AutoRenewCertificates(int daysBeforeExpiry, IAcmeService acmeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var renewedCount = await acmeService.AutoRenewCertificatesAsync(daysBeforeExpiry);
                var message = localization.GetMessage("acme.renewedCount", $"成功续期了 {renewedCount} 个证书").Replace("{0}", renewedCount.ToString());
                return TypedResults.Ok(new AutoRenewResponse { RenewedCount = renewedCount, Message = message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "自动续期证书失败");
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.autoRenewRunFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> FixCertificateStatus(
            string? certificateId,
            IAcmeService acmeService,
            TinyDbContext dbContext,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                var orders = await acmeService.GetCertificateOrdersAsync();
                var ordersToUpdate = new List<string>();
                var debugInfo = new Dictionary<string, object>();

                if (!string.IsNullOrEmpty(certificateId))
                {
                    orders = orders.Where(o => o.Id == certificateId).ToList();
                    debugInfo["targetCertificateId"] = certificateId;
                    debugInfo["foundOrders"] = orders.ToList().Count;
                }

                var progressCollection = dbContext.GetCollection<CertificateApplicationProgress>("progress_tracks");
                var allProgressRecords = progressCollection.FindAll().ToList();
                debugInfo["totalProgressRecords"] = allProgressRecords.Count;
                debugInfo["progressCertificateIds"] = allProgressRecords.Select(p => p.CertificateId).ToList();

                var orderCollection = dbContext.GetCollection<AcmeCertificateOrder>("acme_orders");
                var allOrders = orderCollection.FindAll().ToList();
                debugInfo["totalOrdersInDb"] = allOrders.Count;
                debugInfo["allOrderIdsInDb"] = allOrders.Select(o => new OrderDebugInfo
                {
                    Id = o.Id,
                    Status = o.Status,
                    Domains = o.Domains
                }).ToList();

                foreach (var order in orders)
                {
                    debugInfo["checkingOrderId"] = order.Id;
                    debugInfo["orderStatus"] = order.Status ?? string.Empty;

                    if (order.Status?.ToLower() == "pending")
                    {
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
                    var order = orderCollection.FindAll().FirstOrDefault(o => o.Id == orderId);

                    debugInfo[$"orderFound_{orderId}"] = order != null;

                    if (order != null)
                    {
                        debugInfo[$"originalStatus_{orderId}"] = order.Status;

                        order.Status = "failed";
                        order.Error = localization.GetMessage("acme.orderTimeoutMarkedFailed");
                        order.Metadata["UpdatedAt"] = DateTime.UtcNow;

                        var updateResult = orderCollection.Update(order);
                        debugInfo[$"updateResult_{orderId}"] = updateResult;

                        if (updateResult > 0)
                        {
                            updatedCount++;
                            logger.LogInformation("已将证书 {OrderId} 状态更新为失败", orderId);
                        }
                        else
                        {
                            logger.LogWarning("更新证书 {OrderId} 状态失败", orderId);
                        }
                    }
                    else
                    {
                        logger.LogWarning("未找到证书订单: {OrderId}", orderId);
                    }
                }

                var message = localization.GetMessage("acme.statusFixed", $"成功修复了 {updatedCount} 个证书状态").Replace("{0}", updatedCount.ToString());

                return TypedResults.Ok(new FixStatusResponse
                {
                    UpdatedCount = updatedCount,
                    Message = message,
                    UpdatedOrders = ordersToUpdate,
                    Debug = debugInfo
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "修复证书状态失败");
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.fixStatusFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> VerifyDomainOwnership(string domain, string challengeType, IAcmeService acmeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var result = await acmeService.VerifyDomainOwnershipAsync(domain, challengeType);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "验证域名所有权失败: {Domain}, Type: {Type}", domain, challengeType);
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.domainValidateFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> GenerateCsr(GenerateCsrRequest request, IAcmeService acmeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var csr = await acmeService.GenerateCsrAsync(request.Domains, request.KeyType);
                return TypedResults.Ok(new CsrResponse { Csr = csr });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "生成CSR失败");
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.csrGenerateFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> ValidateCertificate(ValidateCertificateRequest request, IAcmeService acmeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var result = await acmeService.ValidateCertificateAsync(request.CertificateData);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "验证证书失败");
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.certificateValidateFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> GetAccountKeyInfo(string accountId, IAcmeService acmeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var keyInfo = await acmeService.GetAccountKeyInfoAsync(accountId);
                return TypedResults.Ok(keyInfo);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取账户密钥信息失败: {AccountId}", accountId);
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.keyInfoFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> GenerateKeyPair(string keyType, IAcmeService acmeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var keyPair = await acmeService.GenerateKeyPairAsync(keyType);
                return TypedResults.Ok(keyPair);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "生成密钥对失败");
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.keyGenerateFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> ExportAccountKey(string accountId, string format, IAcmeService acmeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var keyData = await acmeService.ExportAccountKeyAsync(accountId, format);
                return TypedResults.Ok(new KeyExportResponse { KeyData = keyData, Format = format });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "导出账户密钥失败: {AccountId}", accountId);
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.keyExportFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> ImportAccountKey(ImportKeyRequest request, IAcmeService acmeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var keyInfo = await acmeService.ImportAccountKeyAsync(request.KeyData, request.Format);
                return TypedResults.Ok(keyInfo);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "导入账户密钥失败");
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.keyImportFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        private static async Task<IResult> GetHttpChallenge(string token, IAcmeChallengeStore challengeStore, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("收到 ACME HTTP-01 挑战请求，Token: {Token}", token);

                var keyAuthorization = await challengeStore.GetHttpChallengeAsync(token);

                if (!string.IsNullOrEmpty(keyAuthorization))
                {
                    logger.LogInformation("成功返回 ACME 挑战响应，Token: {Token}", token);
                    return TypedResults.Text(keyAuthorization, "text/plain");
                }

                logger.LogWarning("未找到 Token 对应的挑战数据: {Token}", token);
                return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("acme.challengeNotFound"), Message = token });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "处理 ACME 挑战请求失败: {Token}", token);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError") }, statusCode: 500);
            }
        }

        private static async Task<IResult> StoreTestChallenge(StoreChallengeRequest request, IAcmeChallengeStore challengeStore, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("存储测试挑战，Token: {Token}", request.Token);

                await challengeStore.StoreHttpChallengeAsync(request.Token, request.KeyAuthorization, DateTime.UtcNow.AddHours(1));

                return TypedResults.Ok(new StoreChallengeResponse { Message = localization.GetMessage("acme.challengeStored"), Token = request.Token });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "存储测试挑战失败: {Token}", request.Token);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError") }, statusCode: 500);
            }
        }

        private static async Task<IResult> RenewBatchCertificates(BatchRenewRequest request, IAcmeService acmeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogDebug("RenewBatchCertificates: CertificateIds={Ids}, DaysBeforeExpiry={Days}",
                    string.Join(",", request.CertificateIds), request.DaysBeforeExpiry);

                var results = new List<RenewResult>();

                foreach (var certificateId in request.CertificateIds)
                {
                    try
                    {
                        var renewedCertificate = await acmeService.RenewCertificateAsync(certificateId);
                        results.Add(new RenewResult
                        {
                            CertificateId = certificateId,
                            Success = true,
                            NewCertificateId = renewedCertificate.Id,
                            Message = localization.GetMessage("acme.renewSuccess")
                        });
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "批量续期失败: {CertificateId}", certificateId);
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

                var message = localization.GetMessage("acme.batchRenewComplete", $"批量续期完成: {successCount}/{totalCount} 成功")
                    .Replace("{0}", successCount.ToString())
                    .Replace("{1}", totalCount.ToString());

                return TypedResults.Ok(new BatchRenewResponse
                {
                    Message = message,
                    Results = results,
                    Summary = new BatchRenewSummary
                    {
                        Total = totalCount,
                        Success = successCount,
                        Failed = totalCount - successCount
                    }
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "批量续期证书失败");
                return TypedResults.Json(new ApiErrorResponse { Message = localization.GetMessage("acme.batchRenewFailed"), Error = ex.Message }, statusCode: 500);
            }
        }

        // 辅助方法

        private static async Task<string> GetOrCreateAccountIdAsync(IAcmeService acmeService, string email, string provider, ILogger<LoggingTag> logger)
        {
            try
            {
                var accounts = await acmeService.GetAccountsAsync();
                var existingAccount = accounts.FirstOrDefault(a => a.Email == email && a.Provider == provider);

                if (existingAccount != null)
                {
                    return existingAccount.Id;
                }

                var createRequest = new CreateAcmeAccountRequest
                {
                    Email = email,
                    Provider = provider
                };

                var newAccount = await acmeService.CreateAccountAsync(createRequest);
                return newAccount.Id;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取或创建ACME账户失败: {Email}, {Provider}", email, provider);
                throw;
            }
        }

        private static async Task SaveCertificateToDatabase(IAcmeService acmeService, TinyDbContext dbContext, string orderId, AcmeCertificateData certificateData, ILogger<LoggingTag> logger)
        {
            try
            {
                var order = await acmeService.GetCertificateOrderAsync(orderId);
                if (order == null)
                {
                    logger.LogWarning("无法找到订单信息: {OrderId}", orderId);
                    return;
                }

                var certBytes = System.Text.Encoding.UTF8.GetBytes(certificateData.Certificate);
                var cert = X509CertificateLoader.LoadCertificate(certBytes);

                var autoRenew = order.Metadata != null && order.Metadata.TryGetValue("autoRenew", out var ar) && ar is bool b && b;

                var certificatesCollection = dbContext.GetCollection<CertificateRecord>("certificates");
                var existingCertificate = certificatesCollection.FindAll().FirstOrDefault(c => c.OrderId == orderId);

                var certificateRecord = new CertificateRecord
                {
                    Id = existingCertificate?.Id ?? string.Empty,
                    Name = certificateData.Domains.FirstOrDefault() ?? "Unknown",
                    Type = "SSL",
                    Domains = certificateData.Domains,
                    Status = "Active",
                    IssuedAt = cert.NotBefore,
                    ExpiresAt = cert.NotAfter,
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
                    AutoRenewalEnabled = autoRenew,
                    Metadata = order.Metadata ?? new Dictionary<string, object>(),
                    CreatedAt = existingCertificate?.CreatedAt ?? DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                if (existingCertificate == null)
                {
                    certificatesCollection.Insert(certificateRecord);
                }
                else
                {
                    certificatesCollection.Update(certificateRecord);
                }

                order.Status = "completed";
                order.CompletedAt = DateTime.UtcNow;
                order.CertificateId = certificateRecord.Id.ToString();

                var ordersCollection = dbContext.GetCollection<AcmeCertificateOrder>("acme_orders");
                ordersCollection.Update(order);

                logger.LogInformation("证书已保存到数据库: OrderId={OrderId}, CertificateId={CertificateId}, Subject={Subject}, AutoRenew={AutoRenew}, ExpiresAt={ExpiresAt}",
                    orderId, certificateRecord.Id, certificateData.Subject, autoRenew, cert.NotAfter);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "保存证书到数据库失败: OrderId={OrderId}", orderId);
                throw;
            }
        }

        /// <summary>
        /// 转换DNS凭据为保存格式 - 确保JsonElement被正确转换为字符串值
        /// </summary>
        private static Dictionary<string, object> ConvertDnsCredentialsForSave(Dictionary<string, object>? dnsCredentials, ILogger<LoggingTag> logger)
        {
            if (dnsCredentials == null)
            {
                logger.LogDebug("ConvertDnsCredentialsForSave: dnsCredentials 为 null");
                return new Dictionary<string, object>();
            }

            try
            {
                logger.LogInformation("ConvertDnsCredentialsForSave: 输入类型={0}, 键数量={1}",
                    dnsCredentials.GetType().Name, dnsCredentials.Count);

                var result = new Dictionary<string, object>();

                foreach (var kvp in dnsCredentials)
                {
                    if (kvp.Value == null)
                    {
                        result[kvp.Key] = string.Empty;
                        logger.LogDebug("ConvertDnsCredentialsForSave: 键 {Key} 的值为 null，设置为空字符串", kvp.Key);
                        continue;
                    }

                    ProcessDnsCredentialValue(kvp.Value, result, kvp.Key, logger);

                    logger.LogDebug("ConvertDnsCredentialsForSave: 处理键 {Key}，值类型: {ValueType}",
                        kvp.Key, kvp.Value.GetType().FullName);
                }

                logger.LogInformation("ConvertDnsCredentialsForSave: 转换完成，结果键数量={0}", result.Count);

                if (result.Count == 0 && dnsCredentials.Count > 0)
                {
                    logger.LogWarning("ConvertDnsCredentialsForSave: 警告 - 输入有 {InputCount} 个键，但输出为空",
                        dnsCredentials.Count);
                }

                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ConvertDnsCredentialsForSave: 转换失败，输入键数量: {Count}",
                    dnsCredentials?.Count ?? 0);

                return new Dictionary<string, object>();
            }
        }

        private static void ProcessDnsCredentialValue(object value, Dictionary<string, object> result, string providerKey, ILogger<LoggingTag> logger)
        {
            try
            {
                if (value is System.Text.Json.JsonElement jsonElement)
                {
                    switch (jsonElement.ValueKind)
                    {
                        case System.Text.Json.JsonValueKind.String:
                            var stringValue = jsonElement.GetString();
                            logger.LogDebug("JsonElement(String): 值长度={0}", stringValue?.Length ?? 0);
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
                            ProcessJsonObject(jsonElement, result, logger);
                            break;

                        case System.Text.Json.JsonValueKind.Array:
                            try
                            {
                                var serialized = jsonElement.GetRawText();
                                logger.LogDebug("JsonElement(Array): 序列化长度={0}", serialized.Length);
                                result[providerKey] = serialized;
                            }
                            catch (Exception serializeEx)
                            {
                                logger.LogWarning(serializeEx, "序列化JsonElement失败，使用ToString");
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
                            foreach (var nestedKvp in dict)
                            {
                                ProcessDnsCredentialValue(nestedKvp.Value, result, nestedKvp.Key, logger);
                            }
                            break;
                        case System.Collections.IEnumerable enumerable and not string:
                            var list = new List<object>();
                            foreach (var item in enumerable)
                            {
                                list.Add(ProcessListItemValue(item, logger));
                            }
                            result[providerKey] = Serialization.JsonValueWriter.ToJsonString(list, Serialization.JsonSerializers.Options);
                            break;
                        default:
                            var defaultStringValue = value.ToString();
                            logger.LogDebug("其他类型 {0} 转换为字符串，长度: {1}",
                                value.GetType().Name, defaultStringValue?.Length ?? 0);
                            result[providerKey] = defaultStringValue ?? string.Empty;
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ProcessDnsCredentialValue: 处理值失败，类型: {Type}", value.GetType().Name);
                result[providerKey] = string.Empty;
            }
        }

        private static void ProcessJsonObject(System.Text.Json.JsonElement jsonElement, Dictionary<string, object> result, ILogger<LoggingTag> logger)
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
                        ProcessJsonObject(value, result, logger);
                        break;
                    case System.Text.Json.JsonValueKind.Array:
                        var list = new List<object>();
                        foreach (var item in value.EnumerateArray())
                        {
                            list.Add(ProcessListItemValue(item, logger));
                        }
                        result[key] = Serialization.JsonValueWriter.ToJsonString(list, Serialization.JsonSerializers.Options);
                        break;
                    default:
                        result[key] = value.ToString();
                        break;
                }
            }
        }

        private static object ProcessListItemValue(object item, ILogger<LoggingTag> logger)
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
                        ProcessJsonObject(jsonElement, dict, logger);
                        return dict;
                    case System.Text.Json.JsonValueKind.Array:
                        var list = new List<object>();
                        foreach (var arrayItem in jsonElement.EnumerateArray())
                        {
                            list.Add(ProcessListItemValue(arrayItem, logger));
                        }
                        return list;
                    default:
                        return jsonElement.ToString();
                }
            }
            return item;
        }
    }
}