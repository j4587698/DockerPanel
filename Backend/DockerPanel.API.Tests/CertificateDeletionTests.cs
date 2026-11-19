using System.Net;
using DockerPanel.API.Data;
using DockerPanel.API.Models;
using DockerPanel.API.Models.Acme;
using DockerPanel.API.Services;
using DockerPanel.API.Services.Acme;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DockerPanel.API.Tests;

/// <summary>
/// 证书删除行为的回归测试：
/// - 仍在使用中的（未过期）证书不允许删除，且错误信息里必须带上被占用的域名；
/// - 已过期/失效的证书即使仍被 domain_mappings 绑定，也应允许删除，并自动改绑到有效证书。
/// </summary>
public sealed class CertificateDeletionTests : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly IServiceScope _scope;
    private readonly TinyDbContext _dbContext;

    public CertificateDeletionTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _scope = factory.Services.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<TinyDbContext>();
    }

    public void Dispose() => _scope.Dispose();

    private async Task<TestApiClient> CreateAuthorizedClientAsync()
    {
        var api = new TestApiClient(_factory.CreateClient());
        await api.EnsureSetupAsync();
        return api;
    }

    private CertificateRecord InsertCertificate(string domain, DateTime expiresAt, string status)
    {
        var record = new CertificateRecord
        {
            Id = $"cert_{Guid.NewGuid():N}",
            Name = domain,
            Type = "SSL",
            Domains = new List<string> { domain },
            Status = status,
            IssuedAt = expiresAt.AddDays(-90),
            ExpiresAt = expiresAt,
            Issuer = "Test CA",
            CertificateData = "-----BEGIN CERTIFICATE-----\nMIIB\n-----END CERTIFICATE-----",
            PrivateKeyData = "-----BEGIN PRIVATE KEY-----\nMIIB\n-----END PRIVATE KEY-----",
            AccountId = "acct-test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.GetCollection<CertificateRecord>(DbCollections.Certificates).Insert(record);
        return record;
    }

    private DomainMapping InsertMapping(string domain, string certificateId)
    {
        var mapping = new DomainMapping
        {
            Id = $"map_{Guid.NewGuid():N}",
            ContainerId = "container-test",
            ContainerName = "container-test",
            Domain = domain,
            CertificateId = certificateId,
            EnableSsl = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.GetCollection<DomainMapping>("domain_mappings").Insert(mapping);
        return mapping;
    }

    [Fact]
    public async Task DeleteValidCertificate_InUse_ReturnsBadRequestWithDomainList()
    {
        var domain = $"del-inuse-{Guid.NewGuid():N}.example.com";
        var certificate = InsertCertificate(domain, DateTime.UtcNow.AddDays(60), "Active");
        var mapping = InsertMapping(domain, certificate.Id);

        var api = await CreateAuthorizedClientAsync();
        var response = await api.DeleteAsync($"/api/acme/certificates/{certificate.Id}?force=false");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // 回归：GetMessage 的 defaultValue 会被语言包覆盖，导致用户看不到具体域名
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(domain, body);

        var reloaded = _dbContext.GetCollection<DomainMapping>("domain_mappings").FindById(mapping.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(certificate.Id, reloaded!.CertificateId);
    }

    [Fact]
    public async Task DeleteExpiredCertificate_BoundToMapping_RebindsToValidCertificate()
    {
        var domain = $"del-expired-{Guid.NewGuid():N}.example.com";
        var expired = InsertCertificate(domain, DateTime.UtcNow.AddDays(-10), "expired");
        var valid = InsertCertificate(domain, DateTime.UtcNow.AddDays(60), "Active");
        var mapping = InsertMapping(domain, expired.Id);

        var api = await CreateAuthorizedClientAsync();
        var response = await api.DeleteAsync($"/api/acme/certificates/{expired.Id}?force=false");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var reloaded = _dbContext.GetCollection<DomainMapping>("domain_mappings").FindById(mapping.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(valid.Id, reloaded!.CertificateId);
        Assert.True(reloaded.EnableSsl);

        var certificates = _dbContext.GetCollection<CertificateRecord>(DbCollections.Certificates);
        Assert.Null(certificates.FindById(expired.Id));
    }

    [Fact]
    public async Task DeleteExpiredCertificate_WithoutReplacement_UnbindsMapping()
    {
        var domain = $"del-norepl-{Guid.NewGuid():N}.example.com";
        var expired = InsertCertificate(domain, DateTime.UtcNow.AddDays(-10), "expired");
        var mapping = InsertMapping(domain, expired.Id);

        var api = await CreateAuthorizedClientAsync();
        var response = await api.DeleteAsync($"/api/acme/certificates/{expired.Id}?force=false");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var reloaded = _dbContext.GetCollection<DomainMapping>("domain_mappings").FindById(mapping.Id);
        Assert.NotNull(reloaded);
        Assert.True(string.IsNullOrEmpty(reloaded!.CertificateId));
        Assert.False(reloaded.EnableSsl);
    }
}
