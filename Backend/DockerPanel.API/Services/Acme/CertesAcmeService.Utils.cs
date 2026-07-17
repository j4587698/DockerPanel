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

        public async Task<int> CheckCertificateExpiryAsync(string certificateId)
        {
            var certificate = FindCertificateRecord(certificateId);
            if (certificate != null)
            {
                return (int)Math.Floor((certificate.ExpiresAt.ToUniversalTime() - DateTime.UtcNow).TotalDays);
            }

            var ordersCollection = _dbContext.GetCollection<AcmeCertificateOrder>(DbCollections.AcmeOrders);
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
            var certificatesCollection = _dbContext.GetCollection<CertificateRecord>(DbCollections.Certificates);
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
                    var certificates = _dbContext.GetCollection<CertificateRecord>(DbCollections.Certificates)
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
            var logsCollection = _dbContext.GetCollection<AcmeOperationLog>(DbCollections.AcmeOperationLogs);
            var logs = string.IsNullOrWhiteSpace(accountId)
                ? logsCollection.FindAll()
                : logsCollection.Find(l => l.AccountId == accountId);

            return logs
                .OrderByDescending(l => l.Timestamp == default ? l.StartedAt : l.Timestamp)
                .Skip(Math.Max(0, offset))
                .Take(Math.Clamp(limit, 1, 500))
                .ToList();
        }


    }
}
