using System;
using System.Collections.Generic;

namespace DockerPanel.API.Models.Acme
{
    /// <summary>
    /// 生成CSR请求
    /// </summary>
    public class GenerateCsrRequest
    {
        public List<string> Domains { get; set; } = new();
        public string KeyType { get; set; } = "rsa2048";
    }

    /// <summary>
    /// 验证证书请求
    /// </summary>
    public class ValidateCertificateRequest
    {
        public string CertificateData { get; set; } = string.Empty;
    }

    /// <summary>
    /// 导入账户密钥请求
    /// </summary>
    public class ImportKeyRequest
    {
        public string KeyData { get; set; } = string.Empty;
        public string Format { get; set; } = "pem";
    }

    /// <summary>
    /// 证书订单请求（兼容前端格式）
    /// </summary>
    public class CertificateOrderRequest
    {
        public string Domain { get; set; } = string.Empty;
        public List<string>? AlternativeNames { get; set; }
        public string ChallengeType { get; set; } = "http-01";
        public string AcmeProvider { get; set; } = "letsencrypt";
        public string Email { get; set; } = string.Empty;
        public string? DnsProvider { get; set; }
        public Dictionary<string, object>? DnsCredentials { get; set; }
        public bool AutoRenew { get; set; } = true;
        public string? AccountId { get; set; }
        public string? AccountKey { get; set; } // 可选的直接传递账户密钥
    }

    /// <summary>
    /// 测试挑战存储请求
    /// </summary>
    public class StoreChallengeRequest
    {
        public string Token { get; set; } = string.Empty;
        public string KeyAuthorization { get; set; } = string.Empty;
    }

    /// <summary>
    /// 批量续期请求
    /// </summary>
    public class BatchRenewRequest
    {
        public List<string> CertificateIds { get; set; } = new();
        public int DaysBeforeExpiry { get; set; } = 30;
    }

    /// <summary>
    /// 续期结果
    /// </summary>
    public class RenewResult
    {
        public string CertificateId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? NewCertificateId { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}