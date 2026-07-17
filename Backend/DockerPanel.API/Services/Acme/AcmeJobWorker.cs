using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DockerPanel.API.Services.Acme
{
    public class AcmeJobWorker : BackgroundService
    {
        private readonly ILogger<AcmeJobWorker> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5); // 每 5 秒轮询一次

        public AcmeJobWorker(ILogger<AcmeJobWorker> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ACME 持久化任务队列消费者已启动");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessNextJobAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ACME 任务队列轮询发生未捕获异常");
                }

                // 避免过快轮询
                await Task.Delay(_pollingInterval, stoppingToken);
            }

            _logger.LogInformation("ACME 持久化任务队列消费者已停止");
        }

        private async Task ProcessNextJobAsync(CancellationToken stoppingToken)
        {
            // 每次处理创建一个新 scope，以保证 DbContext 等生命周期干净
            using var scope = _serviceProvider.CreateScope();
            var queueService = scope.ServiceProvider.GetRequiredService<AcmeJobQueueService>();
            var acmeService = scope.ServiceProvider.GetRequiredService<IAcmeService>();

            var job = await queueService.DequeueAsync();
            if (job == null)
            {
                return; // 没有任务
            }

            try
            {
                _logger.LogInformation("从 TinyDb 拉取到 ACME 待处理任务: JobId={JobId}, Type={Type}", job.Id, job.JobType);
                
                if (job.JobType == "AutoValidation")
                {
                    await acmeService.ProcessJobAsync(job);
                }
                else if (job.JobType == "AutoRenewal")
                {
                    var autoService = scope.ServiceProvider.GetRequiredService<ICertificateAutoService>();
                    var payload = System.Text.Json.JsonSerializer.Deserialize<AutoRenewalJobPayload>(job.Payload);
                    if (payload != null && !string.IsNullOrEmpty(payload.CertificateId))
                    {
                        await autoService.AutoRenewCertificateAsync(payload.CertificateId, stoppingToken);
                    }
                    else
                    {
                        throw new ArgumentException("AutoRenewal 任务的 Payload 无效");
                    }
                }
                else
                {
                    throw new NotSupportedException($"不支持的 ACME 任务类型: {job.JobType}");
                }
                
                await queueService.MarkAsCompletedAsync(job.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行 ACME 任务失败: JobId={JobId}, Type={Type}", job.Id, job.JobType);
                await queueService.MarkAsFailedAsync(job.Id, ex.Message);
            }
        }

        private class AutoRenewalJobPayload
        {
            public string CertificateId { get; set; } = string.Empty;
        }
    }
}
