using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DockerPanel.API.Models.Acme;

namespace DockerPanel.API.Services.Acme
{
    /// <summary>
    /// 通配符证书服务接口
    /// </summary>
    public interface IWildcardCertificateService
    {
        /// <summary>
        /// 申请通配符证书
        /// </summary>
        /// <param name="request">通配符证书申请请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>申请结果</returns>
        Task<WildcardCertificateResult> RequestWildcardCertificateAsync(WildcardCertificateRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 续期通配符证书
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>续期结果</returns>
        Task<WildcardCertificateResult> RenewWildcardCertificateAsync(string certificateId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 验证通配符证书申请前置条件
        /// </summary>
        /// <param name="request">申请请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>验证结果</returns>
        Task<WildcardCertificateValidationResult> ValidateWildcardRequestAsync(WildcardCertificateRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取支持的通配符证书类型
        /// </summary>
        /// <returns>支持的证书类型列表</returns>
        Task<IEnumerable<WildcardCertificateType>> GetSupportedWildcardTypesAsync();

        /// <summary>
        /// 检查域名是否支持通配符证书
        /// </summary>
        /// <param name="domain">域名</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>检查结果</returns>
        Task<WildcardDomainSupportResult> CheckWildcardSupportAsync(string domain, CancellationToken cancellationToken = default);

        /// <summary>
        /// 生成通配符证书CSR
        /// </summary>
        /// <param name="request">CSR生成请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>CSR字符串</returns>
        Task<string> GenerateWildcardCsrAsync(WildcardCsrRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 配置DNS挑战用于通配符证书
        /// </summary>
        /// <param name="request">DNS挑战配置请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>配置结果</returns>
        Task<WildcardDnsChallengeResult> ConfigureWildcardDnsChallengeAsync(WildcardDnsChallengeRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 验证DNS挑战状态
        /// </summary>
        /// <param name="request">验证请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>验证结果</returns>
        Task<WildcardDnsChallengeResult> ValidateWildcardDnsChallengeAsync(WildcardDnsChallengeRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 清理通配符证书DNS挑战配置
        /// </summary>
        /// <param name="request">清理请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>清理结果</returns>
        Task<WildcardDnsChallengeCleanupResult> CleanupWildcardDnsChallengeAsync(WildcardDnsChallengeCleanupRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取通配符证书列表
        /// </summary>
        /// <param name="accountId">可选账户ID过滤</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>通配符证书列表</returns>
        Task<IEnumerable<WildcardCertificateInfo>> GetWildcardCertificatesAsync(string? accountId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取通配符证书详情
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>证书详情</returns>
        Task<WildcardCertificateInfo?> GetWildcardCertificateAsync(string certificateId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 删除通配符证书
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>删除结果</returns>
        Task<WildcardCertificateDeletionResult> DeleteWildcardCertificateAsync(string certificateId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 强制删除通配符证书（用于处理超时或异常状态的证书）
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>删除结果</returns>
        Task<WildcardCertificateDeletionResult> ForceDeleteWildcardCertificateAsync(string certificateId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 导出通配符证书
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="format">导出格式</param>
        /// <param name="includePrivateKey">是否包含私钥</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>导出结果</returns>
        Task<WildcardCertificateExportResult> ExportWildcardCertificateAsync(string certificateId, string format = "pem", bool includePrivateKey = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// 导入通配符证书
        /// </summary>
        /// <param name="request">导入请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>导入结果</returns>
        Task<WildcardCertificateImportResult> ImportWildcardCertificateAsync(WildcardCertificateImportRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取通配符证书统计信息
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>统计信息</returns>
        Task<WildcardCertificateStatistics> GetWildcardCertificateStatisticsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 测试通配符证书申请流程
        /// </summary>
        /// <param name="request">测试请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>测试结果</returns>
        Task<WildcardCertificateTestResult> TestWildcardCertificateFlowAsync(WildcardCertificateRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 预览通配符证书申请配置
        /// </summary>
        /// <param name="request">申请请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>配置预览</returns>
        Task<WildcardCertificatePreview> PreviewWildcardConfigurationAsync(WildcardCertificateRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取通配符证书详情
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>证书详情</returns>
        Task<WildcardCertificateDetails?> GetWildcardCertificateDetailsAsync(string certificateId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 验证通配符证书
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>验证结果</returns>
        Task<AcmeCertificateValidationResult> ValidateWildcardCertificateAsync(string certificateId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取支持的DNS提供商列表
        /// </summary>
        /// <returns>DNS提供商列表</returns>
        IEnumerable<DnsProviderInfo> GetSupportedDnsProviders();

        /// <summary>
        /// 批量操作通配符证书
        /// </summary>
        /// <param name="request">批量操作请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>批量操作结果</returns>
        Task<WildcardCertificateBatchResult> BatchOperationWildcardCertificatesAsync(WildcardCertificateBatchRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 检查通配符证书状态
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>证书状态</returns>
        Task<WildcardCertificateStatus> CheckWildcardCertificateStatusAsync(string certificateId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 自动配置通配符证书挑战
        /// </summary>
        /// <param name="request">自动挑战请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>配置结果</returns>
        Task<WildcardAutoChallengeResult> AutoConfigureWildcardChallengeAsync(WildcardAutoChallengeRequest request, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 通配符证书验证结果
    /// </summary>
    public class WildcardCertificateValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool Passed { get; set; }
        public List<string> ValidationChecks { get; set; } = new();
        public List<string> PassedChecks { get; set; } = new();
        public List<string> FailedChecks { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> ValidationDetails { get; set; } = new();
        public bool CanProceed { get; set; }
        public List<string> RecommendedActions { get; set; } = new();
        public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 通配符证书类型
    /// </summary>
    public class WildcardCertificateType
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> SupportedKeyTypes { get; set; } = new();
        public List<string> SupportedDnsProviders { get; set; } = new();
        public int MaxValidityDays { get; set; }
        public bool SupportsMultiDomain { get; set; }
        public Dictionary<string, object> Configuration { get; set; } = new();
    }

    /// <summary>
    /// 通配符域名支持结果
    /// </summary>
    public class WildcardDomainSupportResult
    {
        public string Domain { get; set; } = string.Empty;
        public bool SupportsWildcard { get; set; }
        public string? WildcardPattern { get; set; }
        public List<string> SupportedProviders { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public List<string> PotentialIssues { get; set; } = new();
        public Dictionary<string, object> SupportDetails { get; set; } = new();
    }

    /// <summary>
    /// 通配符CSR请求
    /// </summary>
    public class WildcardCsrRequest
    {
        public string BaseDomain { get; set; } = string.Empty;
        public List<string> Subdomains { get; set; } = new();
        public string KeyType { get; set; } = "RSA2048";
        public Dictionary<string, object> SubjectInfo { get; set; } = new();
        public Dictionary<string, object> Extensions { get; set; } = new();
    }

    /// <summary>
    /// 通配符DNS挑战请求
    /// </summary>
    public class WildcardDnsChallengeRequest
    {
        public string Domain { get; set; } = string.Empty;
        public string ChallengeToken { get; set; } = string.Empty;
        public string KeyAuthorization { get; set; } = string.Empty;
        public string DnsProvider { get; set; } = string.Empty;
        public Dictionary<string, object>? Credentials { get; set; }
        public string RecordName { get; set; } = string.Empty;
        public string RecordValue { get; set; } = string.Empty;
        public string RecordType { get; set; } = "TXT";
        public Dictionary<string, object> ChallengeDetails { get; set; } = new();
    }

    /// <summary>
    /// 通配符DNS挑战结果
    /// </summary>
    public class WildcardDnsChallengeResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string DnsProvider { get; set; } = string.Empty;
        public string RecordName { get; set; } = string.Empty;
        public string RecordValue { get; set; } = string.Empty;
        public string RecordType { get; set; } = string.Empty;
        public DateTime ConfiguredAt { get; set; }
        public DateTime? ValidatedAt { get; set; }
        public List<string> ConfigurationSteps { get; set; } = new();
        public List<string> ValidationSteps { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> ChallengeDetails { get; set; } = new();
        public Dictionary<string, object> ConfigurationDetails { get; set; } = new();
        public TimeSpan? DnsPropagationTime { get; set; }
    }

    /// <summary>
    /// 通配符DNS挑战清理请求
    /// </summary>
    public class WildcardDnsChallengeCleanupRequest
    {
        public string Domain { get; set; } = string.Empty;
        public string DnsProvider { get; set; } = string.Empty;
        public Dictionary<string, object>? Credentials { get; set; }
        public string RecordName { get; set; } = string.Empty;
        public string RecordType { get; set; } = "TXT";
        public Dictionary<string, object> CleanupDetails { get; set; } = new();
    }

    /// <summary>
    /// 通配符DNS挑战清理结果
    /// </summary>
    public class WildcardDnsChallengeCleanupResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string DnsProvider { get; set; } = string.Empty;
        public string RecordName { get; set; } = string.Empty;
        public string RecordType { get; set; } = string.Empty;
        public DateTime CleanedAt { get; set; }
        public List<string> CleanupSteps { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> CleanupDetails { get; set; } = new();
    }

    /// <summary>
    /// 通配符证书信息
    /// </summary>
    public class WildcardCertificateInfo
    {
        public string Id { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public string BaseDomain { get; set; } = string.Empty;
        public List<string> Subdomains { get; set; } = new();
        public List<string> FullDomains { get; set; } = new();
        public string KeyType { get; set; } = string.Empty;
        public string CertificateFingerprint { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string Issuer { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public List<string> SanDomains { get; set; } = new();
        public string Status { get; set; } = string.Empty; // active, expired, revoked, pending
        public bool IsWildcard { get; set; }
        public string DnsProvider { get; set; } = string.Empty;
        public bool AutoRenewalEnabled { get; set; }
        public DateTime? LastRenewalAttempt { get; set; }
        public DateTime? NextRenewalAttempt { get; set; }
        public int RenewalAttempts { get; set; }
        public int RenewalDaysBeforeExpiry { get; set; } = 15;
        public List<string> NotificationEmails { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public DateTime? ImportedAt { get; set; }
        public string CertificateData { get; set; } = string.Empty;
        public string PrivateKeyData { get; set; } = string.Empty;
        public int DaysUntilExpiry => (int)(ExpiresAt - DateTime.UtcNow).TotalDays;
    }

    /// <summary>
    /// 通配符证书删除结果
    /// </summary>
    public class WildcardCertificateDeletionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string CertificateId { get; set; } = string.Empty;
        public DateTime DeletedAt { get; set; }
        public List<string> DeletionSteps { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> DeletionDetails { get; set; } = new();
    }

    /// <summary>
    /// 通配符证书导出结果
    /// </summary>
    public class WildcardCertificateExportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string CertificateId { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public string ExportData { get; set; } = string.Empty;
        public DateTime ExportedAt { get; set; }
        public string? FileName { get; set; }
        public long FileSize { get; set; }
        public List<string> ExportSteps { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public Dictionary<string, object> ExportDetails { get; set; } = new();
    }

    /// <summary>
    /// 通配符证书导入请求
    /// </summary>
    public class WildcardCertificateImportRequest
    {
        public string AccountId { get; set; } = string.Empty;
        public string CertificateData { get; set; } = string.Empty;
        public string? PrivateKeyData { get; set; }
        public string Format { get; set; } = "pem";
        public string? Password { get; set; }
        public bool EnableAutoRenewal { get; set; } = true;
        public int RenewalDaysBeforeExpiry { get; set; } = 15;
        public List<string> NotificationEmails { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public bool ValidateCertificate { get; set; } = true;
        public List<string> Domains { get; set; } = new();
    }

    /// <summary>
    /// 通配符证书导入结果
    /// </summary>
    public class WildcardCertificateImportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? CertificateId { get; set; }
        public string BaseDomain { get; set; } = string.Empty;
        public List<string> Subdomains { get; set; } = new();
        public List<string> FullDomains { get; set; } = new();
        public DateTime ImportedAt { get; set; }
        public string CertificateFingerprint { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public List<string> ImportSteps { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> ImportDetails { get; set; } = new();
        public WildcardCertificateValidationResult? ValidationResult { get; set; }
    }

    /// <summary>
    /// 通配符证书统计信息
    /// </summary>
    public class WildcardCertificateStatistics
    {
        public int TotalWildcardCertificates { get; set; }
        public int ActiveCertificates { get; set; }
        public int ExpiredCertificates { get; set; }
        public int RevokedCertificates { get; set; }
        public int CertificatesExpiringNext30Days { get; set; }
        public int CertificatesExpiringNext7Days { get; set; }
        public Dictionary<string, int> CertificatesByProvider { get; set; } = new();
        public Dictionary<string, int> CertificatesByKeyType { get; set; } = new();
        public Dictionary<string, int> CertificatesByStatus { get; set; } = new();
        public DateTime LastCertificateIssued { get; set; }
        public DateTime NextScheduledRenewal { get; set; }
        public List<string> MostRecentCertificates { get; set; } = new();
        public List<string> UpcomingExpirations { get; set; } = new();
        public double AverageRenewalSuccessRate { get; set; }
        public int TotalRenewalAttempts { get; set; }
        public int SuccessfulRenewals { get; set; }
        public int FailedRenewals { get; set; }
    }

    /// <summary>
    /// 通配符证书测试结果
    /// </summary>
    public class WildcardCertificateTestResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string BaseDomain { get; set; } = string.Empty;
        public DateTime TestStartedAt { get; set; }
        public DateTime? TestCompletedAt { get; set; }
        public List<string> TestSteps { get; set; } = new();
        public List<string> PassedTests { get; set; } = new();
        public List<string> FailedTests { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public Dictionary<string, object> TestResults { get; set; } = new();
        public bool CanIssueCertificate { get; set; }
        public List<string> Recommendations { get; set; } = new();
        public TimeSpan? EstimatedIssueTime { get; set; }
        public WildcardCertificateValidationResult? ValidationResult { get; set; }
        public WildcardDnsChallengeResult? DnsChallengeResult { get; set; }
    }

    /// <summary>
    /// 通配符证书配置预览
    /// </summary>
    public class WildcardCertificatePreview
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public string BaseDomain { get; set; } = string.Empty;
        public List<string> Subdomains { get; set; } = new();
        public List<string> FullDomains { get; set; } = new();
        public string KeyType { get; set; } = string.Empty;
        public string SelectedDnsProvider { get; set; } = string.Empty;
        public int CertificateValidityDays { get; set; }
        public DateTime EstimatedIssuedAt { get; set; }
        public DateTime EstimatedExpiresAt { get; set; }
        public List<string> ConfigurationSteps { get; set; } = new();
        public List<string> PotentialIssues { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public Dictionary<string, object> ConfigurationDetails { get; set; } = new();
        public Dictionary<string, object> CostEstimate { get; set; } = new();
    }

    /// <summary>
    /// 通配符证书证书导入结果（兼容性类型）
    /// </summary>
    public class WildcardCertificateCertificateImportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? CertificateId { get; set; }
        public DateTime ImportedAt { get; set; }
        public List<string> ImportSteps { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> ImportDetails { get; set; } = new();
    }
}