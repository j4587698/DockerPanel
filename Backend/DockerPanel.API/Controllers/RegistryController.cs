using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using DockerPanel.API.Models;
using DockerPanel.API.Services;
using Microsoft.AspNetCore.Authorization;

namespace DockerPanel.API.Controllers;

/// <summary>
/// 镜像仓库管理控制器
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/registries")]
[Produces("application/json")]
[Authorize(Roles = AuthRoles.Admin)]
public class RegistryController : ControllerBase
{
    private readonly IRegistryService _registryService;
    private readonly ILogger<RegistryController> _logger;
    private readonly ILocalizationService _localization;

    public RegistryController(IRegistryService registryService, ILogger<RegistryController> logger, ILocalizationService localization)
    {
        _registryService = registryService;
        _logger = logger;
        _localization = localization;
    }

    /// <summary>
    /// 获取所有镜像仓库
    /// </summary>
    /// <returns>镜像仓库列表</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ImageRegistry>>> GetRegistries()
    {
        try
        {
            var registries = await _registryService.GetRegistriesAsync();
            return Ok(registries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取镜像仓库列表失败");
            return StatusCode(500, new { error = _localization.GetMessage("registry.listFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 按类型获取镜像仓库
    /// </summary>
    /// <param name="type">仓库类型：Private=私有仓库，Mirror=镜像加速器</param>
    /// <returns>镜像仓库列表</returns>
    [HttpGet("by-type/{type}")]
    public async Task<ActionResult<IEnumerable<ImageRegistry>>> GetRegistriesByType(RegistryType type)
    {
        try
        {
            var registries = await _registryService.GetRegistriesAsync();
            var filtered = registries.Where(r => r.RegistryType == type);
            return Ok(filtered);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取镜像仓库列表失败");
            return StatusCode(500, new { error = _localization.GetMessage("registry.listFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 获取所有镜像加速器
    /// </summary>
    /// <returns>镜像加速器列表</returns>
    [HttpGet("mirrors")]
    public async Task<ActionResult<IEnumerable<ImageRegistry>>> GetMirrors()
    {
        try
        {
            var registries = await _registryService.GetRegistriesAsync();
            var mirrors = registries.Where(r => r.RegistryType == RegistryType.Mirror);
            return Ok(mirrors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取镜像加速器列表失败");
            return StatusCode(500, new { error = _localization.GetMessage("registry.mirrorListFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 获取所有私有仓库
    /// </summary>
    /// <returns>私有仓库列表</returns>
    [HttpGet("private")]
    public async Task<ActionResult<IEnumerable<ImageRegistry>>> GetPrivateRegistries()
    {
        try
        {
            var registries = await _registryService.GetRegistriesAsync();
            var privateRegistries = registries.Where(r => r.RegistryType == RegistryType.Private);
            return Ok(privateRegistries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取私有仓库列表失败");
            return StatusCode(500, new { error = _localization.GetMessage("registry.privateListFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 根据ID获取镜像仓库
    /// </summary>
    /// <param name="id">仓库ID</param>
    /// <returns>镜像仓库详情</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<ImageRegistry>> GetRegistry(string id)
    {
        try
        {
            var registry = await _registryService.GetRegistryByIdAsync(id);
            if (registry == null)
            {
                return NotFound(new { error = _localization.GetMessage("registry.notFound"), message = $"仓库ID {id} 不存在" });
            }
            return Ok(registry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取镜像仓库详情失败: {RegistryId}", id);
            return StatusCode(500, new { error = _localization.GetMessage("registry.detailFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 创建镜像仓库
    /// </summary>
    /// <param name="request">创建仓库请求</param>
    /// <returns>创建的镜像仓库</returns>
    [HttpPost]
    public async Task<ActionResult<ImageRegistry>> CreateRegistry([FromBody] CreateRegistryRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var registry = await _registryService.CreateRegistryAsync(request);
            return CreatedAtAction(nameof(GetRegistry), new { id = registry.Id }, registry);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = _localization.GetMessage("error.invalidParams"), message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建镜像仓库失败: {Name}", request.Name);
            return StatusCode(500, new { error = _localization.GetMessage("registry.createFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 更新镜像仓库
    /// </summary>
    /// <param name="id">仓库ID</param>
    /// <param name="request">更新仓库请求</param>
    /// <returns>更新的镜像仓库</returns>
    [HttpPut("{id}")]
    public async Task<ActionResult<ImageRegistry>> UpdateRegistry(string id, [FromBody] UpdateRegistryRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var registry = await _registryService.UpdateRegistryAsync(id, request);
            return Ok(registry);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = _localization.GetMessage("error.invalidParams"), message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新镜像仓库失败: {RegistryId}", id);
            return StatusCode(500, new { error = _localization.GetMessage("registry.updateFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 删除镜像仓库
    /// </summary>
    /// <param name="id">仓库ID</param>
    /// <returns>删除结果</returns>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteRegistry(string id)
    {
        try
        {
            var result = await _registryService.DeleteRegistryAsync(id);
            if (!result)
            {
                return NotFound(new { error = _localization.GetMessage("registry.notFound"), message = $"仓库ID {id} 不存在" });
            }
            return Ok(new { message = _localization.GetMessage("registry.deleteSuccess") });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = _localization.GetMessage("error.invalidOperation"), message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除镜像仓库失败: {RegistryId}", id);
            return StatusCode(500, new { error = _localization.GetMessage("registry.deleteFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 测试仓库连接
    /// </summary>
    /// <param name="id">仓库ID</param>
    /// <returns>连接测试结果</returns>
    [HttpPost("{id}/test")]
    public async Task<ActionResult<RegistryTestResult>> TestRegistryConnection(string id)
    {
        try
        {
            var result = await _registryService.TestRegistryConnectionAsync(id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试仓库连接失败: {RegistryId}", id);
            return StatusCode(500, new { error = _localization.GetMessage("registry.testFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 测试仓库配置连接（无需保存）
    /// </summary>
    /// <param name="request">仓库配置</param>
    /// <returns>连接测试结果</returns>
    [HttpPost("test-config")]
    public async Task<ActionResult<RegistryTestResult>> TestRegistryConfig([FromBody] TestRegistryConfigRequest request)
    {
        try
        {
            var result = await _registryService.TestRegistryConfigAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试仓库配置连接失败");
            return StatusCode(500, new { error = _localization.GetMessage("registry.testConfigFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 搜索仓库镜像
    /// </summary>
    [HttpPost("{id}/search")]
    public async Task<ActionResult> SearchRegistryImages(string id, [FromBody] RegistrySearchRequest request)
    {
        try
        {
            var result = await _registryService.SearchRegistryImagesAsync(
                id, 
                request.Query ?? "", 
                request.Limit > 0 ? request.Limit : 20, 
                request.Offset > 0 ? request.Offset : 0
            );
            return Ok(new
            {
                results = result.Results,
                total = result.Total,
                query = result.Query,
                registryId = result.RegistryId,
                registryName = result.RegistryName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "搜索仓库镜像失败: {RegistryId}", id);
            return StatusCode(500, new { error = _localization.GetMessage("registry.searchFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 设置默认仓库
    /// </summary>
    /// <param name="id">仓库ID</param>
    /// <returns>设置结果</returns>
    [HttpPost("{id}/set-default")]
    public async Task<ActionResult> SetDefaultRegistry(string id)
    {
        try
        {
            var result = await _registryService.SetDefaultRegistryAsync(id);
            if (!result)
            {
                return NotFound(new { error = _localization.GetMessage("registry.notFound"), message = $"仓库ID {id} 不存在" });
            }
            return Ok(new { message = _localization.GetMessage("registry.setDefaultSuccess") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置默认仓库失败: {RegistryId}", id);
            return StatusCode(500, new { error = _localization.GetMessage("registry.setDefaultFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 登录到私有仓库
    /// </summary>
    /// <param name="id">仓库ID</param>
    /// <param name="request">登录请求</param>
    /// <returns>登录结果</returns>
    [HttpPost("{id}/login")]
    public async Task<ActionResult> LoginToRegistry(string id, [FromBody] RegistryLoginRequest? request = null)
    {
        try
        {
            var result = await _registryService.LoginToRegistryAsync(id, request?.Username, request?.Password);
            return Ok(new { success = result, message = result ? _localization.GetMessage("registry.loginSuccess") : _localization.GetMessage("registry.loginFailed") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登录私有仓库失败: {RegistryId}", id);
            return StatusCode(500, new { error = _localization.GetMessage("registry.loginFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 从私有仓库登出
    /// </summary>
    /// <param name="id">仓库ID</param>
    /// <returns>登出结果</returns>
    [HttpPost("{id}/logout")]
    public async Task<ActionResult> LogoutFromRegistry(string id)
    {
        try
        {
            var result = await _registryService.LogoutFromRegistryAsync(id);
            return Ok(new { success = result, message = result ? _localization.GetMessage("registry.logoutSuccess") : _localization.GetMessage("registry.logoutFailed") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从私有仓库登出失败: {RegistryId}", id);
            return StatusCode(500, new { error = _localization.GetMessage("registry.logoutFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 验证仓库认证信息
    /// </summary>
    /// <param name="id">仓库ID</param>
    /// <returns>认证验证结果</returns>
    [HttpPost("{id}/validate-auth")]
    public async Task<ActionResult<RegistryAuthResult>> ValidateRegistryAuth(string id)
    {
        try
        {
            var result = await _registryService.ValidateRegistryAuthAsync(id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证仓库认证失败: {RegistryId}", id);
            return StatusCode(500, new { error = _localization.GetMessage("registry.validateAuthFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 同步仓库镜像信息
    /// </summary>
    /// <param name="id">仓库ID</param>
    /// <returns>同步结果</returns>
    [HttpPost("{id}/sync")]
    public async Task<ActionResult<RegistrySyncResult>> SyncRegistryImages(string id)
    {
        try
        {
            var result = await _registryService.SyncRegistryImagesAsync(id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步仓库镜像信息失败: {RegistryId}", id);
            return StatusCode(500, new { error = _localization.GetMessage("registry.syncFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 获取仓库统计数据
    /// </summary>
    /// <param name="id">仓库ID，可选</param>
    /// <returns>统计数据</returns>
    [HttpGet("statistics")]
    public async Task<ActionResult<RegistryStatistics>> GetRegistryStatistics([FromQuery] string? id = null)
    {
        try
        {
            var statistics = await _registryService.GetRegistryStatisticsAsync(id);
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取仓库统计数据失败: {RegistryId}", id);
            return StatusCode(500, new { error = _localization.GetMessage("registry.statisticsFailed"), message = ex.Message });
        }
    }
}

/// <summary>
/// 仓库登录请求
/// </summary>
public class RegistryLoginRequest
{
    /// <summary>
    /// 用户名
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// 密码
    /// </summary>
    public string? Password { get; set; }
}