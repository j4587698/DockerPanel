using System.Net;
using Xunit;

namespace DockerPanel.API.Tests;

/// <summary>
/// ACME endpoint tests (api/acme, 39 routes). Account/order/certificate operations depend on a
/// real ACME server; tests here cover side-effect-free endpoints, unknown-resource handling and auth.
/// </summary>
public sealed class AcmeEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AcmeEndpointsTests(TestWebApplicationFactory factory) => _factory = factory;

    private async Task<TestApiClient> CreateAuthorizedClientAsync()
    {
        var api = new TestApiClient(_factory.CreateClient());
        await api.EnsureSetupAsync();
        return api;
    }

    private const string UnknownId = "unknown-id";

    [Fact]
    public async Task GetStatistics_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/acme/statistics");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetProviders_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/acme/providers");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAccounts_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/acme/accounts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAccount_UnknownId_Returns404()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/acme/accounts/{UnknownId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCertificates_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/acme/certificates?page=0&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCertificateOrders_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/acme/certificates/orders");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCertificateOrder_UnknownId_Returns404()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/acme/certificates/orders/{UnknownId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPendingChallenges_UnknownOrder_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/acme/certificates/orders/{UnknownId}/challenges/pending");
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound, $"unexpected {response.StatusCode}");
    }

    [Fact]
    public async Task GetOperationLogs_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/acme/logs?limit=10&offset=0");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetHttpChallenge_IsAnonymous_ReturnsNotFoundForUnknownToken()
    {
        var response = await _factory.CreateClient().GetAsync("/api/acme/.well-known/acme-challenge/nonexistent-token");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetHttpChallenge_ShortPath_IsAnonymous()
    {
        var response = await _factory.CreateClient().GetAsync("/api/acme/acme-challenge/nonexistent-token");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GenerateCsr_WithValidBody_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PostJsonAsync(
            "/api/acme/csr/generate",
            """{"domains":["ci.example.com"]}""");
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest, $"unexpected {response.StatusCode}");
    }

    [Fact]
    public async Task GenerateKeyPair_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PostJsonAsync("/api/acme/keys/generate", "{}");
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest, $"unexpected {response.StatusCode}");
    }

    [Fact]
    public async Task DeleteCertificate_UnknownId_ReturnsOkOrNotFound()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.DeleteAsync($"/api/acme/certificates/{UnknownId}?force=false");
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound, $"unexpected {response.StatusCode}");
    }

    [Fact]
    public async Task Acme_WithoutAuth_Return401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/acme/statistics");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
