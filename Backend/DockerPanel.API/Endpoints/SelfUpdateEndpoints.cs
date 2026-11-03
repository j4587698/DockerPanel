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
    ///
    /// 升级是分钟级操作（拉镜像 + 重建容器），接口本身只做秒级前置校验，
    /// 通过后立即返回 202 并把耗时部分交给后台任务，前端靠
    /// <c>GET upgrade/status</c> 轮询 + SignalR 进度跟踪。
    /// 这样用户刷新页面、代理超时都不会再中断升级。
    /// </summary>
    public static class SelfUpdateEndpoints
    {
        public static IEndpointRouteBuilder MapSelfUpdateEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/system/update");

            group.MapGet("check", CheckUpdateAsync);
            group.MapPost("upgrade", ExecuteUpgradeAsync)
                .RequireAuthorization(new AuthorizeAttribute { Roles = AuthRoles.Admin });
            group.MapGet("upgrade/status", GetUpgradeStatus)
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
            ISelfUpgradeJobManager jobManager,
            SelfUpgradeRequest request,
            CancellationToken ct = default)
        {
            try
            {
                // 已有任务在跑：返回 200 + 现有状态。语义上不是错误，前端直接复用该状态恢复进度展示，
                // 避免升级按钮被重复点击时弹出一个无意义的失败提示。
                var running = jobManager.GetStatus();
                if (running.IsRunning)
                {
                    return TypedResults.Json(running, WebJsonContext.Default.SelfUpgradeJobStatus);
                }

                // 前置校验放在请求内完成，失败能立刻告知用户，不会留下半完成的升级
                var plan = await selfUpdateService.PrepareSelfUpgradeAsync(request, ct);

                if (!jobManager.TryStart(plan, out var status))
                {
                    return TypedResults.Json(status, WebJsonContext.Default.SelfUpgradeJobStatus);
                }

                return TypedResults.Json(status, WebJsonContext.Default.SelfUpgradeJobStatus, statusCode: 202);
            }
            catch (Exception ex)
            {
                return TypedResults.Json(new ApiErrorResponse { Error = "执行升级失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        public static IResult GetUpgradeStatus(ISelfUpgradeJobManager jobManager)
        {
            return TypedResults.Json(jobManager.GetStatus(), WebJsonContext.Default.SelfUpgradeJobStatus);
        }
    }
}
