using DockerPanel.API.Data;
using DockerPanel.API.Models.Acme;
using DockerPanel.API.Services;
using DockerPanel.API.Services.Acme;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DockerPanel.API.Tests;

/// <summary>
/// 证书自动续期关键行为回归测试
/// </summary>
public class CertificateRenewalTests : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    private readonly IServiceScope _scope;
    private readonly TinyDbContext _dbContext;
    private readonly ICertificateManagementService _certificateManagementService;
    private readonly ICertificateAutoService _certificateAutoService;

    public CertificateRenewalTests(TestWebApplicationFactory factory)
    {
        _scope = factory.Services.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<TinyDbContext>();
        _certificateManagementService = _scope.ServiceProvider.GetRequiredService<ICertificateManagementService>();
        _certificateAutoService = _scope.ServiceProvider.GetRequiredService<ICertificateAutoService>();
    }

    public void Dispose()
    {
        _scope.Dispose();
    }

    private CertificateRecord InsertCertificate(DateTime expiresAt, bool autoRenewalEnabled, string status = "active")
    {
        var domain = $"renew-{Guid.NewGuid():N}.example.com";
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
            AutoRenewalEnabled = autoRenewalEnabled,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.GetCollection<CertificateRecord>(DbCollections.Certificates).Insert(record);
        return record;
    }

    [Fact]
    public async Task GetExpiringCertificates_IncludesAlreadyExpiredCertificate()
    {
        // 回归：过期检查曾排除 ExpiresAt <= now，导致证书一旦过期就永远不会再进入续期队列
        var record = InsertCertificate(DateTime.UtcNow.AddDays(-5), autoRenewalEnabled: true);

        var result = (await _certificateManagementService.GetExpiringCertificatesAsync(15)).ToList();

        var target = result.FirstOrDefault(c => c.CertificateId == record.Id);
        Assert.NotNull(target);
        Assert.True(target!.DaysUntilExpiry <= 0);
        Assert.Equal("expired", target.Status);
        Assert.True(target.AutoRenewalEnabled);
    }

    [Fact]
    public async Task GetExpiringCertificates_ExcludesRevokedCertificate()
    {
        var record = InsertCertificate(DateTime.UtcNow.AddDays(-1), autoRenewalEnabled: true, status: "revoked");

        var result = (await _certificateManagementService.GetExpiringCertificatesAsync(15)).ToList();

        Assert.DoesNotContain(result, c => c.CertificateId == record.Id);
    }

    [Fact]
    public async Task GetAutoRenewalConfiguration_SynthesizesFromRecordWhenMissing()
    {
        // 回归：配置只存在内存中，进程重启后丢失。缺少配置时应能从证书记录自动补全
        var record = InsertCertificate(DateTime.UtcNow.AddDays(3), autoRenewalEnabled: true);

        var config = await _certificateAutoService.GetAutoRenewalConfigurationAsync(record.Id);

        Assert.NotNull(config);
        Assert.True(config!.AutoRenewalEnabled);
        Assert.Equal(record.Id, config.CertificateId);
        Assert.Equal(record.AccountId, config.AccountId);
        Assert.True(config.RenewalDaysBeforeExpiry > 0);
        Assert.True(config.Settings.ContainsKey("PreferredChallengeTypes"));
    }

    [Fact]
    public async Task GetAutoRenewalConfiguration_ReturnsNullWhenAutoRenewalDisabled()
    {
        var record = InsertCertificate(DateTime.UtcNow.AddDays(3), autoRenewalEnabled: false);

        var config = await _certificateAutoService.GetAutoRenewalConfigurationAsync(record.Id);

        Assert.Null(config);
    }

    [Fact]
    public async Task SetAutoRenewalConfiguration_PersistsToCertificateRecord()
    {
        // 回归：配置仅写内存，重启即丢；必须落到证书记录上，且写回后整表仍可正常读取
        var record = InsertCertificate(DateTime.UtcNow.AddDays(10), autoRenewalEnabled: false);

        var configuration = new AutoRenewalConfiguration
        {
            CertificateId = record.Id,
            AccountId = "acct-test",
            AutoRenewalEnabled = true,
            RenewalDaysBeforeExpiry = 20
        };

        var result = await _certificateAutoService.SetAutoRenewalConfigurationAsync(record.Id, configuration);
        Assert.True(result.Success);

        var stored = _dbContext.GetCollection<CertificateRecord>(DbCollections.Certificates)
            .FindAll()
            .FirstOrDefault(c => c.Id == record.Id);

        Assert.NotNull(stored);
        Assert.True(stored!.AutoRenewalEnabled);
        Assert.NotNull(stored.NextRenewalAttempt);
        Assert.NotNull(stored.AutoRenewalConfiguration);
        Assert.Equal(20, stored.AutoRenewalConfiguration!.RenewalDaysBeforeExpiry);
        Assert.Equal(record.Id, stored.AutoRenewalConfiguration.CertificateId);
    }
}
