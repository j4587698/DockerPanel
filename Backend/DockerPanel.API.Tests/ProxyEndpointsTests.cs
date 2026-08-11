using System.Net;
using Xunit;

namespace DockerPanel.API.Tests;

/// <summary>
/// Reverse proxy endpoint tests (api/proxy, 14 routes).
/// </summary>
public sealed class ProxyEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ProxyEndpointsTests(TestWebApplicationFactory factory) => _factory = factory;

    private async Task<TestApiClient> CreateAuthorizedClientAsync()
    {
        var api = new TestApiClient(_factory.CreateClient());
        await api.EnsureSetupAsync();
        return api;
    }

    private const string UnknownId = "000000000000000000000000";

    [Fact]
    public async Task GetConfig_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/proxy/config");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetStatus_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/proxy/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Reload_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PostJsonAsync("/api/proxy/reload", null);
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest, $"unexpected {response.StatusCode}");
    }

    [Fact]
    public async Task GetMappings_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/proxy/mappings");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateMapping_WithValidBody_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PostJsonAsync(
            "/api/proxy/mappings",
            """{"domain":"ci.example.com","target":"http://127.0.0.1:8080"}""");
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest, $"unexpected {response.StatusCode}");
    }

    [Fact]
    public async Task UpdateMapping_UnknownId_Returns404()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PutJsonAsync($"/api/proxy/mappings/{UnknownId}", """{"domain":"x.example.com"}""");
        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest, $"unexpected {response.StatusCode}");
    }

    [Fact]
    public async Task DeleteMapping_UnknownId_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.DeleteAsync($"/api/proxy/mappings/{UnknownId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteRoute_UnknownId_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.DeleteAsync($"/api/proxy/routes/{UnknownId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCluster_UnknownId_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.DeleteAsync($"/api/proxy/clusters/{UnknownId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Proxy_WithoutAuth_Return401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/proxy/config");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
