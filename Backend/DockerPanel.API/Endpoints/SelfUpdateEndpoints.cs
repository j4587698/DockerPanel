using System;
using System.Threading;
using System.Threading.Tasks;
using DockerPanel.API.Models;
using DockerPanel.API.Serialization;
using DockerPanel.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// DockerPanel 自身升级端点。
    /// </summary>
    public static class SelfUpdateEndpoints
    {
        public static IEndpointRouteBuilder MapSelfUpdateEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/system/update");

            group.MapGet("check", CheckUpdateAsync);
            group.MapPost("upgrade", ExecuteUpgradeAsync)
                .RequireAuthorization(new AuthorizeAttribute { Roles = AuthRoles.Admin });

            return app;
        }

        public static async Task<IResult> CheckUpdateAsync(
            ISelfUpdateService selfUpdateService,
            bool? force = null,
            CancellationToken ct = default)
        {
            try
            {
                var result = await selfUpdateService.CheckUpdateAsync(force ?? false, ct);
                return TypedResults.Json(result, WebJsonContext.Default.SelfUpdateCheckResult);
            }
            catch (Exception ex)
            {
                return TypedResults.Json(new ApiErrorResponse { Error = "检查更新失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        public static async Task<IResult> ExecuteUpgradeAsync(
            ISelfUpdateService selfUpdateService,
            SelfUpgradeRequest request,
            CancellationToken ct = default)
        {
            try
            {
                var result = await selfUpdateService.ExecuteSelfUpgradeAsync(request, ct);
                return TypedResults.Json(result, WebJsonContext.Default.SelfUpgradeResponse);
            }
            catch (Exception ex)
            {
                return TypedResults.Json(new ApiErrorResponse { Error = "执行升级失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }
    }
}
