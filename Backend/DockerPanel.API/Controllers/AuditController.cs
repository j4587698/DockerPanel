using DockerPanel.API.Models;
using DockerPanel.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace DockerPanel.API.Controllers;

/// <summary>
/// 操作审计日志
/// </summary>
[ApiController]
[Route("api/audit")]
public class AuditController : ControllerBase
{
    private readonly IOperationAuditService _auditService;

    public AuditController(IOperationAuditService auditService)
    {
        _auditService = auditService;
    }

    /// <summary>
    /// 获取操作审计日志
    /// </summary>
    [HttpGet("logs")]
    public async Task<ActionResult<OperationAuditLogPage>> GetLogs([FromQuery] OperationAuditLogFilter filter)
    {
        return Ok(await _auditService.GetLogsAsync(filter));
    }

    /// <summary>
    /// 获取操作审计日志详情
    /// </summary>
    [HttpGet("logs/{id}")]
    public async Task<ActionResult<OperationAuditLog>> GetLog(string id)
    {
        var log = await _auditService.GetLogAsync(id);
        return log == null ? NotFound(new { id }) : Ok(log);
    }
}