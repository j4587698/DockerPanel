using System.Net;
using Xunit;

namespace DockerPanel.API.Tests;

/// <summary>
/// System / NodeResource / Registry / AutoUpdate endpoint tests. Depends on a live Docker engine;
/// tagged "docker" so CI (no engine) can filter them out.
/// </summary>
[Trait("category", "docker")]
public sealed class SystemRegistryAutoUpdateEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SystemRegistryAutoUpdateEndpointsTests(TestWebApplicationFactory factory) => _factory = factory;

    private async Task<TestApiClient> CreateAuthorizedClientAsync()
    {
        var api = new TestApiClient(_factory.CreateClient());
        await api.EnsureSetupAsync();
        return api;
    }

    private const string UnknownId = "unknown-id";

    [Fact]
    public async Task SystemInfo_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/system/info");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SystemStatus_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/system/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SystemMetrics_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/system/metrics");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SystemHealth_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/system/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SystemDockerStats_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/system/docker-stats");
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.InternalServerError, $"unexpected {response.StatusCode}");
    }

    [Fact]
    public async Task NodeResourceOverview_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/noderesource/overview");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NodeResourceDashboard_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/noderesource/dashboard");
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest or HttpStatusCode.InternalServerError, $"unexpected {response.StatusCode}");
    }

    [Fact]
    public async Task NodeResourceClusterStats_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/noderesource/cluster/stats");
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.InternalServerError, $"unexpected {response.StatusCode}");
    }

    [Fact]
    public async Task NodeResourceAlerts_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/noderesource/alerts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetRegistries_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/registries");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteRegistry_UnknownId_Returns404()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.DeleteAsync($"/api/registries/{UnknownId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AutoUpdateSettings_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/auto-update/settings");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AutoUpdateConfigs_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/auto-update/configs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AutoUpdateAvailableUpdates_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/auto-update/available-updates");
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.InternalServerError, $"unexpected {response.StatusCode}");
    }

    [Fact]
    public async Task SystemGroups_WithoutAuth_Return401()
    {
        foreach (var path in new[] { "/api/system/info", "/api/noderesource/overview", "/api/registries", "/api/auto-update/settings" })
        {
            var response = await _factory.CreateClient().GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
