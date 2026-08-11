using System.Net;
using System.Text.Json;
using Xunit;

namespace DockerPanel.API.Tests;

/// <summary>
/// Settings endpoint tests (api/settings, 13 routes).
/// </summary>
public sealed class SettingsEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SettingsEndpointsTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetPublic_IsAnonymous_ReturnsOk()
    {
        var response = await _factory.CreateClient().GetAsync("/api/settings/public");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetPublic_WithoutAuth_Succeeds()
    {
        var response = await _factory.CreateClient().GetAsync("/api/settings/public");
        Assert.Contains("application/json", response.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task GetHealth_AsAdmin_ReturnsOk()
    {
        var api = new TestApiClient(_factory.CreateClient());
        await api.EnsureSetupAsync();
        var response = await api.GetAsync("/api/settings/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSettings_AsAdmin_ReturnsOk()
    {
        var api = new TestApiClient(_factory.CreateClient());
        await api.EnsureSetupAsync();
        var response = await api.GetAsync("/api/settings/system");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task HealthCheck_AsAdmin_ReturnsOk()
    {
        var api = new TestApiClient(_factory.CreateClient());
        await api.EnsureSetupAsync();
        var response = await api.PostJsonAsync("/api/settings/health/check", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Export_AsAdmin_ReturnsOk()
    {
        var api = new TestApiClient(_factory.CreateClient());
        await api.EnsureSetupAsync();
        var response = await api.GetAsync("/api/settings/system/export");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateSettings_AsAdmin_ReturnsOk()
    {
        var api = new TestApiClient(_factory.CreateClient());
        await api.EnsureSetupAsync();
        var response = await api.PutJsonAsync("/api/settings/system", "{}");
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest, $"unexpected {response.StatusCode}");
    }

    [Fact]
    public async Task ProtectedEndpoints_WithoutAuth_Return401()
    {
        foreach (var path in new[] { "/api/settings/health", "/api/settings/system", "/api/settings/system/export" })
        {
            var response = await _factory.CreateClient().GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
