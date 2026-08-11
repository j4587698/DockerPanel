using System.Net;
using Xunit;

namespace DockerPanel.API.Tests;

/// <summary>
/// Template endpoint tests (api/templates, 8 routes).
/// </summary>
public sealed class TemplateEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public TemplateEndpointsTests(TestWebApplicationFactory factory) => _factory = factory;

    private async Task<TestApiClient> CreateAuthorizedClientAsync()
    {
        var api = new TestApiClient(_factory.CreateClient());
        await api.EnsureSetupAsync();
        return api;
    }

    private const string UnknownId = "000000000000000000000000";

    [Fact]
    public async Task GetTemplates_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/templates/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTemplate_UnknownId_Returns404()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/templates/{UnknownId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateTemplate_WithValidBody_CreatesAndCanFetch()
    {
        var api = await CreateAuthorizedClientAsync();
        var create = await api.PostJsonAsync(
            "/api/templates/",
            """{"name":"ci-test-template","description":"created by integration test","content":"version: \"3\""}""");
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var location = create.Headers.Location?.ToString();
        Assert.False(string.IsNullOrEmpty(location), "expected Location header");
        var fetch = await api.GetAsync(location);
        Assert.Equal(HttpStatusCode.OK, fetch.StatusCode);
    }

    [Fact]
    public async Task UpdateTemplate_UnknownId_Returns404()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PutJsonAsync(
            $"/api/templates/{UnknownId}",
            """{"name":"renamed","description":"x","content":"yaml"}""");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTemplate_UnknownId_Returns404()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.DeleteAsync($"/api/templates/{UnknownId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DuplicateTemplate_UnknownId_Returns404()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PostJsonAsync($"/api/templates/{UnknownId}/duplicate", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ExportTemplate_UnknownId_Returns404()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/templates/{UnknownId}/export");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ImportTemplate_WithValidBody_ReturnsCreated()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PostJsonAsync(
            "/api/templates/import",
            """{"name":"ci-imported","description":"x","content":"yaml"}""");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Templates_WithoutAuth_Return401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/templates/");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
