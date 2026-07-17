using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DockerPanel.API.Models.Acme;
using DockerPanel.API.Data;
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

        public async Task<string> EnqueueAsync(string jobType, object payload)
        {
            var job = new AcmeJobRecord
            {
                JobType = jobType,
                Payload = JsonSerializer.Serialize(payload),
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

        public async Task<AcmeJobRecord?> DequeueAsync()
        {
            try
            {
                var collection = _dbContext.GetCollection<AcmeJobRecord>(DbCollections.AcmeJobs);
                
                // 查找 Pending，或者 Processing 时间过长（比如超过 10 分钟）被认为假死可以重试的任务
                var timeoutThreshold = DateTime.UtcNow.AddMinutes(-10);
                var jobs = collection.FindAll().ToList();
                
                var jobToProcess = jobs.FirstOrDefault(j => 
                    j.Status == "Pending" || 
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
