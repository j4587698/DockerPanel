using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using TinyDb.Attributes;

namespace DockerPanel.API.Models.Acme
{
    /// <summary>
    /// ACME账户信息
    /// </summary>
    [Entity]
    public class AcmeAccount
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string AccountKey { get; set; } = string.Empty;
        public string AccountUri { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// ACME提供商信息
    /// </summary>
    public class AcmeProvider
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string DirectoryUrl { get; set; } = string.Empty;
        public bool IsProduction { get; set; }
        public bool IsStaging { get; set; }
        public string Description { get; set; } = string.Empty;
        public List<string> SupportedChallengeTypes { get; set; } = new();
        public Dictionary<string, object> Configuration { get; set; } = new();
    }

    /// <summary>
    /// 证书申请请求
    /// </summary>
    public class AcmeCertificateRequest
    {
        public string AccountId { get; set; } = string.Empty;
        public List<string> Domains { get; set; } = new();
        public string KeyType { get; set; } = "RSA2048"; // RSA2048, RSA4096, ECDSA256, ECDSA384
        public bool UseWildcard { get; set; }
        public bool UseStaging { get; set; } = true;
        public string AcmeProvider { get; set; } = "letsencrypt-staging"; // letsencrypt, letsencrypt-staging, zerossl
        public List<string> ChallengeTypes { get; set; } = new(); // http-01, dns-01, tls-alpn-01
        public int? CertificateValidityDays { get; set; } = 90;
        public Dictionary<string, object> Metadata { get; set; } = new();
        public string? AccountKey { get; set; } // 可选的直接传递账户密钥
    }

    /// <summary>
    /// 证书订单信息
    /// </summary>
    [Entity]
    public class AcmeCertificateOrder
    {
        public string Id { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public string OrderUri { get; set; } = string.Empty;
        public List<string> Domains { get; set; } = new();
        public string Status { get; set; } = string.Empty; // pending, ready, processing, valid, invalid
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? CertificateId { get; set; }
        public string? ProgressId { get; set; } // 进度跟踪ID
        public List<AcmeAuthorization> Authorizations { get; set; } = new();
        public string? FinalizeUri { get; set; }
        public string? CertificateUri { get; set; }
        public string? Error { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// ACME授权信息
    /// </summary>
    public class AcmeAuthorization
    {
        public string Id { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // pending, valid, invalid, deactivated, expired
        public DateTime ExpiresAt { get; set; }
        public bool IsWildcard { get; set; }
        public List<AcmeChallenge> Challenges { get; set; } = new();
        public string? Error { get; set; }
    }

    /// <summary>
    /// ACME挑战信息
    /// </summary>
    public class AcmeChallenge
    {
        public string Type { get; set; } = string.Empty; // http-01, dns-01, tls-alpn-01
        public string Status { get; set; } = string.Empty; // pending, processing, valid, invalid
        public string Url { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string? KeyAuthorization { get; set; }
        public DateTime? ValidatedAt { get; set; }
        public string? Error { get; set; }
        public Dictionary<string, object> ChallengeData { get; set; } = new();
    }

    /// <summary>
    /// 挑战结果
    /// </summary>
    public class AcmeChallengeResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ChallengeType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? ValidatedAt { get; set; }
        public string? Error { get; set; }
        public string Token { get; set; } = string.Empty;
        public string? ValidationUrl { get; set; }
        public string? ErrorType { get; set; }
        public string? ErrorDetails { get; set; }
        public Dictionary<string, object> Details { get; set; } = new();
    }

    /// <summary>
    /// 证书数据
    /// </summary>
    public class AcmeCertificateData
    {
        public string Certificate { get; set; } = string.Empty; // PEM格式证书
        public string? CertificateChain { get; set; } // PEM格式证书链
        public string? PrivateKey { get; set; } // PEM格式私钥
        public string CertificateFingerprint { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public List<string> Domains { get; set; } = new();
        public string Issuer { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// 连接测试结果
    /// </summary>
    public class AcmeConnectionTestResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string DirectoryUrl { get; set; } = string.Empty;
        public TimeSpan ResponseTime { get; set; }
        public string Version { get; set; } = string.Empty;
        public List<string> SupportedChallengeTypes { get; set; } = new();
        public Dictionary<string, object> AdditionalInfo { get; set; } = new();
    }

    /// <summary>
    /// 证书续期配置
    /// </summary>
    public class AcmeRenewalConfiguration
    {
        public string CertificateId { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public bool AutoRenewalEnabled { get; set; }
        public int RenewalDaysBeforeExpiry { get; set; } = 15;
        public string RenewalSchedule { get; set; } = string.Empty; // Cron表达式
        public List<string> NotificationEmails { get; set; } = new();
        public Dictionary<string, object> Settings { get; set; } = new();
        public DateTime? LastRenewalAttempt { get; set; }
        public DateTime? NextRenewalAttempt { get; set; }
        public int RenewalAttempts { get; set; }
        public bool RenewalInProgress { get; set; }
    }

    /// <summary>
    /// 创建ACME账户请求
    /// </summary>
    public class CreateAcmeAccountRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// EAB Key ID（外部账户绑定标识）
        /// ZeroSSL、Google Trust Services、SSL.com 等需要
        /// </summary>
        public string? EabKid { get; set; }

        /// <summary>
        /// EAB HMAC Key（外部账户绑定密钥）
        /// ZeroSSL、Google Trust Services、SSL.com 等需要
        /// </summary>
        public string? EabHmacKey { get; set; }
    }

    /// <summary>
    /// 完成挑战请求
    /// </summary>
    public class CompleteChallengeRequest
    {
        public string ChallengeType { get; set; } = string.Empty;
    }

    /// <summary>
    /// 撤销证书请求
    /// </summary>
    public class RevokeCertificateRequest
    {
        public int Reason { get; set; } = 0; // 0=unspecified, 1=keyCompromise, 2=affiliationChanged, etc.
    }

    /// <summary>
    /// 自动续期配置
    /// </summary>
    public class AutoRenewalConfiguration
    {
        public string Id { get; set; } = string.Empty;
        public string CertificateId { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public bool AutoRenewalEnabled { get; set; } = true;
        public int RenewalDaysBeforeExpiry { get; set; } = 15;
        public string RenewalSchedule { get; set; } = string.Empty; // Cron表达式
        public List<string> NotificationEmails { get; set; } = new();
        public Dictionary<string, object> Settings { get; set; } = new();
        public DateTime? LastRenewalAttempt { get; set; }
        public DateTime? NextRenewalAttempt { get; set; }
        public int RenewalAttempts { get; set; }
        public bool RenewalInProgress { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public RenewalStatus Status { get; set; } = new();
    }

    /// <summary>
    /// 续期状态
    /// </summary>
    public class RenewalStatus
    {
        public string State { get; set; } = string.Empty; // pending, success, failed, disabled
        public string? Message { get; set; }
        public DateTime? LastCheck { get; set; }
        public DateTime? LastSuccess { get; set; }
        public DateTime? LastFailure { get; set; }
        public int ConsecutiveFailures { get; set; }
        public int TotalRenewals { get; set; }
        public DateTime? NextAttempt { get; set; }
    }

    /// <summary>
    /// ACME操作日志
    /// </summary>
    public class AcmeOperationLog
    {
        public string Id { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty; // create, renew, revoke, challenge
        public string OperationType { get; set; } = string.Empty; // create, renew, revoke, challenge
        public string AccountId { get; set; } = string.Empty;
        public string? CertificateId { get; set; }
        public string? OrderId { get; set; }
        public string Status { get; set; } = string.Empty; // started, completed, failed
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public Dictionary<string, object> RequestData { get; set; } = new();
        public Dictionary<string, object> ResponseData { get; set; } = new();
        public string? NodeId { get; set; }
        public string? UserId { get; set; }
    }

    /// <summary>
    /// ACME证书验证结果
    /// </summary>
    public class AcmeCertificateValidationResult
    {
        public string CertificateId { get; set; } = string.Empty;
        public string CertificateChain { get; set; } = string.Empty;
        public string PrivateKey { get; set; } = string.Empty;
        public string CertificateFingerprint { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public List<string> Domains { get; set; } = new();
        public string Issuer { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public ValidationResult ValidationStatus { get; set; } = new();
        public List<string> ValidationErrors { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();

        // 缺失的属性
        public List<string> SubjectAlternativeNames { get; set; } = new();
        public bool DomainMatch { get; set; }
        public int DaysUntilExpiry { get; set; }
        public List<string> Warnings { get; set; } = new();
        public bool SelfSigned { get; set; }
        public bool Valid { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();
        public DateTime ValidatedAt { get; set; }
    }

    /// <summary>
    /// 验证结果
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string Status { get; set; } = string.Empty; // valid, invalid, expired
        public string? Reason { get; set; }
        public DateTime ValidatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public List<DomainValidation> DomainValidations { get; set; } = new();
    }

    /// <summary>
    /// 域名验证
    /// </summary>
    public class DomainValidation
    {
        public string Domain { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public DateTime ValidatedAt { get; set; }
    }

    /// <summary>
    /// ACME密钥信息
    /// </summary>
    public class AcmeKeyInfo
    {
        public string KeyId { get; set; } = string.Empty;
        public string KeyType { get; set; } = string.Empty; // RSA, ECDSA
        public int KeySize { get; set; }
        public string PublicKey { get; set; } = string.Empty;
        public string PrivateKey { get; set; } = string.Empty;
        public string KeyAlgorithm { get; set; } = string.Empty;
        public string KeyFingerprint { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public bool IsActive { get; set; }
        public List<string> AssociatedCertificates { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();

        // 缺失的属性
        public string PublicKeyPem { get; set; } = string.Empty;
    }

    /// <summary>
    /// ACME密钥对
    /// </summary>
    public class AcmeKeyPair
    {
        public string KeyId { get; set; } = string.Empty;
        public string PublicKeyPem { get; set; } = string.Empty;
        public string PrivateKeyPem { get; set; } = string.Empty;
        public string KeyType { get; set; } = string.Empty;
        public int KeySize { get; set; }
        public string KeyAlgorithm { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsExpired { get; set; }
        public List<string> CertificateIds { get; set; } = new();

        // 缺失的属性
        public string KeyFingerprint { get; set; } = string.Empty;
    }

    /// <summary>
    /// DNS挑战配置结果
    /// </summary>
    public class DnsChallengeConfigurationResult
    {
        public bool Success { get; set; }
        public string Domain { get; set; } = string.Empty;
        public string ChallengeType { get; set; } = "dns-01";
        public string ChallengeToken { get; set; } = string.Empty;
        public string KeyAuthorization { get; set; } = string.Empty;
        public string DnsRecordName { get; set; } = string.Empty;
        public string DnsRecordValue { get; set; } = string.Empty;
        public string DnsProvider { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // configured, failed, pending
        public string? ErrorMessage { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();
        public DateTime ConfiguredAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public Dictionary<string, object> ConfigurationData { get; set; } = new();
    }

    /// <summary>
    /// DNS挑战清理结果
    /// </summary>
    public class DnsChallengeCleanupResult
    {
        public bool Success { get; set; }
        public string Domain { get; set; } = string.Empty;
        public string DnsRecordName { get; set; } = string.Empty;
        public string DnsProvider { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // cleaned, failed
        public string? ErrorMessage { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();
        public DateTime CleanedAt { get; set; }
        public Dictionary<string, object> CleanupData { get; set; } = new();
    }

    /// <summary>
    /// DNS提供商信息
    /// </summary>
    public class DnsProviderInfo
    {
        public string ProviderId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> SupportedChallengeTypes { get; set; } = new();
        public Dictionary<string, DnsProviderField> ConfigurationFields { get; set; } = new();
        public bool IsEnabled { get; set; } = true;
        public string DocumentationUrl { get; set; } = string.Empty;
        public Dictionary<string, object> DefaultSettings { get; set; } = new();
    }

    /// <summary>
    /// DNS提供商配置字段
    /// </summary>
    public class DnsProviderField
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // string, number, boolean, secret
        public bool Required { get; set; } = false;
        public string? DefaultValue { get; set; }
        public string? Description { get; set; }
        public List<string> Options { get; set; } = new();
        public string? ValidationPattern { get; set; }
    }

    
    /// <summary>
    /// 通配符证书详情
    /// </summary>
    public class WildcardCertificateDetails
    {
        public string CertificateId { get; set; } = string.Empty;
        public string BaseDomain { get; set; } = string.Empty;
        public string WildcardDomain { get; set; } = string.Empty; // *.example.com
        public List<string> AdditionalDomains { get; set; } = new();
        public string CertificateChain { get; set; } = string.Empty;
        public string PrivateKey { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string Issuer { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public AutoRenewalConfiguration? RenewalConfiguration { get; set; }
        public List<string> AppliedServices { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// 通配符证书摘要
    /// </summary>
    public class WildcardCertificateSummary
    {
        public string CertificateId { get; set; } = string.Empty;
        public string BaseDomain { get; set; } = string.Empty;
        public string WildcardDomain { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public int DaysUntilExpiry { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool AutoRenewalEnabled { get; set; }
        public DateTime? LastRenewed { get; set; }
        public int ServiceCount { get; set; }
        public bool IsExpiringSoon { get; set; }
    }

    /// <summary>
    /// 通配符证书批量请求
    /// </summary>
    public class WildcardCertificateBatchRequest
    {
        public List<WildcardCertificateRequestItem> Certificates { get; set; } = new();
        public string AccountId { get; set; } = string.Empty;
        public bool SkipExisting { get; set; } = true;
        public Dictionary<string, object> GlobalSettings { get; set; } = new();
        public string Operation { get; set; } = string.Empty;
        public List<string> CertificateIds { get; set; } = new();
    }

    /// <summary>
    /// 通配符证书请求项
    /// </summary>
    public class WildcardCertificateRequestItem
    {
        public string BaseDomain { get; set; } = string.Empty;
        public List<string> AdditionalDomains { get; set; } = new();
        public string DnsProvider { get; set; } = string.Empty;
        public Dictionary<string, string> DnsProviderConfig { get; set; } = new();
        public Dictionary<string, object> CertificateSettings { get; set; } = new();
        public bool EnableAutoRenewal { get; set; } = true;
        public List<string> NotificationEmails { get; set; } = new();
    }

    /// <summary>
    /// 通配符证书批量结果
    /// </summary>
    public class WildcardCertificateBatchResult
    {
        public int TotalCertificates { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<WildcardCertificateResultItem> Results { get; set; } = new();
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();
        public DateTime BatchStartedAt { get; set; } = DateTime.UtcNow;
        public DateTime BatchCompletedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 通配符证书结果项
    /// </summary>
    public class WildcardCertificateResultItem
    {
        public string BaseDomain { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? CertificateId { get; set; }
        public string? ErrorMessage { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? CompletedAt { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// 通配符证书状态
    /// </summary>
    public class WildcardCertificateStatus
    {
        public string CertificateId { get; set; } = string.Empty;
        public string BaseDomain { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // pending, valid, invalid, expired, revoked
        public DateTime? IssuedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public int DaysUntilExpiry { get; set; }
        public bool IsExpiringSoon { get; set; }
        public bool IsExpired { get; set; }
        public string? LastError { get; set; }
        public DateTime? LastChecked { get; set; }
        public List<string> AppliedServices { get; set; } = new();
        public RenewalStatus? RenewalStatus { get; set; }
    }

    /// <summary>
    /// 通配符自动挑战请求
    /// </summary>
    public class WildcardAutoChallengeRequest
    {
        public string CertificateId { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string DnsProvider { get; set; } = string.Empty;
        public Dictionary<string, string> DnsProviderConfig { get; set; } = new();
        public List<string> ChallengeTypes { get; set; } = new(); // dns-01
        public bool AutoCleanup { get; set; } = true;
        public int ChallengeTimeoutMinutes { get; set; } = 10;
        public Dictionary<string, object> ChallengeSettings { get; set; } = new();
    }

    /// <summary>
    /// 通配符自动挑战结果
    /// </summary>
    public class WildcardAutoChallengeResult
    {
        public bool Success { get; set; }
        public string CertificateId { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string DnsProvider { get; set; } = string.Empty;
        public List<ChallengeResult> ChallengeResults { get; set; } = new();
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public string? ErrorMessage { get; set; }
        public bool CleanupCompleted { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();
        public DateTime ConfiguredAt { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// 挑战结果
    /// </summary>
    public class ChallengeResult
    {
        public string ChallengeType { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public Dictionary<string, object> ChallengeData { get; set; } = new();
    }

    /// <summary>
    /// 通配符证书申请请求
    /// </summary>
    public class WildcardCertificateRequest
    {
        /// <summary>
        /// 申请的域名列表
        /// </summary>
        [Required]
        public List<string> Domains { get; set; } = new();

        /// <summary>
        /// 基础域名
        /// </summary>
        public string BaseDomain { get; set; } = string.Empty;

        /// <summary>
        /// 子域名列表
        /// </summary>
        public List<string> Subdomains { get; set; } = new();

        /// <summary>
        /// ACME账户ID
        /// </summary>
        [Required]
        public string AccountId { get; set; } = string.Empty;

        /// <summary>
        /// 密钥类型
        /// </summary>
        public string KeyType { get; set; } = "RSA2048";

        /// <summary>
        /// DNS提供商
        /// </summary>
        [Required]
        public string DnsProvider { get; set; } = string.Empty;

        /// <summary>
        /// DNS提供商配置
        /// </summary>
        public Dictionary<string, string> DnsProviderConfig { get; set; } = new();

        /// <summary>
        /// DNS凭证
        /// </summary>
        public Dictionary<string, Dictionary<string, object>> DnsCredentials { get; set; } = new();

        /// <summary>
        /// 首选DNS提供商列表
        /// </summary>
        public List<string> PreferredDnsProviders { get; set; } = new();

        /// <summary>
        /// 是否使用测试环境
        /// </summary>
        public bool UseStaging { get; set; } = true;

        /// <summary>
        /// 挑战类型列表
        /// </summary>
        public List<string> ChallengeTypes { get; set; } = new() { "dns-01" };

        /// <summary>
        /// 证书有效期（天）
        /// </summary>
        public int? CertificateValidityDays { get; set; } = 90;

        /// <summary>
        /// 是否启用自动续期
        /// </summary>
        public bool EnableAutoRenewal { get; set; } = true;

        /// <summary>
        /// 通知邮箱列表
        /// </summary>
        public List<string> NotificationEmails { get; set; } = new();

        /// <summary>
        /// 续期天数（过期前多少天续期）
        /// </summary>
        public int RenewalDaysBeforeExpiry { get; set; } = 15;

        /// <summary>
        /// 续期计划（Cron表达式）
        /// </summary>
        public string? RenewalSchedule { get; set; }

        /// <summary>
        /// 元数据
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// 通配符证书申请结果
    /// </summary>
    public class WildcardCertificateResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 证书ID
        /// </summary>
        public string? CertificateId { get; set; }

        /// <summary>
        /// 基础域名
        /// </summary>
        public string BaseDomain { get; set; } = string.Empty;

        /// <summary>
        /// 子域名列表
        /// </summary>
        public List<string> Subdomains { get; set; } = new();

        /// <summary>
        /// 完整域名列表
        /// </summary>
        public List<string> FullDomains { get; set; } = new();

        /// <summary>
        /// 证书指纹
        /// </summary>
        public string CertificateFingerprint { get; set; } = string.Empty;

        /// <summary>
        /// 签发时间
        /// </summary>
        public DateTime IssuedAt { get; set; }

        /// <summary>
        /// 过期时间
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 错误列表
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// 警告列表
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// 状态
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// 订单ID
        /// </summary>
        public string? OrderId { get; set; }

        /// <summary>
        /// 申请时间
        /// </summary>
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 完成时间
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// 处理时长
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// 验证步骤
        /// </summary>
        public List<string> ValidationSteps { get; set; } = new();

        /// <summary>
        /// 预验证结果
        /// </summary>
        public AcmeCertificateValidationResult? PreValidationResult { get; set; }

        /// <summary>
        /// 元数据
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// 通配符DNS结果
    /// </summary>
    public class WildcardDnsResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string DnsProvider { get; set; } = string.Empty;
        public string RecordName { get; set; } = string.Empty;
        public string RecordValue { get; set; } = string.Empty;
        public string RecordType { get; set; } = "TXT";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ValidatedAt { get; set; }
        public List<string> ValidationSteps { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> DnsDetails { get; set; } = new();
        public TimeSpan? DnsPropagationTime { get; set; }
        public bool IsWildcard { get; set; }
        public string WildcardPattern { get; set; } = string.Empty;
    }

    /// <summary>
    /// 通配符导入结果
    /// </summary>
    public class WildcardImportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? CertificateId { get; set; }
        public string BaseDomain { get; set; } = string.Empty;
        public List<string> ImportedDomains { get; set; } = new();
        public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
        public string CertificateFingerprint { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string Issuer { get; set; } = string.Empty;
        public List<string> ImportSteps { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> ImportDetails { get; set; } = new();
        public bool IsWildcard { get; set; }
        public string ImportSource { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public long FileSize { get; set; }
    }

    
    /// <summary>
    /// ACME辅助方法
    /// </summary>
    public static class AcmeHelpers
    {
        /// <summary>
        /// 检查域名是否为二级域名
        /// </summary>
        /// <param name="domain">域名</param>
        /// <returns>是否为二级域名</returns>
        public static bool IsSecondLevelDomain(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return false;

            // 移除协议前缀
            domain = domain.Replace("http://", "").Replace("https://", "");

            // 移除路径
            var pathIndex = domain.IndexOf('/');
            if (pathIndex >= 0)
                domain = domain.Substring(0, pathIndex);

            // 移除端口
            var portIndex = domain.IndexOf(':');
            if (portIndex >= 0)
                domain = domain.Substring(0, portIndex);

            // 分割域名为标签
            var labels = domain.Split('.');
            if (labels.Length < 2)
                return false;

            // 检查顶级域名是否为已知TLD
            var tld = labels[labels.Length - 1].ToLower();
            var knownTlds = new HashSet<string>
            {
                "com", "org", "net", "edu", "gov", "mil", "int", "arpa",
                "aero", "biz", "coop", "info", "museum", "name", "pro",
                "mobi", "travel", "jobs", "cat", "tel", "asia", "xxx",
                "cn", "com.cn", "net.cn", "org.cn", "gov.cn", "edu.cn",
                "jp", "co.jp", "ac.jp", "go.jp", "or.jp",
                "uk", "co.uk", "org.uk", "gov.uk", "ac.uk",
                "de", "fr", "it", "nl", "se", "no", "fi", "dk", "es", "pt",
                "au", "com.au", "net.au", "org.au", "gov.au", "edu.au",
                "ca", "gc.ca",
                "io", "ai", "co", "me", "tv", "cc", "ws", "bz", "nu", "tk"
            };

            // 检查是否为二级域名
            if (labels.Length == 2)
            {
                return knownTlds.Contains(tld);
            }

            // 对于三级域名，检查是否为已知的二级TLD
            if (labels.Length == 3)
            {
                var secondTld = $"{labels[1]}.{labels[2]}".ToLower();
                return knownTlds.Contains(secondTld);
            }

            return false;
        }

        /// <summary>
        /// 通配符证书结果转换为删除结果
        /// </summary>
        /// <param name="result">原始结果</param>
        /// <returns>删除结果</returns>
        public static Services.Acme.WildcardCertificateDeletionResult ToDeletionResult(this WildcardCertificateResult result)
        {
            return new Services.Acme.WildcardCertificateDeletionResult
            {
                Success = result.Success,
                Message = result.Message,
                CertificateId = result.CertificateId ?? string.Empty,
                DeletedAt = DateTime.UtcNow,
                DeletionSteps = result.ValidationSteps,
                Errors = result.Errors,
                Warnings = result.Warnings,
                DeletionDetails = result.Metadata
            };
        }

        /// <summary>
        /// 通配符证书结果转换为导出结果
        /// </summary>
        /// <param name="result">原始结果</param>
        /// <param name="format">导出格式</param>
        /// <param name="exportData">导出数据</param>
        /// <returns>导出结果</returns>
        public static Services.Acme.WildcardCertificateExportResult ToExportResult(this WildcardCertificateResult result, string format = "pem", string? exportData = null)
        {
            return new Services.Acme.WildcardCertificateExportResult
            {
                Success = result.Success,
                Message = result.Message,
                CertificateId = result.CertificateId ?? string.Empty,
                Format = format,
                ExportData = exportData ?? string.Empty,
                ExportedAt = DateTime.UtcNow,
                FileName = result.CertificateId != null ? $"{result.CertificateId}.{format}" : null,
                FileSize = exportData?.Length ?? 0,
                ExportSteps = result.ValidationSteps,
                Errors = result.Errors,
                ExportDetails = result.Metadata
            };
        }

        /// <summary>
        /// 通配符证书结果转换为续期结果
        /// </summary>
        /// <param name="result">原始结果</param>
        /// <returns>续期结果</returns>
        public static Services.Acme.CertificateRenewalResult ToRenewalResult(this WildcardCertificateResult result)
        {
            return new Services.Acme.CertificateRenewalResult
            {
                Success = result.Success,
                Message = result.Message,
                CertificateId = result.CertificateId ?? string.Empty,
                NewCertificateId = result.CertificateId,
                RenewalStartedAt = result.RequestedAt,
                RenewalCompletedAt = result.CompletedAt,
                RenewalSteps = result.ValidationSteps,
                Errors = result.Errors,
                Warnings = result.Warnings,
                RenewalDetails = result.Metadata
            };
        }

        /// <summary>
        /// 证书验证结果转换为通配符验证结果
        /// </summary>
        /// <param name="result">原始验证结果</param>
        /// <param name="baseDomain">基础域名</param>
        /// <returns>通配符验证结果</returns>
        public static Services.Acme.WildcardCertificateValidationResult ToWildcardValidationResult(this AcmeCertificateValidationResult result, string baseDomain)
        {
            return new Services.Acme.WildcardCertificateValidationResult
            {
                IsValid = result.ValidationStatus.IsValid,
                Message = result.ValidationStatus.Reason ?? string.Empty,
                Passed = result.ValidationStatus.IsValid,
                ValidationChecks = result.ValidationStatus.DomainValidations.Select(d => d.Domain).ToList(),
                PassedChecks = result.ValidationStatus.DomainValidations
                    .Where(d => d.IsValid)
                    .Select(d => d.Domain)
                    .ToList(),
                FailedChecks = result.ValidationStatus.DomainValidations
                    .Where(d => !d.IsValid)
                    .Select(d => d.Domain)
                    .ToList(),
                Errors = result.ValidationErrors,
                Warnings = new List<string>(),
                ValidationDetails = result.Metadata,
                CanProceed = result.ValidationStatus.IsValid,
                RecommendedActions = new List<string>(),
                ValidatedAt = result.ValidationStatus.ValidatedAt
            };
        }
    }
}

#region 证书申请进度跟踪模型

/// <summary>
/// 证书申请进度跟踪器
/// </summary>
public class CertificateApplicationProgress
{
    /// <summary>
    /// 进度ID
    /// </summary>
    public string ProgressId { get; set; } = string.Empty;

    /// <summary>
    /// 证书ID
    /// </summary>
    public string CertificateId { get; set; } = string.Empty;

    /// <summary>
    /// 申请名称
    /// </summary>
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>
    /// 当前进度步骤
    /// </summary>
    public CertificateApplicationStep CurrentStep { get; set; } = CertificateApplicationStep.NotStarted;

    /// <summary>
    /// 总进度百分比 (0-100)
    /// </summary>
    public int ProgressPercentage { get; set; } = 0;

    /// <summary>
    /// 状态
    /// </summary>
    public CertificateApplicationStatus Status { get; set; } = CertificateApplicationStatus.Pending;

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 所有步骤详情
    /// </summary>
    public List<CertificateApplicationStepDetail> Steps { get; set; } = new();

    /// <summary>
    /// 错误信息
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// 警告信息
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// 元数据
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// 预计剩余时间（秒）
    /// </summary>
    public int? EstimatedRemainingSeconds { get; set; }

    /// <summary>
    /// 是否已完成
    /// </summary>
    public bool IsCompleted => Status == CertificateApplicationStatus.Completed || Status == CertificateApplicationStatus.Failed;

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess => Status == CertificateApplicationStatus.Completed && !Errors.Any();

    /// <summary>
    /// 获取当前步骤描述
    /// </summary>
    public string GetCurrentStepDescription()
    {
        return CurrentStep.GetDescription();
    }

    /// <summary>
    /// 添加步骤
    /// </summary>
    public void AddStep(CertificateApplicationStep step, string message, bool isCompleted = false)
    {
        var stepDetail = new CertificateApplicationStepDetail
        {
            Step = step,
            Description = step.GetDescription(),
            Message = message,
            StartedAt = DateTime.UtcNow,
            IsCompleted = isCompleted,
            CompletedAt = isCompleted ? DateTime.UtcNow : null
        };

        Steps.Add(stepDetail);
        CurrentStep = step;
        LastUpdatedAt = DateTime.UtcNow;

        if (isCompleted)
        {
            UpdateProgressPercentage();
        }
    }

    /// <summary>
    /// 完成当前步骤
    /// </summary>
    public void CompleteCurrentStep(string? message = null)
    {
        var currentStepDetail = Steps.LastOrDefault(s => !s.IsCompleted);
        if (currentStepDetail != null)
        {
            currentStepDetail.IsCompleted = true;
            currentStepDetail.CompletedAt = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(message))
            {
                currentStepDetail.Message = message;
            }
        }

        UpdateProgressPercentage();
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 添加错误
    /// </summary>
    public void AddError(string error)
    {
        Errors.Add($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {error}");
        Status = CertificateApplicationStatus.Failed;
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 添加警告
    /// </summary>
    public void AddWarning(string warning)
    {
        Warnings.Add($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {warning}");
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 更新进度百分比
    /// </summary>
    private void UpdateProgressPercentage()
    {
        var totalSteps = Enum.GetValues<CertificateApplicationStep>().Length;
        var completedSteps = Steps.Count(s => s.IsCompleted);
        ProgressPercentage = (int)Math.Round((double)completedSteps / totalSteps * 100);
    }

    /// <summary>
    /// 标记完成
    /// </summary>
    public void MarkAsCompleted()
    {
        Status = CertificateApplicationStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        ProgressPercentage = 100;
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 标记失败
    /// </summary>
    public void MarkAsFailed(string errorMessage)
    {
        Status = CertificateApplicationStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        AddError(errorMessage);
    }
}

/// <summary>
/// 证书申请步骤
/// </summary>
public enum CertificateApplicationStep
{
    /// <summary>
    /// 未开始
    /// </summary>
    NotStarted = 0,

    /// <summary>
    /// 初始化ACME客户端
    /// </summary>
    InitializingAcmeClient = 1,

    /// <summary>
    /// 创建或获取ACME账户
    /// </summary>
    CreatingAccount = 2,

    /// <summary>
    /// 创建证书订单
    /// </summary>
    CreatingOrder = 3,

    /// <summary>
    /// 获取域名授权
    /// </summary>
    GettingAuthorizations = 4,

    /// <summary>
    /// 配置DNS验证记录
    /// </summary>
    ConfiguringDnsChallenge = 5,

    /// <summary>
    /// 等待DNS传播
    /// </summary>
    WaitingForDnsPropagation = 6,

    /// <summary>
    /// 验证域名控制权
    /// </summary>
    ValidatingDomains = 7,

    /// <summary>
    /// 清理DNS记录
    /// </summary>
    CleaningDnsRecords = 8,

    /// <summary>
    /// 下载证书
    /// </summary>
    DownloadingCertificate = 9,

    /// <summary>
    /// 保存证书到本地
    /// </summary>
    SavingCertificate = 10,

    /// <summary>
    /// 完成申请
    /// </summary>
    Completed = 11
}

/// <summary>
/// 证书申请状态
/// </summary>
public enum CertificateApplicationStatus
{
    /// <summary>
    /// 等待中
    /// </summary>
    Pending = 0,

    /// <summary>
    /// 进行中
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// 已完成
    /// </summary>
    Completed = 2,

    /// <summary>
    /// 失败
    /// </summary>
    Failed = 3,

    /// <summary>
    /// 已取消
    /// </summary>
    Cancelled = 4
}

/// <summary>
/// 证书申请步骤详情
/// </summary>
public class CertificateApplicationStepDetail
{
    /// <summary>
    /// 步骤
    /// </summary>
    public CertificateApplicationStep Step { get; set; }

    /// <summary>
    /// 步骤描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 详细消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 是否已完成
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; } = true;

    /// <summary>
    /// 错误信息
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// 警告信息
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// 元数据
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// 耗时（秒）
    /// </summary>
    public double? DurationSeconds => CompletedAt?.Subtract(StartedAt).TotalSeconds;
}

/// <summary>
/// 进度跟踪请求
/// </summary>
public class ProgressTrackRequest
{
    /// <summary>
    /// 证书ID
    /// </summary>
    public string CertificateId { get; set; } = string.Empty;

    /// <summary>
    /// 申请名称
    /// </summary>
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>
    /// 域名列表
    /// </summary>
    public List<string> Domains { get; set; } = new();

    /// <summary>
    /// ACME服务商
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// 验证方式
    /// </summary>
    public string ChallengeType { get; set; } = string.Empty;

    /// <summary>
    /// 元数据
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// 进度跟踪响应
/// </summary>
public class ProgressTrackResponse
{
    /// <summary>
    /// 进度ID
    /// </summary>
    public string ProgressId { get; set; } = string.Empty;

    /// <summary>
    /// 证书ID
    /// </summary>
    public string CertificateId { get; set; } = string.Empty;

    /// <summary>
    /// 申请名称
    /// </summary>
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>
    /// 当前步骤
    /// </summary>
    public CertificateApplicationStep CurrentStep { get; set; }

    /// <summary>
    /// 当前步骤描述
    /// </summary>
    public string CurrentStepDescription { get; set; } = string.Empty;

    /// <summary>
    /// 进度百分比
    /// </summary>
    public int ProgressPercentage { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public CertificateApplicationStatus Status { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 预计剩余时间（秒）
    /// </summary>
    public int? EstimatedRemainingSeconds { get; set; }

    /// <summary>
    /// 所有步骤
    /// </summary>
    public List<CertificateApplicationStepDetail> Steps { get; set; } = new();

    /// <summary>
    /// 错误信息
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// 警告信息
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// 元数据
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// 是否已完成
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }
}

/// <summary>
/// 进度更新通知
/// </summary>
public class ProgressUpdateNotification
{
    /// <summary>
    /// 进度ID
    /// </summary>
    public string ProgressId { get; set; } = string.Empty;

    /// <summary>
    /// 证书ID
    /// </summary>
    public string CertificateId { get; set; } = string.Empty;

    /// <summary>
    /// 当前步骤
    /// </summary>
    public CertificateApplicationStep CurrentStep { get; set; }

    /// <summary>
    /// 当前步骤描述
    /// </summary>
    public string CurrentStepDescription { get; set; } = string.Empty;

    /// <summary>
    /// 进度百分比
    /// </summary>
    public int ProgressPercentage { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public CertificateApplicationStatus Status { get; set; }

    /// <summary>
    /// 消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 错误信息
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// 警告信息
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// 预计剩余时间（秒）
    /// </summary>
    public int? EstimatedRemainingSeconds { get; set; }
}

#endregion

#region 证书申请步骤扩展方法

/// <summary>
/// 证书申请步骤扩展方法
/// </summary>
public static class CertificateApplicationStepExtensions
{
    /// <summary>
    /// 获取步骤描述
    /// </summary>
    public static string GetDescription(this CertificateApplicationStep step)
    {
        return step switch
        {
            CertificateApplicationStep.NotStarted => "未开始",
            CertificateApplicationStep.InitializingAcmeClient => "初始化ACME客户端",
            CertificateApplicationStep.CreatingAccount => "创建ACME账户",
            CertificateApplicationStep.CreatingOrder => "创建证书订单",
            CertificateApplicationStep.GettingAuthorizations => "获取域名授权",
            CertificateApplicationStep.ConfiguringDnsChallenge => "配置DNS验证记录",
            CertificateApplicationStep.WaitingForDnsPropagation => "等待DNS传播",
            CertificateApplicationStep.ValidatingDomains => "验证域名控制权",
            CertificateApplicationStep.CleaningDnsRecords => "清理DNS记录",
            CertificateApplicationStep.DownloadingCertificate => "下载证书",
            CertificateApplicationStep.SavingCertificate => "保存证书到本地",
            CertificateApplicationStep.Completed => "申请完成",
            _ => "未知步骤"
        };
    }

    /// <summary>
    /// 获取步骤权重（用于计算进度百分比）
    /// </summary>
    public static int GetWeight(this CertificateApplicationStep step)
    {
        return step switch
        {
            CertificateApplicationStep.NotStarted => 0,
            CertificateApplicationStep.InitializingAcmeClient => 5,
            CertificateApplicationStep.CreatingAccount => 10,
            CertificateApplicationStep.CreatingOrder => 15,
            CertificateApplicationStep.GettingAuthorizations => 10,
            CertificateApplicationStep.ConfiguringDnsChallenge => 15,
            CertificateApplicationStep.WaitingForDnsPropagation => 10,
            CertificateApplicationStep.ValidatingDomains => 15,
            CertificateApplicationStep.CleaningDnsRecords => 5,
            CertificateApplicationStep.DownloadingCertificate => 10,
            CertificateApplicationStep.SavingCertificate => 3,
            CertificateApplicationStep.Completed => 2,
            _ => 0
        };
    }

    /// <summary>
    /// 获取预计耗时（秒）
    /// </summary>
    public static int GetEstimatedDuration(this CertificateApplicationStep step)
    {
        return step switch
        {
            CertificateApplicationStep.NotStarted => 0,
            CertificateApplicationStep.InitializingAcmeClient => 2,
            CertificateApplicationStep.CreatingAccount => 3,
            CertificateApplicationStep.CreatingOrder => 5,
            CertificateApplicationStep.GettingAuthorizations => 3,
            CertificateApplicationStep.ConfiguringDnsChallenge => 10,
            CertificateApplicationStep.WaitingForDnsPropagation => 120, // DNS传播可能需要较长时间
            CertificateApplicationStep.ValidatingDomains => 15,
            CertificateApplicationStep.CleaningDnsRecords => 5,
            CertificateApplicationStep.DownloadingCertificate => 5,
            CertificateApplicationStep.SavingCertificate => 2,
            CertificateApplicationStep.Completed => 0,
            _ => 5
        };
    }

    /// <summary>
    /// 是否为关键步骤
    /// </summary>
    public static bool IsCritical(this CertificateApplicationStep step)
    {
        return step switch
        {
            CertificateApplicationStep.CreatingOrder => true,
            CertificateApplicationStep.ConfiguringDnsChallenge => true,
            CertificateApplicationStep.ValidatingDomains => true,
            CertificateApplicationStep.DownloadingCertificate => true,
            _ => false
        };
    }
}

#endregion