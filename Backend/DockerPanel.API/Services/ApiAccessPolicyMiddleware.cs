using DockerPanel.API.Endpoints;
using DockerPanel.API.Models;
using DockerPanel.API.Serialization;
using Microsoft.AspNetCore.Authorization;

namespace DockerPanel.API.Services;

/// <summary>
/// Minimal API 统一访问策略中间件（对应 MVC 侧的全局 AuthorizeFilter + RoleWriteAccessFilter）。
/// 必须在 UseAuthentication/UseAuthorization 之后注册。
/// 规则：
///  1. 显式声明了授权元数据的端点（AllowAnonymous / RequireAuthorization）交由授权中间件处理；
///  2. /api/auth/* 认证流程端点放行（登出即使令牌失效也应可用）；
///  3. 其余端点一律要求已登录，否则 401（与 MVC 全局 AuthorizeFilter 语义一致）；
///  4. 已登录用户的写操作（非 GET/HEAD/OPTIONS）要求 Admin/Operator 角色，否则 403（Viewer 只读）。
/// 不使用 AddAuthorization 的 FallbackPolicy：会波及 YARP 代理端点、健康检查、
/// /.well-known 等非 /api 端点；本中间件仅作用于 /api 请求且跳过 YARP 代理路由。
/// </summary>
public sealed class ApiAccessPolicyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiAccessPolicyMiddleware> _logger;

    public ApiAccessPolicyMiddleware(RequestDelegate next, ILogger<ApiAccessPolicyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();

        // YARP 代理请求不适用本策略（被代理的容器应用有自己的鉴权）
        bool isYarp = endpoint?.Metadata?.GetMetadata<Yarp.ReverseProxy.Model.RouteModel>() != null;
        if (!isYarp && context.Request.Path.StartsWithSegments("/api"))
        {
            // 显式声明授权元数据（AllowAnonymous / RequireAuthorization）→ 授权中间件已处理
            if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is null &&
                endpoint?.Metadata.GetMetadata<IAuthorizeData>() is null &&
                // 认证流程端点保持匿名（与 RoleWriteAccessFilter.IsAuthEndpoint 一致）
                !context.Request.Path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase))
            {
                if (context.User.Identity?.IsAuthenticated != true)
                {
                    _logger.LogWarning("拒绝匿名访问: {Method} {Path}", context.Request.Method, context.Request.Path);
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new ApiErrorResponse
                    {
                        Code = "UNAUTHORIZED",
                        Message = "未登录或登录已过期，请重新登录。"
                    }, WebJsonContext.Default.ApiErrorResponse);
                    return;
                }

                var method = context.Request.Method;
                if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method) && !HttpMethods.IsOptions(method))
                {
                    if (!context.User.IsInRole(AuthRoles.Admin) && !context.User.IsInRole(AuthRoles.Operator))
                    {
                        _logger.LogWarning("拒绝无写权限角色: {Role} {Method} {Path}",
                            context.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "unknown",
                            context.Request.Method, context.Request.Path);
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsJsonAsync(new ApiErrorResponse
                        {
                            Code = "FORBIDDEN",
                            Message = "当前角色没有执行写操作的权限。"
                        }, WebJsonContext.Default.ApiErrorResponse);
                        return;
                    }
                }
            }
        }

        await _next(context);
    }
}
