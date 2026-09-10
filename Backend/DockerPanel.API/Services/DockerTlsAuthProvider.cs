using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Docker.DotNet.Handler.Abstractions;
using DockerPanel.API.Models;

namespace DockerPanel.API.Services;

/// <summary>
/// Docker TLS（双向认证）提供程序。
/// 按节点 TLS 配置加载 CA / 客户端证书，在传输 Handler 上配置客户端证书与服务器证书校验：
/// - 提供 CA 证书时：以该 CA 为信任根校验服务器证书（私有 CA 场景），并校验主机名
/// - SkipVerify=true 时跳过服务器证书校验（不安全，仅调试用）
/// - ServerName 用于证书主机名与连接地址不一致的场景（如按 IP 连接、证书签发给域名）
/// </summary>
public sealed class DockerTlsAuthProvider : IAuthProvider
{
    private readonly X509Certificate2? _caCert;
    private readonly X509Certificate2Collection _clientCertificates = new();
    private readonly bool _skipVerify;
    private readonly string? _serverName;

    public DockerTlsAuthProvider(NodeTlsConfig config)
    {
        _skipVerify = config.SkipVerify;
        _serverName = string.IsNullOrWhiteSpace(config.ServerName) ? null : config.ServerName.Trim();

        if (!string.IsNullOrWhiteSpace(config.CaCertPath) && File.Exists(config.CaCertPath))
        {
            _caCert = LoadCertificate(config.CaCertPath, null);
        }

        if (!string.IsNullOrWhiteSpace(config.ClientCertPath) && File.Exists(config.ClientCertPath))
        {
            var clientCert = LoadCertificate(config.ClientCertPath, config.ClientKeyPath);
            _clientCertificates.Add(clientCert);
        }
    }

    public bool TlsEnabled => true;

    public HttpMessageHandler ConfigureHandler(HttpMessageHandler handler)
    {
        switch (handler)
        {
            case HttpClientHandler httpClientHandler:
                Configure(httpClientHandler.ClientCertificates,
                    () => httpClientHandler.ServerCertificateCustomValidationCallback = ValidateServerCertificate);
                httpClientHandler.ClientCertificateOptions = ClientCertificateOption.Manual;
                break;

            case SocketsHttpHandler socketsHttpHandler:
                socketsHttpHandler.SslOptions.ClientCertificates ??= new X509CertificateCollection();
                Configure(socketsHttpHandler.SslOptions.ClientCertificates,
                    () => socketsHttpHandler.SslOptions.RemoteCertificateValidationCallback =
                        (sender, cert, chain, errors) => ValidateServerCertificate(sender, cert, chain, errors));
                break;

            case DelegatingHandler delegatingHandler when delegatingHandler.InnerHandler != null:
                ConfigureHandler(delegatingHandler.InnerHandler);
                break;
        }

        return handler;
    }

    private void Configure(X509CertificateCollection handlerCertificates, Action applyValidation)
    {
        foreach (var cert in _clientCertificates)
        {
            handlerCertificates.Add(cert);
        }

        // 需要自定义校验：跳过校验，或提供了私有 CA / ServerName 覆盖
        if (_skipVerify || _caCert != null || _serverName != null)
        {
            applyValidation();
        }
    }

    private bool ValidateServerCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
    {
        if (_skipVerify) return true;
        if (certificate is not X509Certificate2 cert) return false;

        var expectedHost = _serverName;
        if (string.IsNullOrEmpty(expectedHost) && sender is HttpRequestMessage request)
        {
            expectedHost = request.RequestUri?.Host;
        }

        // 主机名校验（证书 SAN/CN 与期望主机名匹配，支持通配符）
        if (!string.IsNullOrEmpty(expectedHost) &&
            !cert.MatchesHostname(expectedHost, allowWildcards: true, allowCommonName: true))
        {
            return false;
        }

        // 私有 CA 校验：以配置的 CA 为唯一信任根
        if (_caCert != null)
        {
            using var customChain = new X509Chain();
            customChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            customChain.ChainPolicy.CustomTrustStore.Add(_caCert);
            customChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            return customChain.Build(cert);
        }

        // 未提供 CA：回退系统默认校验结果（但主机名已在上面自行校验过）
        return sslPolicyErrors == SslPolicyErrors.None ||
               (expectedHost != null && sslPolicyErrors == SslPolicyErrors.RemoteCertificateNameMismatch);
    }

    private static X509Certificate2 LoadCertificate(string certPath, string? keyPath)
    {
        // 优先按 PEM 加载（cert/key 对或单文件），失败则按 DER/PFX 加载
        try
        {
            return string.IsNullOrWhiteSpace(keyPath) || !File.Exists(keyPath)
                ? X509Certificate2.CreateFromPemFile(certPath)
                : X509Certificate2.CreateFromPemFile(certPath, keyPath);
        }
        catch
        {
            try
            {
                return X509CertificateLoader.LoadCertificateFromFile(certPath);
            }
            catch
            {
                return X509CertificateLoader.LoadPkcs12FromFile(certPath, password: null);
            }
        }
    }
}
