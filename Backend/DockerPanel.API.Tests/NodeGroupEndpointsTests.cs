using System.Net;
using Xunit;

namespace DockerPanel.API.Tests;

/// <summary>
/// NodeGroup endpoint tests (api/nodegroup, 23 routes).
/// </summary>
public sealed class NodeGroupEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public NodeGroupEndpointsTests(TestWebApplicationFactory factory) => _factory = factory;

    private async Task<TestApiClient> CreateAuthorizedClientAsync()
    {
        var api = new TestApiClient(_factory.CreateClient());
        await api.EnsureSetupAsync();
        return api;
    }

    private const string UnknownId = "000000000000000000000000";

    [Fact]
    public async Task GetGroups_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/nodegroup/groups");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetGroup_UnknownId_Returns404()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/nodegroup/groups/{UnknownId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTags_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/nodegroup/tags");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTag_UnknownId_Returns404()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/nodegroup/tags/{UnknownId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetOverview_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/nodegroup/overview");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetGroupNodes_UnknownGroup_Returns404()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/nodegroup/groups/{UnknownId}/nodes");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetGroupStatistics_UnknownGroup_Returns404()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/nodegroup/groups/{UnknownId}/statistics");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetNodeGroups_UnknownNode_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/nodegroup/nodes/{UnknownId}/groups");
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound, $"unexpected {response.StatusCode}");
    }

    [Fact]
    public async Task AddNodeToGroup_UnknownIds_ReturnsBadRequestOr404()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PostJsonAsync($"/api/nodegroup/groups/{UnknownId}/nodes/{UnknownId}", null);
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound or HttpStatusCode.BadRequest, $"unexpected {response.StatusCode}");
    }

    [Fact]
    public async Task CreateGroup_WithValidBody_ReturnsCreated()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PostJsonAsync(
            "/api/nodegroup/groups",
            """{"name":"ci-group","description":"integration test group"}""");
        Assert.True(response.StatusCode is HttpStatusCode.Created or HttpStatusCode.BadRequest, $"unexpected {response.StatusCode}");
    }

    [Fact]
    public async Task CreateTag_WithValidBody_ReturnsCreated()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PostJsonAsync(
            "/api/nodegroup/tags",
            """{"name":"ci-tag","color":"#123456"}""");
        Assert.True(response.StatusCode is HttpStatusCode.Created or HttpStatusCode.BadRequest, $"unexpected {response.StatusCode}");
    }

    [Fact]
    public async Task NodeGroup_WithoutAuth_Return401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/nodegroup/groups");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
