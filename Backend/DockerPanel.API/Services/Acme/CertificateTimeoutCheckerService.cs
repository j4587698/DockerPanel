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
                var ordersCollection = _db.GetCollection<AcmeCertificateOrder>("acme_orders");

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