using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DockerPanel.API.Services.Acme
{
    /// <summary>
    /// ACME 挑战数据存储服务
    /// 用于存储和检索 HTTP-01 和 DNS-01 挑战数据
    /// TLS-ALPN-01 挑战由 TlsAlpnChallengeService 纯内存管理，不经过此接口
    /// </summary>
    public interface IAcmeChallengeStore
    {
        /// <summary>
        /// 存储 HTTP-01 挑战数据
        /// </summary>
        Task StoreHttpChallengeAsync(string token, string keyAuthorization, DateTime? expiresAt = null);

        /// <summary>
        /// 获取 HTTP-01 挑战数据
        /// </summary>
        Task<string?> GetHttpChallengeAsync(string token);

        /// <summary>
        /// 删除 HTTP-01 挑战数据
        /// </summary>
        Task RemoveHttpChallengeAsync(string token);

        /// <summary>
        /// 存储 DNS-01 挑战数据
        /// </summary>
        Task StoreDnsChallengeAsync(string domain, string recordName, string recordValue, DateTime? expiresAt = null);

        /// <summary>
        /// 获取 DNS-01 挑战数据
        /// </summary>
        Task<string?> GetDnsChallengeAsync(string domain);

        /// <summary>
        /// 删除 DNS-01 挑战数据
        /// </summary>
        Task RemoveDnsChallengeAsync(string domain);

        /// <summary>
        /// 清理过期的挑战数据
        /// </summary>
        Task CleanupExpiredChallengesAsync();
    }

    /// <summary>
    /// 内存中的 ACME 挑战数据存储实现
    /// </summary>
    public class InMemoryAcmeChallengeStore : IAcmeChallengeStore
    {
        private readonly ILogger<InMemoryAcmeChallengeStore> _logger;
        private readonly ConcurrentDictionary<string, (string KeyAuthorization, DateTime ExpiresAt)> _httpChallenges;
        private readonly ConcurrentDictionary<string, (string RecordValue, DateTime ExpiresAt)> _dnsChallenges;

        public InMemoryAcmeChallengeStore(ILogger<InMemoryAcmeChallengeStore> logger)
        {
            _logger = logger;
            _httpChallenges = new ConcurrentDictionary<string, (string, DateTime)>();
            _dnsChallenges = new ConcurrentDictionary<string, (string, DateTime)>();

            // 启动定期清理任务
            _ = Task.Run(PeriodicCleanup);
        }

        public async Task StoreHttpChallengeAsync(string token, string keyAuthorization, DateTime? expiresAt = null)
        {
            await Task.Run(() =>
            {
                var expiry = expiresAt ?? DateTime.UtcNow.AddHours(1); // 默认1小时过期
                _httpChallenges[token] = (keyAuthorization, expiry);

                _logger.LogDebug("存储 HTTP-01 挑战数据: Token={Token}, 过期时间={ExpiresAt}", token, expiry);
            });
        }

        public async Task<string?> GetHttpChallengeAsync(string token)
        {
            return await Task.Run(() =>
            {
                if (_httpChallenges.TryGetValue(token, out var challenge))
                {
                    // 检查是否过期
                    if (DateTime.UtcNow > challenge.ExpiresAt)
                    {
                        _httpChallenges.TryRemove(token, out _);
                        _logger.LogDebug("HTTP-01 挑战已过期，已删除: Token={Token}", token);
                        return null;
                    }

                    _logger.LogDebug("获取 HTTP-01 挑战数据: Token={Token}", token);
                    return challenge.KeyAuthorization;
                }

                _logger.LogDebug("未找到 HTTP-01 挑战数据: Token={Token}", token);
                return null;
            });
        }

        public async Task RemoveHttpChallengeAsync(string token)
        {
            await Task.Run(() =>
            {
                if (_httpChallenges.TryRemove(token, out _))
                {
                    _logger.LogDebug("删除 HTTP-01 挑战数据: Token={Token}", token);
                }
            });
        }

        public async Task StoreDnsChallengeAsync(string domain, string recordName, string recordValue, DateTime? expiresAt = null)
        {
            await Task.Run(() =>
            {
                var expiry = expiresAt ?? DateTime.UtcNow.AddHours(1); // 默认1小时过期
                _dnsChallenges[domain] = (recordValue, expiry);

                _logger.LogDebug("存储 DNS-01 挑战数据: Domain={Domain}, RecordName={RecordName}, 过期时间={ExpiresAt}",
                    domain, recordName, expiry);
            });
        }

        public async Task<string?> GetDnsChallengeAsync(string domain)
        {
            return await Task.Run(() =>
            {
                if (_dnsChallenges.TryGetValue(domain, out var challenge))
                {
                    // 检查是否过期
                    if (DateTime.UtcNow > challenge.ExpiresAt)
                    {
                        _dnsChallenges.TryRemove(domain, out _);
                        _logger.LogDebug("DNS-01 挑战已过期，已删除: Domain={Domain}", domain);
                        return null;
                    }

                    _logger.LogDebug("获取 DNS-01 挑战数据: Domain={Domain}", domain);
                    return challenge.RecordValue;
                }

                _logger.LogDebug("未找到 DNS-01 挑战数据: Domain={Domain}", domain);
                return null;
            });
        }

        public async Task RemoveDnsChallengeAsync(string domain)
        {
            await Task.Run(() =>
            {
                if (_dnsChallenges.TryRemove(domain, out _))
                {
                    _logger.LogDebug("删除 DNS-01 挑战数据: Domain={Domain}", domain);
                }
            });
        }

        public async Task CleanupExpiredChallengesAsync()
        {
            await Task.Run(() =>
            {
                var now = DateTime.UtcNow;
                var expiredHttpTokens = new List<string>();
                var expiredDnsDomains = new List<string>();

                // 查找过期的 HTTP 挑战
                foreach (var kvp in _httpChallenges)
                {
                    if (now > kvp.Value.ExpiresAt)
                    {
                        expiredHttpTokens.Add(kvp.Key);
                    }
                }

                // 查找过期的 DNS 挑战
                foreach (var kvp in _dnsChallenges)
                {
                    if (now > kvp.Value.ExpiresAt)
                    {
                        expiredDnsDomains.Add(kvp.Key);
                    }
                }

                // 删除过期的挑战
                var removedHttpCount = 0;
                foreach (var token in expiredHttpTokens)
                {
                    if (_httpChallenges.TryRemove(token, out _))
                    {
                        removedHttpCount++;
                    }
                }

                var removedDnsCount = 0;
                foreach (var domain in expiredDnsDomains)
                {
                    if (_dnsChallenges.TryRemove(domain, out _))
                    {
                        removedDnsCount++;
                    }
                }

                if (removedHttpCount > 0 || removedDnsCount > 0)
                {
                    _logger.LogInformation("清理过期挑战数据完成: HTTP挑战={RemovedHttp}个, DNS挑战={RemovedDns}个",
                        removedHttpCount, removedDnsCount);
                }
            });
        }

        /// <summary>
        /// 定期清理过期挑战数据
        /// </summary>
        private async Task PeriodicCleanup()
        {
            while (true)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(10)); // 每10分钟清理一次
                    await CleanupExpiredChallengesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "定期清理过期挑战数据时发生错误");
                }
            }
        }
    }
}