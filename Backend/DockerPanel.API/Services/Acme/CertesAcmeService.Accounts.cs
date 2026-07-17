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

        public async Task<IEnumerable<AcmeAccount>> GetAccountsAsync()
        {
            try
            {
                var accountsCollection = _dbContext.GetCollection<AcmeAccount>(DbCollections.AcmeAccounts);
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
                var accountsCollection = _dbContext.GetCollection<AcmeAccount>(DbCollections.AcmeAccounts);
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
                    var accountsCollection = _dbContext.GetCollection<AcmeAccount>(DbCollections.AcmeAccounts);
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
                var ordersCollection = _dbContext.GetCollection<AcmeCertificateOrder>(DbCollections.AcmeOrders);
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
                var accountsCollection = _dbContext.GetCollection<AcmeAccount>(DbCollections.AcmeAccounts);
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


    }
}
