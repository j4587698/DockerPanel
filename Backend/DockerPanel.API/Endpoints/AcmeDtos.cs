using System;
using System.Collections.Generic;
using DockerPanel.API.Models.Acme;

namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// ACME 统计信息响应（替代 GetAcmeStatistics 匿名对象）。
    /// </summary>
    public sealed class AcmeStatisticsResponse
    {
        public int TotalCertificates { get; set; }
        public int ValidCertificates { get; set; }
        public int ExpiringCertificates { get; set; }
        public int ExpiredCertificates { get; set; }
        public int PendingCertificates { get; set; }
        public int InvalidCertificates { get; set; }
        public int TotalAccounts { get; set; }
        public int ActiveAccounts { get; set; }
        public DateTime LastUpdated { get; set; }
        public int RenewalThresholdDays { get; set; }
        public int UpcomingRenewals { get; set; }
    }

    /// <summary>
    /// 证书分页列表响应（替代 GetCertificates 匿名对象）。
    /// </summary>
    public sealed class CertificateListResponse
    {
        public List<CertificateListItemDto> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    /// <summary>
    /// 证书到期检查响应（替代 CheckCertificateExpiry 匿名对象）。
    /// </summary>
    public sealed class CertificateExpiryResponse
    {
        public string CertificateId { get; set; } = string.Empty;
        public int DaysUntilExpiry { get; set; }
    }

    /// <summary>
    /// 自动续期结果响应（替代 AutoRenewCertificates 匿名对象）。
    /// </summary>
    public sealed class AutoRenewResponse
    {
        public int RenewedCount { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// 修复证书状态响应（替代 FixCertificateStatus 匿名对象）。
    /// </summary>
    public sealed class FixStatusResponse
    {
        public int UpdatedCount { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> UpdatedOrders { get; set; } = new();
        public Dictionary<string, object> Debug { get; set; } = new();
    }

    /// <summary>
    /// 修复状态调试用订单信息（替代匿名 { id, status, domains }）。
    /// </summary>
    public sealed class OrderDebugInfo
    {
        public string Id { get; set; } = string.Empty;
        public string? Status { get; set; }
        public List<string>? Domains { get; set; }
    }

    /// <summary>
    /// CSR 生成响应（替代 GenerateCsr 匿名对象）。
    /// </summary>
    public sealed class CsrResponse
    {
        public string Csr { get; set; } = string.Empty;
    }

    /// <summary>
    /// 账户密钥导出响应（替代 ExportAccountKey 匿名对象）。
    /// </summary>
    public sealed class KeyExportResponse
    {
        public string KeyData { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
    }

    /// <summary>
    /// 测试挑战存储响应（替代 StoreTestChallenge 匿名对象）。
    /// </summary>
    public sealed class StoreChallengeResponse
    {
        public string Message { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
    }

    /// <summary>
    /// 待处理订单冲突响应（替代 OrderCertificate Conflict 匿名对象）。
    /// </summary>
    public sealed class PendingOrderResponse
    {
        public string Message { get; set; } = string.Empty;
        public string ExistingOrderId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// 重试提交响应（替代 RetryCertificate 匿名对象）。
    /// </summary>
    public sealed class RetryResponse
    {
        public string Message { get; set; } = string.Empty;
        public string CertificateId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 批量续期汇总（替代 RenewBatchCertificates summary 匿名对象）。
    /// </summary>
    public sealed class BatchRenewSummary
    {
        public int Total { get; set; }
        public int Success { get; set; }
        public int Failed { get; set; }
    }

    /// <summary>
    /// 批量续期响应（替代 RenewBatchCertificates 匿名对象）。
    /// </summary>
    public sealed class BatchRenewResponse
    {
        public string Message { get; set; } = string.Empty;
        public List<RenewResult> Results { get; set; } = new();
        public BatchRenewSummary Summary { get; set; } = new();
    }
}