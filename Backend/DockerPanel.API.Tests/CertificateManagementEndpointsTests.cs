using System.Net;
using Xunit;
using System.Text.Json;

namespace DockerPanel.API.Tests;

/// <summary>
/// CertificateManagement endpoint tests (former CertificateManagementController, 17 endpoints).
/// </summary>
public sealed class CertificateManagementEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public CertificateManagementEndpointsTests(TestWebApplicationFactory factory)
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
    public async Task GetCertificates_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/certificatemanagement");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("application/json", response.Content.Headers.ContentType?.ToString());
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("certificates", out _));
    }

    [Fact]
    public async Task GetExpiringCertificates_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/certificatemanagement/expiring");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetStatistics_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/certificatemanagement/statistics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SearchCertificates_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/certificatemanagement/search");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSummary_ReturnsOkWithExpectedShape()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/certificatemanagement/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("totalCertificates", out _));
        Assert.True(root.TryGetProperty("status", out _));
        Assert.True(root.TryGetProperty("upcomingRenewals", out _));
    }

    [Fact]
    public async Task GetDetails_UnknownId_Returns404()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/certificatemanagement/{UnknownId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Renew_UnknownId_Returns400()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PostJsonAsync($"/api/certificatemanagement/{UnknownId}/renew", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EnableAutoRenewal_WithValidBody_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PostJsonAsync(
            $"/api/certificatemanagement/{UnknownId}/auto-renewal/enable",
            $$"""{"certificateId":"{{UnknownId}}","accountId":"acct-test","autoRenewalEnabled":true,"renewalDaysBeforeExpiry":30}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DisableAutoRenewal_UnknownId_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PostJsonAsync($"/api/certificatemanagement/{UnknownId}/auto-renewal/disable", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Delete_UnknownId_Returns404()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.DeleteAsync($"/api/certificatemanagement/{UnknownId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Export_UnknownId_Returns400()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/certificatemanagement/{UnknownId}/export");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Validate_UnknownId_ReturnsOkWithInvalidResult()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PostJsonAsync($"/api/certificatemanagement/{UnknownId}/validate", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("isValid", json);
    }

    [Fact]
    public async Task GetUsageStatistics_UnknownId_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/certificatemanagement/{UnknownId}/statistics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetOperationHistory_UnknownId_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/certificatemanagement/{UnknownId}/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Download_UnknownId_Returns404()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/certificatemanagement/{UnknownId}/download");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Batch_ValidBody_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PostJsonAsync(
            "/api/certificatemanagement/batch",
            """{"operation":"validate","certificateIds":[]}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
