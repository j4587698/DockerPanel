using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TinyDb;
using TinyDb.Core;
using TinyDb.Attributes;
using DockerPanel.API.Data;

namespace DockerPanel.API.Services.Acme
{
    /// <summary>
    /// 基于 TinyDb 的 ACME 挑战数据持久化存储实现
    /// 使用主数据库，不再使用单独的数据库文件
    /// </summary>
    public class TinyDbAcmeChallengeStore : IAcmeChallengeStore, IDisposable
    {
        private readonly ILogger<TinyDbAcmeChallengeStore> _logger;
        private readonly TinyDbContext _dbContext;
        private readonly CancellationTokenSource _cleanupCancellation = new();
        private static readonly ConcurrentDictionary<string, CachedChallengeValue> HttpChallengeCache = new();
        private static readonly ConcurrentDictionary<string, CachedChallengeValue> DnsChallengeCache = new();

        // 挑战数据保持时间：24小时（足够完成整个ACME验证流程）
        private readonly TimeSpan _challengeExpiration = TimeSpan.FromHours(24);

        public TinyDbAcmeChallengeStore(TinyDbEngine database, ILogger<TinyDbAcmeChallengeStore> logger)
        {
            _dbContext = new TinyDbContext(database);
            _logger = logger;

            _logger.LogInformation("TinyDb ACME 挑战存储已初始化，使用主数据库");

            // 启动定期清理任务
            _ = Task.Run(() => PeriodicCleanup(_cleanupCancellation.Token));
        }

        /// <summary>
        /// 存储 HTTP-01 挑战数据
        /// </summary>
        public async Task StoreHttpChallengeAsync(string token, string keyAuthorization, DateTime? expiresAt = null)
        {
            await Task.Run(() =>
            {
                try
                {
                    var expiry = expiresAt ?? DateTime.UtcNow.Add(_challengeExpiration);

                    var entity = new HttpChallengeEntity
                    {
                        Token = token,
                        KeyAuthorization = keyAuthorization,
                        ExpiresAt = expiry,
                        CreatedAt = DateTime.UtcNow
                    };

                    HttpChallengeCache[token] = new CachedChallengeValue(keyAuthorization, expiry);

                    // 使用 Upsert 替换已存在的记录。TinyDb 历史数据损坏时保留内存兜底，避免真实 ACME HTTP-01 签发被阻断。
                    try
                    {
                        _dbContext.HttpChallenges.Upsert(entity);
                    }
                    catch (Exception dbEx)
                    {
                        _logger.LogWarning(dbEx, "持久化 HTTP-01 挑战失败，已使用进程内兜底缓存: Token={Token}", token);
                    }

                    _logger.LogDebug("持久化存储 HTTP-01 挑战数据: Token={Token}, 过期时间={ExpiresAt}",
                        token, expiry);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "存储 HTTP-01 挑战数据失败: Token={Token}", token);
                    throw;
                }
            });
        }

        /// <summary>
        /// 获取 HTTP-01 挑战数据
        /// </summary>
        public async Task<string?> GetHttpChallengeAsync(string token)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (TryGetCachedChallenge(HttpChallengeCache, token, out var cachedKeyAuthorization))
                    {
                        _logger.LogDebug("从进程内缓存获取 HTTP-01 挑战数据: Token={Token}", token);
                        return cachedKeyAuthorization;
                    }

                    var entity = _dbContext.HttpChallenges.FindById(token);

                    if (entity != null)
                    {
                        // 检查是否过期
                        if (DateTime.UtcNow > entity.ExpiresAt)
                        {
                            _dbContext.HttpChallenges.Delete(token);
                            _logger.LogDebug("HTTP-01 挑战已过期，已删除: Token={Token}", token);
                            return null;
                        }

                        _logger.LogDebug("获取 HTTP-01 挑战数据: Token={Token}", token);
                        HttpChallengeCache[token] = new CachedChallengeValue(entity.KeyAuthorization, entity.ExpiresAt);
                        return entity.KeyAuthorization;
                    }

                    _logger.LogDebug("未找到 HTTP-01 挑战数据: Token={Token}", token);
                    return null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "获取 HTTP-01 挑战数据失败: Token={Token}", token);
                    return null;
                }
            });
        }

        /// <summary>
        /// 删除 HTTP-01 挑战数据
        /// </summary>
        public async Task RemoveHttpChallengeAsync(string token)
        {
            await Task.Run(() =>
            {
                try
                {
                    HttpChallengeCache.TryRemove(token, out _);

                    var deleted = _dbContext.HttpChallenges.Delete(token);
                    if (deleted > 0)
                    {
                        _logger.LogDebug("删除 HTTP-01 挑战数据: Token={Token}", token);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "删除 HTTP-01 挑战数据失败: Token={Token}", token);
                }
            });
        }

        /// <summary>
        /// 存储 DNS-01 挑战数据
        /// </summary>
        public async Task StoreDnsChallengeAsync(string domain, string recordName, string recordValue, DateTime? expiresAt = null)
        {
            await Task.Run(() =>
            {
                try
                {
                    var expiry = expiresAt ?? DateTime.UtcNow.Add(_challengeExpiration);

                    var entity = new DnsChallengeEntity
                    {
                        Domain = domain,
                        RecordName = recordName,
                        RecordValue = recordValue,
                        ExpiresAt = expiry,
                        CreatedAt = DateTime.UtcNow
                    };

                    DnsChallengeCache[domain] = new CachedChallengeValue(recordValue, expiry);

                    // 使用 Upsert 替换已存在的记录。TinyDb 历史数据损坏时保留内存兜底。
                    try
                    {
                        _dbContext.DnsChallenges.Upsert(entity);
                    }
                    catch (Exception dbEx)
                    {
                        _logger.LogWarning(dbEx, "持久化 DNS-01 挑战失败，已使用进程内兜底缓存: Domain={Domain}", domain);
                    }

                    _logger.LogDebug("持久化存储 DNS-01 挑战数据: Domain={Domain}, RecordName={RecordName}, 过期时间={ExpiresAt}",
                        domain, recordName, expiry);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "存储 DNS-01 挑战数据失败: Domain={Domain}", domain);
                    throw;
                }
            });
        }

        /// <summary>
        /// 获取 DNS-01 挑战数据
        /// </summary>
        public async Task<string?> GetDnsChallengeAsync(string domain)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (TryGetCachedChallenge(DnsChallengeCache, domain, out var cachedRecordValue))
                    {
                        _logger.LogDebug("从进程内缓存获取 DNS-01 挑战数据: Domain={Domain}", domain);
                        return cachedRecordValue;
                    }

                    var entity = _dbContext.DnsChallenges.FindById(domain);

                    if (entity != null)
                    {
                        // 检查是否过期
                        if (DateTime.UtcNow > entity.ExpiresAt)
                        {
                            _dbContext.DnsChallenges.Delete(domain);
                            _logger.LogDebug("DNS-01 挑战已过期，已删除: Domain={Domain}", domain);
                            return null;
                        }

                        _logger.LogDebug("获取 DNS-01 挑战数据: Domain={Domain}", domain);
                        DnsChallengeCache[domain] = new CachedChallengeValue(entity.RecordValue, entity.ExpiresAt);
                        return entity.RecordValue;
                    }

                    _logger.LogDebug("未找到 DNS-01 挑战数据: Domain={Domain}", domain);
                    return null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "获取 DNS-01 挑战数据失败: Domain={Domain}", domain);
                    return null;
                }
            });
        }

        /// <summary>
        /// 删除 DNS-01 挑战数据
        /// </summary>
        public async Task RemoveDnsChallengeAsync(string domain)
        {
            await Task.Run(() =>
            {
                try
                {
                    DnsChallengeCache.TryRemove(domain, out _);

                    var deleted = _dbContext.DnsChallenges.Delete(domain);
                    if (deleted > 0)
                    {
                        _logger.LogDebug("删除 DNS-01 挑战数据: Domain={Domain}", domain);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "删除 DNS-01 挑战数据失败: Domain={Domain}", domain);
                }
            });
        }

        /// <summary>
        /// 清理过期的挑战数据
        /// </summary>
        public async Task CleanupExpiredChallengesAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    var now = DateTime.UtcNow;

                    var removedHttpCacheCount = RemoveExpiredCachedChallenges(HttpChallengeCache, now);
                    var removedDnsCacheCount = RemoveExpiredCachedChallenges(DnsChallengeCache, now);

                    // 清理过期的 HTTP 挑战
                    var expiredHttpChallenges = _dbContext.HttpChallenges.Find(x => x.ExpiresAt < now).ToList();
                    var removedHttpCount = 0;

                    foreach (var challenge in expiredHttpChallenges)
                    {
                        if (_dbContext.HttpChallenges.Delete(challenge.Token) > 0)
                        {
                            removedHttpCount++;
                        }
                    }

                    // 清理过期的 DNS 挑战
                    var expiredDnsChallenges = _dbContext.DnsChallenges.Find(x => x.ExpiresAt < now).ToList();
                    var removedDnsCount = 0;

                    foreach (var challenge in expiredDnsChallenges)
                    {
                        if (_dbContext.DnsChallenges.Delete(challenge.Domain) > 0)
                        {
                            removedDnsCount++;
                        }
                    }

                    if (removedHttpCount > 0 || removedDnsCount > 0 || removedHttpCacheCount > 0 || removedDnsCacheCount > 0)
                    {
                        _logger.LogInformation("清理过期挑战数据完成: HTTP挑战={RemovedHttp}个, DNS挑战={RemovedDns}个, HTTP缓存={RemovedHttpCache}个, DNS缓存={RemovedDnsCache}个",
                            removedHttpCount, removedDnsCount, removedHttpCacheCount, removedDnsCacheCount);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "清理过期挑战数据失败");
                }
            });
        }

        /// <summary>
        /// 获取存储统计信息
        /// </summary>
        public async Task<ChallengeStoreStatistics> GetStatisticsAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var now = DateTime.UtcNow;

                    var httpCount = SafeCountHttpChallenges(now, out var httpExpiredCount);
                    var httpActiveCount = httpCount - httpExpiredCount;

                    var dnsCount = SafeCountDnsChallenges(now, out var dnsExpiredCount);
                    var dnsActiveCount = dnsCount - dnsExpiredCount;

                    return new ChallengeStoreStatistics
                    {
                        HttpTotalCount = (int)httpCount,
                        HttpActiveCount = (int)httpActiveCount,
                        HttpExpiredCount = (int)httpExpiredCount,
                        DnsTotalCount = (int)dnsCount,
                        DnsActiveCount = (int)dnsActiveCount,
                        DnsExpiredCount = (int)dnsExpiredCount,
                        DatabasePath = "主数据库 (DockerPanel.db)"
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "获取存储统计信息失败");
                    return new ChallengeStoreStatistics { DatabasePath = "主数据库 (DockerPanel.db)" };
                }
            });
        }

        private static bool TryGetCachedChallenge(
            ConcurrentDictionary<string, CachedChallengeValue> cache,
            string key,
            out string value)
        {
            value = string.Empty;

            if (!cache.TryGetValue(key, out var cached))
            {
                return false;
            }

            if (DateTime.UtcNow > cached.ExpiresAt)
            {
                cache.TryRemove(key, out _);
                return false;
            }

            value = cached.Value;
            return true;
        }

        private static int RemoveExpiredCachedChallenges(
            ConcurrentDictionary<string, CachedChallengeValue> cache,
            DateTime now)
        {
            var removed = 0;
            foreach (var item in cache)
            {
                if (now <= item.Value.ExpiresAt)
                {
                    continue;
                }

                if (cache.TryRemove(item.Key, out _))
                {
                    removed++;
                }
            }

            return removed;
        }

        private long SafeCountHttpChallenges(DateTime now, out long expiredCount)
        {
            try
            {
                expiredCount = _dbContext.HttpChallenges.Find(x => x.ExpiresAt < now).Count();
                return _dbContext.HttpChallenges.Count();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "统计 HTTP-01 挑战持久化数据失败，改用进程内缓存统计");
                expiredCount = HttpChallengeCache.Count(x => x.Value.ExpiresAt < now);
                return HttpChallengeCache.Count;
            }
        }

        private long SafeCountDnsChallenges(DateTime now, out long expiredCount)
        {
            try
            {
                expiredCount = _dbContext.DnsChallenges.Find(x => x.ExpiresAt < now).Count();
                return _dbContext.DnsChallenges.Count();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "统计 DNS-01 挑战持久化数据失败，改用进程内缓存统计");
                expiredCount = DnsChallengeCache.Count(x => x.Value.ExpiresAt < now);
                return DnsChallengeCache.Count;
            }
        }

        private sealed record CachedChallengeValue(string Value, DateTime ExpiresAt);

        /// <summary>
        /// 定期清理过期挑战数据
        /// </summary>
        private async Task PeriodicCleanup(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(30), cancellationToken); // 每30分钟清理一次
                    await CleanupExpiredChallengesAsync();

                    // 记录统计信息
                    var stats = await GetStatisticsAsync();
                    if (stats.HttpActiveCount > 0 || stats.DnsActiveCount > 0)
                    {
                        _logger.LogDebug("挑战存储统计: HTTP={HttpActive}个, DNS={DnsActive}个",
                            stats.HttpActiveCount, stats.DnsActiveCount);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "定期清理过期挑战数据时发生错误");
                }
            }
        }

        public void Dispose()
        {
            _cleanupCancellation.Cancel();
            _cleanupCancellation.Dispose();
            _dbContext.Dispose();
        }
    }

    /// <summary>
    /// HTTP-01 挑战数据实体
    /// </summary>
    [Entity("http_challenges")]
    public class HttpChallengeEntity
    {
        [Id]
        public string Token { get; set; } = string.Empty;

        public string KeyAuthorization { get; set; } = string.Empty;

        [Index]
        public DateTime ExpiresAt { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// DNS-01 挑战数据实体
    /// </summary>
    [Entity("dns_challenges")]
    public class DnsChallengeEntity
    {
        [Id]
        public string Domain { get; set; } = string.Empty;

        public string RecordName { get; set; } = string.Empty;

        public string RecordValue { get; set; } = string.Empty;

        [Index]
        public DateTime ExpiresAt { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// 挑战存储统计信息
    /// </summary>
    public class ChallengeStoreStatistics
    {
        public int HttpTotalCount { get; set; }
        public int HttpActiveCount { get; set; }
        public int HttpExpiredCount { get; set; }

        public int DnsTotalCount { get; set; }
        public int DnsActiveCount { get; set; }
        public int DnsExpiredCount { get; set; }

        public string DatabasePath { get; set; } = string.Empty;
        public long DatabaseSize { get; set; }
    }
}
