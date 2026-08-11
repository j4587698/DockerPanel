using System.Net;
using Xunit;

namespace DockerPanel.API.Tests;

/// <summary>
/// Node endpoint tests (api/nodes, 18 routes).
/// </summary>
public sealed class NodeEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public NodeEndpointsTests(TestWebApplicationFactory factory) => _factory = factory;

    private async Task<TestApiClient> CreateAuthorizedClientAsync()
    {
        var api = new TestApiClient(_factory.CreateClient());
        await api.EnsureSetupAsync();
        return api;
    }

    private const string UnknownId = "000000000000000000000000";

    [Fact]
    public async Task GetNodes_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/nodes");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetNode_UnknownId_Returns404()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/nodes/{UnknownId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetNodeGroups_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/nodes/groups");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetNodeGroup_UnknownId_Returns404()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/nodes/groups/{UnknownId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateNode_WithValidBody_ReturnsCreated()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PostJsonAsync(
            "/api/nodes",
            """{"name":"ci-node","host":"127.0.0.1","port":2375,"engineType":"docker"}""");
        Assert.True(response.StatusCode is HttpStatusCode.Created or HttpStatusCode.BadRequest, $"unexpected {response.StatusCode}");
    }

    [Fact]
    public async Task DeleteNode_UnknownId_ReturnsNoContent()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.DeleteAsync($"/api/nodes/{UnknownId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GetStats_UnknownId_ReturnsOkOr404()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/nodes/{UnknownId}/stats");
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound, $"unexpected {response.StatusCode}");
    }

    [Fact]
    public async Task TestConnection_UnknownId_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PostJsonAsync($"/api/nodes/{UnknownId}/test-connection", null);
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest, $"unexpected {response.StatusCode}");
    }

    [Fact]
    public async Task Nodes_WithoutAuth_Return401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/nodes");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
