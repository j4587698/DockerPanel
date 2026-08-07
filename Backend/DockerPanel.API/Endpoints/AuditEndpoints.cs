using DockerPanel.API.Models;
using DockerPanel.API.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// 操作审计日志 Minimal API 端点（原 AuditController）。
    /// </summary>
    public static class AuditEndpoints
    {
        /// <summary>
        /// 映射审计日志相关路由。
        /// </summary>
        public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/audit");

            group.MapGet("/logs", GetLogs);
            group.MapGet("/logs/{id}", GetLog);

            return app;
        }

        private static async Task<Ok<OperationAuditLogPage>> GetLogs([FromQuery] OperationAuditLogFilter filter, IOperationAuditService auditService)
        {
            var result = await auditService.GetLogsAsync(filter);
            return TypedResults.Ok(result);
        }

        private static async Task<Results<Ok<OperationAuditLog>, NotFound<ApiErrorResponse>>> GetLog(string id, IOperationAuditService auditService)
        {
            var log = await auditService.GetLogAsync(id);
            return log == null
                ? TypedResults.NotFound(new ApiErrorResponse { Error = "审计日志不存在", Message = id })
                : TypedResults.Ok(log);
        }
    }
}