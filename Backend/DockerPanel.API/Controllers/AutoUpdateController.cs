using Microsoft.AspNetCore.Mvc;
using DockerPanel.API.Models;
using DockerPanel.API.Services;

namespace DockerPanel.API.Controllers;

/// <summary>
/// 容器自动升级管理控制器
/// </summary>
[ApiController]
[Route("api/auto-update")]
public class AutoUpdateController : ControllerBase
{
    private readonly IAutoUpdateService _autoUpdateService;
    private readonly ILogger<AutoUpdateController> _logger;

    public AutoUpdateController(IAutoUpdateService autoUpdateService, ILogger<AutoUpdateController> logger)
    {
        _autoUpdateService = autoUpdateService;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有自动升级配置
    /// </summary>
    [HttpGet("configs")]
    public async Task<ActionResult<List<ContainerAutoUpdateConfig>>> GetAllConfigs()
    {
        try
        {
            var configs = await _autoUpdateService.GetAllConfigsAsync();
            return Ok(configs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取自动升级配置失败");
            return StatusCode(500, new { error = "获取自动升级配置失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 获取容器的自动升级配置
    /// </summary>
    [HttpGet("configs/{containerId}")]
    public async Task<ActionResult<ContainerAutoUpdateConfig>> GetConfig(string containerId)
    {
        try
        {
            var config = await _autoUpdateService.GetConfigAsync(containerId);
            if (config == null)
            {
                return Ok(new ContainerAutoUpdateConfig
                {
                    ContainerId = containerId,
                    EnableUpdateCheck = true,
                    EnableAutoPull = false,
                    EnableAutoRestart = false,
                    CheckIntervalHours = 6,
                    Status = AutoUpdateStatus.Disabled
                });
            }
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取容器 {ContainerId} 的自动升级配置失败", containerId);
            return StatusCode(500, new { error = "获取配置失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 设置容器的自动升级配置
    /// </summary>
    [HttpPut("configs/{containerId}")]
    public async Task<ActionResult<ContainerAutoUpdateConfig>> SetConfig(string containerId, [FromBody] ContainerAutoUpdateConfig config)
    {
        try
        {
            var result = await _autoUpdateService.SetConfigAsync(containerId, config);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置容器 {ContainerId} 的自动升级配置失败", containerId);
            return StatusCode(500, new { error = "设置配置失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 删除容器的自动升级配置
    /// </summary>
    [HttpDelete("configs/{containerId}")]
    public async Task<ActionResult> DeleteConfig(string containerId)
    {
        try
        {
            await _autoUpdateService.DeleteConfigAsync(containerId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除容器 {ContainerId} 的自动升级配置失败", containerId);
            return StatusCode(500, new { error = "删除配置失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 检查容器的镜像更新
    /// </summary>
    [HttpPost("check/{containerId}")]
    public async Task<ActionResult<ImageUpdateCheckResult>> CheckUpdate(string containerId)
    {
        try
        {
            var result = await _autoUpdateService.CheckUpdateAsync(containerId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查容器 {ContainerId} 的镜像更新失败", containerId);
            return StatusCode(500, new { error = "检查更新失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 检查所有容器的镜像更新
    /// </summary>
    [HttpPost("check-all")]
    public async Task<ActionResult<List<ImageUpdateCheckResult>>> CheckAllUpdates()
    {
        try
        {
            var results = await _autoUpdateService.CheckAllUpdatesAsync();
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查所有容器更新失败");
            return StatusCode(500, new { error = "检查更新失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 获取有可用更新的容器列表
    /// </summary>
    [HttpGet("available-updates")]
    public async Task<ActionResult<List<ContainerAutoUpdateConfig>>> GetAvailableUpdates()
    {
        try
        {
            var configs = await _autoUpdateService.GetContainersWithUpdatesAsync();
            return Ok(configs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取可用更新列表失败");
            return StatusCode(500, new { error = "获取可用更新失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 更新容器（拉取新镜像并可选重启）
    /// </summary>
    [HttpPost("update/{containerId}")]
    public async Task<ActionResult<UpdateResult>> UpdateContainer(string containerId, [FromQuery] bool pullOnly = false)
    {
        try
        {
            var result = await _autoUpdateService.UpdateContainerAsync(containerId, pullOnly);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新容器 {ContainerId} 失败", containerId);
            return StatusCode(500, new { error = "更新容器失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 获取全局设置
    /// </summary>
    [HttpGet("settings")]
    public async Task<ActionResult<GlobalAutoUpdateSettings>> GetGlobalSettings()
    {
        try
        {
            var settings = await _autoUpdateService.GetGlobalSettingsAsync();
            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取全局设置失败");
            return StatusCode(500, new { error = "获取全局设置失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 设置全局设置
    /// </summary>
    [HttpPut("settings")]
    public async Task<ActionResult<GlobalAutoUpdateSettings>> SetGlobalSettings([FromBody] GlobalAutoUpdateSettings settings)
    {
        try
        {
            var result = await _autoUpdateService.SetGlobalSettingsAsync(settings);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置全局设置失败");
            return StatusCode(500, new { error = "设置全局设置失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 获取镜像的所有可用标签
    /// </summary>
    [HttpGet("image-tags")]
    public async Task<ActionResult<List<string>>> GetImageTags([FromQuery] string imageName)
    {
        try
        {
            var tags = await _autoUpdateService.GetImageTagsAsync(imageName);
            return Ok(tags);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取镜像标签失败: {ImageName}", imageName);
            return StatusCode(500, new { error = "获取镜像标签失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 回滚容器到指定镜像版本
    /// </summary>
    [HttpPost("rollback/{containerId}")]
    public async Task<ActionResult<UpdateResult>> RollbackContainer(string containerId, [FromQuery] string targetTag)
    {
        try
        {
            if (string.IsNullOrEmpty(targetTag))
            {
                return BadRequest(new { error = "目标标签不能为空" });
            }
            
            var result = await _autoUpdateService.RollbackContainerAsync(containerId, targetTag);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "回滚容器 {ContainerId} 失败", containerId);
            return StatusCode(500, new { error = "回滚容器失败", message = ex.Message });
        }
    }
}
