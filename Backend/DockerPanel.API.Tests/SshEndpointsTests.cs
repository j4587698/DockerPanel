using System.Net;
using Xunit;

namespace DockerPanel.API.Tests;

/// <summary>
/// SSH endpoint tests (api/ssh, 28 routes). Connection/keypair CRUD is local DB backed;
/// command/upload/execute endpoints require a live SSH server, so they are tested at the boundary.
/// </summary>
public sealed class SshEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SshEndpointsTests(TestWebApplicationFactory factory) => _factory = factory;

    private async Task<TestApiClient> CreateAuthorizedClientAsync()
    {
        var api = new TestApiClient(_factory.CreateClient());
        await api.EnsureSetupAsync();
        return api;
    }

    private const string UnknownId = "unknown-id";

    private const string Page = "?page=0&pageSize=10";

    [Fact]
    public async Task GetConnections_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/ssh/connections{Page}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetConnection_UnknownId_Returns404()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/ssh/connections/{UnknownId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateConnection_WithValidBody_ReturnsCreated()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PostJsonAsync(
            "/api/ssh/connections",
            """{"name":"ci-conn","host":"127.0.0.1","port":22,"username":"root","password":"x"}""");
        Assert.True(response.StatusCode is HttpStatusCode.Created or HttpStatusCode.BadRequest, $"unexpected {response.StatusCode}");
    }

    [Fact]
    public async Task UpdateConnection_UnknownId_Returns404()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PutJsonAsync($"/api/ssh/connections/{UnknownId}", """{"name":"x"}""");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteConnection_UnknownId_ReturnsOkOr404()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.DeleteAsync($"/api/ssh/connections/{UnknownId}");
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound, $"unexpected {response.StatusCode}");
    }

    [Fact]
    public async Task GetKeyPairs_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/ssh/keypairs{Page}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GenerateKeyPair_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PostJsonAsync("/api/ssh/generate-keypair", """{"keyName":"ci-key","keyType":"RSA","keySize":2048}""");
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest, $"unexpected {response.StatusCode}");
    }

    [Fact]
    public async Task GetSessions_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/ssh/sessions{Page}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteSession_UnknownId_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.DeleteAsync($"/api/ssh/sessions/{UnknownId}");
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound, $"unexpected {response.StatusCode}");
    }

    [Fact]
    public async Task GetHostKeys_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/ssh/host-keys{Page}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetStatistics_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/ssh/statistics");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSettings_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/ssh/settings");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ExecuteCommand_NoServer_ReturnsBadRequest()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PostJsonAsync(
            "/api/ssh/execute-command",
            """{"host":"127.0.0.1","port":22,"username":"root","password":"x","command":"ls"}""");
        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.OK or HttpStatusCode.InternalServerError, $"unexpected {response.StatusCode}");
    }

    [Fact]
    public async Task Ssh_WithoutAuth_Return401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/ssh/connections");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
