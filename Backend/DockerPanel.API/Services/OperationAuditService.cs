using DockerPanel.API.Data;
using DockerPanel.API.Models;

namespace DockerPanel.API.Services;

public interface IOperationAuditService
{
    Task RecordAsync(OperationAuditLog log);
    Task<OperationAuditLogPage> GetLogsAsync(OperationAuditLogFilter filter);
    Task<OperationAuditLog?> GetLogAsync(string id);
    Task<int> DeleteOlderThanAsync(DateTime cutoffUtc);
}

/// <summary>
/// 操作审计服务
/// </summary>
public class OperationAuditService : IOperationAuditService
{
    private readonly TinyDbContext _dbContext;
    private readonly ILogger<OperationAuditService> _logger;

    public OperationAuditService(TinyDbContext dbContext, ILogger<OperationAuditService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public Task RecordAsync(OperationAuditLog log)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(log.Id)) log.Id = Guid.NewGuid().ToString("N");
            if (log.Timestamp == default) log.Timestamp = DateTime.UtcNow;

            var collection = _dbContext.GetCollection<OperationAuditLog>(DbCollections.OperationAudits);
            collection.Insert(log);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "记录操作审计日志失败");
        }

        return Task.CompletedTask;
    }

    public Task<OperationAuditLogPage> GetLogsAsync(OperationAuditLogFilter filter)
    {
        filter.Page = Math.Max(1, filter.Page);
        filter.PageSize = Math.Clamp(filter.PageSize, 1, 200);

        var logs = _dbContext.GetCollection<OperationAuditLog>(DbCollections.OperationAudits)
            .FindAll()
            .AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filter.OperationType))
            logs = logs.Where(l => string.Equals(l.OperationType, filter.OperationType, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.ResourceType))
            logs = logs.Where(l => string.Equals(l.ResourceType, filter.ResourceType, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.ResourceId))
            logs = logs.Where(l => string.Equals(l.ResourceId, filter.ResourceId, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.Status))
            logs = logs.Where(l => string.Equals(l.Status, filter.Status, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.NodeId))
            logs = logs.Where(l => string.Equals(l.NodeId, filter.NodeId, StringComparison.OrdinalIgnoreCase));
        if (filter.StartDate.HasValue)
            logs = logs.Where(l => l.Timestamp >= filter.StartDate.Value);
        if (filter.EndDate.HasValue)
            logs = logs.Where(l => l.Timestamp <= filter.EndDate.Value);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            logs = logs.Where(l =>
                l.Path.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (l.ResourceId?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (l.Action?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var ordered = logs.OrderByDescending(l => l.Timestamp).ToList();
        return Task.FromResult(new OperationAuditLogPage
        {
            Total = ordered.Count,
            Page = filter.Page,
            PageSize = filter.PageSize,
            Items = ordered.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToList()
        });
    }

    public Task<OperationAuditLog?> GetLogAsync(string id)
    {
        var log = _dbContext.GetCollection<OperationAuditLog>(DbCollections.OperationAudits)
            .Find(l => l.Id == id)
            .FirstOrDefault();
        return Task.FromResult(log);
    }

    public Task<int> DeleteOlderThanAsync(DateTime cutoffUtc)
    {
        var deleted = _dbContext.GetCollection<OperationAuditLog>(DbCollections.OperationAudits)
            .DeleteMany(l => l.Timestamp < cutoffUtc);
        return Task.FromResult(deleted);
    }
}