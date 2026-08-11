using System.Diagnostics;
using System.Security.Claims;
using DockerPanel.API.Models;

namespace DockerPanel.API.Services;

/// <summary>
/// Minimal API 操作审计中间件（替代已移除的 MVC OperationAuditFilter 全局过滤器）。
/// 对 /api 下的写操作（以及 export/download/files/content 类 GET）记录审计日志，
/// 敏感查询参数打码，并从路由匹配信息中提取资源 ID / 节点 ID。
/// </summary>
public static class OperationAuditMiddleware
{
    private static readonly HashSet<string> SensitiveQueryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "passphrase", "token", "secret", "key", "privateKey", "authorization", "accessToken", "refreshToken"
    };

    private static readonly string[] ResourceIdKeys =
    {
        "id", "volumeId", "networkId", "imageId", "name", "accountId", "certificateId", "backupId"
    };

    public static IApplicationBuilder UseOperationAudit(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            if (!ShouldAudit(context.Request))
            {
                await next();
                return;
            }

            var startedAt = DateTime.UtcNow;
            var stopwatch = Stopwatch.StartNew();
            Exception? exception = null;
            int statusCode = 200;

            try
            {
                await next();
                statusCode = context.Response.StatusCode;
            }
            catch (Exception ex)
            {
                exception = ex;
                statusCode = 500;
                throw;
            }
            finally
            {
                stopwatch.Stop();
                try
                {
                    var auditService = context.RequestServices.GetService<IOperationAuditService>();
                    if (auditService != null)
                    {
                        await auditService.RecordAsync(CreateLog(context, exception, startedAt, stopwatch.Elapsed.TotalMilliseconds, statusCode));
                    }
                }
                catch (Exception auditEx)
                {
                    var logger = context.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("OperationAudit");
                    logger?.LogError(auditEx, "写入操作审计失败");
                }
            }
        });
    }

    private static bool ShouldAudit(HttpRequest request)
    {
        if (!request.Path.StartsWithSegments("/api")) return false;
        if (request.Path.StartsWithSegments("/api/audit")) return false;
        if (HttpMethods.IsGet(request.Method))
        {
            var path = request.Path.Value ?? string.Empty;
            return path.Contains("/export", StringComparison.OrdinalIgnoreCase) ||
                   path.Contains("/download", StringComparison.OrdinalIgnoreCase) ||
                   path.Contains("/files/content", StringComparison.OrdinalIgnoreCase);
        }

        return !HttpMethods.IsHead(request.Method) && !HttpMethods.IsOptions(request.Method);
    }

    private static OperationAuditLog CreateLog(HttpContext context, Exception? exception, DateTime timestamp, double durationMs, int statusCode)
    {
        var request = context.Request;
        var routeValues = request.RouteValues.ToDictionary(
            kv => kv.Key,
            kv => kv.Value?.ToString() ?? string.Empty,
            StringComparer.OrdinalIgnoreCase);
        var routePattern = context.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.RouteEndpoint>()?.RoutePattern.RawText;
        var resourceType = ExtractResourceType(routePattern);
        var user = context.User;

        return new OperationAuditLog
        {
            Timestamp = timestamp,
            Method = request.Method,
            Path = request.Path.Value ?? string.Empty,
            Controller = null,
            Action = null,
            UserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            UserName = user.Identity?.Name ?? user.FindFirst(ClaimTypes.Name)?.Value,
            OperationType = InferOperationType(request.Method, routePattern ?? request.Path.Value ?? string.Empty),
            ResourceType = resourceType,
            ResourceId = GetFirstRouteValue(routeValues),
            NodeId = GetNodeId(request, routeValues),
            Status = exception == null && statusCode < 400 ? "success" : "failed",
            StatusCode = statusCode,
            DurationMs = durationMs,
            ClientIp = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = request.Headers.UserAgent.ToString(),
            ErrorMessage = exception?.Message,
            RouteValues = routeValues,
            Query = request.Query.ToDictionary(k => k.Key, v => SensitiveQueryKeys.Contains(v.Key) ? "***" : v.ToString())
        };
    }

    private static string ExtractResourceType(string? routePattern)
    {
        if (string.IsNullOrEmpty(routePattern)) return "api";
        var segments = routePattern.Split('/');
        if (segments.Length < 2) return "api";
        return segments[1].Split('?')[0];
    }

    private static string InferOperationType(string method, string source)
    {
        var lowered = source.ToLowerInvariant();
        if (lowered.Contains("delete") || lowered.Contains("remove") || HttpMethods.IsDelete(method)) return "delete";
        if (lowered.Contains("prune") || lowered.Contains("clean")) return "prune";
        if (lowered.Contains("exec") || lowered.Contains("command")) return "exec";
        if (lowered.Contains("backup")) return "backup";
        if (lowered.Contains("restore")) return "restore";
        if (lowered.Contains("export") || lowered.Contains("download")) return "export";
        if (lowered.Contains("upload")) return "upload";
        if (lowered.Contains("start")) return "start";
        if (lowered.Contains("stop")) return "stop";
        if (lowered.Contains("restart")) return "restart";
        if (lowered.Contains("rename")) return "rename";
        if (lowered.Contains("update") || HttpMethods.IsPatch(method) || HttpMethods.IsPut(method)) return "update";
        if (lowered.Contains("create") || HttpMethods.IsPost(method)) return "create";
        return method.ToLowerInvariant();
    }

    private static string? GetNodeId(HttpRequest request, Dictionary<string, string> routeValues)
    {
        if (request.Query.TryGetValue("nodeId", out var queryNodeId) && !string.IsNullOrWhiteSpace(queryNodeId))
            return queryNodeId.ToString();
        if (routeValues.TryGetValue("nodeId", out var routeNodeId) && !string.IsNullOrWhiteSpace(routeNodeId))
            return routeNodeId;
        return null;
    }

    private static string? GetFirstRouteValue(Dictionary<string, string> routeValues)
    {
        foreach (var key in ResourceIdKeys)
        {
            if (routeValues.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)) return value;
        }

        return null;
    }
}
