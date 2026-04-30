using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DockerPanel.API.Models.Acme;

namespace DockerPanel.API.Services.Acme
{
    /// <summary>
    /// 证书自动申请和续期服务接口
    /// </summary>
    public interface ICertificateAutoService
    {
        /// <summary>
        /// 自动申请证书
        /// </summary>
        /// <param name="request">自动申请请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>申请结果</returns>
        Task<AutoCertificateResult> AutoRequestCertificateAsync(AutoCertificateRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 自动续期证书
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>续期结果</returns>
        Task<AutoRenewalResult> AutoRenewCertificateAsync(string certificateId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 批量自动续期即将到期的证书
        /// </summary>
        /// <param name="daysBeforeExpiry">到期前天数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>批量续期结果</returns>
        Task<BatchAutoRenewalResult> BatchAutoRenewCertificatesAsync(int daysBeforeExpiry = 15, CancellationToken cancellationToken = default);

        /// <summary>
        /// 设置证书自动续期配置
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="configuration">续期配置</param>
        /// <returns>配置结果</returns>
        Task<CertificateAutoRenewalConfigResult> SetAutoRenewalConfigurationAsync(string certificateId, AutoRenewalConfiguration configuration);

        /// <summary>
        /// 获取证书自动续期配置
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <returns>续期配置</returns>
        Task<AutoRenewalConfiguration?> GetAutoRenewalConfigurationAsync(string certificateId);

        /// <summary>
        /// 启用证书自动续期
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <returns>操作结果</returns>
        Task<bool> EnableAutoRenewalAsync(string certificateId);

        /// <summary>
        /// 禁用证书自动续期
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <returns>操作结果</returns>
        Task<bool> DisableAutoRenewalAsync(string certificateId);

        /// <summary>
        /// 获取即将到期的证书列表
        /// </summary>
        /// <param name="daysBeforeExpiry">到期前天数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>即将到期的证书列表</returns>
        Task<IEnumerable<ExpiringCertificate>> GetExpiringCertificatesAsync(int daysBeforeExpiry = 15, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取自动续期任务状态
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <returns>任务状态</returns>
        Task<AutoRenewalTaskStatus> GetAutoRenewalTaskStatusAsync(string taskId);

        /// <summary>
        /// 获取所有自动续期任务
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务列表</returns>
        Task<IEnumerable<AutoRenewalTaskStatus>> GetAllAutoRenewalTasksAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 取消自动续期任务
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <returns>取消结果</returns>
        Task<bool> CancelAutoRenewalTaskAsync(string taskId);

        /// <summary>
        /// 监控自动续期进度
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>进度更新流</returns>
        IAsyncEnumerable<AutoRenewalProgressUpdate> MonitorAutoRenewalProgressAsync(string taskId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 预检查证书申请条件
        /// </summary>
        /// <param name="request">申请请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>预检查结果</returns>
        Task<CertificatePreCheckResult> PreCheckCertificateRequestAsync(AutoCertificateRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取证书申请历史
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="limit">限制数量</param>
        /// <param name="offset">偏移量</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>申请历史</returns>
        Task<IEnumerable<CertificateRequestHistory>> GetCertificateRequestHistoryAsync(string certificateId, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取自动续期统计信息
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>统计信息</returns>
        Task<AutoRenewalStatistics> GetAutoRenewalStatisticsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 设置自动续期全局配置
        /// </summary>
        /// <param name="configuration">全局配置</param>
        /// <returns>配置结果</returns>
        Task<GlobalAutoRenewalConfigResult> SetGlobalAutoRenewalConfigurationAsync(GlobalAutoRenewalConfiguration configuration);

        /// <summary>
        /// 获取自动续期全局配置
        /// </summary>
        /// <returns>全局配置</returns>
        Task<GlobalAutoRenewalConfiguration> GetGlobalAutoRenewalConfigurationAsync();

        /// <summary>
        /// 测试自动续期流程
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>测试结果</returns>
        Task<AutoRenewalTestResult> TestAutoRenewalFlowAsync(string certificateId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 证书自动申请请求
    /// </summary>
    public class AutoCertificateRequest
    {
        public string AccountId { get; set; } = string.Empty;
        public List<string> Domains { get; set; } = new();
        public string KeyType { get; set; } = "RSA2048";
        public bool UseWildcard { get; set; }
        public List<string> PreferredChallengeTypes { get; set; } = new() { "http-01", "dns-01" };
        public Dictionary<string, Dictionary<string, object>>? DnsCredentials { get; set; }
        public bool EnableAutoRenewal { get; set; } = true;
        public int RenewalDaysBeforeExpiry { get; set; } = 15;
        public List<string> NotificationEmails { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public bool DryRun { get; set; } = false;
        public int RetryAttempts { get; set; } = 3;
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// 证书自动申请结果
    /// </summary>
    public class AutoCertificateResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? CertificateId { get; set; }
        public string? OrderId { get; set; }
        public List<string> Domains { get; set; } = new();
        public DateTime RequestedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string CertificateData { get; set; } = string.Empty;
        public string? PrivateKeyData { get; set; }
        public List<string> ValidationSteps { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> RequestDetails { get; set; } = new();
        public AutoRenewalConfiguration? AutoRenewalConfig { get; set; }
    }

    /// <summary>
    /// 自动续期结果
    /// </summary>
    public class AutoRenewalResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string CertificateId { get; set; } = string.Empty;
        public string? NewCertificateId { get; set; }
        public string? TaskId { get; set; }
        public DateTime RenewalStartedAt { get; set; }
        public DateTime? RenewalCompletedAt { get; set; }
        public List<string> RenewalSteps { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> RenewalDetails { get; set; } = new();
        public TimeSpan? TimeToNextRenewal { get; set; }
    }

    /// <summary>
    /// 批量自动续期结果
    /// </summary>
    public class BatchAutoRenewalResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime BatchStartedAt { get; set; }
        public DateTime? BatchCompletedAt { get; set; }
        public int TotalCertificates { get; set; }
        public int SuccessfulRenewals { get; set; }
        public int FailedRenewals { get; set; }
        public int SkippedRenewals { get; set; }
        public List<AutoRenewalResult> RenewalResults { get; set; } = new();
        public List<string> BatchErrors { get; set; } = new();
        public List<string> BatchWarnings { get; set; } = new();
        public TimeSpan TotalDuration { get; set; }
        public Dictionary<string, object> BatchStatistics { get; set; } = new();
    }

    /// <summary>
    /// 证书自动续期配置结果
    /// </summary>
    public class CertificateAutoRenewalConfigResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string CertificateId { get; set; } = string.Empty;
        public AutoRenewalConfiguration Configuration { get; set; } = new();
        public DateTime ConfiguredAt { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
    }

    /// <summary>
    /// 即将到期的证书
    /// </summary>
    public class ExpiringCertificate
    {
        public string CertificateId { get; set; } = string.Empty;
        public List<string> Domains { get; set; } = new();
        public DateTime ExpiresAt { get; set; }
        public int DaysUntilExpiry { get; set; }
        public bool AutoRenewalEnabled { get; set; }
        public DateTime? NextRenewalAttempt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public List<string> NotificationEmails { get; set; } = new();
    }

    /// <summary>
    /// 自动续期任务状态
    /// </summary>
    public class AutoRenewalTaskStatus
    {
        public string TaskId { get; set; } = string.Empty;
        public string CertificateId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // pending, running, completed, failed, cancelled
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int ProgressPercentage { get; set; }
        public string CurrentStep { get; set; } = string.Empty;
        public List<string> CompletedSteps { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> TaskDetails { get; set; } = new();
        public TimeSpan? EstimatedTimeRemaining { get; set; }
    }

    /// <summary>
    /// 自动续期进度更新
    /// </summary>
    public class AutoRenewalProgressUpdate
    {
        public string TaskId { get; set; } = string.Empty;
        public string CertificateId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int ProgressPercentage { get; set; }
        public string CurrentStep { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string? Message { get; set; }
        public Dictionary<string, object>? Details { get; set; }
    }

    /// <summary>
    /// 证书申请预检查结果
    /// </summary>
    public class CertificatePreCheckResult
    {
        public bool Passed { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> CheckResults { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public Dictionary<string, object> CheckDetails { get; set; } = new();
        public bool CanProceed { get; set; }
        public List<string> RecommendedActions { get; set; } = new();
    }

    /// <summary>
    /// 证书申请历史
    /// </summary>
    public class CertificateRequestHistory
    {
        public string Id { get; set; } = string.Empty;
        public string CertificateId { get; set; } = string.Empty;
        public string RequestType { get; set; } = string.Empty; // initial, renewal, reissue
        public DateTime RequestedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<string> Domains { get; set; } = new();
        public string AccountId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object> RequestDetails { get; set; } = new();
        public string? OrderId { get; set; }
    }

    /// <summary>
    /// 自动续期统计信息
    /// </summary>
    public class AutoRenewalStatistics
    {
        public int TotalCertificates { get; set; }
        public int CertificatesWithAutoRenewal { get; set; }
        public int SuccessfulRenewalsLast30Days { get; set; }
        public int FailedRenewalsLast30Days { get; set; }
        public int ExpiringNext30Days { get; set; }
        public int ExpiringNext7Days { get; set; }
        public DateTime LastRenewalCheck { get; set; }
        public DateTime NextScheduledCheck { get; set; }
        public Dictionary<string, int> RenewalSuccessRateByProvider { get; set; } = new();
        public Dictionary<string, int> RenewalFailureReasons { get; set; } = new();
        public List<string> MostRecentRenewals { get; set; } = new();
        public List<string> UpcomingRenewals { get; set; } = new();
    }

    /// <summary>
    /// 全局自动续期配置
    /// </summary>
    public class GlobalAutoRenewalConfiguration
    {
        public bool GlobalAutoRenewalEnabled { get; set; } = true;
        public int DefaultRenewalDaysBeforeExpiry { get; set; } = 15;
        public TimeSpan RenewalCheckInterval { get; set; } = TimeSpan.FromHours(6);
        public int MaxConcurrentRenewals { get; set; } = 5;
        public TimeSpan RenewalTimeout { get; set; } = TimeSpan.FromHours(2);
        public int DefaultRetryAttempts { get; set; } = 3;
        public TimeSpan DefaultRetryDelay { get; set; } = TimeSpan.FromMinutes(5);
        public List<string> DefaultNotificationEmails { get; set; } = new();
        public bool EnableRenewalNotifications { get; set; } = true;
        public bool EnableFailureNotifications { get; set; } = true;
        public bool EnableSuccessNotifications { get; set; } = false;
        public Dictionary<string, object> ProviderSettings { get; set; } = new();
        public Dictionary<string, object> NotificationSettings { get; set; } = new();
    }

    /// <summary>
    /// 全局自动续期配置结果
    /// </summary>
    public class GlobalAutoRenewalConfigResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public GlobalAutoRenewalConfiguration Configuration { get; set; } = new();
        public DateTime ConfiguredAt { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
        public List<string> AppliedChanges { get; set; } = new();
    }

    /// <summary>
    /// 自动续期测试结果
    /// </summary>
    public class AutoRenewalTestResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string CertificateId { get; set; } = string.Empty;
        public DateTime TestStartedAt { get; set; }
        public DateTime? TestCompletedAt { get; set; }
        public List<string> TestSteps { get; set; } = new();
        public List<string> PassedTests { get; set; } = new();
        public List<string> FailedTests { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> TestResults { get; set; } = new();
        public bool CanAutoRenew { get; set; }
        public List<string> Recommendations { get; set; } = new();
        public TimeSpan? EstimatedRenewalTime { get; set; }
    }
}