using Microsoft.AspNetCore.Mvc;
using DockerPanel.API.Models;
using DockerPanel.API.Services;
using DockerPanel.API.Services.Acme;
using DockerPanel.API.Models.Acme;

namespace DockerPanel.API.Controllers;

/// <summary>
/// 反向代理(YARP)管理控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProxyController : ControllerBase
{
    private readonly IReverseProxyFactory _proxyFactory;
    private readonly IAcmeService _acmeService;
    private readonly ILogger<ProxyController> _logger;
    private readonly ILocalizationService _localization;

    public ProxyController(IReverseProxyFactory proxyFactory, IAcmeService acmeService, ILogger<ProxyController> logger, ILocalizationService localization)
    {
        _proxyFactory = proxyFactory;
        _acmeService = acmeService;
        _logger = logger;
        _localization = localization;
    }

    /// <summary>
    /// 获取当前代理配置
    /// </summary>
    [HttpGet("config")]
    public ActionResult<YarpProxyConfig> GetConfig()
    {
        try
        {
            var config = _proxyFactory.GetConfig();
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取代理配置失败");
            return StatusCode(500, new { message = _localization.GetMessage("proxy.configFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 重新加载配置
    /// </summary>
    [HttpPost("reload")]
    public async Task<ActionResult> ReloadConfig()
    {
        try
        {
            await _proxyFactory.ReloadConfigAsync();
            return Ok(new { message = _localization.GetMessage("proxy.reloadSuccess") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重新加载配置失败");
            return StatusCode(500, new { message = _localization.GetMessage("proxy.reloadFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 添加路由
    /// </summary>
    [HttpPost("routes")]
    public async Task<ActionResult> AddRoute([FromBody] ProxyRouteConfig route)
    {
        try
        {
            var success = await _proxyFactory.AddRouteAsync(route);
            if (success)
                return Ok(new { message = _localization.GetMessage("proxy.routeAddSuccess") });
            return BadRequest(new { message = _localization.GetMessage("proxy.routeAddFailed") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加路由失败");
            return StatusCode(500, new { message = _localization.GetMessage("proxy.routeAddFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 更新路由
    /// </summary>
    [HttpPut("routes/{routeId}")]
    public async Task<ActionResult> UpdateRoute(string routeId, [FromBody] ProxyRouteConfig route)
    {
        try
        {
            if (route.RouteId != routeId)
                return BadRequest(new { message = _localization.GetMessage("proxy.routeIdMismatch") });

            var success = await _proxyFactory.UpdateRouteAsync(route);
            if (success)
                return Ok(new { message = _localization.GetMessage("proxy.routeUpdateSuccess") });
            return BadRequest(new { message = _localization.GetMessage("proxy.routeUpdateFailed") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新路由失败");
            return StatusCode(500, new { message = _localization.GetMessage("proxy.routeUpdateFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 删除路由
    /// </summary>
    [HttpDelete("routes/{routeId}")]
    public async Task<ActionResult> RemoveRoute(string routeId)
    {
        try
        {
            var success = await _proxyFactory.RemoveRouteAsync(routeId);
            if (success)
                return Ok(new { message = _localization.GetMessage("proxy.routeDeleteSuccess") });
            return NotFound(new { message = _localization.GetMessage("proxy.routeNotFound") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除路由失败");
            return StatusCode(500, new { message = _localization.GetMessage("proxy.routeDeleteFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 添加集群
    /// </summary>
    [HttpPost("clusters")]
    public async Task<ActionResult> AddCluster([FromBody] ProxyClusterConfig cluster)
    {
        try
        {
            var success = await _proxyFactory.AddClusterAsync(cluster);
            if (success)
                return Ok(new { message = _localization.GetMessage("proxy.clusterAddSuccess") });
            return BadRequest(new { message = _localization.GetMessage("proxy.clusterAddFailed") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加集群失败");
            return StatusCode(500, new { message = _localization.GetMessage("proxy.clusterAddFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 更新集群
    /// </summary>
    [HttpPut("clusters/{clusterId}")]
    public async Task<ActionResult> UpdateCluster(string clusterId, [FromBody] ProxyClusterConfig cluster)
    {
        try
        {
            if (cluster.ClusterId != clusterId)
                return BadRequest(new { message = _localization.GetMessage("proxy.clusterIdMismatch") });

            var success = await _proxyFactory.UpdateClusterAsync(cluster);
            if (success)
                return Ok(new { message = _localization.GetMessage("proxy.clusterUpdateSuccess") });
            return BadRequest(new { message = _localization.GetMessage("proxy.clusterUpdateFailed") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新集群失败");
            return StatusCode(500, new { message = _localization.GetMessage("proxy.clusterUpdateFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 删除集群
    /// </summary>
    [HttpDelete("clusters/{clusterId}")]
    public async Task<ActionResult> RemoveCluster(string clusterId)
    {
        try
        {
            var success = await _proxyFactory.RemoveClusterAsync(clusterId);
            if (success)
                return Ok(new { message = _localization.GetMessage("proxy.clusterDeleteSuccess") });
            return NotFound(new { message = _localization.GetMessage("proxy.clusterNotFound") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除集群失败");
            return StatusCode(500, new { message = _localization.GetMessage("proxy.clusterDeleteFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 添加域名映射
    /// </summary>
    [HttpPost("mappings")]
    public async Task<ActionResult> AddDomainMapping([FromBody] DomainMapping mapping)
    {
        try
        {
            // 处理自动申请证书
            string? certificateId = mapping.CertificateId;
            bool sslEnabled = mapping.EnableSsl;
            string? certificateMessage = null;

            if (mapping.AutoRequestCertificate && !string.IsNullOrEmpty(mapping.Domain))
            {
                try
                {
                    AcmeAccount? account = null;

                    // 优先使用指定的账户ID
                    if (!string.IsNullOrEmpty(mapping.AccountId))
                    {
                        var allAccounts = await _acmeService.GetAccountsAsync();
                        account = allAccounts.FirstOrDefault(a => a.Id == mapping.AccountId);
                        if (account == null)
                        {
                            _logger.LogWarning("指定的ACME账户不存在: {AccountId}", mapping.AccountId);
                        }
                    }

                    // 如果没有指定账户或指定的账户不存在，获取第一个可用账户
                    if (account == null)
                    {
                        var accounts = await _acmeService.GetAccountsAsync();
                        account = accounts.FirstOrDefault();
                    }

                    if (account == null)
                    {
                        return BadRequest(new { message = _localization.GetMessage("proxy.noAcmeAccount") });
                    }

                    // 启动证书申请流程（异步，在后台执行）
                    var request = new AcmeCertificateRequest
                    {
                        Domains = new List<string> { mapping.Domain },
                        AccountId = account.Id,
                        KeyType = "ECDSA256",
                        UseStaging = false,
                        AcmeProvider = account.Provider == "letsencrypt" ? "letsencrypt" : account.Provider,
                        Metadata = new Dictionary<string, object>
                        {
                            ["autoRequested"] = true,
                            ["challengeType"] = "http-01"
                        },
                        AccountKey = account.AccountKey
                    };

                    var order = await _acmeService.OrderCertificateAsync(request);
                    if (order != null)
                    {
                        certificateMessage = $"证书申请已启动（订单ID: {order.Id}），证书将在域名验证通过后自动颁发。验证完成后请手动绑定证书。";
                        _logger.LogInformation("已启动证书自动申请流程: OrderId={OrderId}, Domain={Domain}, AccountId={AccountId}", 
                            order.Id, mapping.Domain, account.Id);
                    }

                    // 不立即启用 SSL，等证书申请完成后再绑定
                    sslEnabled = false;
                    certificateId = null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "启动证书申请失败: {Domain}", mapping.Domain);
                    return BadRequest(new { message = $"启动证书申请失败: {ex.Message}" });
                }
            }

            // 更新映射配置
            mapping.EnableSsl = sslEnabled;
            mapping.CertificateId = certificateId;

            var success = await _proxyFactory.AddDomainMappingAsync(mapping);
            if (success)
            {
                return Ok(new { 
                    message = certificateMessage ?? _localization.GetMessage("proxy.domainMappingAddSuccess"),
                    certificateRequested = mapping.AutoRequestCertificate && certificateMessage != null
                });
            }
            return BadRequest(new { message = _localization.GetMessage("proxy.domainMappingAddFailed") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加域名映射失败");
            return StatusCode(500, new { message = _localization.GetMessage("proxy.domainMappingAddFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 获取所有域名映射
    /// </summary>
    [HttpGet("mappings")]
    public async Task<ActionResult<IEnumerable<DomainMapping>>> GetDomainMappings()
    {
        try
        {
            // 直接从数据库获取所有映射（包括禁用的）
            var config = await _proxyFactory.BuildConfigFromDatabaseAsync();
            var allMappings = await _proxyFactory.GetAllDomainMappingsAsync();
            return Ok(allMappings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取域名映射失败");
            return StatusCode(500, new { message = _localization.GetMessage("proxy.getMappingsFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 更新域名映射（支持部分更新）
    /// </summary>
    [HttpPut("mappings/{mappingId}")]
    public async Task<ActionResult> UpdateDomainMapping(string mappingId, [FromBody] UpdateDomainMappingRequest request)
    {
        try
        {
            _logger.LogInformation("更新域名映射: mappingId={MappingId}, enabled={Enabled}", mappingId, request.Enabled);
            
            // 先重新从数据库加载配置，确保内存中有最新的映射
            await _proxyFactory.BuildConfigFromDatabaseAsync();
            
            // 获取所有映射
            var allMappings = await _proxyFactory.GetAllDomainMappingsAsync();
            _logger.LogInformation("获取到 {Count} 个映射", allMappings.Count);
            
            var existingMapping = allMappings.FirstOrDefault(m => m.Id == mappingId);
            
            if (existingMapping == null)
            {
                _logger.LogWarning("映射不存在: {MappingId}", mappingId);
                return NotFound(new { message = _localization.GetMessage("proxy.domainMappingNotFound") });
            }

            _logger.LogInformation("找到映射: {Domain}, 当前状态: {CurrentEnabled}, 新状态: {NewEnabled}",
                existingMapping.Domain, existingMapping.Enabled, request.Enabled);

            // 更新字段
            if (!string.IsNullOrWhiteSpace(request.Domain))
                existingMapping.Domain = request.Domain.Trim();
            if (!string.IsNullOrWhiteSpace(request.ContainerId))
                existingMapping.ContainerId = request.ContainerId.Trim();
            if (request.ContainerName != null)
                existingMapping.ContainerName = request.ContainerName.Trim();
            if (!string.IsNullOrWhiteSpace(request.DestinationAddress))
                existingMapping.DestinationAddress = request.DestinationAddress.Trim();
            if (request.ContainerPort.HasValue)
                existingMapping.ContainerPort = Math.Clamp(request.ContainerPort.Value, 1, 65535);
            if (request.PathPrefix != null)
                existingMapping.PathPrefix = string.IsNullOrWhiteSpace(request.PathPrefix) ? "/" : request.PathPrefix.Trim();
            if (!string.IsNullOrWhiteSpace(request.Protocol))
                existingMapping.Protocol = request.Protocol.Trim().ToLowerInvariant();
            if (request.EnableSsl.HasValue)
                existingMapping.EnableSsl = request.EnableSsl.Value;
            if (request.CertificateId != null)
                existingMapping.CertificateId = string.IsNullOrWhiteSpace(request.CertificateId) ? null : request.CertificateId.Trim();
            if (request.AccountId != null)
                existingMapping.AccountId = string.IsNullOrWhiteSpace(request.AccountId) ? null : request.AccountId.Trim();
            if (request.AutoRequestCertificate.HasValue)
                existingMapping.AutoRequestCertificate = request.AutoRequestCertificate.Value;
            if (request.Priority.HasValue)
                existingMapping.Priority = request.Priority.Value;
            if (request.Enabled.HasValue)
                existingMapping.Enabled = request.Enabled.Value;
            
            // 如果显式要求更新高级设置，则直接赋值（允许设为null来清除）
            if (request.UpdateAdvancedSettings == true)
            {
                existingMapping.ActivityTimeoutSeconds = request.ActivityTimeoutSeconds;
                existingMapping.RequestTimeoutSeconds = request.RequestTimeoutSeconds;
                existingMapping.ForceHttps = request.ForceHttps ?? false;
                existingMapping.HttpVersion = request.HttpVersion;
                existingMapping.EnableWebSocketOptimization = request.EnableWebSocketOptimization ?? false;
            }
            
            existingMapping.UpdatedAt = DateTime.UtcNow;
            
            var success = await _proxyFactory.UpdateDomainMappingAsync(existingMapping);
            if (success)
            {
                _logger.LogInformation("映射更新成功: {MappingId}", mappingId);
                return Ok(new { message = _localization.GetMessage("proxy.domainMappingUpdateSuccess") });
            }
            
            _logger.LogWarning("映射更新失败: {MappingId}", mappingId);
            return BadRequest(new { message = _localization.GetMessage("proxy.domainMappingUpdateFailed") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新域名映射失败");
            return StatusCode(500, new { message = _localization.GetMessage("proxy.domainMappingUpdateFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 删除域名映射
    /// </summary>
    [HttpDelete("mappings/{mappingId}")]
    public async Task<ActionResult> RemoveDomainMapping(string mappingId)
    {
        try
        {
            var success = await _proxyFactory.RemoveDomainMappingAsync(mappingId);
            if (success)
                return Ok(new { message = _localization.GetMessage("proxy.domainMappingDeleteSuccess") });
            return NotFound(new { message = _localization.GetMessage("proxy.domainMappingNotFound") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除域名映射失败");
            return StatusCode(500, new { message = _localization.GetMessage("proxy.domainMappingDeleteFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 更新域名映射的证书绑定
    /// </summary>
    [HttpPut("mappings/{mappingId}/certificate")]
    public async Task<ActionResult> UpdateMappingCertificate(string mappingId, [FromBody] UpdateCertificateRequest request)
    {
        try
        {
            var success = await _proxyFactory.UpdateDomainMappingCertificateAsync(mappingId, request.CertificateId);
            if (success)
                return Ok(new { message = _localization.GetMessage("proxy.certificateBindingUpdated") });
            return NotFound(new { message = _localization.GetMessage("proxy.domainMappingNotFound") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新证书绑定失败");
            return StatusCode(500, new { message = _localization.GetMessage("proxy.certificateBindingUpdateFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 获取YARP代理状态
    /// </summary>
    [HttpGet("status")]
    public ActionResult GetYarpStatus()
    {
        try
        {
            var config = _proxyFactory.GetConfig();
            var mappings = _proxyFactory.GetAllDomainMappingsAsync().GetAwaiter().GetResult();

            return Ok(new
            {
                isHealthy = true,
                totalRoutes = config.Routes.Count,
                totalClusters = config.Clusters.Count,
                totalDomainMappings = mappings.Count,
                activeMappings = mappings.Count(m => m.Enabled),
                sslEnabledMappings = mappings.Count(m => m.EnableSsl),
                lastUpdated = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取代理状态失败");
            return StatusCode(500, new { message = _localization.GetMessage("proxy.statusFailed"), error = ex.Message });
        }
    }
}

/// <summary>
/// 更新证书绑定请求
/// </summary>
public class UpdateCertificateRequest
{
    /// <summary>
    /// 证书ID（为null时取消绑定）
    /// </summary>
    public string? CertificateId { get; set; }
}

/// <summary>
/// 更新域名映射请求（支持部分更新）
/// </summary>
public class UpdateDomainMappingRequest
{
    public string? Domain { get; set; }
    public string? ContainerId { get; set; }
    public string? ContainerName { get; set; }
    public string? DestinationAddress { get; set; }
    public int? ContainerPort { get; set; }
    public string? PathPrefix { get; set; }
    public string? Protocol { get; set; }
    public bool? EnableSsl { get; set; }
    public string? CertificateId { get; set; }
    public string? AccountId { get; set; }
    public bool? AutoRequestCertificate { get; set; }
    public int? Priority { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool? Enabled { get; set; }

    public int? ActivityTimeoutSeconds { get; set; }
    public int? RequestTimeoutSeconds { get; set; }
    public bool? ForceHttps { get; set; }
    public string? HttpVersion { get; set; }
    public bool? EnableWebSocketOptimization { get; set; }

    /// <summary>
    /// 标记是否从表单更新，用于处理高级设置的清除操作
    /// </summary>
    public bool? UpdateAdvancedSettings { get; set; }
}