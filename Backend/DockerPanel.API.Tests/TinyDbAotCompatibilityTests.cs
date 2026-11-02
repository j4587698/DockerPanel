using DockerPanel.API.Data;
using DockerPanel.API.Models.Acme;
using DockerPanel.API.Services;
using DockerPanel.API.Services.Acme;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DockerPanel.API.Tests;

/// <summary>
/// TinyDb AOT 序列化兼容性守护测试。
///
/// 背景：TinyDb 0.6.2 的源生成器在反序列化**嵌套对象内部的 DateTime?（Nullable&lt;DateTime&gt;）**时
/// 会抛 NotSupportedException（"AOT generic conversion does not support target type Nullable&lt;DateTime&gt;"），
/// 且异常发生在 FromDocument 阶段——意味着只要集合里有一条带该字段的记录，
/// 整张表的 FindAll() 都会失败（不是单条记录读不出来，是整表都读不出来）。
///
/// 该问题在 TinyDb 0.6.3 中已修复。本测试用于锁定这个版本下限：
/// 若有人把 TinyDb 降级到 0.6.2 及以下，本测试会立刻失败并给出明确原因。
/// </summary>
public class TinyDbAotCompatibilityTests : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    private readonly IServiceScope _scope;
    private readonly TinyDbContext _dbContext;

    public TinyDbAotCompatibilityTests(TestWebApplicationFactory factory)
    {
        _scope = factory.Services.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<TinyDbContext>();
    }

    public void Dispose() => _scope.Dispose();

    [Fact]
    public void NestedObjectWithNullableDateTime_RoundTrips()
    {
        var collection = _dbContext.GetCollection<CertificateRecord>(DbCollections.Certificates);
        var record = new CertificateRecord
        {
            Id = $"probe_{Guid.NewGuid():N}",
            Name = "probe.example.com",
            Domains = new List<string> { "probe.example.com" },
            Status = "active",
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(90),
            AccountId = "acct-probe",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // AutoRenewalConfiguration 同时覆盖了嵌套 DateTime?、List<string>、
        // Dictionary<string, object> 和二级嵌套对象（RenewalStatus）
        record.AutoRenewalConfiguration = new AutoRenewalConfiguration
        {
            CertificateId = record.Id,
            AccountId = "acct-probe",
            AutoRenewalEnabled = true,
            RenewalDaysBeforeExpiry = 20,
            LastRenewalAttempt = DateTime.UtcNow,
            NextRenewalAttempt = DateTime.UtcNow.AddDays(10),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            NotificationEmails = new List<string> { "a@example.com" },
            Settings = new Dictionary<string, object> { ["KeyType"] = "RSA2048" },
            Status = new RenewalStatus { State = "pending" }
        };

        collection.Insert(record);

        var read = collection.FindAll().FirstOrDefault(c => c.Id == record.Id);

        Assert.NotNull(read);
        Assert.NotNull(read!.AutoRenewalConfiguration);
        Assert.NotNull(read.AutoRenewalConfiguration!.NextRenewalAttempt);
        Assert.Equal(20, read.AutoRenewalConfiguration.RenewalDaysBeforeExpiry);
        Assert.Equal("pending", read.AutoRenewalConfiguration.Status.State);
        // Dictionary<string, object> 也要能往返（续期配置里的 DnsCredentials 依赖它）
        Assert.Equal("RSA2048", read.AutoRenewalConfiguration.Settings["KeyType"]?.ToString());
        Assert.Equal("a@example.com", read.AutoRenewalConfiguration.NotificationEmails[0]);
    }
}
