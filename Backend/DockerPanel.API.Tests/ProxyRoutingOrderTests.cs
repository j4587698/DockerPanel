using System.Diagnostics;
using System.Net;
using System.Text;
using DockerPanel.API.Data;
using DockerPanel.API.Models;
using DockerPanel.API.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Yarp.ReverseProxy.Model;

namespace DockerPanel.API.Tests;

/// <summary>
/// 反向代理路由优先级回归测试。
///
/// 背景：面板自身注册了 /api/auth/login、/api/auth/me、/api/auth/change-password 等端点，
/// 与容器应用的 API 路径完全重合。ASP.NET Core 端点排序先比 Order、Order 相同才比路由段
/// 优先级（字面量段 > catch-all），因此默认 Order=0 时面板端点会抢走本应转发给容器的请求，
/// 并被 CSRF 中间件拦成 403 CSRF_INVALID（桌面客户端/服务端调用不会带 X-DockerPanel-Api 头）。
///
/// 修复：把 YARP 端点 Order 提到 -1；ACME HTTP-01 端点提到 -2（否则证书签发会被代理吞掉）。
/// 本测试锁定这两个 Order 值，并验证映射域名的 /api/auth/login 确实进了代理。
/// </summary>
public sealed class ProxyRoutingOrderTests : IClassFixture<TestWebApplicationFactory>
{
    /// <summary>测试用映射域名。</summary>
    private const string MappedHost = "mapped.test";

    /// <summary>上游地址：端口 1 必然拒绝连接，用于确认请求进入了 YARP 而非面板。</summary>
    private const string UnreachableUpstream = "127.0.0.1:1";

    private static readonly SemaphoreSlim SeedLock = new(1, 1);

    private readonly TestWebApplicationFactory _factory;

    public ProxyRoutingOrderTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProxyEndpoints_MustOutrankPanelEndpoints()
    {
        await SeedMappingAsync();

        var endpoints = GetRouteEndpoints();

        var proxyEndpoints = endpoints
            .Where(e => e.Metadata.GetMetadata<RouteModel>() is not null)
            .ToList();

        Assert.NotEmpty(proxyEndpoints);
        Assert.All(proxyEndpoints, e => Assert.Equal(-1, e.Order));

        var acme = Assert.Single(endpoints, e => e.RoutePattern.RawText == "/.well-known/acme-challenge/{token}");
        Assert.Equal(-2, acme.Order);

        // 面板自身端点保持默认优先级，非映射域名的行为不受影响。
        // 注意：MapGroup("api/auth") 生成的原始模板不带前导斜杠。
        var panelLogin = Assert.Single(
            endpoints,
            e => string.Equals(
                (e.RoutePattern.RawText ?? string.Empty).TrimStart('/'),
                "api/auth/login",
                StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, panelLogin.Order);
    }

    [Fact]
    public async Task MappedHost_ApiAuthLogin_IsProxied_InsteadOfBlockedByCsrf()
    {
        await SeedMappingAsync();

        using var client = _factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = new StringContent(
                """{"username":"admin","password":"x"}""",
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Host = MappedHost;
        // 故意不带 X-DockerPanel-Api：这正是容器应用客户端的真实调用方式。

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("CSRF_INVALID", body);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);

        // 上游不可达 → YARP 返回 502，说明请求确实被代理接管，而不是落在面板端点上。
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    /// <summary>
    /// 播种一条域名映射并重载 YARP 配置（幂等，同一测试类内多次调用只生效一次）。
    /// 同时覆盖"动态新增映射后端点约定仍然生效"这一场景。
    /// </summary>
    private async Task SeedMappingAsync()
    {
        await SeedLock.WaitAsync();
        try
        {
            var proxy = _factory.Services.GetRequiredService<IReverseProxyFactory>();
            if (proxy.GetConfig().Routes.Count > 0)
            {
                return;
            }

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TinyDbContext>();
                var collection = db.GetCollection<DomainMapping>("domain_mappings");
                if (!collection.FindAll().Any(m => m.Domain == MappedHost))
                {
                    collection.Insert(new DomainMapping
                    {
                        ContainerId = "test-container",
                        ContainerName = "test-container",
                        Domain = MappedHost,
                        DestinationAddress = UnreachableUpstream,
                        ContainerPort = 1,
                        PathPrefix = "/",
                        Protocol = "http",
                        Enabled = true
                    });
                }
            }

            await proxy.ReloadConfigAsync();
            await WaitForProxyEndpointsAsync();
        }
        finally
        {
            SeedLock.Release();
        }
    }

    /// <summary>等待 YARP 在配置变更后重建端点。</summary>
    private async Task WaitForProxyEndpointsAsync()
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < TimeSpan.FromSeconds(5))
        {
            if (GetRouteEndpoints().Any(e => e.Metadata.GetMetadata<RouteModel>() is not null))
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.Fail("YARP 未在 5 秒内根据新域名映射重建端点。");
    }

    private IReadOnlyList<RouteEndpoint> GetRouteEndpoints()
    {
        return _factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }
}
