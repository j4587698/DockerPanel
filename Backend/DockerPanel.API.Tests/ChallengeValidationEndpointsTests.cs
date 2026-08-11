using System.Net;
using Xunit;

namespace DockerPanel.API.Tests;

/// <summary>
/// ChallengeValidation endpoint tests (former ChallengeValidationController, 14 endpoints, group-level RequireAuthorization).
/// </summary>
public sealed class ChallengeValidationEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ChallengeValidationEndpointsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<TestApiClient> CreateAuthorizedClientAsync()
    {
        var api = new TestApiClient(_factory.CreateClient());
        await api.EnsureSetupAsync();
        return api;
    }

    private const string Base = "/api/challengevalidation";

    [Fact]
    public async Task GetStats_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"{Base}/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSupportedDnsProviders_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"{Base}/dns-providers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetChallengeStatus_UnknownId_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync($"{Base}/status/unknown-challenge");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ConfigureAndValidate_HttpChallenge_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var body = """{"domain":"example.com","token":"tok-http-1","keyAuthorization":"key-auth"}""";

        var configure = await api.PostJsonAsync($"{Base}/http/configure", body);
        Assert.Equal(HttpStatusCode.OK, configure.StatusCode);

        var validate = await api.PostJsonAsync($"{Base}/http/validate", body);
        Assert.Equal(HttpStatusCode.OK, validate.StatusCode);
    }

    [Fact]
    public async Task ConfigureAndValidate_DnsChallenge_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var body = """{"domain":"example.com","dnsProvider":"cloudflare","token":"tok-dns-1","keyAuthorization":"key-auth","credentials":{}}""";

        var configure = await api.PostJsonAsync($"{Base}/dns/configure", body);
        Assert.Equal(HttpStatusCode.OK, configure.StatusCode);

        var validate = await api.PostJsonAsync($"{Base}/dns/validate", body);
        Assert.Equal(HttpStatusCode.OK, validate.StatusCode);
    }

    [Fact]
    public async Task ConfigureAndValidate_TlsAlpnChallenge_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var body = """{"domain":"example.com","token":"tok-tls-1","keyAuthorization":"key-auth"}""";

        var configure = await api.PostJsonAsync($"{Base}/tls-alpn/configure", body);
        Assert.Equal(HttpStatusCode.OK, configure.StatusCode);

        var validate = await api.PostJsonAsync($"{Base}/tls-alpn/validate", body);
        Assert.Equal(HttpStatusCode.OK, validate.StatusCode);
    }

    [Fact]
    public async Task Cleanup_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PostJsonAsync(
            $"{Base}/cleanup",
            """{"domain":"example.com","challengeType":"http-01","token":"tok-1","keyAuthorization":"key-auth"}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AutoConfigure_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PostJsonAsync(
            $"{Base}/auto-configure",
            """{"domain":"example.com","challengeType":"http-01","token":"tok-auto-1","keyAuthorization":"key-auth","preferredChallengeTypes":["http-01"]}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task BatchCleanup_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.PostJsonAsync(
            $"{Base}/batch-cleanup",
            """{"challenges":[]}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Monitor_UnknownChallenge_ReturnsEventStream()
    {
        var api = await CreateAuthorizedClientAsync();

        using var response = await api.GetSseAsync($"{Base}/monitor/unknown-challenge", TimeSpan.FromSeconds(10));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
    }
}

