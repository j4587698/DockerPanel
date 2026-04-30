using DockerPanel.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DockerPanel.API.Services;

/// <summary>
/// 写操作权限过滤器。Viewer 只能读取，Admin/Operator 可以执行日常写操作。
/// 更敏感的接口仍通过 [Authorize(Roles = AuthRoles.Admin)] 单独限制。
/// </summary>
public sealed class RoleWriteAccessFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (IsReadOnlyMethod(request.Method) || IsAuthEndpoint(request.Path))
        {
            await next();
            return;
        }

        var user = context.HttpContext.User;
        if (user.IsInRole(AuthRoles.Admin) || user.IsInRole(AuthRoles.Operator))
        {
            await next();
            return;
        }

        context.Result = new ObjectResult(new { message = "当前角色没有执行写操作的权限。" })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }

    private static bool IsReadOnlyMethod(string method)
    {
        return HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);
    }

    private static bool IsAuthEndpoint(PathString path)
    {
        return path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase);
    }
}
