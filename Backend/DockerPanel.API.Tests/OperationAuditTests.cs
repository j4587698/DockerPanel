using System.Net;
using System.Text.Json;
using Xunit;

namespace DockerPanel.API.Tests;

/// <summary>
/// Verifies the Minimal API operation-audit middleware (successor of the removed MVC global filter):
/// write operations are recorded and visible via /api/audit/logs, read-only GETs are not.
/// </summary>
public sealed class OperationAuditTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public OperationAuditTests(TestWebApplicationFactory factory) => _factory = factory;

    private async Task<TestApiClient> CreateAuthorizedClientAsync()
    {
        var api = new TestApiClient(_factory.CreateClient());
        await api.EnsureSetupAsync();
        return api;
    }

    private static JsonElement FirstMatching(JsonElement items, Func<JsonElement, bool> predicate)
    {
        foreach (var item in items.EnumerateArray())
        {
            if (predicate(item)) return item;
        }

        return default;
    }

    [Fact]
    public async Task WriteOperation_IsRecordedInAuditLog()
    {
        var api = await CreateAuthorizedClientAsync();

        var create = await api.PostJsonAsync(
            "/api/templates/",
            """{"name":"audit-check","description":"x","content":"yaml"}""");
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var logs = await api.GetAsync("/api/audit/logs");
        Assert.Equal(HttpStatusCode.OK, logs.StatusCode);
        using var doc = JsonDocument.Parse(await logs.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("items", out var items));
        Assert.True(items.ValueKind == JsonValueKind.Array && items.GetArrayLength() > 0, "expected at least one audit record");

        var record = FirstMatching(items, i =>
            i.GetProperty("path").GetString()?.Contains("/api/templates") == true &&
            i.GetProperty("method").GetString() == "POST");
        Assert.True(record.ValueKind == JsonValueKind.Object, "expected audit record for POST /api/templates");
        Assert.Equal("create", record.GetProperty("operationType").GetString());
        Assert.Equal("success", record.GetProperty("status").GetString());
    }

    [Fact]
    public async Task FailedOperation_IsRecordedAsFailed()
    {
        var api = await CreateAuthorizedClientAsync();

        var response = await api.PostJsonAsync("/api/templates/000000000000000000000000/duplicate", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var logs = await api.GetAsync("/api/audit/logs");
        using var doc = JsonDocument.Parse(await logs.Content.ReadAsStringAsync());
        var items = doc.RootElement.GetProperty("items");
        var record = FirstMatching(items, i =>
            i.GetProperty("path").GetString()?.Contains("/api/templates/000000000000000000000000/duplicate") == true);
        Assert.True(record.ValueKind == JsonValueKind.Object, "expected audit record for failed POST");
        Assert.Equal(404, record.GetProperty("statusCode").GetInt32());
        Assert.Equal("failed", record.GetProperty("status").GetString());
    }

    [Fact]
    public async Task SensitiveQueryParameter_IsMasked()
    {
        var api = await CreateAuthorizedClientAsync();

        await api.PostJsonAsync("/api/nodes?token=supersecret", "{}");

        var logs = await api.GetAsync("/api/audit/logs");
        using var doc = JsonDocument.Parse(await logs.Content.ReadAsStringAsync());
        var items = doc.RootElement.GetProperty("items");
        var record = FirstMatching(items, i =>
            i.GetProperty("path").GetString()?.Contains("/api/nodes") == true);
        Assert.True(record.ValueKind == JsonValueKind.Object, "expected audit record for POST /api/nodes");
        var query = record.GetProperty("query");
        Assert.Equal("***", query.GetProperty("token").GetString());
    }

    [Fact]
    public async Task PlainGet_IsNotAudited()
    {
        var api = await CreateAuthorizedClientAsync();

        var response = await api.GetAsync("/api/templates/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var logs = await api.GetAsync("/api/audit/logs");
        using var doc = JsonDocument.Parse(await logs.Content.ReadAsStringAsync());
        var items = doc.RootElement.GetProperty("items");
        var record = FirstMatching(items, i =>
            i.GetProperty("path").GetString() == "/api/templates/" && i.GetProperty("method").GetString() == "GET");
        Assert.True(record.ValueKind == JsonValueKind.Undefined, "plain GET should not be audited");
    }
}
