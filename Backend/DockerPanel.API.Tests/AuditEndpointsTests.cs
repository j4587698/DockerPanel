using System.Net;
using System.Text.Json;
using Xunit;

namespace DockerPanel.API.Tests;

/// <summary>
/// Audit log endpoint tests (api/audit, 2 routes).
/// </summary>
public sealed class AuditEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AuditEndpointsTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetLogs_AsAdmin_ReturnsOkWithPage()
    {
        var api = new TestApiClient(_factory.CreateClient());
        await api.EnsureSetupAsync();
        var response = await api.GetAsync("/api/audit/logs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("items", out _));
    }

    [Fact]
    public async Task GetLog_UnknownId_Returns404()
    {
        var api = new TestApiClient(_factory.CreateClient());
        await api.EnsureSetupAsync();
        var response = await api.GetAsync("/api/audit/logs/000000000000000000000000");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Logs_WithoutAuth_Return401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/audit/logs");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
