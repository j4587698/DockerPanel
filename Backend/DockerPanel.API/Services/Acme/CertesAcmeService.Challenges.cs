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
                    var accountsCollection = _dbContext.GetCollection<AcmeAccount>(DbCollections.AcmeAccounts);
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


    }
}
