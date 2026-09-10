using System.Diagnostics;
using System.Security.Claims;
using DockerPanel.API.Models;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace DockerPanel.API.Services;

/// <summary>
/// Minimal API 操作审计中间件。
/// OperationAuditFilter 是 MVC ActionFilter，只覆盖控制器；
/// 该中间件补齐 Minimal API 端点（/api/* 中非控制器、非 YARP 代理的端点）的审计，
/// 否则登录、registry 变更、ACME 私钥导出等敏感操作不会留下任何审计记录。
/// </summary>
public class OperationAuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<OperationAuditMiddleware> _logger;

    public OperationAuditMiddleware(RequestDelegate next, ILogger<OperationAuditMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IOperationAuditService auditService)
    {
        if (!ShouldAudit(context))
        {
            await _next(context);
            return;
        }

        var startedAt = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        Exception? exception = null;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            try
            {
                await auditService.RecordAsync(CreateLog(context, exception, startedAt, stopwatch.Elapsed.TotalMilliseconds));
            }
            catch (Exception auditEx)
            {
                _logger.LogError(auditEx, "写入操作审计失败");
            }
        }
    }

    private static bool ShouldAudit(HttpContext context)
    {
        var request = context.Request;
        if (!request.Path.StartsWithSegments("/api")) return false;
        if (request.Path.StartsWithSegments("/api/audit")) return false;

        var endpoint = context.GetEndpoint();
        if (endpoint == null) return false;
        // 控制器操作已由 MVC 的 OperationAuditFilter 审计，避免重复记录
        if (endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() != null) return false;
        // YARP 代理转发不属于面板自身的管理操作
        if (endpoint.Metadata.GetMetadata<Yarp.ReverseProxy.Model.RouteModel>() != null) return false;

        if (HttpMethods.IsGet(request.Method))
        {
            var path = request.Path.Value ?? string.Empty;
            return path.Contains("/export", StringComparison.OrdinalIgnoreCase) ||
                   path.Contains("/download", StringComparison.OrdinalIgnoreCase) ||
                   path.Contains("/files/content", StringComparison.OrdinalIgnoreCase);
        }

        return !HttpMethods.IsHead(request.Method) && !HttpMethods.IsOptions(request.Method);
    }

    private static OperationAuditLog CreateLog(HttpContext context, Exception? exception, DateTime timestamp, double durationMs)
    {
        var request = context.Request;
        var endpoint = context.GetEndpoint();
        var routeValues = request.RouteValues.ToDictionary(k => k.Key, v => v.Value?.ToString() ?? string.Empty);
        var statusCode = exception != null ? 500 : context.Response.StatusCode;
        var user = context.User;

        // 资源类型取路径第二段：/api/{resource}/...
        var segments = request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        var resourceType = segments.Length >= 2 ? segments[1] : "api";

        return new OperationAuditLog
        {
            Timestamp = timestamp,
            Method = request.Method,
            Path = request.Path.Value ?? string.Empty,
            Controller = endpoint?.DisplayName,
            UserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            UserName = user.Identity?.Name ?? user.FindFirst(ClaimTypes.Name)?.Value,
            OperationType = OperationAuditFilter.InferOperationType(request.Method, null, request.Path.Value),
            ResourceType = resourceType,
            ResourceId = OperationAuditFilter.GetFirstRouteValue(routeValues, "id", "containerId", "accountId", "certificateId", "orderId", "name"),
            NodeId = request.Query.TryGetValue("nodeId", out var queryNodeId) && !string.IsNullOrWhiteSpace(queryNodeId)
                ? queryNodeId.ToString()
                : null,
            Status = exception == null && statusCode < 400 ? "success" : "failed",
            StatusCode = statusCode,
            DurationMs = durationMs,
            ClientIp = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = request.Headers.UserAgent.ToString(),
            ErrorMessage = exception?.Message,
            RouteValues = routeValues,
            Query = request.Query.ToDictionary(k => k.Key, v => OperationAuditFilter.SensitiveQueryKeys.Contains(v.Key) ? "***" : v.Value.ToString())
        };
    }
}
