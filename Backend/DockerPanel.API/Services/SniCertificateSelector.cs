using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using DockerPanel.API.Data;
using DockerPanel.API.Services.Acme;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DockerPanel.API.Services;

/// <summary>
/// SNI 证书选择器
/// 负责从数据库加载证书，支持普通 HTTPS 和 TLS-ALPN-01 挑战
/// </summary>
public class SniCertificateSelector
{
    private readonly ILogger<SniCertificateSelector> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TlsAlpnChallengeService _tlsAlpnChallengeService;
    private readonly ConcurrentDictionary<string, X509Certificate2> _certificateCache = new();
    private readonly ConcurrentDictionary<string, DateTime> _cacheExpiry = new();
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    public SniCertificateSelector(
        ILogger<SniCertificateSelector> logger,
        IServiceScopeFactory scopeFactory,
        TlsAlpnChallengeService tlsAlpnChallengeService)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _tlsAlpnChallengeService = tlsAlpnChallengeService;
    }

    /// <summary>
    /// 根据 SNI 选择证书
    /// 用于 Kestrel ServerCertificateSelector
    /// </summary>
    public X509Certificate2? SelectCertificate(string? sni)
    {
        if (string.IsNullOrEmpty(sni))
        {
            return null;
        }

        try
        {
            // 1. 检查 TLS-ALPN-01 挑战（优先级最高）
            var challengeCert = GetTlsAlpnChallengeCertificate(sni);
            if (challengeCert != null)
            {
                _logger.LogDebug("使用 TLS-ALPN-01 挑战证书: {Domain}", sni);
                return challengeCert;
            }

            // 2. 从缓存获取
            if (_certificateCache.TryGetValue(sni, out var cachedCert))
            {
                if (_cacheExpiry.TryGetValue(sni, out var expiry) && expiry > DateTime.UtcNow)
                {
                    // 验证证书是否仍然有效
                    if (cachedCert.NotAfter > DateTime.UtcNow)
                    {
                        return cachedCert;
                    }
                }
                // 缓存过期或证书过期，移除
                _certificateCache.TryRemove(sni, out _);
                _cacheExpiry.TryRemove(sni, out _);
            }

            // 3. 从数据库加载
            var cert = LoadCertificateFromDatabase(sni);
            if (cert != null)
            {
                // 缓存证书
                _certificateCache[sni] = cert;
                _cacheExpiry[sni] = DateTime.UtcNow.Add(_cacheDuration);
                _logger.LogDebug("从数据库加载证书: {Domain}, 过期: {Expiry}", sni, cert.NotAfter);
                return cert;
            }

            // 4. 尝试通配符匹配
            var wildcardCert = FindWildcardCertificate(sni);
            if (wildcardCert != null)
            {
                _certificateCache[sni] = wildcardCert;
                _cacheExpiry[sni] = DateTime.UtcNow.Add(_cacheDuration);
                _logger.LogDebug("使用通配符证书: {Domain}", sni);
                return wildcardCert;
            }

            _logger.LogWarning("未找到证书: {Domain}", sni);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "选择证书失败: {Domain}", sni);
            return null;
        }
    }

    /// <summary>
    /// 获取 TLS-ALPN-01 挑战证书（纯内存，无数据库依赖）
    /// </summary>
    private X509Certificate2? GetTlsAlpnChallengeCertificate(string domain)
    {
        try
        {
            // 快速检查：如果没有活跃的挑战，直接跳过
            if (!_tlsAlpnChallengeService.HasActiveChallenge)
            {
                return null;
            }

            var cert = _tlsAlpnChallengeService.GetChallengeCertificate(domain);
            
            if (cert != null)
            {
                _logger.LogInformation("TLS-ALPN-01 挑战证书已返回: Domain={Domain}", domain);
            }
            
            return cert;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 TLS-ALPN-01 挑战证书失败: Domain={Domain}", domain);
            return null;
        }
    }

    /// <summary>
    /// 从数据库加载证书
    /// </summary>
    private X509Certificate2? LoadCertificateFromDatabase(string domain)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TinyDbContext>();
            
            var collection = dbContext.GetCollection<CertificateRecord>("certificates");
            
            // 查找精确匹配的证书
            var record = collection.FindOne(r => 
                r.Domains.Contains(domain) && 
                (r.Status == "valid" || r.Status == "Active") &&
                r.ExpiresAt > DateTime.UtcNow &&
                r.CertificateData != null && r.CertificateData != "" &&
                r.PrivateKeyData != null && r.PrivateKeyData != "");

            if (record != null)
            {
                return CreateCertificateFromRecord(record);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从数据库加载证书失败: {Domain}", domain);
            return null;
        }
    }

    /// <summary>
    /// 查找通配符证书
    /// </summary>
    private X509Certificate2? FindWildcardCertificate(string domain)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TinyDbContext>();
            
            var collection = dbContext.GetCollection<CertificateRecord>("certificates");
            
            // 获取所有有效的通配符证书
            var wildcardRecords = collection.Find(r => 
                (r.Status == "valid" || r.Status == "Active") &&
                r.ExpiresAt > DateTime.UtcNow &&
                r.CertificateData != null && r.CertificateData != "" &&
                r.PrivateKeyData != null && r.PrivateKeyData != "" &&
                r.Domains.Any(d => d.StartsWith("*.")));

            foreach (var record in wildcardRecords)
            {
                // 检查域名是否匹配通配符
                foreach (var certDomain in record.Domains)
                {
                    if (MatchesWildcard(certDomain, domain))
                    {
                        return CreateCertificateFromRecord(record);
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查找通配符证书失败: {Domain}", domain);
            return null;
        }
    }

    /// <summary>
    /// 检查域名是否匹配通配符
    /// </summary>
    private static bool MatchesWildcard(string pattern, string domain)
    {
        if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(domain))
        {
            return false;
        }

        // 精确匹配
        if (string.Equals(pattern, domain, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 通配符匹配 *.example.com -> sub.example.com
        if (pattern.StartsWith("*."))
        {
            var baseDomain = pattern[2..]; // 移除 "*."
            if (domain.EndsWith(baseDomain, StringComparison.OrdinalIgnoreCase))
            {
                // 确保是一级子域名
                var prefix = domain[..^baseDomain.Length].TrimEnd('.');
                if (!string.IsNullOrEmpty(prefix) && !prefix.Contains('.'))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 从记录创建证书对象
    /// </summary>
    private X509Certificate2? CreateCertificateFromRecord(CertificateRecord record)
    {
        try
        {
            // 合并证书链
            var certPem = record.CertificateData;
            if (!string.IsNullOrEmpty(record.CertificateChain))
            {
                certPem = certPem + "\n" + record.CertificateChain;
            }

            // 创建带私钥的证书
            var cert = X509Certificate2.CreateFromPem(certPem, record.PrivateKeyData);
            
            // 导出为 PFX 并重新加载（确保证书可导出）
            var pfxBytes = cert.Export(X509ContentType.Pfx);
            return X509CertificateLoader.LoadPkcs12(pfxBytes, null, X509KeyStorageFlags.Exportable);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建证书对象失败: {Id}", record.Id);
            return null;
        }
    }

    /// <summary>
    /// 清除证书缓存
    /// </summary>
    public void ClearCache(string? domain = null)
    {
        if (string.IsNullOrEmpty(domain))
        {
            _certificateCache.Clear();
            _cacheExpiry.Clear();
            _logger.LogInformation("已清除所有证书缓存");
        }
        else
        {
            _certificateCache.TryRemove(domain, out _);
            _cacheExpiry.TryRemove(domain, out _);
            _logger.LogInformation("已清除证书缓存: {Domain}", domain);
        }
    }

    /// <summary>
    /// 预热缓存 - 加载所有有效证书
    /// </summary>
    public void WarmupCache()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TinyDbContext>();
            
            var collection = dbContext.GetCollection<CertificateRecord>("certificates");
            var records = collection.Find(r => 
                (r.Status == "valid" || r.Status == "Active") &&
                r.ExpiresAt > DateTime.UtcNow &&
                r.CertificateData != null && r.CertificateData != "" &&
                r.PrivateKeyData != null && r.PrivateKeyData != "");

            foreach (var record in records)
            {
                var cert = CreateCertificateFromRecord(record);
                if (cert == null) continue;

                foreach (var domain in record.Domains)
                {
                    if (!_certificateCache.ContainsKey(domain))
                    {
                        _certificateCache[domain] = cert;
                        _cacheExpiry[domain] = DateTime.UtcNow.Add(_cacheDuration);
                    }
                }
            }

            _logger.LogInformation("证书缓存预热完成: {Count} 个域名", _certificateCache.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "证书缓存预热失败");
        }
    }
}

/// <summary>
/// 静态服务定位器 - 用于 Kestrel ServerCertificateSelector 回调
/// </summary>
public static class SniCertificateSelectorLocator
{
    private static SniCertificateSelector? _instance;
    
    public static SniCertificateSelector? Instance => _instance;
    
    public static void Initialize(SniCertificateSelector selector)
    {
        _instance = selector;
    }
    
    public static void Reset()
    {
        _instance = null;
    }
}
