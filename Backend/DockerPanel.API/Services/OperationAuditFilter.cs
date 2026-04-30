using System.Diagnostics;
using System.Security.Claims;
using DockerPanel.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace DockerPanel.API.Services;

/// <summary>
/// MVC 操作审计过滤器
/// </summary>
public class OperationAuditFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> SensitiveQueryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "passphrase", "token", "secret", "key", "privateKey", "authorization", "accessToken", "refreshToken"
    };

    private readonly IOperationAuditService _auditService;
    private readonly ILogger<OperationAuditFilter> _logger;

    public OperationAuditFilter(IOperationAuditService auditService, ILogger<OperationAuditFilter> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!ShouldAudit(context.HttpContext.Request))
        {
            await next();
            return;
        }

        var startedAt = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        ActionExecutedContext? executedContext = null;
        Exception? exception = null;

        try
        {
            executedContext = await next();
            exception = executedContext.Exception;
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
                await _auditService.RecordAsync(CreateLog(context, executedContext, exception, startedAt, stopwatch.Elapsed.TotalMilliseconds));
            }
            catch (Exception auditEx)
            {
                _logger.LogError(auditEx, "写入操作审计失败");
            }
        }
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

    private static OperationAuditLog CreateLog(ActionExecutingContext context, ActionExecutedContext? executedContext, Exception? exception, DateTime timestamp, double durationMs)
    {
        var request = context.HttpContext.Request;
        var response = context.HttpContext.Response;
        var routeValues = context.RouteData.Values.ToDictionary(k => k.Key, v => v.Value?.ToString() ?? string.Empty);
        var statusCode = GetStatusCode(executedContext, response, exception);
        var user = context.HttpContext.User;

        return new OperationAuditLog
        {
            Timestamp = timestamp,
            Method = request.Method,
            Path = request.Path.Value ?? string.Empty,
            Controller = routeValues.GetValueOrDefault("controller"),
            Action = routeValues.GetValueOrDefault("action"),
            UserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            UserName = user.Identity?.Name ?? user.FindFirst(ClaimTypes.Name)?.Value,
            OperationType = InferOperationType(request.Method, routeValues.GetValueOrDefault("action"), request.Path.Value),
            ResourceType = routeValues.GetValueOrDefault("controller") ?? "api",
            ResourceId = GetFirstRouteValue(routeValues, "id", "volumeId", "networkId", "imageId", "name", "accountId", "certificateId", "backupId"),
            NodeId = GetNodeId(context),
            Status = exception == null && statusCode < 400 ? "success" : "failed",
            StatusCode = statusCode,
            DurationMs = durationMs,
            ClientIp = context.HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = request.Headers.UserAgent.ToString(),
            ErrorMessage = exception?.Message,
            RouteValues = routeValues,
            Query = request.Query.ToDictionary(k => k.Key, v => SensitiveQueryKeys.Contains(v.Key) ? "***" : v.Value.ToString())
        };
    }

    private static int GetStatusCode(ActionExecutedContext? executedContext, HttpResponse response, Exception? exception)
    {
        if (exception != null) return 500;
        if (executedContext?.Result is IStatusCodeActionResult statusCodeResult && statusCodeResult.StatusCode.HasValue)
            return statusCodeResult.StatusCode.Value;
        return response.StatusCode == 0 ? 200 : response.StatusCode;
    }

    private static string InferOperationType(string method, string? action, string? path)
    {
        var source = $"{action} {path}".ToLowerInvariant();
        if (source.Contains("delete") || source.Contains("remove") || HttpMethods.IsDelete(method)) return "delete";
        if (source.Contains("prune") || source.Contains("clean")) return "prune";
        if (source.Contains("exec") || source.Contains("command")) return "exec";
        if (source.Contains("backup")) return "backup";
        if (source.Contains("restore")) return "restore";
        if (source.Contains("export") || source.Contains("download")) return "export";
        if (source.Contains("upload")) return "upload";
        if (source.Contains("start")) return "start";
        if (source.Contains("stop")) return "stop";
        if (source.Contains("restart")) return "restart";
        if (source.Contains("rename")) return "rename";
        if (source.Contains("update") || HttpMethods.IsPatch(method) || HttpMethods.IsPut(method)) return "update";
        if (source.Contains("create") || HttpMethods.IsPost(method)) return "create";
        return method.ToLowerInvariant();
    }

    private static string? GetNodeId(ActionExecutingContext context)
    {
        if (context.HttpContext.Request.Query.TryGetValue("nodeId", out var queryNodeId) && !string.IsNullOrWhiteSpace(queryNodeId))
            return queryNodeId.ToString();

        foreach (var argument in context.ActionArguments.Values)
        {
            var value = argument?.GetType().GetProperty("NodeId")?.GetValue(argument)?.ToString();
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }

        return null;
    }

    private static string? GetFirstRouteValue(Dictionary<string, string> routeValues, params string[] names)
    {
        foreach (var name in names)
        {
            if (routeValues.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)) return value;
        }

        return null;
    }
}