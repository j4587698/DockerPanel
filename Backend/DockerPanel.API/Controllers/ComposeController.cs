using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using DockerPanel.API.Models;
using DockerPanel.API.Services;

namespace DockerPanel.API.Controllers;

/// <summary>
/// Docker Compose管理控制器
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/compose")]
[Produces("application/json")]
public class ComposeController : ControllerBase
{
    private readonly IComposeService _composeService;
    private readonly ILogger<ComposeController> _logger;
    private readonly ILocalizationService _localization;

    public ComposeController(IComposeService composeService, ILogger<ComposeController> logger, ILocalizationService localization)
    {
        _composeService = composeService;
        _logger = logger;
        _localization = localization;
    }

    /// <summary>
    /// 获取所有Compose文件
    /// </summary>
    /// <param name="nodeId">节点ID（可选）</param>
    /// <param name="includeContent">是否包含文件内容</param>
    /// <returns>Compose文件列表</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ComposeFile>>> GetComposeFiles(
        [FromQuery] string? nodeId = null,
        [FromQuery] bool includeContent = false)
    {
        try
        {
            var files = await _composeService.GetComposeFilesAsync(nodeId, includeContent);
            return Ok(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Compose文件列表失败");
            return StatusCode(500, new { error = _localization.GetMessage("compose.listFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 根据ID获取Compose文件
    /// </summary>
    /// <param name="id">Compose文件ID</param>
    /// <param name="includeContent">是否包含文件内容</param>
    /// <returns>Compose文件详情</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<ComposeFile>> GetComposeFile(
        string id,
        [FromQuery] bool includeContent = true)
    {
        try
        {
            var file = await _composeService.GetComposeFileAsync(id, includeContent);
            if (file == null)
            {
                return NotFound(new { error = _localization.GetMessage("compose.notFound"), message = $"文件ID {id} 不存在" });
            }
            return Ok(file);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Compose文件详情失败: {FileId}", id);
            return StatusCode(500, new { error = _localization.GetMessage("compose.detailFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 创建Compose文件
    /// </summary>
    /// <param name="request">创建请求</param>
    /// <returns>创建的Compose文件</returns>
    [HttpPost]
    public async Task<ActionResult<ComposeFile>> CreateComposeFile([FromBody] CreateComposeFileRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var file = await _composeService.CreateComposeFileAsync(request);
            return CreatedAtAction(nameof(GetComposeFile), new { id = file.Id }, file);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = _localization.GetMessage("error.invalidParams"), message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建Compose文件失败: {Name}", request.Name);
            return StatusCode(500, new { error = _localization.GetMessage("compose.createFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 更新Compose文件
    /// </summary>
    /// <param name="id">Compose文件ID</param>
    /// <param name="request">更新请求</param>
    /// <returns>更新后的Compose文件</returns>
    [HttpPut("{id}")]
    public async Task<ActionResult<ComposeFile>> UpdateComposeFile(
        string id,
        [FromBody] UpdateComposeFileRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var file = await _composeService.UpdateComposeFileAsync(id, request);
            return Ok(file);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = _localization.GetMessage("error.invalidParams"), message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新Compose文件失败: {FileId}", id);
            return StatusCode(500, new { error = _localization.GetMessage("compose.updateFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 删除Compose文件
    /// </summary>
    /// <param name="id">Compose文件ID</param>
    /// <param name="force">是否强制删除</param>
    /// <returns>删除结果</returns>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteComposeFile(string id, [FromQuery] bool force = false)
    {
        try
        {
            var result = await _composeService.DeleteComposeFileAsync(id, force);
            if (!result)
            {
                return NotFound(new { error = _localization.GetMessage("compose.notFound"), message = $"文件ID {id} 不存在" });
            }
            return Ok(new { message = _localization.GetMessage("compose.deleteSuccess") });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = _localization.GetMessage("error.invalidOperation"), message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除Compose文件失败: {FileId}", id);
            return StatusCode(500, new { error = _localization.GetMessage("compose.deleteFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 验证Compose文件
    /// </summary>
    /// <param name="id">Compose文件ID</param>
    /// <param name="content">文件内容（可选）</param>
    /// <returns>验证结果</returns>
    [HttpPost("{id}/validate")]
    public async Task<ActionResult<ComposeValidationResult>> ValidateComposeFile(
        string id,
        [FromBody] string? content = null)
    {
        try
        {
            var result = await _composeService.ValidateComposeFileAsync(id, content);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证Compose文件失败: {FileId}", id);
            return StatusCode(500, new { error = _localization.GetMessage("compose.validateFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 解析Compose内容（用于编辑器联动）
    /// </summary>
    /// <param name="request">解析请求</param>
    /// <returns>解析后的Compose信息</returns>
    [HttpPost("parse")]
    public async Task<ActionResult<ComposeFile>> ParseComposeContent(
        [FromBody] ParseComposeContentRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request?.Content))
            {
                return BadRequest(new { error = _localization.GetMessage("compose.contentEmpty") });
            }

            var result = await _composeService.ParseComposeContentAsync(request.Content);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析Compose内容失败");
            return StatusCode(500, new { error = _localization.GetMessage("compose.parseFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 验证Compose内容（无需保存）
    /// </summary>
    /// <param name="request">验证请求</param>
    /// <returns>验证结果</returns>
    [HttpPost("validate")]
    public async Task<ActionResult<ComposeValidationResult>> ValidateComposeContent(
        [FromBody] ValidateComposeContentRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request?.Content))
            {
                return BadRequest(new { error = _localization.GetMessage("compose.contentEmpty") });
            }

            // 使用解析来验证内容
            var parsed = await _composeService.ParseComposeContentAsync(request.Content);
            var result = new ComposeValidationResult
            {
                IsValid = true,
                ValidatedAt = DateTime.UtcNow,
                Version = parsed.Version,
                ServiceCount = parsed.Services?.Count ?? 0,
                NetworkCount = parsed.Networks?.Count ?? 0,
                VolumeCount = parsed.Volumes?.Count ?? 0
            };
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证Compose内容失败");
            return Ok(new ComposeValidationResult
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new ValidationError { Message = ex.Message }
                },
                ValidatedAt = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// 部署Compose项目
    /// </summary>
    /// <param name="request">部署请求</param>
    /// <returns>部署结果</returns>
    [HttpPost("deploy")]
    public async Task<ActionResult<ComposeOperationResult>> DeployCompose([FromBody] DeployComposeRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _composeService.DeployComposeAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "部署Compose项目失败");
            return StatusCode(500, new { error = _localization.GetMessage("compose.deployFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 停止Compose项目
    /// </summary>
    /// <param name="request">停止请求</param>
    /// <returns>操作结果</returns>
    [HttpPost("stop")]
    public async Task<ActionResult<ComposeOperationResult>> StopCompose([FromBody] ComposeOperationRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _composeService.StopComposeAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止Compose项目失败");
            return StatusCode(500, new { error = _localization.GetMessage("compose.stopFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 启动Compose项目
    /// </summary>
    /// <param name="request">启动请求</param>
    /// <returns>操作结果</returns>
    [HttpPost("start")]
    public async Task<ActionResult<ComposeOperationResult>> StartCompose([FromBody] ComposeOperationRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _composeService.StartComposeAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动Compose项目失败");
            return StatusCode(500, new { error = _localization.GetMessage("compose.startFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 重启Compose项目
    /// </summary>
    /// <param name="request">重启请求</param>
    /// <returns>操作结果</returns>
    [HttpPost("restart")]
    public async Task<ActionResult<ComposeOperationResult>> RestartCompose([FromBody] ComposeOperationRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _composeService.RestartComposeAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重启Compose项目失败");
            return StatusCode(500, new { error = _localization.GetMessage("compose.restartFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 删除Compose项目
    /// </summary>
    /// <param name="request">删除请求</param>
    /// <returns>操作结果</returns>
    [HttpPost("remove")]
    public async Task<ActionResult<ComposeOperationResult>> RemoveCompose([FromBody] ComposeOperationRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _composeService.RemoveComposeAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除Compose项目失败");
            return StatusCode(500, new { error = _localization.GetMessage("compose.removeFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 获取Compose项目状态
    /// </summary>
    /// <param name="composeFileId">Compose文件ID</param>
    /// <param name="nodeId">节点ID（可选）</param>
    /// <returns>Compose项目信息</returns>
    [HttpGet("projects/{composeFileId}/status")]
    public async Task<ActionResult<ComposeProject>> GetComposeProjectStatus(
        string composeFileId,
        [FromQuery] string? nodeId = null)
    {
        try
        {
            var project = await _composeService.GetComposeProjectStatusAsync(composeFileId, nodeId);
            if (project == null)
            {
                return NotFound(new { error = _localization.GetMessage("compose.projectNotFound"), message = $"项目ID {composeFileId} 不存在" });
            }
            return Ok(project);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Compose项目状态失败: {ComposeFileId}", composeFileId);
            return StatusCode(500, new { error = _localization.GetMessage("compose.projectStatusFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 获取Compose项目列表
    /// </summary>
    /// <param name="nodeId">节点ID（可选）</param>
    /// <returns>Compose项目列表</returns>
    [HttpGet("projects")]
    public async Task<ActionResult<IEnumerable<ComposeProject>>> GetComposeProjects([FromQuery] string? nodeId = null)
    {
        try
        {
            var projects = await _composeService.GetComposeProjectsAsync(nodeId);
            return Ok(projects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Compose项目列表失败");
            return StatusCode(500, new { error = _localization.GetMessage("compose.projectListFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 获取Compose日志
    /// </summary>
    /// <param name="request">日志请求</param>
    /// <returns>日志响应</returns>
    [HttpPost("logs")]
    public async Task<ActionResult<ComposeLogsResponse>> GetComposeLogs([FromBody] ComposeLogsRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var logs = await _composeService.GetComposeLogsAsync(request);
            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Compose日志失败");
            return StatusCode(500, new { error = _localization.GetMessage("compose.logsFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 获取Compose项目统计信息
    /// </summary>
    /// <param name="composeFileId">Compose文件ID</param>
    /// <param name="nodeId">节点ID（可选）</param>
    /// <returns>统计信息</returns>
    [HttpGet("projects/{composeFileId}/stats")]
    public async Task<ActionResult<ComposeProjectStats>> GetComposeProjectStats(
        string composeFileId,
        [FromQuery] string? nodeId = null)
    {
        try
        {
            var stats = await _composeService.GetComposeProjectStatsAsync(composeFileId, nodeId);
            if (stats == null)
            {
                return NotFound(new { error = _localization.GetMessage("compose.projectStatsNotFound"), message = $"项目ID {composeFileId} 不存在" });
            }
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Compose项目统计信息失败: {ComposeFileId}", composeFileId);
            return StatusCode(500, new { error = _localization.GetMessage("compose.projectStatsFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 导出Compose文件
    /// </summary>
    /// <param name="id">Compose文件ID</param>
    /// <param name="format">导出格式（yaml, json）</param>
    /// <returns>导出的文件内容</returns>
    [HttpGet("{id}/export")]
    public async Task<ActionResult<string>> ExportComposeFile(
        string id,
        [FromQuery] string format = "yaml")
    {
        try
        {
            var content = await _composeService.ExportComposeFileAsync(id, format);
            return Ok(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出Compose文件失败: {FileId}", id);
            return StatusCode(500, new { error = _localization.GetMessage("compose.exportFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 导入Compose文件
    /// </summary>
    /// <param name="request">导入请求</param>
    /// <returns>导入的Compose文件</returns>
    [HttpPost("import")]
    public async Task<ActionResult<ComposeFile>> ImportComposeFile([FromBody] ImportComposeFileRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var file = await _composeService.ImportComposeFileAsync(
                request.Content,
                request.Name,
                request.Description,
                request.NodeId
            );
            return Ok(file);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入Compose文件失败: {Name}", request.Name);
            return StatusCode(500, new { error = _localization.GetMessage("compose.importFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 获取Compose模板列表
    /// </summary>
    /// <param name="category">分类（可选）</param>
    /// <param name="tags">标签（可选）</param>
    /// <returns>模板列表</returns>
    [HttpGet("templates")]
    public async Task<ActionResult<IEnumerable<ComposeTemplate>>> GetComposeTemplates(
        [FromQuery] string? category = null,
        [FromQuery] List<string>? tags = null)
    {
        try
        {
            var templates = await _composeService.GetComposeTemplatesAsync(category, tags);
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Compose模板列表失败");
            return StatusCode(500, new { error = _localization.GetMessage("compose.templateListFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 根据模板创建Compose文件
    /// </summary>
    /// <param name="request">模板创建请求</param>
    /// <returns>创建的Compose文件</returns>
    [HttpPost("create-from-template")]
    public async Task<ActionResult<ComposeFile>> CreateFromTemplate([FromBody] CreateFromTemplateRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var file = await _composeService.CreateFromTemplateAsync(
                request.TemplateId,
                request.Variables,
                request.Name,
                request.Description
            );
            return Ok(file);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "根据模板创建Compose文件失败: {TemplateId}", request.TemplateId);
            return StatusCode(500, new { error = _localization.GetMessage("compose.createFromTemplateFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 批量操作Compose文件
    /// </summary>
    /// <param name="request">批量操作请求</param>
    /// <returns>批量操作结果</returns>
    [HttpPost("batch-operation")]
    public async Task<ActionResult<Dictionary<string, ComposeOperationResult>>> BatchOperation([FromBody] BatchComposeOperationRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var results = await _composeService.BatchOperationAsync(
                request.FileIds,
                request.Operation,
                request.Parameters
            );
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量操作Compose文件失败: {Operation}", request.Operation);
            return StatusCode(500, new { error = _localization.GetMessage("compose.batchOperationFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 获取Compose文件历史版本
    /// </summary>
    /// <param name="id">Compose文件ID</param>
    /// <returns>历史版本列表</returns>
    [HttpGet("{id}/history")]
    public async Task<ActionResult<IEnumerable<ComposeFileVersion>>> GetComposeFileHistory(string id)
    {
        try
        {
            var history = await _composeService.GetComposeFileHistoryAsync(id);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Compose文件历史版本失败: {FileId}", id);
            return StatusCode(500, new { error = _localization.GetMessage("compose.historyFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 恢复Compose文件到指定版本
    /// </summary>
    /// <param name="id">Compose文件ID</param>
    /// <param name="request">恢复请求</param>
    /// <returns>恢复后的Compose文件</returns>
    [HttpPost("{id}/restore")]
    public async Task<ActionResult<ComposeFile>> RestoreComposeFileVersion(
        string id,
        [FromBody] RestoreFileVersionRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var file = await _composeService.RestoreComposeFileVersionAsync(id, request.VersionId);
            return Ok(file);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "恢复Compose文件版本失败: {FileId}", id);
            return StatusCode(500, new { error = _localization.GetMessage("compose.restoreFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 检查Compose文件依赖
    /// </summary>
    /// <param name="id">Compose文件ID</param>
    /// <returns>依赖检查结果</returns>
    [HttpPost("{id}/check-dependencies")]
    public async Task<ActionResult<ComposeDependencyCheck>> CheckComposeDependencies(string id)
    {
        try
        {
            var check = await _composeService.CheckComposeDependenciesAsync(id);
            return Ok(check);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查Compose文件依赖失败: {FileId}", id);
            return StatusCode(500, new { error = _localization.GetMessage("compose.checkDependenciesFailed"), message = ex.Message });
        }
    }
}

/// <summary>
/// 导入Compose文件请求
/// </summary>
public class ImportComposeFileRequest
{
    /// <summary>
    /// 文件内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 文件名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 节点ID
    /// </summary>
    public string? NodeId { get; set; }
}

/// <summary>
/// 根据模板创建请求
/// </summary>
public class CreateFromTemplateRequest
{
    /// <summary>
    /// 模板ID
    /// </summary>
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>
    /// 变量值
    /// </summary>
    public Dictionary<string, object> Variables { get; set; } = new();

    /// <summary>
    /// 文件名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// 批量操作请求
/// </summary>
public class BatchComposeOperationRequest
{
    /// <summary>
    /// 文件ID列表
    /// </summary>
    public List<string> FileIds { get; set; } = new();

    /// <summary>
    /// 操作类型
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// 操作参数
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// 恢复文件版本请求
/// </summary>
public class RestoreFileVersionRequest
{
    /// <summary>
    /// 版本ID
    /// </summary>
    public string VersionId { get; set; } = string.Empty;
}