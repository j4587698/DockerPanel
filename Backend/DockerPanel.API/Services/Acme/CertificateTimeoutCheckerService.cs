using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DockerPanel.API.Services.Acme;
using TinyDb;
using TinyDb.Bson;
using TinyDb.Core;
using TinyDb.Collections;
using DockerPanel.API.Models;
using DockerPanel.API.Models.Acme;

namespace DockerPanel.API.Services
{
    /// <summary>
    /// 证书申请超时检查服务 - 定期检查长时间未完成的证书申请
    /// </summary>
    public class CertificateTimeoutCheckerService : IHostedService, IDisposable
    {
        private readonly ILogger<CertificateTimeoutCheckerService> _logger;
        private readonly ICertificateProgressService _progressService;
        private readonly Timer? _timer;
        private readonly TinyDbEngine _db;
        private readonly TimeSpan _certificateTimeout = TimeSpan.FromMinutes(10); // 证书申请10分钟超时
        private readonly TimeSpan _failedOrderRetention = TimeSpan.FromHours(24); // 失败订单保留24小时后自动清理

        public CertificateTimeoutCheckerService(
            ILogger<CertificateTimeoutCheckerService> logger,
            ICertificateProgressService progressService,
            TinyDbEngine db)
        {
            _logger = logger;
            _progressService = progressService;
            _db = db;

            // 每分钟检查一次超时
            _timer = new Timer(CheckTimeouts, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("证书超时检查服务已启动");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("证书超时检查服务正在停止");
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        private async void CheckTimeouts(object? state)
        {
            try
            {
                // 检查内存中的进度记录
                await _progressService.CheckAndMarkTimeoutsAsync();

                // 检查数据库中的证书订单
                await CheckDatabaseCertificateTimeoutsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "证书超时检查失败");
            }
        }

        /// <summary>
        /// 检查数据库中过期的证书订单
        /// </summary>
        private async Task CheckDatabaseCertificateTimeoutsAsync()
        {
            try
            {
                var now = DateTime.UtcNow;
                var timeoutThreshold = now.Subtract(_certificateTimeout); // 计算阈值时间
                var ordersCollection = _db.GetCollection<AcmeCertificateOrder>(DbCollections.AcmeOrders);

                // 查找过期的pending证书订单
                var expiredOrders = ordersCollection.Find(o =>
                    o.Status == "pending" &&
                    o.CreatedAt < timeoutThreshold
                ).ToList();

                if (expiredOrders.Any())
                {
                    _logger.LogInformation("发现 {Count} 个过期的pending证书订单", expiredOrders.Count);

                    foreach (var order in expiredOrders)
                    {
                        // 更新订单状态为failed
                        order.Status = "failed";

                        // 添加失败原因到metadata
                        if (order.Metadata == null)
                            order.Metadata = new Dictionary<string, object>();

                        order.Metadata["timeoutReason"] = $"证书申请超时（{_certificateTimeout.TotalMinutes}分钟）";
                        order.Metadata["timeoutAt"] = now.ToString("yyyy-MM-dd HH:mm:ss");

                        ordersCollection.Update(order);

                        _logger.LogWarning("证书订单已标记为超时失败: {OrderId}, 域名: {Domains}, 创建时间: {CreatedAt}",
                            order.Id, string.Join(", ", order.Domains ?? new List<string>()), order.CreatedAt);
                    }
                }

                // 同域名集合的重复"有效"证书记录去重：保留最合适的一条，旧的标记 Superseded 并改绑域名映射。
                // 历史上 FinalizeCertificateAsync 无幂等保护 + 续期入参歧义，造成同域名多条"有效"记录堆积
                try
                {
                    var certificatesCollection = _db.GetCollection<CertificateRecord>(DbCollections.Certificates);
                    var domainMappingsCollection = _db.GetCollection<DomainMapping>("domain_mappings");
                    var allCertificates = certificatesCollection.FindAll().ToList();

                    // 状态大小写历史上混用（Active/active），这里统一按大小写无关判定
                    var activeCerts = allCertificates
                        .Where(c => CertificateValidity.HasStatus(c.Status, "Active"))
                        .ToList();

                    var duplicateGroups = activeCerts
                        .Where(c => c.Domains != null && c.Domains.Count > 0)
                        .GroupBy(c => string.Join("|", c.Domains.Select(d => d.Trim()).OrderBy(d => d, StringComparer.OrdinalIgnoreCase)))
                        .Where(g => g.Count() > 1);

                    foreach (var group in duplicateGroups)
                    {
                        // 优先保留仍在承载 TLS 的、到期时间最晚的
                        var keep = group
                            .OrderByDescending(c => CertificateValidity.IsServing(c, now))
                            .ThenByDescending(c => c.ExpiresAt)
                            .ThenByDescending(c => c.CreatedAt)
                            .First();

                        foreach (var stale in group.Where(c => !string.Equals(c.Id, keep.Id, StringComparison.Ordinal)))
                        {
                            stale.Status = "Superseded";
                            stale.UpdatedAt = DateTime.UtcNow;
                            stale.Metadata ??= new Dictionary<string, object>();
                            stale.Metadata["supersededBy"] = keep.Id;
                            certificatesCollection.Update(stale);

                            foreach (var boundMapping in domainMappingsCollection.Find(m => m.CertificateId == stale.Id).ToList())
                            {
                                boundMapping.CertificateId = keep.Id;
                                boundMapping.UpdatedAt = DateTime.UtcNow;
                                domainMappingsCollection.Update(boundMapping);
                            }

                            _logger.LogInformation("重复证书记录已去重: Domain={Domain}, 保留={KeepId}, 覆盖={StaleId}",
                                keep.Name, keep.Id, stale.Id);
                        }
                    }

                    // 存量自愈：域名映射指向缺失/过期/撤销/被覆盖的证书时，改绑到同域名下仍有效的证书。
                    // 没有可用替代证书时保持原样（SNI 选择器会按域名回退），避免误清空绑定。
                    try
                    {
                        var healed = 0;
                        var certificateById = allCertificates
                            .Where(c => !string.IsNullOrEmpty(c.Id))
                            .GroupBy(c => c.Id, StringComparer.Ordinal)
                            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

                        foreach (var mapping in domainMappingsCollection.FindAll().ToList())
                        {
                            if (string.IsNullOrWhiteSpace(mapping.CertificateId))
                            {
                                continue;
                            }

                            if (certificateById.TryGetValue(mapping.CertificateId!, out var bound) &&
                                CertificateValidity.IsServing(bound, now))
                            {
                                continue;
                            }

                            var replacement = CertificateValidity.FindBestForDomain(
                                allCertificates, mapping.Domain, mapping.CertificateId, now);

                            if (replacement == null)
                            {
                                continue;
                            }

                            var oldId = mapping.CertificateId;
                            mapping.CertificateId = replacement.Id;
                            mapping.EnableSsl = true;
                            mapping.UpdatedAt = DateTime.UtcNow;
                            domainMappingsCollection.Update(mapping);
                            healed++;

                            _logger.LogInformation("域名映射已从失效证书改绑到有效证书: Domain={Domain}, Old={OldId}, New={NewId}",
                                mapping.Domain, oldId, replacement.Id);
                        }

                        if (healed > 0)
                        {
                            _logger.LogInformation("已自动修复 {Count} 个指向失效证书的域名映射", healed);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "修复域名映射的失效证书绑定失败");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "重复证书记录去重失败");
                }

                // 清理超过保留期的失败订单（未被任何证书记录引用），避免失败记录无限堆积
                try
                {
                    var retentionCutoff = now.Subtract(_failedOrderRetention);
                    var certificatesCollection = _db.GetCollection<CertificateRecord>(DbCollections.Certificates);
                    var staleFailedOrders = ordersCollection.Find(o =>
                        o.Status == "failed" &&
                        o.CreatedAt < retentionCutoff
                    ).ToList();

                    foreach (var staleOrder in staleFailedOrders)
                    {
                        // 被证书记录引用的失败订单不清理，避免破坏关联
                        var referenced = certificatesCollection.Find(c =>
                            c.OrderId == staleOrder.Id || c.CertificateId == staleOrder.Id).Any();
                        if (referenced)
                        {
                            continue;
                        }

                        ordersCollection.Delete(staleOrder.Id);
                        _logger.LogInformation("已清理过期失败订单: {OrderId}, 域名: {Domains}",
                            staleOrder.Id, string.Join(", ", staleOrder.Domains ?? new List<string>()));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "清理过期失败订单失败");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查数据库证书超时失败");
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}