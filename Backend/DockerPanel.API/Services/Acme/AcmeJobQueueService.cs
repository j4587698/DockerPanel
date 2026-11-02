using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DockerPanel.API.Models.Acme;
using DockerPanel.API.Data;
using DockerPanel.API.Serialization;
using Microsoft.Extensions.Logging;
using TinyDb;

namespace DockerPanel.API.Services.Acme
{
    public class AcmeJobQueueService
    {
        private readonly TinyDbContext _dbContext;
        private readonly ILogger<AcmeJobQueueService> _logger;

        public AcmeJobQueueService(TinyDbContext dbContext, ILogger<AcmeJobQueueService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<string> EnqueueAsync<T>(string jobType, T payload, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
        {
            var job = new AcmeJobRecord
            {
                JobType = jobType,
                Payload = JsonSerializer.Serialize(payload, typeInfo),
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            try
            {
                var collection = _dbContext.GetCollection<AcmeJobRecord>(DbCollections.AcmeJobs);
                collection.Insert(job);
                _logger.LogInformation("ACME任务入队成功: JobId={JobId}, JobType={JobType}", job.Id, job.JobType);
                return job.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ACME任务入队失败: JobType={JobType}", jobType);
                throw;
            }
        }

        /// <summary>
        /// 判断指定类型 + 指定证书是否已存在待处理/处理中的任务（用于入队去重）
        /// </summary>
        public async Task<bool> HasActiveJobAsync(string jobType, string certificateId)
        {
            try
            {
                await Task.CompletedTask;

                if (string.IsNullOrWhiteSpace(certificateId))
                {
                    return false;
                }

                var collection = _dbContext.GetCollection<AcmeJobRecord>(DbCollections.AcmeJobs);
                var activeJobs = collection
                    .FindAll()
                    .Where(j => string.Equals(j.JobType, jobType, StringComparison.OrdinalIgnoreCase) &&
                                (j.Status == "Pending" || j.Status == "Processing"))
                    .ToList();

                foreach (var job in activeJobs)
                {
                    if (string.IsNullOrWhiteSpace(job.Payload))
                    {
                        continue;
                    }

                    try
                    {
                        using var document = JsonDocument.Parse(job.Payload);
                        if (document.RootElement.TryGetProperty("CertificateId", out var property) &&
                            string.Equals(property.GetString(), certificateId, StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }
                    catch (JsonException)
                    {
                        // 忽略无法解析的历史任务载荷
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查重复 ACME 任务失败: JobType={JobType}, CertificateId={CertificateId}", jobType, certificateId);
                return false;
            }
        }

        public async Task<AcmeJobRecord?> DequeueAsync()
        {
            try
            {
                var collection = _dbContext.GetCollection<AcmeJobRecord>(DbCollections.AcmeJobs);
                
                // 查找 Pending，或者 Processing 时间过长（比如超过 10 分钟）被认为假死可以重试的任务
                var timeoutThreshold = DateTime.UtcNow.AddMinutes(-10);
                var jobs = collection.FindAll().ToList();
                
                var jobToProcess = jobs.FirstOrDefault(j => 
                    (j.Status == "Pending" && (j.NextAttemptAt == null || j.NextAttemptAt <= DateTime.UtcNow)) ||
                    (j.Status == "Processing" && j.UpdatedAt < timeoutThreshold));

                if (jobToProcess != null)
                {
                    jobToProcess.Status = "Processing";
                    jobToProcess.UpdatedAt = DateTime.UtcNow;
                    collection.Update(jobToProcess);
                    return jobToProcess;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取待处理的ACME任务失败");
                return null;
            }
        }

        public async Task MarkAsCompletedAsync(string jobId)
        {
            try
            {
                var collection = _dbContext.GetCollection<AcmeJobRecord>(DbCollections.AcmeJobs);
                var job = collection.FindById(jobId);
                if (job != null)
                {
                    job.Status = "Completed";
                    job.UpdatedAt = DateTime.UtcNow;
                    collection.Update(job);
                    _logger.LogInformation("ACME任务执行成功: JobId={JobId}", jobId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "标记ACME任务为成功时失败: JobId={JobId}", jobId);
            }
        }

        public async Task MarkAsFailedAsync(string jobId, string errorMessage)
        {
            try
            {
                var collection = _dbContext.GetCollection<AcmeJobRecord>(DbCollections.AcmeJobs);
                var job = collection.FindById(jobId);
                if (job != null)
                {
                    job.RetryCount++;
                    if (job.RetryCount >= 3)
                    {
                        job.Status = "Failed";
                    }
                    else
                    {
                        job.Status = "Pending"; // 允许重试
                        // 退避重试：5 分钟、10 分钟，避免失败后立即重复向 CA 下单触发限流
                        job.NextAttemptAt = DateTime.UtcNow.AddMinutes(5 * job.RetryCount);
                    }
                    
                    job.ErrorMessage = errorMessage;
                    job.UpdatedAt = DateTime.UtcNow;
                    collection.Update(job);
                    _logger.LogWarning("ACME任务执行失败 (Retry: {Retry}): JobId={JobId}, Error={Error}", job.RetryCount, jobId, errorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "标记ACME任务为失败时发生异常: JobId={JobId}", jobId);
            }
        }
    }
}
