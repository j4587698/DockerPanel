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

                    var certificatesCollection = _dbContext.GetCollection<DockerPanel.API.Services.Acme.CertificateRecord>(DbCollections.Certificates);
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
                var ordersCollection = _dbContext.GetCollection<AcmeCertificateOrder>(DbCollections.AcmeOrders);
                var existingOrder = ordersCollection.FindById(certificateId);

                // 如果在 acme_orders 中没找到，尝试从 certificates 集合获取
                if (existingOrder == null)
                {
                    var certificatesCollection = _dbContext.GetCollection<CertificateRecord>(DbCollections.Certificates);
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
                var ordersCollection = _dbContext.GetCollection<AcmeCertificateOrder>(DbCollections.AcmeOrders);
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

                var certificatesCollection = _dbContext.GetCollection<CertificateRecord>(DbCollections.Certificates);
                var ordersCollection = _dbContext.GetCollection<AcmeCertificateOrder>(DbCollections.AcmeOrders);

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


    }
}
