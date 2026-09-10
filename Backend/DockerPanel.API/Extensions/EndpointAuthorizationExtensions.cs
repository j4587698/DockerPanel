using DockerPanel.API.Models;
using Microsoft.AspNetCore.Authorization;

namespace DockerPanel.API.Extensions;

/// <summary>
/// Minimal API 授权约定扩展。
/// MVC 控制器由全局 AuthorizeFilter / RoleWriteAccessFilter 保护，但这两个过滤器对 Minimal API 不生效，
/// 这里用等价的端点元数据补齐同样的语义。
/// </summary>
public static class EndpointAuthorizationExtensions
{
    /// <summary>
    /// 写操作权限：等价于 RoleWriteAccessFilter，仅 Admin/Operator 可执行（Viewer 只读）。
    /// </summary>
    public static TBuilder RequireWriteAccess<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
        => builder.RequireAuthorization(new AuthorizeAttribute { Roles = AuthRoles.AdminOrOperator });

    /// <summary>
    /// 仅 Admin 可访问（用于私钥导出等高危端点）。
    /// </summary>
    public static TBuilder RequireAdmin<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
        => builder.RequireAuthorization(new AuthorizeAttribute { Roles = AuthRoles.Admin });
}
