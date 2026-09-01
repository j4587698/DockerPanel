using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DockerPanel.API.Data;
using DockerPanel.API.Models;
using DockerPanel.API.Services;
using DockerPanel.API.Services.Acme;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DockerPanel.API.Tests;

public class SniCertificateSelectorTests : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly IServiceScope _scope;
    private readonly SniCertificateSelector _selector;
    private readonly TinyDbContext _dbContext;

    public SniCertificateSelectorTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _scope = factory.Services.CreateScope();
        _selector = _scope.ServiceProvider.GetRequiredService<SniCertificateSelector>();
        _dbContext = _scope.ServiceProvider.GetRequiredService<TinyDbContext>();
    }

    public void Dispose()
    {
        _scope.Dispose();
    }

    private static (string CertPem, string KeyPem) GenerateSelfSignedCert(string domain)
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest($"CN={domain}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName(domain);
        req.CertificateExtensions.Add(sanBuilder.Build());

        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var certPem = cert.ExportCertificatePem();
        var keyPem = rsa.ExportPkcs8PrivateKeyPem();
        return (certPem, keyPem);
    }

    [Fact]
    public void SelectCertificate_NullOrEmpty_ReturnsDefaultCert()
    {
        var cert = _selector.SelectCertificate(null);
        Assert.NotNull(cert);
    }

    [Fact]
    public void SelectCertificate_DomainWithMatchingMappingCertificateId_ReturnsExactCert()
    {
        var domain = $"app-{Guid.NewGuid():N}.example.com";
        var (certPem, keyPem) = GenerateSelfSignedCert(domain);
        var certCollection = _dbContext.GetCollection<CertificateRecord>(DbCollections.Certificates);
        var certRecord = new CertificateRecord
        {
            Id = $"cert_{Guid.NewGuid():N}",
            Name = domain,
            Domains = new List<string> { domain },
            Status = "active",
            IssuedAt = DateTime.UtcNow.AddDays(-1),
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CertificateData = certPem,
            PrivateKeyData = keyPem
        };
        certCollection.Insert(certRecord);

        var mappingCollection = _dbContext.GetCollection<DomainMapping>("domain_mappings");
        var mapping = new DomainMapping
        {
            Id = $"map_{Guid.NewGuid():N}",
            Domain = domain,
            ContainerId = "c1",
            DestinationAddress = "127.0.0.1:8080",
            CertificateId = certRecord.Id,
            EnableSsl = true,
            Enabled = true
        };
        mappingCollection.Insert(mapping);

        _selector.ClearCache(domain);
        var selected = _selector.SelectCertificate(domain);
        Assert.NotNull(selected);
        Assert.Contains(domain, selected.Subject);
    }

    [Fact]
    public void SelectCertificate_FallbackDirectCertMatch_ReturnsCert()
    {
        var domain = $"standalone-{Guid.NewGuid():N}.example.com";
        var (certPem, keyPem) = GenerateSelfSignedCert(domain);
        var certCollection = _dbContext.GetCollection<CertificateRecord>(DbCollections.Certificates);
        var certRecord = new CertificateRecord
        {
            Id = $"cert_{Guid.NewGuid():N}",
            Name = domain,
            Domains = new List<string> { domain },
            Status = "active",
            IssuedAt = DateTime.UtcNow.AddDays(-1),
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CertificateData = certPem,
            PrivateKeyData = keyPem
        };
        certCollection.Insert(certRecord);

        _selector.ClearCache(domain);
        // No domain mapping exists, should fallback to cert by domain
        var selected = _selector.SelectCertificate(domain);
        Assert.NotNull(selected);
        Assert.Contains(domain, selected.Subject);
    }
}
