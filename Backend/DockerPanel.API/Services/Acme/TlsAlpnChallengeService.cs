using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace DockerPanel.API.Services.Acme;

/// <summary>
/// TLS-ALPN-01 挑战验证服务
/// 根据 RFC 8737 实现 TLS-ALPN-01 验证
/// 纯内存管理挑战证书，验证完毕即清除
/// </summary>
public class TlsAlpnChallengeService
{
    private readonly ILogger<TlsAlpnChallengeService> _logger;
    
    /// <summary>
    /// ALPN 协议标识符
    /// </summary>
    public const string AcmeTlsAlpnProtocol = "acme-tls/1";
    
    /// <summary>
    /// TLS-ALPN-01 验证证书的 OID (id-pe-acmeIdentifier)
    /// </summary>
    public const string IdPeAcmeIdentifier = "1.3.6.1.5.5.7.1.31";
    
    /// <summary>
    /// 内存中的挑战证书（域名 -> 证书）
    /// </summary>
    private readonly ConcurrentDictionary<string, X509Certificate2> _challengeCertificates = new(StringComparer.OrdinalIgnoreCase);

    public TlsAlpnChallengeService(ILogger<TlsAlpnChallengeService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 生成 TLS-ALPN-01 验证证书并缓存到内存
    /// </summary>
    /// <param name="domain">域名</param>
    /// <param name="keyAuthorization">Key Authorization (token + thumbprint)</param>
    /// <returns>自签名的验证证书</returns>
    public X509Certificate2 PrepareChallengeCertificate(string domain, string keyAuthorization)
    {
        var nonEmptyKeyAuthorization = !string.IsNullOrWhiteSpace(keyAuthorization)
            ? keyAuthorization
            : throw new ArgumentException("Key authorization cannot be empty.", nameof(keyAuthorization));

        _logger.LogInformation("生成 TLS-ALPN-01 验证证书: Domain={Domain}, KeyAuthz长度={Length}", 
            domain, nonEmptyKeyAuthorization.Length);

        var cert = GenerateChallengeCertificate(domain, nonEmptyKeyAuthorization);
        
        // 放入内存缓存，替换旧的
        if (_challengeCertificates.TryRemove(domain, out var oldCert))
        {
            oldCert.Dispose();
        }
        _challengeCertificates[domain] = cert;
        
        // 详细诊断日志
        _logger.LogInformation(
            "TLS-ALPN-01 验证证书已就绪: Domain={Domain}, Serial={Serial}, HasPrivateKey={HasPrivateKey}, " +
            "Subject={Subject}, NotBefore={NotBefore}, NotAfter={NotAfter}, Extensions={ExtCount}",
            domain, cert.SerialNumber, cert.HasPrivateKey,
            cert.Subject, cert.NotBefore, cert.NotAfter, cert.Extensions.Count);
        
        // 列出所有扩展的 OID 方便调试
        foreach (var ext in cert.Extensions)
        {
            _logger.LogDebug("  证书扩展: OID={Oid}, Critical={Critical}, Name={Name}", 
                ext.Oid?.Value, ext.Critical, ext.Oid?.FriendlyName);
        }
        
        return cert;
    }

    /// <summary>
    /// 获取挑战证书（TLS 握手时调用）
    /// </summary>
    public X509Certificate2? GetChallengeCertificate(string domain)
    {
        if (_challengeCertificates.TryGetValue(domain, out var cert))
        {
            if (cert.NotAfter > DateTime.UtcNow)
            {
                return cert;
            }
            // 已过期，清除
            _challengeCertificates.TryRemove(domain, out _);
            cert.Dispose();
        }
        return null;
    }

    /// <summary>
    /// 是否有活跃的挑战（快速检查，避免不必要的操作）
    /// </summary>
    public bool HasActiveChallenge => !_challengeCertificates.IsEmpty;

    /// <summary>
    /// 清理指定域名的挑战证书
    /// </summary>
    public void CleanupChallenge(string domain)
    {
        if (_challengeCertificates.TryRemove(domain, out var cert))
        {
            cert.Dispose();
            _logger.LogInformation("清理 TLS-ALPN-01 挑战证书: Domain={Domain}", domain);
        }
    }

    /// <summary>
    /// 清理所有挑战证书
    /// </summary>
    public void CleanupAll()
    {
        foreach (var kvp in _challengeCertificates)
        {
            if (_challengeCertificates.TryRemove(kvp.Key, out var cert))
            {
                cert.Dispose();
            }
        }
        _logger.LogInformation("已清理所有 TLS-ALPN-01 挑战证书");
    }

    /// <summary>
    /// 检查是否是 TLS-ALPN-01 挑战请求
    /// </summary>
    public static bool IsAcmeTlsAlpnChallenge(string? alpnProtocol)
    {
        return string.Equals(alpnProtocol, AcmeTlsAlpnProtocol, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 生成 TLS-ALPN-01 验证证书（内部方法）
    /// </summary>
    private X509Certificate2 GenerateChallengeCertificate(string domain, string keyAuthorization)
    {
        // 生成 ECDSA P-256 密钥对
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        
        var subject = new X500DistinguishedName($"CN={domain}");
        
        // 计算验证值：SHA-256(keyAuthorization)
        var acmeIdentifierHash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.ASCII.GetBytes(keyAuthorization));
        
        var request = new CertificateRequest(subject, ecdsa, HashAlgorithmName.SHA256);
        
        // 添加 SAN (Subject Alternative Name) 扩展
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName(domain);
        request.CertificateExtensions.Add(sanBuilder.Build());
        
        // 添加 id-pe-acmeIdentifier 扩展 (critical = true)
        // RFC 8737 要求扩展值必须是 ASN.1 DER 编码的 OCTET STRING
        // 即: Tag(0x04) + Length(0x20) + SHA-256 哈希值(32字节)
        var derEncoded = new byte[34];
        derEncoded[0] = 0x04; // ASN.1 OCTET STRING tag
        derEncoded[1] = 0x20; // Length = 32
        Array.Copy(acmeIdentifierHash, 0, derEncoded, 2, 32);
        
        var acmeExtension = new X509Extension(
            IdPeAcmeIdentifier,
            derEncoded,
            critical: true);
        request.CertificateExtensions.Add(acmeExtension);
        
        // 添加扩展密钥用途 - TLS 服务器认证
        var eku = new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, // id-kp-serverAuth
            false);
        request.CertificateExtensions.Add(eku);
        
        // 生成自签名证书（有效期 1 小时）
        var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        var notAfter = DateTimeOffset.UtcNow.AddHours(1);
        
        using var tempCert = request.CreateSelfSigned(notBefore, notAfter);
        
        // 导出为 PFX 并重新加载，确保私钥独立于原始 ECDSA 对象
        // 在 Windows 上，CreateSelfSigned 返回的证书可能只引用原始密钥对象，
        // 当 using 块结束后密钥会被 Dispose，导致 TLS 握手时私钥不可用
        var pfxBytes = tempCert.Export(X509ContentType.Pfx);
        return X509CertificateLoader.LoadPkcs12(pfxBytes, null, X509KeyStorageFlags.Exportable);
    }
}
