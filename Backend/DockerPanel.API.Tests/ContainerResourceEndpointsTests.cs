using System.Net;
using System.Text.Json;
using Xunit;

namespace DockerPanel.API.Tests;

/// <summary>
/// Container / Image / Network / Volume / Compose endpoint tests (api/containers, api/images, api/network,
/// api/volumes, api/compose). These depend on a live Docker engine; tagged "docker" so CI (no engine)
/// can filter them out. Assertions follow the engine-available behavior observed locally.
/// </summary>
[Trait("category", "docker")]
public sealed class ContainerResourceEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ContainerResourceEndpointsTests(TestWebApplicationFactory factory) => _factory = factory;

    private async Task<TestApiClient> CreateAuthorizedClientAsync()
    {
        var api = new TestApiClient(_factory.CreateClient());
        await api.EnsureSetupAsync();
        return api;
    }

    private const string UnknownId = "unknown-id";

    [Fact]
    public async Task GetContainers_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/containers");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task GetContainer_UnknownId_Returns404()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/containers/{UnknownId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetImages_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/images");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task GetImage_UnknownId_Returns404()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/images/{UnknownId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetNetworks_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/network");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetNetwork_UnknownId_Returns404()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/network/{UnknownId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetVolumes_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/volumes");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetVolume_UnknownId_Returns404()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/volumes/{UnknownId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteVolume_UnknownId_Returns404()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.DeleteAsync($"/api/volumes/{UnknownId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task StartContainer_UnknownId_ReturnsErrorResponse()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PostJsonAsync($"/api/containers/{UnknownId}/start", null);
        Assert.True(response.StatusCode is HttpStatusCode.InternalServerError or HttpStatusCode.NotFound, $"unexpected {response.StatusCode}");
    }

    [Fact]
    public async Task GetComposeProjects_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/compose/projects");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task ResourceGroups_WithoutAuth_Return401()
    {
        foreach (var path in new[] { "/api/containers", "/api/images", "/api/network", "/api/volumes", "/api/compose/projects" })
        {
            var response = await _factory.CreateClient().GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
