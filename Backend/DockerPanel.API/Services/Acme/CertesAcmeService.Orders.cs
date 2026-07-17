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
    public partial class CertesAcmeService
    {

        public async Task<IEnumerable<AcmeCertificateOrder>> GetPendingOrdersForDomainsAsync(IEnumerable<string> domains)
        {
            try
            {
                var ordersCollection = _dbContext.GetCollection<AcmeCertificateOrder>(DbCollections.AcmeOrders);
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
            var ordersCollection = _dbContext.GetCollection<AcmeCertificateOrder>(DbCollections.AcmeOrders);

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
                var ordersCollection = _dbContext.GetCollection<AcmeCertificateOrder>(DbCollections.AcmeOrders);
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
                    var ordersCollection = _dbContext.GetCollection<AcmeCertificateOrder>(DbCollections.AcmeOrders);
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

                // 🚀 将自动验证流程移至基于 TinyDb 的持久化后台任务队列，避免重启导致任务丢失
                _logger.LogInformation("将自动验证挑战加入持久化后台队列: OrderId={OrderId}", order.Id);

                await _jobQueue.EnqueueAsync("AutoValidation", new
                {
                    OrderId = order.Id,
                    ProgressId = progressId
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

                        var certificatesCollection = _dbContext.GetCollection<DockerPanel.API.Services.Acme.CertificateRecord>(DbCollections.Certificates);

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
                                    var ordersCol = _dbContext.GetCollection<AcmeCertificateOrder>(DbCollections.AcmeOrders);
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


    }
}
