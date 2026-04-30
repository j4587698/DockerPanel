using Microsoft.AspNetCore.Mvc;
using DockerPanel.API.Services;
using DockerPanel.API.Models;

namespace DockerPanel.API.Controllers;

/// <summary>
/// 网络管理控制器
/// </summary>
[ApiController]
[Route("api/network")]
public class NetworkController : ControllerBase
{
    private readonly INetworkService _networkService;
    private readonly ILogger<NetworkController> _logger;
    private readonly ILocalizationService _localization;

    public NetworkController(INetworkService networkService, ILogger<NetworkController> logger, ILocalizationService localization)
    {
        _networkService = networkService;
        _logger = logger;
        _localization = localization;
    }

    /// <summary>
    /// 获取网络列表
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DockerPanel.API.Models.NetworkInfo>>> GetNetworks([FromQuery] string? nodeId = null)
    {
        try
        {
            var networks = await _networkService.GetNetworksAsync(nodeId);
            _logger.LogDebug("获取网络列表: {Count} 个网络", networks?.Count() ?? 0);
            return Ok(networks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取网络列表失败");
            return StatusCode(500, new { error = _localization.GetMessage("network.listFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 根据ID获取网络详情
    /// </summary>
    [HttpGet("{networkId}")]
    public async Task<ActionResult<NetworkDetailInfo>> GetNetwork(string networkId, [FromQuery] string? nodeId = null)
    {
        try
        {
            var network = await _networkService.GetNetworkByIdAsync(networkId, nodeId);
            if (network == null)
            {
                return NotFound(new { error = _localization.GetMessage("network.notFound"), networkId });
            }
            return Ok(network);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取网络详情失败: {NetworkId}", networkId);
            return StatusCode(500, new { error = _localization.GetMessage("network.detailFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 创建网络
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<DockerPanel.API.Models.NetworkInfo>> CreateNetwork([FromBody] CreateNetworkRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var network = await _networkService.CreateNetworkAsync(request);
            return CreatedAtAction(nameof(GetNetwork), new { networkId = network.Id, nodeId = request.NodeId }, network);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "创建网络参数错误: {Name}", request.Name);
            return BadRequest(new { error = ex.Message, name = request.Name });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "创建网络操作失败: {Name}", request.Name);
            return Conflict(new { error = ex.Message, name = request.Name });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建网络失败: {Name}", request.Name);
            return StatusCode(500, new { error = _localization.GetMessage("network.createFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 删除网络
    /// </summary>
    [HttpDelete("{networkId}")]
    public async Task<ActionResult> DeleteNetwork(string networkId, [FromQuery] string? nodeId = null)
    {
        try
        {
            var success = await _networkService.DeleteNetworkAsync(networkId, nodeId);
            if (success)
            {
                return Ok(new { message = _localization.GetMessage("network.deleteSuccess"), networkId });
            }
            else
            {
                return NotFound(new { error = _localization.GetMessage("network.notFound"), networkId });
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "删除网络操作失败: {NetworkId}", networkId);
            return Conflict(new { error = ex.Message, networkId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除网络失败: {NetworkId}", networkId);
            return StatusCode(500, new { error = _localization.GetMessage("network.deleteFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 连接容器到网络
    /// </summary>
    [HttpPost("{networkId}/connect/{containerId}")]
    public async Task<ActionResult> ConnectContainerToNetwork(string networkId, string containerId,
        [FromBody] NetworkConnectionConfig? config = null, [FromQuery] string? nodeId = null)
    {
        try
        {
            var success = await _networkService.ConnectContainerToNetworkAsync(networkId, containerId,
                config != null ? new NetworkConfig
                {
                    Aliases = config.Aliases,
                    IPv4Address = config.IPv4Address,
                    IPv6Address = config.IPv6Address,
                    Links = config.Links
                } : null);
            if (success)
            {
                return Ok(new { message = _localization.GetMessage("network.connectSuccess"), networkId, containerId });
            }
            else
            {
                return BadRequest(new { error = _localization.GetMessage("network.connectFailed"), networkId, containerId });
            }
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "连接容器到网络参数错误: {NetworkId}, {ContainerId}", networkId, containerId);
            return BadRequest(new { error = ex.Message, networkId, containerId });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "连接容器到网络操作失败: {NetworkId}, {ContainerId}", networkId, containerId);
            return Conflict(new { error = ex.Message, networkId, containerId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "连接容器到网络失败: {NetworkId}, {ContainerId}", networkId, containerId);
            return StatusCode(500, new { error = _localization.GetMessage("network.connectFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 断开容器与网络的连接
    /// </summary>
    [HttpPost("{networkId}/disconnect/{containerId}")]
    public async Task<ActionResult> DisconnectContainerFromNetwork(string networkId, string containerId, [FromQuery] string? nodeId = null)
    {
        try
        {
            var success = await _networkService.DisconnectContainerFromNetworkAsync(networkId, containerId);
            if (success)
            {
                return Ok(new { message = _localization.GetMessage("network.disconnectSuccess"), networkId, containerId });
            }
            else
            {
                return BadRequest(new { error = _localization.GetMessage("network.disconnectFailed"), networkId, containerId });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "断开容器与网络的连接失败: {NetworkId}, {ContainerId}", networkId, containerId);
            return StatusCode(500, new { error = _localization.GetMessage("network.disconnectFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 获取网络中的容器列表
    /// </summary>
    [HttpGet("{networkId}/containers")]
    public async Task<ActionResult<IEnumerable<NetworkContainerInfo>>> GetNetworkContainers(string networkId, [FromQuery] string? nodeId = null)
    {
        try
        {
            var containers = await _networkService.GetNetworkContainersAsync(networkId, nodeId);
            return Ok(containers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取网络容器列表失败: {NetworkId}", networkId);
            return StatusCode(500, new { error = _localization.GetMessage("network.containersFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 清理未使用的网络
    /// </summary>
    [HttpPost("prune")]
    public async Task<ActionResult<NetworkPruneResult>> PruneNetworks([FromBody] PruneNetworksRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var networksDeleted = await _networkService.PruneNetworksAsync();
            var result = new NetworkPruneResult
            {
                NetworksDeleted = networksDeleted,
                SpaceReclaimed = 0,
                Success = true
            };
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理网络失败");
            return StatusCode(500, new { error = _localization.GetMessage("network.pruneFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 获取网络统计信息
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult<NetworkStatistics>> GetNetworkStatistics([FromQuery] string? nodeId = null)
    {
        try
        {
            var statistics = await _networkService.GetNetworkStatisticsAsync(nodeId);
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取网络统计信息失败");
            return StatusCode(500, new { error = _localization.GetMessage("network.statisticsFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 检查网络是否存在
    /// </summary>
    [HttpGet("{networkId}/exists")]
    public async Task<ActionResult<bool>> NetworkExists(string networkId, [FromQuery] string? nodeId = null)
    {
        try
        {
            var exists = await _networkService.NetworkExistsAsync(networkId, nodeId);
            return Ok(exists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查网络是否存在失败: {NetworkId}", networkId);
            return StatusCode(500, new { error = _localization.GetMessage("network.existsCheckFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 获取网络的IPAM信息
    /// </summary>
    [HttpGet("{networkId}/ipam")]
    public async Task<ActionResult<NetworkIpamInfo>> GetNetworkIpamInfo(string networkId, [FromQuery] string? nodeId = null)
    {
        try
        {
            var ipamInfo = await _networkService.GetNetworkIpamInfoAsync(networkId, nodeId);
            return Ok(ipamInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取网络IPAM信息失败: {NetworkId}", networkId);
            return StatusCode(500, new { error = _localization.GetMessage("network.ipamFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 更新网络配置
    /// </summary>
    [HttpPut("{networkId}")]
    public async Task<ActionResult<DockerPanel.API.Models.NetworkInfo>> UpdateNetwork(string networkId, [FromBody] UpdateNetworkRequest request, [FromQuery] string? nodeId = null)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var success = await _networkService.UpdateNetworkAsync(networkId, request);
            if (!success)
            {
                return NotFound(new { error = _localization.GetMessage("network.notFound"), networkId });
            }

            // 获取更新后的网络信息
            var network = await _networkService.GetNetworkByIdAsync(networkId, nodeId);
            return Ok(network);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "更新网络配置参数错误: {NetworkId}", networkId);
            return BadRequest(new { error = ex.Message, networkId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新网络配置失败: {NetworkId}", networkId);
            return StatusCode(500, new { error = _localization.GetMessage("network.updateFailed"), message = ex.Message });
        }
    }
}

/// <summary>
/// 清理网络请求
/// </summary>
public class PruneNetworksRequest
{
    public bool Filters { get; set; } = false;
    public string? LabelFilter { get; set; }
    public string? NodeId { get; set; }
}