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

        private CertificateRecord? FindCertificateRecord(string certificateId)
        {
            if (string.IsNullOrWhiteSpace(certificateId))
            {
                return null;
            }

            var certificatesCollection = _dbContext.GetCollection<CertificateRecord>(DbCollections.Certificates);
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
                var logsCollection = _dbContext.GetCollection<AcmeOperationLog>(DbCollections.AcmeOperationLogs);
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
                    var accountsCollection = _dbContext.GetCollection<AcmeAccount>(DbCollections.AcmeAccounts);
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
                var accountsCollection = _dbContext.GetCollection<AcmeAccount>(DbCollections.AcmeAccounts);
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
                    var accountsCollection = _dbContext.GetCollection<AcmeAccount>(DbCollections.AcmeAccounts);
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
                var accountsCollection = _dbContext.GetCollection<AcmeAccount>(DbCollections.AcmeAccounts);
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
            var accountsCollection = _dbContext.GetCollection<AcmeAccount>(DbCollections.AcmeAccounts);
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
                var accountsCollection = _dbContext.GetCollection<AcmeAccount>(DbCollections.AcmeAccounts);
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

    }
}
