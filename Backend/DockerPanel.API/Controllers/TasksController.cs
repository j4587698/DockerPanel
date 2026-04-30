using Microsoft.AspNetCore.Mvc;
using DockerPanel.API.Services;

namespace DockerPanel.API.Controllers;

/// <summary>
/// 后台任务控制器
/// 用于获取和管理后台任务状态
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly BackgroundTaskService _taskService;
    private readonly ILogger<TasksController> _logger;
    private readonly ILocalizationService _localization;

    public TasksController(BackgroundTaskService taskService, ILogger<TasksController> logger, ILocalizationService localization)
    {
        _taskService = taskService;
        _logger = logger;
        _localization = localization;
    }

    /// <summary>
    /// 获取所有任务
    /// </summary>
    [HttpGet]
    public ActionResult GetTasks()
    {
        var tasks = _taskService.GetAllTasks();
        return Ok(tasks);
    }

    /// <summary>
    /// 获取进行中的任务
    /// </summary>
    [HttpGet("active")]
    public ActionResult GetActiveTasks()
    {
        var tasks = _taskService.GetActiveTasks();
        return Ok(tasks);
    }

    /// <summary>
    /// 获取特定任务
    /// </summary>
    [HttpGet("{id}")]
    public ActionResult GetTask(string id)
    {
        var task = _taskService.GetTask(id);
        if (task == null)
        {
            return NotFound(new { error = "任务不存在" });
        }
        return Ok(task);
    }

    /// <summary>
    /// 删除任务
    /// </summary>
    [HttpDelete("{id}")]
    public ActionResult RemoveTask(string id)
    {
        _taskService.RemoveTask(id);
        return Ok(new { message = _localization.GetMessage("task.deleted") });
    }

    /// <summary>
    /// 清理已完成的任务
    /// </summary>
    [HttpPost("clear-completed")]
    public ActionResult ClearCompleted()
    {
        _taskService.ClearCompletedTasks();
        return Ok(new { message = _localization.GetMessage("task.cleared") });
    }
}
