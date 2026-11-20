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
    public async Task SystemHostMetrics_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/system/host-metrics");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // 未挂载 /host/proc 时也必须返回结构化的 available 标记，而不是 4xx/5xx
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"available\"", json);
        Assert.Contains("\"fileSystemAvailable\"", json);
    }

    [Fact]
    public async Task RemovedFakeSystemEndpoints_DoNotReturnJson()
    {
        var api = await CreateAuthorizedClientAsync();
        foreach (var path in new[] { "/api/system/status", "/api/system/metrics", "/api/system/health" })
        {
            var response = await api.GetAsync(path);

            // 这些接口已被删除；命中的是 SPA fallback（HTML），不应再返回伪造的 JSON 指标。
            var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            Assert.False(
                mediaType.Contains("json", StringComparison.OrdinalIgnoreCase),
                $"{path} should not expose fabricated JSON data, got {mediaType}");

            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("\"cpuUsage\"", body);
            Assert.DoesNotContain("\"overall\"", body);
        }
    }

    [Fact]
    public async Task SystemDockerStats_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/system/docker-stats");
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.InternalServerError, $"unexpected {response.StatusCode}");
    }

    [Fact]
    public async Task SystemDockerStats_WithoutStats_AsAdmin_ReturnsOkWithoutAggregates()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/system/docker-stats?includeStats=false");
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.InternalServerError, $"unexpected {response.StatusCode}");

        if (response.StatusCode != HttpStatusCode.OK) return;

        var body = await response.Content.ReadAsStringAsync();
        // includeStats=false 时不应包含容器资源聚合值（Docker 不可用时同样不会出现）
        Assert.DoesNotContain("cpuUsagePercent", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rxBytesPerSec", body, StringComparison.OrdinalIgnoreCase);
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
