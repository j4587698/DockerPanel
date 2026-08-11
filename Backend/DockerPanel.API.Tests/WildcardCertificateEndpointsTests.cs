using System.Net;
using Xunit;
using System.Text.Json;

namespace DockerPanel.API.Tests;

/// <summary>
/// WildcardCertificate endpoint tests (former WildcardCertificateController, 16 endpoints).
/// </summary>
public sealed class WildcardCertificateEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public WildcardCertificateEndpointsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<TestApiClient> CreateAuthorizedClientAsync()
    {
        var api = new TestApiClient(_factory.CreateClient());
        await api.EnsureSetupAsync();
        return api;
    }

    private const string UnknownId = "000000000000000000000000";

    [Fact]
    public async Task GetWildcardCertificates_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/wildcardcertificate");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("application/json", response.Content.Headers.ContentType?.ToString());
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task GetSupportedDnsProviders_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/wildcardcertificate/dns-providers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetStatistics_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/wildcardcertificate/statistics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetDetails_UnknownId_Returns404()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/wildcardcertificate/{UnknownId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CheckStatus_UnknownId_ReturnsOkWithInvalidStatus()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/wildcardcertificate/{UnknownId}/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"invalid\"", json);
    }

    [Fact]
    public async Task Validate_UnknownId_ReturnsOkWithInvalidResult()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PostJsonAsync($"/api/wildcardcertificate/{UnknownId}/validate", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("isValid", json);
    }

    [Fact]
    public async Task Renew_UnknownId_Returns400()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PostJsonAsync($"/api/wildcardcertificate/{UnknownId}/renew", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_UnknownId_Returns400()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.DeleteAsync($"/api/wildcardcertificate/{UnknownId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Export_UnknownId_Returns400()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/wildcardcertificate/{UnknownId}/export");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Batch_ValidBody_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PostJsonAsync(
            "/api/wildcardcertificate/batch",
            """{"operation":"validate","certificateIds":[]}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
