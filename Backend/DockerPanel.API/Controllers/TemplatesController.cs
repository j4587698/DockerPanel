using DockerPanel.API.Data;
using DockerPanel.API.Models;
using DockerPanel.API.Services;
using Microsoft.AspNetCore.Mvc;
using TinyDb;

namespace DockerPanel.API.Controllers;

/// <summary>
/// 容器模板控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TemplatesController : ControllerBase
{
    private readonly ILogger<TemplatesController> _logger;
    private readonly TinyDbContext _dbContext;
    private readonly ILocalizationService _localization;

    public TemplatesController(ILogger<TemplatesController> logger, TinyDbContext dbContext, ILocalizationService localization)
    {
        _logger = logger;
        _dbContext = dbContext;
        _localization = localization;
    }

    /// <summary>
    /// 获取所有模板
    /// </summary>
    [HttpGet]
    public ActionResult<IEnumerable<ContainerTemplate>> GetTemplates([FromQuery] string? type = null)
    {
        try
        {
            var templates = _dbContext.ContainerTemplates.Query();

            if (!string.IsNullOrEmpty(type))
            {
                templates = templates.Where(t => t.Type == type);
            }

            var result = templates.OrderByDescending(t => t.CreatedAt).ToList();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取模板列表失败");
            return StatusCode(500, new { error = "获取模板列表失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 获取单个模板
    /// </summary>
    [HttpGet("{id}")]
    public ActionResult<ContainerTemplate> GetTemplate(string id)
    {
        try
        {
            var template = _dbContext.ContainerTemplates.FindById(id);
            if (template == null)
            {
                return NotFound(new { error = "模板不存在" });
            }
            return Ok(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取模板失败: {Id}", id);
            return StatusCode(500, new { error = "获取模板失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 创建模板
    /// </summary>
    [HttpPost]
    public ActionResult<ContainerTemplate> CreateTemplate([FromBody] CreateTemplateRequest request)
    {
        try
        {
            var template = new ContainerTemplate
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                Type = request.Type,
                Description = request.Description,
                Image = request.Image,
                Command = request.Command,
                WorkingDir = request.WorkingDir,
                User = request.User,
                Ports = request.Ports,
                Volumes = request.Volumes,
                Environment = request.Environment,
                Labels = request.Labels,
                RestartPolicy = request.RestartPolicy,
                NetworkMode = request.NetworkMode,
                Networks = request.Networks,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.ContainerTemplates.Insert(template);
            _logger.LogInformation("创建模板成功: {Name}", template.Name);
            return CreatedAtAction(nameof(GetTemplate), new { id = template.Id }, template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建模板失败");
            return StatusCode(500, new { error = "创建模板失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 更新模板
    /// </summary>
    [HttpPut("{id}")]
    public ActionResult<ContainerTemplate> UpdateTemplate(string id, [FromBody] UpdateTemplateRequest request)
    {
        try
        {
            var existing = _dbContext.ContainerTemplates.FindById(id);
            if (existing == null)
            {
                return NotFound(new { error = "模板不存在" });
            }

            existing.Name = request.Name;
            existing.Type = request.Type;
            existing.Description = request.Description;
            existing.Image = request.Image;
            existing.Command = request.Command;
            existing.WorkingDir = request.WorkingDir;
            existing.User = request.User;
            existing.Ports = request.Ports;
            existing.Volumes = request.Volumes;
            existing.Environment = request.Environment;
            existing.Labels = request.Labels;
            existing.RestartPolicy = request.RestartPolicy;
            existing.NetworkMode = request.NetworkMode;
            existing.Networks = request.Networks;
            existing.UpdatedAt = DateTime.UtcNow;

            _dbContext.ContainerTemplates.Update(existing);
            _logger.LogInformation("更新模板成功: {Id}", id);
            return Ok(existing);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新模板失败: {Id}", id);
            return StatusCode(500, new { error = "更新模板失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 删除模板
    /// </summary>
    [HttpDelete("{id}")]
    public ActionResult DeleteTemplate(string id)
    {
        try
        {
            var existing = _dbContext.ContainerTemplates.FindById(id);
            if (existing == null)
            {
                return NotFound(new { error = "模板不存在" });
            }

            _dbContext.ContainerTemplates.Delete(id);
            _logger.LogInformation("删除模板成功: {Id}", id);
            return Ok(new { message = _localization.GetMessage("template.deleteSuccess") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除模板失败: {Id}", id);
            return StatusCode(500, new { error = "删除模板失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 复制模板
    /// </summary>
    [HttpPost("{id}/duplicate")]
    public ActionResult<ContainerTemplate> DuplicateTemplate(string id)
    {
        try
        {
            var existing = _dbContext.ContainerTemplates.FindById(id);
            if (existing == null)
            {
                return NotFound(new { error = "模板不存在" });
            }

            var newTemplate = new ContainerTemplate
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"{existing.Name} (副本)",
                Type = existing.Type,
                Description = existing.Description,
                Image = existing.Image,
                Command = existing.Command?.ToList(),
                WorkingDir = existing.WorkingDir,
                User = existing.User,
                Ports = existing.Ports?.Select(p => new TemplatePortMapping
                {
                    HostIp = p.HostIp,
                    HostPort = p.HostPort,
                    ContainerPort = p.ContainerPort,
                    Protocol = p.Protocol
                }).ToList(),
                Volumes = existing.Volumes?.Select(v => new TemplateVolumeMapping
                {
                    HostPath = v.HostPath,
                    ContainerPath = v.ContainerPath,
                    ReadOnly = v.ReadOnly
                }).ToList(),
                Environment = existing.Environment?.ToDictionary(k => k.Key, k => k.Value),
                Labels = existing.Labels?.ToDictionary(k => k.Key, k => k.Value),
                RestartPolicy = existing.RestartPolicy != null ? new TemplateRestartPolicy
                {
                    Name = existing.RestartPolicy.Name,
                    MaximumRetryCount = existing.RestartPolicy.MaximumRetryCount
                } : null,
                NetworkMode = existing.NetworkMode,
                Networks = existing.Networks?.Select(n => new TemplateNetworkConfig
                {
                    NetworkId = n.NetworkId,
                    NetworkName = n.NetworkName,
                    Aliases = n.Aliases?.ToList(),
                    IpAddress = n.IpAddress
                }).ToList(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.ContainerTemplates.Insert(newTemplate);
            _logger.LogInformation("复制模板成功: {Name}", newTemplate.Name);
            return CreatedAtAction(nameof(GetTemplate), new { id = newTemplate.Id }, newTemplate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "复制模板失败: {Id}", id);
            return StatusCode(500, new { error = "复制模板失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 导出模板
    /// </summary>
    [HttpGet("{id}/export")]
    public ActionResult ExportTemplate(string id)
    {
        try
        {
            var template = _dbContext.ContainerTemplates.FindById(id);
            if (template == null)
            {
                return NotFound(new { error = "模板不存在" });
            }

            return Ok(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出模板失败: {Id}", id);
            return StatusCode(500, new { error = "导出模板失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 导入模板
    /// </summary>
    [HttpPost("import")]
    public ActionResult<ContainerTemplate> ImportTemplate([FromBody] ContainerTemplate template)
    {
        try
        {
            // 生成新ID，避免冲突
            template.Id = Guid.NewGuid().ToString();
            template.CreatedAt = DateTime.UtcNow;
            template.UpdatedAt = DateTime.UtcNow;

            _dbContext.ContainerTemplates.Insert(template);
            _logger.LogInformation("导入模板成功: {Name}", template.Name);
            return CreatedAtAction(nameof(GetTemplate), new { id = template.Id }, template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入模板失败");
            return StatusCode(500, new { error = "导入模板失败", message = ex.Message });
        }
    }
}
