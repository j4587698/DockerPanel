using System.Net;
using System.Text.Json;
using Xunit;

namespace DockerPanel.API.Tests;

/// <summary>
/// CertificateProgress 端点测试（原 CertificateProgressController，12 个端点）。
/// </summary>
public sealed class CertificateProgressEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public CertificateProgressEndpointsTests(TestWebApplicationFactory factory)
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

    private static string CreateProgressBody(string certificateId = "cert-test-1") =>
        $$"""
          {
            "certificateId": "{{certificateId}}",
            "applicationName": "integration-test",
            "domains": ["example.com"],
            "provider": "letsencrypt",
            "challengeType": "http-01",
            "metadata": {}
          }
          """;

    [Fact]
    public async Task CreateProgress_ReturnsOkWithProgressId()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PostJsonAsync("/api/certificateprogress/create", CreateProgressBody());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("progressId", out var progressId));
        Assert.False(string.IsNullOrWhiteSpace(progressId.GetString()));
    }

    [Fact]
    public async Task FullLifecycle_Succeeds()
    {
        var api = await CreateAuthorizedClientAsync();

        var create = await api.PostJsonAsync("/api/certificateprogress/create", CreateProgressBody());
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        using var doc = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var progressId = doc.RootElement.GetProperty("progressId").GetString()!;

        var byId = await api.GetAsync($"/api/certificateprogress/{progressId}");
        Assert.Equal(HttpStatusCode.OK, byId.StatusCode);

        var byCert = await api.GetAsync("/api/certificateprogress/by-certificate/cert-test-1");
        Assert.Equal(HttpStatusCode.OK, byCert.StatusCode);

        var all = await api.GetAsync("/api/certificateprogress");
        Assert.Equal(HttpStatusCode.OK, all.StatusCode);

        var step = await api.PutJsonAsync(
            $"/api/certificateprogress/{progressId}/step",
            """{"step":"CreatingOrder","message":"creating order","isCompleted":false}""");
        Assert.Equal(HttpStatusCode.OK, step.StatusCode);

        var completeCurrent = await api.PutJsonAsync(
            $"/api/certificateprogress/{progressId}/complete-current",
            """{"message":"step done"}""");
        Assert.Equal(HttpStatusCode.OK, completeCurrent.StatusCode);

        var error = await api.PostJsonAsync(
            $"/api/certificateprogress/{progressId}/error",
            """{"error":"recoverable error"}""");
        Assert.Equal(HttpStatusCode.OK, error.StatusCode);

        var warning = await api.PostJsonAsync(
            $"/api/certificateprogress/{progressId}/warning",
            """{"warning":"test warning"}""");
        Assert.Equal(HttpStatusCode.OK, warning.StatusCode);

        var complete = await api.PutJsonAsync($"/api/certificateprogress/{progressId}/complete", null);
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);

        var delete = await api.DeleteAsync($"/api/certificateprogress/{progressId}");
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
    }

    [Fact]
    public async Task GetProgress_UnknownId_ReturnsOkWithEmptyBody()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"/api/certificateprogress/{UnknownId}");

        // TypedResults.Ok(null) 序列化为空 body
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task MarkAsFailed_WithBody_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();

        var create = await api.PostJsonAsync("/api/certificateprogress/create", CreateProgressBody("cert-fail-1"));
        using var doc = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var progressId = doc.RootElement.GetProperty("progressId").GetString()!;

        var fail = await api.PutJsonAsync(
            $"/api/certificateprogress/{progressId}/fail",
            """{"errorMessage":"simulated failure"}""");
        Assert.Equal(HttpStatusCode.OK, fail.StatusCode);
    }

    [Fact]
    public async Task CleanupExpired_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PostJsonAsync("/api/certificateprogress/cleanup", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
