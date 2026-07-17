using System;
using System.Text.Json;
using System.Threading.Tasks;
using DockerPanel.API.Models.Acme;
using Microsoft.Extensions.Logging;

namespace DockerPanel.API.Services.Acme
{
    public partial class CertesAcmeService
    {
        public async Task ProcessJobAsync(AcmeJobRecord job)
        {
            _logger.LogInformation("开始处理 ACME 任务: JobId={JobId}, JobType={JobType}", job.Id, job.JobType);

            try
            {
                if (job.JobType == "AutoValidation")
                {
                    await ProcessAutoValidationJobAsync(job.Payload);
                }
                else
                {
                    throw new NotSupportedException($"CertesAcmeService 不支持的任务类型: {job.JobType}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理 ACME 任务时发生严重异常: JobId={JobId}, JobType={JobType}", job.Id, job.JobType);
                throw; // 让外层的 Queue Service 捕获并记录失败
            }
        }

        private async Task ProcessAutoValidationJobAsync(string payloadJson)
        {
            var payload = JsonSerializer.Deserialize<AutoValidationJobPayload>(payloadJson);
            if (payload == null || string.IsNullOrEmpty(payload.OrderId))
            {
                throw new ArgumentException("AutoValidation 任务的 Payload 无效");
            }

            var order = _dbContext.GetCollection<AcmeCertificateOrder>(DbCollections.AcmeOrders).FindById(payload.OrderId);
            if (order == null)
            {
                throw new InvalidOperationException($"订单不存在: {payload.OrderId}");
            }

            try
            {
                _logger.LogInformation("持久化任务开始处理订单验证: OrderId={OrderId}", order.Id);

                if (!string.IsNullOrEmpty(payload.ProgressId))
                {
                    await _progressService.UpdateProgressStepAsync(payload.ProgressId,
                        CertificateApplicationStep.ValidatingDomains,
                        "开始后台自动验证域名控制权 (持久化任务)");
                }

                // 执行自动验证
                var autoValidationSuccess = await PerformAutoValidationAsync(order, payload.ProgressId);

                var ordersCollection = _dbContext.GetCollection<AcmeCertificateOrder>(DbCollections.AcmeOrders);

                if (autoValidationSuccess)
                {
                    _logger.LogInformation("自动验证成功: OrderId={OrderId}", order.Id);
                    if (!string.IsNullOrEmpty(payload.ProgressId))
                    {
                        await _progressService.CompleteCurrentStepAsync(payload.ProgressId, "所有域名验证成功");
                        await _progressService.MarkAsCompletedAsync(payload.ProgressId);
                    }
                }
                else
                {
                    _logger.LogError("自动验证失败，订单标记为失败状态: OrderId={OrderId}", order.Id);

                    order.Status = "failed";
                    order.Error = "自动验证失败：无法完成域名验证。请检查网络连接、防火墙设置或DNS配置。";

                    if (!string.IsNullOrEmpty(payload.ProgressId))
                    {
                        await _progressService.AddErrorAsync(payload.ProgressId, "自动验证失败，请检查配置后重试。");
                        await _progressService.MarkAsFailedAsync(payload.ProgressId, "验证失败");
                    }
                }

                ordersCollection.Update(order);
                _logger.LogInformation("订单状态已更新到数据库: OrderId={OrderId}, Status={Status}", order.Id, order.Status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行持久化验证任务时发生异常: OrderId={OrderId}", order.Id);
                
                // 将异常向上抛出，触发 TinyDb 队列的重试机制
                throw;
            }
        }
        
        // 定义 Payload 结构
        private class AutoValidationJobPayload
        {
            public string OrderId { get; set; } = string.Empty;
            public string? ProgressId { get; set; }
        }
    }
}
