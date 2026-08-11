using System;
using System.Collections.Generic;
using DockerPanel.API.Models.Acme;

namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// 证书状态摘要响应（替代 GetCertificateSummary 匿名对象）。
    /// </summary>
    public sealed class CertificateSummaryResponse
    {
        public int TotalCertificates { get; set; }
        public int ActiveCertificates { get; set; }
        public int ExpiredCertificates { get; set; }
        public int ExpiringIn7Days { get; set; }
        public int ExpiringIn30Days { get; set; }
        public int CertificatesWithAutoRenewal { get; set; }
        public int WildcardCertificates { get; set; }
        public DateTime LastUpdated { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<CertificateSummaryUpcomingRenewal> UpcomingRenewals { get; set; } = new();
    }

    /// <summary>
    /// 证书状态摘要中的即将到期条目。
    /// </summary>
    public sealed class CertificateSummaryUpcomingRenewal
    {
        public string CertificateId { get; set; } = string.Empty;
        public List<string> Domains { get; set; } = new();
        public DateTime ExpiresAt { get; set; }
        public int DaysUntilExpiry { get; set; }
        public bool AutoRenewalEnabled { get; set; }
    }

    /// <summary>
    /// 证书进度创建响应（替代 CreateProgress 匿名对象）。
    /// </summary>
    public sealed class ProgressIdResponse
    {
        public string ProgressId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 更新进度步骤请求（原 CertificateProgressController.UpdateProgressStepRequest）。
    /// </summary>
    public sealed class UpdateProgressStepRequest
    {
        public CertificateApplicationStep Step { get; set; }

        public string Message { get; set; } = string.Empty;

        public bool IsCompleted { get; set; } = false;
    }

    /// <summary>
    /// 完成当前步骤请求（原 CertificateProgressController.CompleteCurrentStepRequest）。
    /// </summary>
    public sealed class CompleteCurrentStepRequest
    {
        public string? Message { get; set; }
    }

    /// <summary>
    /// 添加错误请求（原 CertificateProgressController.AddErrorRequest）。
    /// </summary>
    public sealed class AddErrorRequest
    {
        public string Error { get; set; } = string.Empty;
    }

    /// <summary>
    /// 添加警告请求（原 CertificateProgressController.AddWarningRequest）。
    /// </summary>
    public sealed class AddWarningRequest
    {
        public string Warning { get; set; } = string.Empty;
    }

    /// <summary>
    /// 标记失败请求（原 CertificateProgressController.MarkAsFailedRequest）。
    /// </summary>
    public sealed class MarkAsFailedRequest
    {
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// 配置HTTP-01挑战请求（原 ChallengeValidationController.ConfigureHttpChallengeRequest）。
    /// </summary>
    public sealed class ConfigureHttpChallengeRequest
    {
        public string Domain { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string KeyAuthorization { get; set; } = string.Empty;
        public string? Url { get; set; }
    }

    /// <summary>
    /// 验证HTTP-01挑战请求（原 ChallengeValidationController.ValidateHttpChallengeRequest）。
    /// </summary>
    public sealed class ValidateHttpChallengeRequest
    {
        public string Domain { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string KeyAuthorization { get; set; } = string.Empty;
        public string? Url { get; set; }
    }

    /// <summary>
    /// 配置DNS-01挑战请求（原 ChallengeValidationController.ConfigureDnsChallengeRequest）。
    /// </summary>
    public sealed class ConfigureDnsChallengeRequest
    {
        public string Domain { get; set; } = string.Empty;
        public string DnsProvider { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string KeyAuthorization { get; set; } = string.Empty;
        public Dictionary<string, object>? Credentials { get; set; }
        public string? Url { get; set; }
    }

    /// <summary>
    /// 验证DNS-01挑战请求（原 ChallengeValidationController.ValidateDnsChallengeRequest）。
    /// </summary>
    public sealed class ValidateDnsChallengeRequest
    {
        public string Domain { get; set; } = string.Empty;
        public string DnsProvider { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string KeyAuthorization { get; set; } = string.Empty;
        public Dictionary<string, object>? Credentials { get; set; }
        public string? Url { get; set; }
    }

    /// <summary>
    /// 配置TLS-ALPN-01挑战请求（原 ChallengeValidationController.ConfigureTlsAlpnChallengeRequest）。
    /// </summary>
    public sealed class ConfigureTlsAlpnChallengeRequest
    {
        public string Domain { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string KeyAuthorization { get; set; } = string.Empty;
        public string? Url { get; set; }
    }

    /// <summary>
    /// 验证TLS-ALPN-01挑战请求（原 ChallengeValidationController.ValidateTlsAlpnChallengeRequest）。
    /// </summary>
    public sealed class ValidateTlsAlpnChallengeRequest
    {
        public string Domain { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string KeyAuthorization { get; set; } = string.Empty;
        public string? Url { get; set; }
    }

    /// <summary>
    /// 清理挑战请求（原 ChallengeValidationController.CleanupChallengeRequest）。
    /// </summary>
    public sealed class CleanupChallengeRequest
    {
        public string Domain { get; set; } = string.Empty;
        public string ChallengeType { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string KeyAuthorization { get; set; } = string.Empty;
        public string? Url { get; set; }
    }

    /// <summary>
    /// 测试DNS提供商请求（原 ChallengeValidationController.TestDnsProviderRequest）。
    /// </summary>
    public sealed class TestDnsProviderRequest
    {
        public Dictionary<string, object>? Credentials { get; set; }
    }

    /// <summary>
    /// 自动配置挑战请求（原 ChallengeValidationController.AutoConfigureChallengeRequest）。
    /// </summary>
    public sealed class AutoConfigureChallengeRequest
    {
        public string Domain { get; set; } = string.Empty;
        public string ChallengeType { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string KeyAuthorization { get; set; } = string.Empty;
        public List<string>? PreferredChallengeTypes { get; set; }
        public Dictionary<string, Dictionary<string, object>>? DnsCredentials { get; set; }
        public string? Url { get; set; }
    }

    /// <summary>
    /// 批量清理挑战条目（原 ChallengeValidationController.ChallengeInfo）。
    /// </summary>
    public sealed class ChallengeInfo
    {
        public string Domain { get; set; } = string.Empty;
        public string ChallengeType { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string KeyAuthorization { get; set; } = string.Empty;
        public string? Url { get; set; }
    }

    /// <summary>
    /// 批量清理挑战请求（原 ChallengeValidationController.BatchCleanupChallengesRequest）。
    /// </summary>
    public sealed class BatchCleanupChallengesRequest
    {
        public List<ChallengeInfo> Challenges { get; set; } = new();
    }

    /// <summary>
    /// 挑战验证统计（原 ChallengeValidationController.ChallengeValidationStats）。
    /// </summary>
    public sealed class ChallengeValidationStats
    {
        public int TotalChallenges { get; set; }
        public int SuccessfulChallenges { get; set; }
        public int FailedChallenges { get; set; }
        public int PendingChallenges { get; set; }
        public Dictionary<string, int> ChallengeTypeStats { get; set; } = new();
        public Dictionary<string, int> DnsProviderStats { get; set; } = new();
        public DateTime LastUpdated { get; set; }
    }
}
