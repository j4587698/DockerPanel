using DockerPanel.API.Models;
using DockerPanel.API.Services;
using Microsoft.AspNetCore.Http.HttpResults;

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

        private static async Task<Ok<OperationAuditLogPage>> GetLogs(
            IOperationAuditService auditService,
            string? search,
            string? operationType,
            string? resourceType,
            string? resourceId,
            string? status,
            string? nodeId,
            DateTime? startDate,
            DateTime? endDate,
            int page = 1,
            int pageSize = 50)
        {
            var filter = new OperationAuditLogFilter
            {
                Search = search,
                OperationType = operationType,
                ResourceType = resourceType,
                ResourceId = resourceId,
                Status = status,
                NodeId = nodeId,
                StartDate = startDate,
                EndDate = endDate,
                Page = page,
                PageSize = pageSize
            };

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