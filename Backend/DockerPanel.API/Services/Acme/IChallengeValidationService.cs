using DockerPanel.API.Models.Acme;

namespace DockerPanel.API.Services.Acme
{
    /// <summary>
    /// ACME挑战验证服务接口
    /// </summary>
    public interface IChallengeValidationService
    {
        /// <summary>
        /// 配置HTTP-01挑战验证
        /// </summary>
        /// <param name="challenge">挑战信息</param>
        /// <param name="domain">域名</param>
        /// <returns>配置结果</returns>
        Task<ChallengeValidationResult> ConfigureHttpChallengeAsync(AcmeChallenge challenge, string domain);

        /// <summary>
        /// 验证HTTP-01挑战
        /// </summary>
        /// <param name="challenge">挑战信息</param>
        /// <param name="domain">域名</param>
        /// <returns>验证结果</returns>
        Task<ChallengeValidationResult> ValidateHttpChallengeAsync(AcmeChallenge challenge, string domain);

        /// <summary>
        /// 配置DNS-01挑战验证
        /// </summary>
        /// <param name="challenge">挑战信息</param>
        /// <param name="domain">域名</param>
        /// <param name="dnsProvider">DNS提供商类型</param>
        /// <param name="credentials">DNS提供商凭据</param>
        /// <returns>配置结果</returns>
        Task<ChallengeValidationResult> ConfigureDnsChallengeAsync(AcmeChallenge challenge, string domain, string dnsProvider, Dictionary<string, object>? credentials = null);

        /// <summary>
        /// 验证DNS-01挑战
        /// </summary>
        /// <param name="challenge">挑战信息</param>
        /// <param name="domain">域名</param>
        /// <param name="dnsProvider">DNS提供商类型</param>
        /// <param name="credentials">DNS提供商凭据</param>
        /// <returns>验证结果</returns>
        Task<ChallengeValidationResult> ValidateDnsChallengeAsync(AcmeChallenge challenge, string domain, string dnsProvider, Dictionary<string, object>? credentials = null);

        /// <summary>
        /// 配置TLS-ALPN-01挑战验证
        /// </summary>
        /// <param name="challenge">挑战信息</param>
        /// <param name="domain">域名</param>
        /// <returns>配置结果</returns>
        Task<ChallengeValidationResult> ConfigureTlsAlpnChallengeAsync(AcmeChallenge challenge, string domain);

        /// <summary>
        /// 验证TLS-ALPN-01挑战
        /// </summary>
        /// <param name="challenge">挑战信息</param>
        /// <param name="domain">域名</param>
        /// <returns>验证结果</returns>
        Task<ChallengeValidationResult> ValidateTlsAlpnChallengeAsync(AcmeChallenge challenge, string domain);

        /// <summary>
        /// 清理挑战验证配置
        /// </summary>
        /// <param name="challenge">挑战信息</param>
        /// <param name="domain">域名</param>
        /// <param name="challengeType">挑战类型</param>
        /// <returns>清理结果</returns>
        Task<ChallengeCleanupResult> CleanupChallengeAsync(AcmeChallenge challenge, string domain, string challengeType);

        /// <summary>
        /// 获取挑战配置状态
        /// </summary>
        /// <param name="challengeId">挑战ID</param>
        /// <returns>配置状态</returns>
        Task<ChallengeStatus> GetChallengeStatusAsync(string challengeId);

        /// <summary>
        /// 支持的DNS提供商列表
        /// </summary>
        /// <returns>DNS提供商列表</returns>
        Task<IEnumerable<DnsProvider>> GetSupportedDnsProvidersAsync();

        /// <summary>
        /// 测试DNS提供商连接
        /// </summary>
        /// <param name="dnsProvider">DNS提供商类型</param>
        /// <param name="credentials">DNS提供商凭据</param>
        /// <returns>测试结果</returns>
        Task<DnsProviderTestResult> TestDnsProviderConnectionAsync(string dnsProvider, Dictionary<string, object>? credentials = null);

        /// <summary>
        /// 自动配置挑战验证
        /// </summary>
        /// <param name="challenge">挑战信息</param>
        /// <param name="domain">域名</param>
        /// <param name="preferredChallengeTypes">首选挑战类型</param>
        /// <param name="dnsCredentials">DNS凭据</param>
        /// <returns>配置结果</returns>
        Task<AutoChallengeResult> AutoConfigureChallengeAsync(AcmeChallenge challenge, string domain,
            List<string>? preferredChallengeTypes = null,
            Dictionary<string, Dictionary<string, object>>? dnsCredentials = null);

        /// <summary>
        /// 监控挑战验证状态
        /// </summary>
        /// <param name="challengeId">挑战ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>监控结果</returns>
        IAsyncEnumerable<ChallengeStatusUpdate> MonitorChallengeStatusAsync(string challengeId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 挑战验证结果
    /// </summary>
    public class ChallengeValidationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ChallengeType { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string ConfigurationStatus { get; set; } = string.Empty;
        public DateTime ConfiguredAt { get; set; }
        public DateTime? ValidatedAt { get; set; }
        public Dictionary<string, object> ConfigurationDetails { get; set; } = new();
        public List<string> ValidationSteps { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// 挑战清理结果
    /// </summary>
    public class ChallengeCleanupResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ChallengeType { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public DateTime CleanedAt { get; set; }
        public List<string> CleanupSteps { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> CleanupDetails { get; set; } = new();
    }

    /// <summary>
    /// 挑战状态
    /// </summary>
    public class ChallengeStatus
    {
        public string ChallengeId { get; set; } = string.Empty;
        public string ChallengeType { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // pending, configured, validated, failed, cleanup
        public DateTime CreatedAt { get; set; }
        public DateTime? ConfiguredAt { get; set; }
        public DateTime? ValidatedAt { get; set; }
        public string? ValidationUrl { get; set; }
        public string? Token { get; set; }
        public string? KeyAuthorization { get; set; }
        public Dictionary<string, object> StatusDetails { get; set; } = new();
    }

    /// <summary>
    /// 挑战状态更新
    /// </summary>
    public class ChallengeStatusUpdate
    {
        public string ChallengeId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string? Message { get; set; }
        public Dictionary<string, object>? Details { get; set; }
    }

    /// <summary>
    /// DNS提供商信息
    /// </summary>
    public class DnsProvider
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> SupportedChallengeTypes { get; set; } = new();
        public List<DnsProviderFieldConfig> RequiredFields { get; set; } = new();
        public List<string> SupportedRegions { get; set; } = new();
        public Dictionary<string, object> DefaultSettings { get; set; } = new();
        public bool RequiresCredentials { get; set; }
    }

    /// <summary>
    /// DNS提供商字段配置
    /// </summary>
    public class DnsProviderFieldConfig
    {
        public string Name { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // text, password, select, textarea
        public string Description { get; set; } = string.Empty;
        public bool Required { get; set; }
        public List<string> Options { get; set; } = new();
        public Dictionary<string, object> Validation { get; set; } = new();
        public string? Placeholder { get; set; }
    }

    /// <summary>
    /// DNS提供商测试结果
    /// </summary>
    public class DnsProviderTestResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public DateTime TestedAt { get; set; }
        public TimeSpan ResponseTime { get; set; }
        public Dictionary<string, object> TestResults { get; set; } = new();
        public List<string> SupportedFeatures { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// 自动挑战配置结果
    /// </summary>
    public class AutoChallengeResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string SelectedChallengeType { get; set; } = string.Empty;
        public List<ChallengeValidationResult> AttemptedChallenges { get; set; } = new();
        public DateTime ConfiguredAt { get; set; }
        public Dictionary<string, object> ConfigurationSummary { get; set; } = new();
    }
}