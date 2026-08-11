using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace DockerPanel.API.Tests;

/// <summary>
/// Auth / CSRF / authorization boundary tests.
/// </summary>
public sealed class AuthAndSecurityTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AuthAndSecurityTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Status_WithoutUsers_ReportsRequiresSetup()
    {
        // 使用独立 factory（独立数据目录），确保数据库处于"无用户"状态
        using var freshFactory = new TestWebApplicationFactory();
        var response = await freshFactory.CreateClient().GetAsync("/api/auth/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"hasUsers\":false", json);
        Assert.Contains("\"requiresSetup\":true", json);
    }

    [Fact]
    public async Task Setup_WithoutApiHeader_Returns403CsrfInvalid()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/setup")
        {
            Content = JsonContent.Create(new { username = "admin", password = "Admin@12345" })
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"code\":\"CSRF_INVALID\"", json);
    }

    [Fact]
    public async Task Setup_ThenLogin_WithValidCredentials_Succeeds()
    {
        var api = new TestApiClient(_factory.CreateClient());
        await api.EnsureSetupAsync();

        var login = await api.PostJsonAsync("/api/auth/login", """{"username":"admin","password":"Admin@12345"}""");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains("\"accessToken\"", await login.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var api = new TestApiClient(_factory.CreateClient());
        await api.EnsureSetupAsync();

        var login = await api.PostJsonAsync("/api/auth/login", """{"username":"admin","password":"WrongPass123"}""");
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Login_WithoutApiHeader_Returns403CsrfInvalid()
    {
        await new TestApiClient(_factory.CreateClient()).EnsureSetupAsync();

        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { username = "admin", password = "Admin@12345" })
        };

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/certificatemanagement")]
    [InlineData("/api/certificateprogress")]
    [InlineData("/api/wildcardcertificate")]
    [InlineData("/api/challengevalidation/stats")]
    [InlineData("/api/acme")]
    public async Task ProtectedEndpoints_WithoutAuth_Return401(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

