using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace DockerPanel.API.Tests;

public sealed class SelfUpdateEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SelfUpdateEndpointsTests(TestWebApplicationFactory factory) => _factory = factory;

    private async Task<TestApiClient> CreateAuthorizedClientAsync()
    {
        var api = new TestApiClient(_factory.CreateClient());
        await api.EnsureSetupAsync();
        return api;
    }

    [Fact]
    public async Task CheckUpdate_AsAdmin_ReturnsOk()
    {
        var api = await CreateAuthorizedClientAsync();
        var response = await api.GetAsync("/api/system/update/check");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("currentVersion", out var currentVer));
        Assert.False(string.IsNullOrWhiteSpace(currentVer.GetString()));
    }

    [Fact]
    public async Task ExecuteUpgrade_WithoutAuth_Fails()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/system/update/upgrade", null);
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden);
    }
}
