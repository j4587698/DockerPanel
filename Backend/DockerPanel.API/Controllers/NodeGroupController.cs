using Microsoft.AspNetCore.Mvc;
using DockerPanel.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace DockerPanel.API.Controllers;

/// <summary>
/// 节点分组和标签管理控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = DockerPanel.API.Models.AuthRoles.Admin)]
public class NodeGroupController : ControllerBase
{
    private readonly INodeGroupService _nodeGroupService;
    private readonly ILogger<NodeGroupController> _logger;
    private readonly ILocalizationService _localization;

    public NodeGroupController(INodeGroupService nodeGroupService, ILogger<NodeGroupController> logger, ILocalizationService localization)
    {
        _nodeGroupService = nodeGroupService;
        _logger = logger;
        _localization = localization;
    }

    #region 节点分组管理

    /// <summary>
    /// 获取所有节点分组
    /// </summary>
    [HttpGet("groups")]
    public async Task<ActionResult<IEnumerable<NodeGroup>>> GetNodeGroups()
    {
        try
        {
            var groups = await _nodeGroupService.GetNodeGroupsAsync();
            return Ok(groups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取节点分组失败");
            return StatusCode(500, new { message = _localization.GetMessage("nodeGroup.listFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 根据ID获取节点分组
    /// </summary>
    [HttpGet("groups/{id}")]
    public async Task<ActionResult<NodeGroup>> GetNodeGroup(string id)
    {
        try
        {
            var group = await _nodeGroupService.GetNodeGroupByIdAsync(id);
            if (group == null)
            {
                return NotFound(new { message = _localization.GetMessage("nodeGroup.notFound") });
            }
            return Ok(group);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取节点分组失败: {GroupId}", id);
            return StatusCode(500, new { message = _localization.GetMessage("nodeGroup.listFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 创建节点分组
    /// </summary>
    [HttpPost("groups")]
    public async Task<ActionResult<NodeGroup>> CreateNodeGroup([FromBody] CreateNodeGroupRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var group = await _nodeGroupService.CreateNodeGroupAsync(request);
            return CreatedAtAction(nameof(GetNodeGroup), new { id = group.Id }, group);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建节点分组失败: {GroupName}", request.Name);
            return StatusCode(500, new { message = _localization.GetMessage("nodeGroup.createFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 更新节点分组
    /// </summary>
    [HttpPut("groups/{id}")]
    public async Task<ActionResult<NodeGroup>> UpdateNodeGroup(string id, [FromBody] UpdateNodeGroupRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var group = await _nodeGroupService.UpdateNodeGroupAsync(id, request);
            return Ok(group);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新节点分组失败: {GroupId}", id);
            return StatusCode(500, new { message = _localization.GetMessage("nodeGroup.updateFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 删除节点分组
    /// </summary>
    [HttpDelete("groups/{id}")]
    public async Task<ActionResult> DeleteNodeGroup(string id)
    {
        try
        {
            var success = await _nodeGroupService.DeleteNodeGroupAsync(id);
            if (!success)
            {
                return NotFound(new { message = _localization.GetMessage("nodeGroup.notFound") });
            }

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除节点分组失败: {GroupId}", id);
            return StatusCode(500, new { message = _localization.GetMessage("nodeGroup.deleteFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 将节点添加到分组
    /// </summary>
    [HttpPost("groups/{groupId}/nodes/{nodeId}")]
    public async Task<ActionResult> AddNodeToGroup(string groupId, string nodeId)
    {
        try
        {
            var success = await _nodeGroupService.AddNodeToGroupAsync(groupId, nodeId);
            if (!success)
            {
                return BadRequest(new { message = _localization.GetMessage("nodeGroup.addNodeFailed") });
            }

            return Ok(new { message = _localization.GetMessage("nodeGroup.addNodeSuccess") });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加节点到分组失败: {NodeId} -> {GroupId}", nodeId, groupId);
            return StatusCode(500, new { message = _localization.GetMessage("nodeGroup.addNodeFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 从分组中移除节点
    /// </summary>
    [HttpDelete("groups/{groupId}/nodes/{nodeId}")]
    public async Task<ActionResult> RemoveNodeFromGroup(string groupId, string nodeId)
    {
        try
        {
            var success = await _nodeGroupService.RemoveNodeFromGroupAsync(groupId, nodeId);
            if (!success)
            {
                return BadRequest(new { message = _localization.GetMessage("nodeGroup.removeNodeFailed") });
            }

            return Ok(new { message = _localization.GetMessage("nodeGroup.removeNodeSuccess") });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从分组移除节点失败: {NodeId} <- {GroupId}", nodeId, groupId);
            return StatusCode(500, new { message = _localization.GetMessage("nodeGroup.removeNodeFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 获取分组中的节点列表
    /// </summary>
    [HttpGet("groups/{groupId}/nodes")]
    public async Task<ActionResult> GetNodesInGroup(string groupId)
    {
        try
        {
            var nodes = await _nodeGroupService.GetNodesInGroupAsync(groupId);
            return Ok(nodes);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取分组节点失败: {GroupId}", groupId);
            return StatusCode(500, new { message = _localization.GetMessage("nodeGroup.nodesFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 获取节点所属的所有分组
    /// </summary>
    [HttpGet("nodes/{nodeId}/groups")]
    public async Task<ActionResult> GetNodeGroups(string nodeId)
    {
        try
        {
            var groups = await _nodeGroupService.GetNodeGroupsAsync(nodeId);
            return Ok(groups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取节点分组失败: {NodeId}", nodeId);
            return StatusCode(500, new { message = _localization.GetMessage("nodeGroup.listFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 批量更新节点分组
    /// </summary>
    [HttpPost("nodes/batch-update-groups")]
    public async Task<ActionResult> BatchUpdateNodeGroups([FromBody] BatchUpdateGroupsRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var success = await _nodeGroupService.BatchUpdateNodeGroupsAsync(request.NodeIds, request.GroupIds);
            if (!success)
            {
                return BadRequest(new { message = _localization.GetMessage("nodeGroup.batchUpdateFailed") });
            }

            return Ok(new { message = _localization.GetMessage("nodeGroup.batchUpdateSuccess") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量更新节点分组失败");
            return StatusCode(500, new { message = _localization.GetMessage("nodeGroup.batchUpdateFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 获取分组统计信息
    /// </summary>
    [HttpGet("groups/{groupId}/statistics")]
    public async Task<ActionResult<GroupStatistics>> GetGroupStatistics(string groupId)
    {
        try
        {
            var statistics = await _nodeGroupService.GetGroupStatisticsAsync(groupId);
            return Ok(statistics);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取分组统计失败: {GroupId}", groupId);
            return StatusCode(500, new { message = _localization.GetMessage("nodeGroup.statsFailed"), error = ex.Message });
        }
    }

    #endregion

    #region 节点标签管理

    /// <summary>
    /// 获取所有标签
    /// </summary>
    [HttpGet("tags")]
    public async Task<ActionResult<IEnumerable<NodeTag>>> GetAllTags()
    {
        try
        {
            var tags = await _nodeGroupService.GetAllTagsAsync();
            return Ok(tags);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取标签失败");
            return StatusCode(500, new { message = _localization.GetMessage("nodeGroup.tagsFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 创建标签
    /// </summary>
    [HttpPost("tags")]
    public async Task<ActionResult<NodeTag>> CreateTag([FromBody] CreateTagRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var tag = await _nodeGroupService.CreateTagAsync(request);
            return CreatedAtAction(nameof(GetTagById), new { id = tag.Id }, tag);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建标签失败: {TagName}", request.Name);
            return StatusCode(500, new { message = _localization.GetMessage("nodeGroup.tagCreateFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 根据ID获取标签
    /// </summary>
    [HttpGet("tags/{id}")]
    public async Task<ActionResult<NodeTag>> GetTagById(string id)
    {
        try
        {
            var tags = await _nodeGroupService.GetAllTagsAsync();
            var tag = tags.FirstOrDefault(t => t.Id == id);

            if (tag == null)
            {
                return NotFound(new { message = _localization.GetMessage("nodeGroup.tagNotFound") });
            }

            return Ok(tag);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取标签失败: {TagId}", id);
            return StatusCode(500, new { message = _localization.GetMessage("nodeGroup.tagGetFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 更新标签
    /// </summary>
    [HttpPut("tags/{id}")]
    public async Task<ActionResult<NodeTag>> UpdateTag(string id, [FromBody] UpdateTagRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var tag = await _nodeGroupService.UpdateTagAsync(id, request);
            return Ok(tag);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新标签失败: {TagId}", id);
            return StatusCode(500, new { message = _localization.GetMessage("nodeGroup.tagUpdateFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 删除标签
    /// </summary>
    [HttpDelete("tags/{id}")]
    public async Task<ActionResult> DeleteTag(string id)
    {
        try
        {
            var success = await _nodeGroupService.DeleteTagAsync(id);
            if (!success)
            {
                return NotFound(new { message = _localization.GetMessage("nodeGroup.tagNotFound") });
            }

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除标签失败: {TagId}", id);
            return StatusCode(500, new { message = _localization.GetMessage("nodeGroup.tagDeleteFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 为节点添加标签
    /// </summary>
    [HttpPost("nodes/{nodeId}/tags/{tagId}")]
    public async Task<ActionResult> AddTagToNode(string nodeId, string tagId)
    {
        try
        {
            var success = await _nodeGroupService.AddTagToNodeAsync(nodeId, tagId);
            if (!success)
            {
                return BadRequest(new { message = _localization.GetMessage("nodeGroup.addTagFailed") });
            }

            return Ok(new { message = _localization.GetMessage("nodeGroup.addTagSuccess") });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "为节点添加标签失败: {NodeId} + {TagId}", nodeId, tagId);
            return StatusCode(500, new { message = _localization.GetMessage("nodeGroup.addTagFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 从节点移除标签
    /// </summary>
    [HttpDelete("nodes/{nodeId}/tags/{tagId}")]
    public async Task<ActionResult> RemoveTagFromNode(string nodeId, string tagId)
    {
        try
        {
            var success = await _nodeGroupService.RemoveTagFromNodeAsync(nodeId, tagId);
            if (!success)
            {
                return BadRequest(new { message = _localization.GetMessage("nodeGroup.removeTagFailed") });
            }

            return Ok(new { message = _localization.GetMessage("nodeGroup.removeTagSuccess") });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从节点移除标签失败: {NodeId} - {TagId}", nodeId, tagId);
            return StatusCode(500, new { message = _localization.GetMessage("nodeGroup.removeTagFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 获取节点的所有标签
    /// </summary>
    [HttpGet("nodes/{nodeId}/tags")]
    public async Task<ActionResult> GetNodeTags(string nodeId)
    {
        try
        {
            var tags = await _nodeGroupService.GetNodeTagsAsync(nodeId);
            return Ok(tags);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取节点标签失败: {NodeId}", nodeId);
            return StatusCode(500, new { message = _localization.GetMessage("nodeGroup.nodeTagsFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 获取使用指定标签的所有节点
    /// </summary>
    [HttpGet("tags/{tagId}/nodes")]
    public async Task<ActionResult> GetNodesByTag(string tagId)
    {
        try
        {
            var nodes = await _nodeGroupService.GetNodesByTagAsync(tagId);
            return Ok(nodes);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取标签节点失败: {TagId}", tagId);
            return StatusCode(500, new { message = _localization.GetMessage("nodeGroup.tagNodesFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 批量更新节点标签
    /// </summary>
    [HttpPost("nodes/batch-update-tags")]
    public async Task<ActionResult> BatchUpdateNodeTags([FromBody] BatchUpdateTagsRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var success = await _nodeGroupService.BatchUpdateNodeTagsAsync(request.NodeIds, request.TagIds);
            if (!success)
            {
                return BadRequest(new { message = _localization.GetMessage("nodeGroup.batchTagUpdateFailed") });
            }

            return Ok(new { message = _localization.GetMessage("nodeGroup.batchTagUpdateSuccess") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量更新节点标签失败");
            return StatusCode(500, new { message = _localization.GetMessage("nodeGroup.batchTagUpdateFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 获取标签使用统计
    /// </summary>
    [HttpGet("tags/{tagId}/statistics")]
    public async Task<ActionResult<TagStatistics>> GetTagStatistics(string tagId)
    {
        try
        {
            var statistics = await _nodeGroupService.GetTagStatisticsAsync(tagId);
            return Ok(statistics);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取标签统计失败: {TagId}", tagId);
            return StatusCode(500, new { message = _localization.GetMessage("nodeGroup.tagStatsFailed"), error = ex.Message });
        }
    }

    #endregion

    /// <summary>
    /// 获取节点分组和标签的概览信息
    /// </summary>
    [HttpGet("overview")]
    public async Task<ActionResult> GetOverview()
    {
        try
        {
            var groups = await _nodeGroupService.GetNodeGroupsAsync();
            var tags = await _nodeGroupService.GetAllTagsAsync();

            var overview = new
            {
                groups = new
                {
                    total = groups.Count(),
                    system = groups.Count(g => g.IsSystem),
                    custom = groups.Count(g => !g.IsSystem),
                    @default = groups.Count(g => g.IsDefault)
                },
                tags = new
                {
                    total = tags.Count(),
                    system = tags.Count(t => t.IsSystem),
                    custom = tags.Count(t => !t.IsSystem),
                    categories = tags.GroupBy(t => t.Category)
                                   .ToDictionary(g => g.Key, g => g.Count())
                }
            };

            return Ok(overview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取分组标签概览失败");
            return StatusCode(500, new { message = _localization.GetMessage("nodeGroup.overviewFailed"), error = ex.Message });
        }
    }
}

/// <summary>
/// 批量更新分组请求
/// </summary>
public class BatchUpdateGroupsRequest
{
    public string NodeIds { get; set; } = string.Empty;
    public string GroupIds { get; set; } = string.Empty;
}

/// <summary>
/// 批量更新标签请求
/// </summary>
public class BatchUpdateTagsRequest
{
    public string NodeIds { get; set; } = string.Empty;
    public string TagIds { get; set; } = string.Empty;
}