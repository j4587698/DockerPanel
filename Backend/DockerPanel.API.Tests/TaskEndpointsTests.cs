using System.Net;
using Xunit;

namespace DockerPanel.API.Tests;

/// <summary>
/// Background task endpoint tests (api/tasks, 5 routes).
/// </summary>
public sealed class TaskEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public TaskEndpointsTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetTasks_AsAdmin_ReturnsOk()
    {
        var api = new TestApiClient(_factory.CreateClient());
        await api.EnsureSetupAsync();
        var response = await api.GetAsync("/api/tasks/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetActiveTasks_AsAdmin_ReturnsOk()
    {
        var api = new TestApiClient(_factory.CreateClient());
        await api.EnsureSetupAsync();
        var response = await api.GetAsync("/api/tasks/active");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTask_UnknownId_Returns404()
    {
        var api = new TestApiClient(_factory.CreateClient());
        await api.EnsureSetupAsync();
        var response = await api.GetAsync("/api/tasks/nonexistent-id");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RemoveTask_UnknownId_ReturnsOk()
    {
        var api = new TestApiClient(_factory.CreateClient());
        await api.EnsureSetupAsync();
        var response = await api.DeleteAsync("/api/tasks/nonexistent-id");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ClearCompleted_AsAdmin_ReturnsOk()
    {
        var api = new TestApiClient(_factory.CreateClient());
        await api.EnsureSetupAsync();
        var response = await api.PostJsonAsync("/api/tasks/clear-completed", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Tasks_WithoutAuth_Return401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/tasks/");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
