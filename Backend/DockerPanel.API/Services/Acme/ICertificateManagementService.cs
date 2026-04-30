using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DockerPanel.API.Models.Acme;

namespace DockerPanel.API.Services.Acme
{
    /// <summary>
    /// 证书管理服务接口
    /// </summary>
    public interface ICertificateManagementService
    {
        /// <summary>
        /// 获取所有证书列表
        /// </summary>
        /// <param name="includeExpired">是否包含过期证书</param>
        /// <param name="certificateType">证书类型过滤</param>
        /// <param name="statusFilter">状态过滤</param>
        /// <param name="domainFilter">域名过滤</param>
        /// <param name="pageIndex">页索引</param>
        /// <param name="pageSize">页大小</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>证书分页列表</returns>
        Task<CertificateListResult> GetCertificatesAsync(
            bool includeExpired = false,
            string? certificateType = null,
            string? statusFilter = null,
            string? domainFilter = null,
            int pageIndex = 0,
            int pageSize = 50,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取证书详情
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>证书详情</returns>
        Task<CertificateDetails?> GetCertificateDetailsAsync(
            string certificateId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取即将到期的证书列表
        /// </summary>
        /// <param name="daysBeforeExpiry">到期前天数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>即将到期的证书列表</returns>
        Task<IEnumerable<ExpiringCertificate>> GetExpiringCertificatesAsync(
            int daysBeforeExpiry = 15,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 手动续期证书
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>续期结果</returns>
        Task<CertificateRenewalResult> RenewCertificateAsync(
            string certificateId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 启用证书自动续期
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="configuration">自动续期配置</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        Task<CertificateAutoRenewalConfigResult> EnableAutoRenewalAsync(
            string certificateId,
            AutoRenewalConfiguration configuration,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 禁用证书自动续期
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        Task<bool> DisableAutoRenewalAsync(
            string certificateId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 删除证书
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>删除结果</returns>
        Task<CertificateDeletionResult> DeleteCertificateAsync(
            string certificateId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 强制删除证书（用于处理超时或异常状态的证书）
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>删除结果</returns>
        Task<CertificateDeletionResult> ForceDeleteCertificateAsync(
            string certificateId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 导入证书
        /// </summary>
        /// <param name="request">导入请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>导入结果</returns>
        Task<CertificateImportResult> ImportCertificateAsync(
            CertificateImportRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 导出证书
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="format">导出格式</param>
        /// <param name="includePrivateKey">是否包含私钥</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>导出结果</returns>
        Task<CertificateExportResult> ExportCertificateAsync(
            string certificateId,
            string format = "pem",
            bool includePrivateKey = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 验证证书
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>验证结果</returns>
        Task<CertificateValidationResult> ValidateCertificateAsync(
            string certificateId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取证书使用统计
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>使用统计</returns>
        Task<CertificateUsageStatistics> GetCertificateUsageStatisticsAsync(
            string certificateId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取证书操作历史
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="operationType">操作类型过滤</param>
        /// <param name="limit">限制数量</param>
        /// <param name="offset">偏移量</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作历史</returns>
        Task<IEnumerable<CertificateOperationHistory>> GetCertificateOperationHistoryAsync(
            string certificateId,
            string? operationType = null,
            int limit = 50,
            int offset = 0,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 批量操作证书
        /// </summary>
        /// <param name="request">批量操作请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>批量操作结果</returns>
        Task<CertificateBatchOperationResult> BatchOperateCertificatesAsync(
            CertificateBatchOperationRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取证书列表统计信息
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>统计信息</returns>
        Task<CertificateListStatistics> GetCertificateListStatisticsAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 搜索证书
        /// </summary>
        /// <param name="searchTerm">搜索词</param>
        /// <param name="searchFields">搜索字段</param>
        /// <param name="pageIndex">页索引</param>
        /// <param name="pageSize">页大小</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>搜索结果</returns>
        Task<CertificateSearchResult> SearchCertificatesAsync(
            string searchTerm,
            IEnumerable<string>? searchFields = null,
            int pageIndex = 0,
            int pageSize = 50,
            CancellationToken cancellationToken = default);
    }

    #region Result Models

    /// <summary>
    /// 证书列表结果
    /// </summary>
    public class CertificateListResult
    {
        public IEnumerable<CertificateListItem> Certificates { get; set; } = new List<CertificateListItem>();
        public int TotalCount { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPreviousPage => PageIndex > 0;
        public bool HasNextPage => PageIndex < TotalPages - 1;
    }

    /// <summary>
    /// 证书列表项
    /// </summary>
    public class CertificateListItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // single, wildcard, multi-domain
        public List<string> Domains { get; set; } = new();
        public string Status { get; set; } = string.Empty; // active, expired, revoked, pending
        public DateTime IssuedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int DaysUntilExpiry => (int)(ExpiresAt - DateTime.UtcNow).TotalDays;
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
        public bool AutoRenewalEnabled { get; set; }
        public DateTime? NextRenewalAttempt { get; set; }
        public string Issuer { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public string? CertificateId { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public string? Thumbnail { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    /// <summary>
    /// 证书详情
    /// </summary>
    public class CertificateDetails
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public List<string> Domains { get; set; } = new();
        public string Status { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string? RevocationReason { get; set; }
        public string Issuer { get; set; } = string.Empty;
        public string CertificateData { get; set; } = string.Empty;
        public string? PrivateKeyData { get; set; }
        public string CertificateChain { get; set; } = string.Empty;
        public List<string> SubjectAlternativeNames { get; set; } = new();
        public string KeyAlgorithm { get; set; } = string.Empty;
        public int KeySize { get; set; }
        public string SignatureAlgorithm { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string Fingerprint { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public string? OrderId { get; set; }
        public bool AutoRenewalEnabled { get; set; }
        public AutoRenewalConfiguration? AutoRenewalConfiguration { get; set; }
        public DateTime? NextRenewalAttempt { get; set; }
        public List<CertificateValidationResult> ValidationResults { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public CertificateUsageStatistics UsageStatistics { get; set; } = new();
        public List<string> UsedBy { get; set; } = new(); // 服务/应用列表
    }

    /// <summary>
    /// 证书续期结果
    /// </summary>
    public class CertificateRenewalResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string CertificateId { get; set; } = string.Empty;
        public string? NewCertificateId { get; set; }
        public DateTime RenewalStartedAt { get; set; }
        public DateTime? RenewalCompletedAt { get; set; }
        public List<string> RenewalSteps { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> RenewalDetails { get; set; } = new();
    }

    /// <summary>
    /// 证书删除结果
    /// </summary>
    public class CertificateDeletionResult
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
    /// 证书导入请求
    /// </summary>
    public class CertificateImportRequest
    {
        public string Name { get; set; } = string.Empty;
        public string CertificateData { get; set; } = string.Empty;
        public string? PrivateKeyData { get; set; }
        public string? CertificateChain { get; set; }
        public string Password { get; set; } = string.Empty;
        public string Format { get; set; } = "pem"; // pem, pfx, der
        public List<string> Domains { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public bool EnableAutoRenewal { get; set; } = false;
        public AutoRenewalConfiguration? AutoRenewalConfiguration { get; set; }
    }

    /// <summary>
    /// 证书导入结果
    /// </summary>
    public class CertificateImportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? CertificateId { get; set; }
        public DateTime ImportedAt { get; set; }
        public List<string> ImportSteps { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> ImportDetails { get; set; } = new();
        public CertificateDetails? ImportedCertificate { get; set; }
    }

    /// <summary>
    /// 证书导出结果
    /// </summary>
    public class CertificateExportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string CertificateId { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public string ExportedData { get; set; } = string.Empty;
        public DateTime ExportedAt { get; set; }
        public List<string> ExportSteps { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> ExportDetails { get; set; } = new();
    }

    /// <summary>
    /// 证书验证结果
    /// </summary>
    public class CertificateValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public string CertificateId { get; set; } = string.Empty;
        public DateTime ValidatedAt { get; set; }
        public List<ValidationCheck> ValidationChecks { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> ValidationDetails { get; set; } = new();
    }

    /// <summary>
    /// 验证检查项
    /// </summary>
    public class ValidationCheck
    {
        public string Name { get; set; } = string.Empty;
        public bool Passed { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Details { get; set; }
        public DateTime CheckedAt { get; set; }
    }

    /// <summary>
    /// 证书使用统计
    /// </summary>
    public class CertificateUsageStatistics
    {
        public string CertificateId { get; set; } = string.Empty;
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public double SuccessRate => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests * 100 : 0;
        public DateTime LastUsedAt { get; set; }
        public List<string> UsedByServices { get; set; } = new();
        public List<string> UsedByDomains { get; set; } = new();
        public Dictionary<string, int> RequestCountsByDay { get; set; } = new();
        public Dictionary<string, int> RequestCountsByHour { get; set; } = new();
        public DateTime StatisticsGeneratedAt { get; set; }
    }

    /// <summary>
    /// 证书操作历史
    /// </summary>
    public class CertificateOperationHistory
    {
        public string Id { get; set; } = string.Empty;
        public string CertificateId { get; set; } = string.Empty;
        public string OperationType { get; set; } = string.Empty; // created, renewed, revoked, imported, exported, validated
        public string Operation { get; set; } = string.Empty;
        public DateTime OperatedAt { get; set; }
        public string? Operator { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object> OperationDetails { get; set; } = new();
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// 证书批量操作请求
    /// </summary>
    public class CertificateBatchOperationRequest
    {
        public List<string> CertificateIds { get; set; } = new();
        public string Operation { get; set; } = string.Empty; // renew, delete, enable-auto-renewal, disable-auto-renewal, export
        public Dictionary<string, object> OperationParameters { get; set; } = new();
        public bool ContinueOnError { get; set; } = true;
        public int MaxConcurrentOperations { get; set; } = 5;
    }

    /// <summary>
    /// 证书批量操作结果
    /// </summary>
    public class CertificateBatchOperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public DateTime BatchStartedAt { get; set; }
        public DateTime? BatchCompletedAt { get; set; }
        public int TotalCertificates { get; set; }
        public int SuccessfulOperations { get; set; }
        public int FailedOperations { get; set; }
        public int SkippedOperations { get; set; }
        public List<CertificateOperationResult> OperationResults { get; set; } = new();
        public List<string> BatchErrors { get; set; } = new();
        public List<string> BatchWarnings { get; set; } = new();
        public TimeSpan TotalDuration { get; set; }
        public Dictionary<string, object> BatchStatistics { get; set; } = new();
    }

    /// <summary>
    /// 证书操作结果
    /// </summary>
    public class CertificateOperationResult
    {
        public string CertificateId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public DateTime OperatedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public Dictionary<string, object> OperationDetails { get; set; } = new();
    }

    /// <summary>
    /// 证书列表统计信息
    /// </summary>
    public class CertificateListStatistics
    {
        public int TotalCertificates { get; set; }
        public int ActiveCertificates { get; set; }
        public int ExpiredCertificates { get; set; }
        public int RevokedCertificates { get; set; }
        public int PendingCertificates { get; set; }
        public int WildcardCertificates { get; set; }
        public int SingleDomainCertificates { get; set; }
        public int MultiDomainCertificates { get; set; }
        public int CertificatesWithAutoRenewal { get; set; }
        public int ExpiringNext7Days { get; set; }
        public int ExpiringNext30Days { get; set; }
        public int ExpiringNext90Days { get; set; }
        public Dictionary<string, int> CertificatesByIssuer { get; set; } = new();
        public Dictionary<string, int> CertificatesByAccount { get; set; } = new();
        public Dictionary<string, int> CertificatesByStatus { get; set; } = new();
        public DateTime StatisticsGeneratedAt { get; set; }
    }

    /// <summary>
    /// 证书搜索结果
    /// </summary>
    public class CertificateSearchResult
    {
        public IEnumerable<CertificateListItem> Results { get; set; } = new List<CertificateListItem>();
        public int TotalCount { get; set; }
        public string SearchTerm { get; set; } = string.Empty;
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPreviousPage => PageIndex > 0;
        public bool HasNextPage => PageIndex < TotalPages - 1;
        public Dictionary<string, int> SearchHighlights { get; set; } = new();
        public List<string> SuggestedSearchTerms { get; set; } = new();
    }

    #endregion
}