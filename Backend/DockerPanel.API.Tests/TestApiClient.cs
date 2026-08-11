using System.Net;
using System.Text;

namespace DockerPanel.API.Tests;

/// <summary>
/// 集成测试 HTTP 客户端封装：
/// - 自动携带 X-DockerPanel-Api 头（CSRF 保护要求所有非 GET 请求携带）；
/// - 通过 WebApplicationFactory 的 cookie handler 自动保存/回发 jwt_token cookie；
/// - 首次调用 EnsureSetupAsync 创建管理员（admin/Admin@12345）。
/// </summary>
public sealed class TestApiClient
{
    private const string ApiHeaderName = "X-DockerPanel-Api";

    private readonly HttpClient _http;
    private readonly SemaphoreSlim _setupLock = new(1, 1);
    private bool _setupCompleted;

    public TestApiClient(HttpClient http)
    {
        _http = http;
    }

    public HttpClient InnerClient => _http;

    /// <summary>
    /// 确保已创建管理员用户（幂等）。setup 成功后 jwt_token cookie 自动存入 handler。
    /// </summary>
    public async Task EnsureSetupAsync()
    {
        if (_setupCompleted)
        {
            return;
        }

        await _setupLock.WaitAsync();
        try
        {
            if (_setupCompleted)
            {
                return;
            }

            var response = await PostJsonAsync("/api/auth/setup", """{"username":"admin","password":"Admin@12345"}""");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                _setupCompleted = true;
                return;
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                // 数据库已有用户（同 factory 其他 client 已初始化）：直接登录获取 cookie
                var login = await PostJsonAsync("/api/auth/login", """{"username":"admin","password":"Admin@12345"}""");
                if (login.StatusCode != HttpStatusCode.OK)
                {
                    throw new InvalidOperationException(
                        $"管理员登录失败: HTTP {(int)login.StatusCode} {await login.Content.ReadAsStringAsync()}");
                }

                _setupCompleted = true;
                return;
            }

            throw new InvalidOperationException(
                $"管理员初始化失败: HTTP {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        }
        finally
        {
            _setupLock.Release();
        }
    }

    public Task<HttpResponseMessage> GetAsync(string path)
        => SendAsync(HttpMethod.Get, path, null);

    public Task<HttpResponseMessage> DeleteAsync(string path)
        => SendAsync(HttpMethod.Delete, path, null);

    public Task<HttpResponseMessage> PostJsonAsync(string path, string? json)
        => SendAsync(HttpMethod.Post, path, json);

    public Task<HttpResponseMessage> PutJsonAsync(string path, string? json)
        => SendAsync(HttpMethod.Put, path, json);

    /// <summary>
    /// 发起 SSE 请求：只读取响应头即返回，调用方负责释放（连接保持打开）。
    /// </summary>
    public async Task<HttpResponseMessage> GetSseAsync(string path, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        return await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? json)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation(ApiHeaderName, "1");
        if (json != null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return await _http.SendAsync(request);
    }
}
