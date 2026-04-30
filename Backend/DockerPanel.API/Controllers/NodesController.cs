using Microsoft.AspNetCore.Mvc;
using DockerPanel.API.Models;
using DockerPanel.API.Services;
using Microsoft.AspNetCore.Authorization;

namespace DockerPanel.API.Controllers;

/// <summary>
/// 节点管理控制器
/// </summary>
[ApiController]
[Route("api/nodes")]
public class NodesController : ControllerBase
{
    private readonly INodeService _nodeService;
    private readonly ILogger<NodesController> _logger;
    private readonly ILocalizationService _localization;

    public NodesController(INodeService nodeService, ILogger<NodesController> logger, ILocalizationService localization)
    {
        _nodeService = nodeService;
        _logger = logger;
        _localization = localization;
    }

    /// <summary>
    /// 获取节点列表
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<NodeInfo>>> GetNodes([FromQuery] int? page = null, [FromQuery] int? pageSize = null)
    {
        try
        {
            var nodes = await _nodeService.GetNodesAsync();
            return Ok(nodes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取节点列表失败");
            return StatusCode(500, new { message = _localization.GetMessage("node.listFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 根据ID获取节点
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<NodeInfo>> GetNode(string id)
    {
        try
        {
            var node = await _nodeService.GetNodeAsync(id);
            if (node == null)
            {
                return NotFound(new { message = _localization.GetMessage("node.notFound") });
            }
            return Ok(node);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取节点 {Id} 失败", id);
            return StatusCode(500, new { message = _localization.GetMessage("node.getFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 添加节点
    /// </summary>
    [HttpPost]
    [Authorize(Roles = AuthRoles.Admin)]
    public async Task<ActionResult<string>> AddNode([FromBody] AddNodeRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var nodeId = await _nodeService.AddNodeAsync(request);
            return CreatedAtAction(nameof(GetNode), new { id = nodeId }, nodeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加节点失败");
            return StatusCode(500, new { message = _localization.GetMessage("node.addFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 更新节点
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = AuthRoles.Admin)]
    public async Task<IActionResult> UpdateNode(string id, [FromBody] UpdateNodeRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            await _nodeService.UpdateNodeAsync(id, request);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新节点 {Id} 失败", id);
            return StatusCode(500, new { message = _localization.GetMessage("node.updateFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 删除节点
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = AuthRoles.Admin)]
    public async Task<IActionResult> DeleteNode(string id)
    {
        try
        {
            await _nodeService.RemoveNodeAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "删除节点 {Id} 被拒绝", id);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除节点 {Id} 失败", id);
            return StatusCode(500, new { message = _localization.GetMessage("node.deleteFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 测试节点连接
    /// </summary>
    [HttpPost("{id}/test-connection")]
    [Authorize(Roles = AuthRoles.Admin)]
    public async Task<ActionResult<bool>> TestNodeConnection(string id)
    {
        try
        {
            var result = await _nodeService.TestNodeConnectionAsync(id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试节点 {Id} 连接失败", id);
            return StatusCode(500, new { message = _localization.GetMessage("node.testConnectionFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 测试连接参数（不保存节点）
    /// </summary>
    [HttpPost("test-connection")]
    [Authorize(Roles = AuthRoles.Admin)]
    public async Task<ActionResult<TestNodeConnectionResult>> TestConnection([FromBody] TestNodeConnectionRequest request)
    {
        try
        {
            var result = await _nodeService.TestConnectionAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试连接失败");
            return StatusCode(500, new { message = "连接测试失败", error = ex.Message });
        }
    }

    /// <summary>
    /// 获取节点统计信息
    /// </summary>
    [HttpGet("{id}/stats")]
    public async Task<ActionResult<NodeStats>> GetNodeStats(string id)
    {
        try
        {
            var stats = await _nodeService.GetNodeStatsAsync(id);
            if (stats == null)
            {
                return NotFound(new { message = _localization.GetMessage("node.notFound") });
            }
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取节点 {Id} 统计信息失败", id);
            return StatusCode(500, new { message = _localization.GetMessage("node.statsFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 获取节点详细信息
    /// </summary>
    [HttpGet("{id}/info")]
    public async Task<ActionResult<NodeInfo>> GetNodeInfo(string id)
    {
        try
        {
            var info = await _nodeService.GetNodeInfoAsync(id);
            if (info == null)
            {
                return NotFound(new { message = _localization.GetMessage("node.notFound") });
            }
            return Ok(info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取节点 {Id} 详细信息失败", id);
            return StatusCode(500, new { message = _localization.GetMessage("node.detailFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 获取节点健康状态
    /// </summary>
    [HttpGet("{id}/health")]
    public async Task<ActionResult<NodeHealthStatus>> GetNodeHealthStatus(string id)
    {
        try
        {
            var healthStatus = await _nodeService.GetNodeHealthStatusAsync(id);
            return Ok(healthStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取节点 {Id} 健康状态失败", id);
            return StatusCode(500, new { message = _localization.GetMessage("node.healthFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 获取默认节点
    /// </summary>
    [HttpGet("default")]
    public async Task<ActionResult<NodeInfo>> GetDefaultNode()
    {
        try
        {
            var node = await _nodeService.GetDefaultNodeAsync();
            if (node == null)
            {
                return NotFound(new { message = "未设置默认节点" });
            }
            return Ok(node);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取默认节点失败");
            return StatusCode(500, new { message = "获取默认节点失败", error = ex.Message });
        }
    }

    /// <summary>
    /// 设置默认节点
    /// </summary>
    [HttpPost("{id}/set-default")]
    [Authorize(Roles = AuthRoles.Admin)]
    public async Task<IActionResult> SetDefaultNode(string id)
    {
        try
        {
            await _nodeService.SetDefaultNodeAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置默认节点失败");
            return StatusCode(500, new { message = "设置默认节点失败", error = ex.Message });
        }
    }

    /// <summary>
    /// 批量节点操作
    /// </summary>
    [HttpPost("batch")]
    [Authorize(Roles = AuthRoles.Admin)]
    public async Task<ActionResult> BatchOperation([FromBody] BatchNodeOperationRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var results = new List<object>();

            foreach (var nodeId in request.NodeIds)
            {
                try
                {
                    switch (request.Operation.ToLower())
                    {
                        case "test-connection":
                            var isConnected = await _nodeService.TestNodeConnectionAsync(nodeId);
                            results.Add(new { NodeId = nodeId, Success = true, Connected = isConnected });
                            break;
                        case "remove":
                            await _nodeService.RemoveNodeAsync(nodeId);
                            results.Add(new { NodeId = nodeId, Success = true });
                            break;
                        default:
                            results.Add(new { NodeId = nodeId, Success = false, Error = _localization.GetMessage("node.unsupportedOperation") });
                            break;
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new { NodeId = nodeId, Success = false, Error = ex.Message });
                }
            }

            return Ok(new { Results = results });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量节点操作失败");
            return StatusCode(500, new { message = _localization.GetMessage("node.batchOperationFailed"), error = ex.Message });
        }
    }

    #region 分组管理

    /// <summary>
    /// 获取所有分组
    /// </summary>
    [HttpGet("groups")]
    public async Task<ActionResult<IEnumerable<DockerPanel.API.Models.NodeGroup>>> GetGroups()
    {
        try
        {
            var groups = await _nodeService.GetGroupsAsync();
            return Ok(groups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取节点分组失败");
            return StatusCode(500, new { message = "获取节点分组失败", error = ex.Message });
        }
    }

    /// <summary>
    /// 获取分组详情
    /// </summary>
    [HttpGet("groups/{id}")]
    public async Task<ActionResult<DockerPanel.API.Models.NodeGroup>> GetGroup(string id)
    {
        try
        {
            var group = await _nodeService.GetGroupAsync(id);
            if (group == null)
            {
                return NotFound(new { message = "分组不存在" });
            }
            return Ok(group);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取分组 {Id} 失败", id);
            return StatusCode(500, new { message = "获取分组失败", error = ex.Message });
        }
    }

    /// <summary>
    /// 创建分组
    /// </summary>
    [HttpPost("groups")]
    [Authorize(Roles = AuthRoles.Admin)]
    public async Task<ActionResult<DockerPanel.API.Models.NodeGroup>> CreateGroup([FromBody] DockerPanel.API.Models.CreateNodeGroupRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var group = await _nodeService.CreateGroupAsync(request);
            return CreatedAtAction(nameof(GetGroup), new { id = group.Id }, group);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建分组失败");
            return StatusCode(500, new { message = "创建分组失败", error = ex.Message });
        }
    }

    /// <summary>
    /// 更新分组
    /// </summary>
    [HttpPut("groups/{id}")]
    [Authorize(Roles = AuthRoles.Admin)]
    public async Task<IActionResult> UpdateGroup(string id, [FromBody] DockerPanel.API.Models.UpdateNodeGroupRequest request)
    {
        try
        {
            await _nodeService.UpdateGroupAsync(id, request);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新分组 {Id} 失败", id);
            return StatusCode(500, new { message = "更新分组失败", error = ex.Message });
        }
    }

    /// <summary>
    /// 删除分组
    /// </summary>
    [HttpDelete("groups/{id}")]
    [Authorize(Roles = AuthRoles.Admin)]
    public async Task<IActionResult> DeleteGroup(string id)
    {
        try
        {
            await _nodeService.DeleteGroupAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除分组 {Id} 失败", id);
            return StatusCode(500, new { message = "删除分组失败", error = ex.Message });
        }
    }

    #endregion
}