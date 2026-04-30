using TinyDb.Attributes;

namespace DockerPanel.API.Models;

/// <summary>
/// 操作审计日志
/// </summary>
[Entity]
public class OperationAuditLog
{
    [Id]
    [IdGeneration(IdGenerationStrategy.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [Index]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [Index]
    public string OperationType { get; set; } = string.Empty;

    [Index]
    public string ResourceType { get; set; } = string.Empty;

    [Index]
    public string? ResourceId { get; set; }

    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? Controller { get; set; }
    public string? Action { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? NodeId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public double DurationMs { get; set; }
    public string? ClientIp { get; set; }
    public string? UserAgent { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, string> RouteValues { get; set; } = new();
    public Dictionary<string, string> Query { get; set; } = new();
}

/// <summary>
/// 操作审计日志过滤条件
/// </summary>
public class OperationAuditLogFilter
{
    public string? Search { get; set; }
    public string? OperationType { get; set; }
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public string? Status { get; set; }
    public string? NodeId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

/// <summary>
/// 分页操作审计日志
/// </summary>
public class OperationAuditLogPage
{
    public List<OperationAuditLog> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}