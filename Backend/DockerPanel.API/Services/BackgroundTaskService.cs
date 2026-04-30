using System.Collections.Concurrent;

namespace DockerPanel.API.Services;

/// <summary>
/// 后台任务状态
/// </summary>
public class BackgroundTask
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // image-build, image-pull, image-push, compose-deploy, volume-archive
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = "pending"; // pending, running, completed, failed
    public int Progress { get; set; }
    public string? Detail { get; set; }
    public string? Stream { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// 后台任务管理服务
/// 用于跟踪所有后台任务的状态，支持页面刷新后恢复任务列表
/// </summary>
public class BackgroundTaskService
{
    private readonly ConcurrentDictionary<string, BackgroundTask> _tasks = new();
    private readonly ILogger<BackgroundTaskService> _logger;

    public BackgroundTaskService(ILogger<BackgroundTaskService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 添加新任务
    /// </summary>
    public BackgroundTask AddTask(string id, string type, string title, Dictionary<string, object>? metadata = null)
    {
        var task = new BackgroundTask
        {
            Id = id,
            Type = type,
            Title = title,
            Status = "running",
            Progress = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Metadata = metadata ?? new Dictionary<string, object>()
        };

        _tasks[id] = task;
        _logger.LogInformation("添加后台任务: {Id} - {Title}", id, title);
        return task;
    }

    /// <summary>
    /// 更新任务进度
    /// </summary>
    public void UpdateTask(string id, string status, int progress, string? detail = null, string? stream = null, string? error = null)
    {
        if (_tasks.TryGetValue(id, out var task))
        {
            task.Status = status;
            task.Progress = progress;
            task.Detail = detail;
            task.Stream = stream;
            task.Error = error;
            task.UpdatedAt = DateTime.UtcNow;
            
            _logger.LogDebug("更新任务进度: {Id} - {Status} - {Progress}%", id, status, progress);
        }
    }

    /// <summary>
    /// 标记任务完成
    /// </summary>
    public void CompleteTask(string id, string? detail = null)
    {
        if (_tasks.TryGetValue(id, out var task))
        {
            task.Status = "completed";
            task.Progress = 100;
            task.Detail = detail;
            task.UpdatedAt = DateTime.UtcNow;
            
            _logger.LogInformation("任务完成: {Id} - {Title}", id, task.Title);
        }
    }

    /// <summary>
    /// 标记任务失败
    /// </summary>
    public void FailTask(string id, string error)
    {
        if (_tasks.TryGetValue(id, out var task))
        {
            task.Status = "failed";
            task.Progress = 100;
            task.Error = error;
            task.UpdatedAt = DateTime.UtcNow;
            
            _logger.LogWarning("任务失败: {Id} - {Title} - {Error}", id, task.Title, error);
        }
    }

    /// <summary>
    /// 获取任务
    /// </summary>
    public BackgroundTask? GetTask(string id)
    {
        _tasks.TryGetValue(id, out var task);
        return task;
    }

    /// <summary>
    /// 获取所有任务
    /// </summary>
    public List<BackgroundTask> GetAllTasks()
    {
        return _tasks.Values.OrderByDescending(t => t.CreatedAt).ToList();
    }

    /// <summary>
    /// 获取进行中的任务（running 或 pending）
    /// </summary>
    public List<BackgroundTask> GetActiveTasks()
    {
        return _tasks.Values
            .Where(t => t.Status == "running" || t.Status == "pending")
            .OrderByDescending(t => t.CreatedAt)
            .ToList();
    }

    /// <summary>
    /// 删除任务
    /// </summary>
    public void RemoveTask(string id)
    {
        _tasks.TryRemove(id, out _);
        _logger.LogInformation("删除任务: {Id}", id);
    }

    /// <summary>
    /// 清理已完成的任务（超过1小时的）
    /// </summary>
    public void CleanupOldTasks()
    {
        var cutoff = DateTime.UtcNow.AddHours(-1);
        var toRemove = _tasks.Values
            .Where(t => (t.Status == "completed" || t.Status == "failed") && t.UpdatedAt < cutoff)
            .Select(t => t.Id)
            .ToList();

        foreach (var id in toRemove)
        {
            _tasks.TryRemove(id, out _);
        }

        if (toRemove.Count > 0)
        {
            _logger.LogInformation("清理了 {Count} 个过期任务", toRemove.Count);
        }
    }

    /// <summary>
    /// 清理所有已完成的任务
    /// </summary>
    public void ClearCompletedTasks()
    {
        var toRemove = _tasks.Values
            .Where(t => t.Status == "completed" || t.Status == "failed")
            .Select(t => t.Id)
            .ToList();

        foreach (var id in toRemove)
        {
            _tasks.TryRemove(id, out _);
        }

        _logger.LogInformation("清理了 {Count} 个已完成任务", toRemove.Count);
    }
}
