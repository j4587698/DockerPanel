using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DockerPanel.API.Models;
using DockerPanel.API.Serialization;
using DockerPanel.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Authorization;

namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// 节点分组和标签管理 Minimal API 端点（原 NodeGroupController，全部端点要求 Admin 角色）。
    /// </summary>
    public static class NodeGroupEndpoints
    {
        /// <summary>
        /// 日志类别标记（静态类不能作泛型参数）。
        /// </summary>
        public sealed class LoggingTag
        {
        }

        /// <summary>
        /// 映射节点分组和标签管理相关路由。
        /// </summary>
        public static IEndpointRouteBuilder MapNodeGroupEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/nodegroup")
                .RequireAuthorization(new AuthorizeAttribute { Roles = AuthRoles.Admin });

            group.MapGet("groups", GetNodeGroups);
            group.MapGet("groups/{id}", GetNodeGroup);
            group.MapPost("groups", CreateNodeGroup);
            group.MapPut("groups/{id}", UpdateNodeGroup);
            group.MapDelete("groups/{id}", DeleteNodeGroup);
            group.MapPost("groups/{groupId}/nodes/{nodeId}", AddNodeToGroup);
            group.MapDelete("groups/{groupId}/nodes/{nodeId}", RemoveNodeFromGroup);
            group.MapGet("groups/{groupId}/nodes", GetNodesInGroup);
            group.MapGet("nodes/{nodeId}/groups", GetNodeGroupsForNode);
            group.MapPost("nodes/batch-update-groups", BatchUpdateNodeGroups);
            group.MapGet("groups/{groupId}/statistics", GetGroupStatistics);
            group.MapGet("tags", GetAllTags);
            group.MapPost("tags", CreateTag);
            group.MapGet("tags/{id}", GetTagById);
            group.MapPut("tags/{id}", UpdateTag);
            group.MapDelete("tags/{id}", DeleteTag);
            group.MapPost("nodes/{nodeId}/tags/{tagId}", AddTagToNode);
            group.MapDelete("nodes/{nodeId}/tags/{tagId}", RemoveTagFromNode);
            group.MapGet("nodes/{nodeId}/tags", GetNodeTags);
            group.MapGet("tags/{tagId}/nodes", GetNodesByTag);
            group.MapPost("nodes/batch-update-tags", BatchUpdateNodeTags);
            group.MapGet("tags/{tagId}/statistics", GetTagStatistics);
            group.MapGet("overview", GetOverview);

            return app;
        }

        private static async Task<IResult> GetNodeGroups(INodeGroupService nodeGroupService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var groups = await nodeGroupService.GetNodeGroupsAsync();
                return TypedResults.Ok(groups);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取节点分组失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.listFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetNodeGroup(string id, INodeGroupService nodeGroupService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var group = await nodeGroupService.GetNodeGroupByIdAsync(id);
                if (group == null)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.notFound") });
                }
                return TypedResults.Ok(group);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取节点分组失败: {GroupId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.listFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> CreateNodeGroup(DockerPanel.API.Services.CreateNodeGroupRequest request, INodeGroupService nodeGroupService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var group = await nodeGroupService.CreateNodeGroupAsync(request);
                return TypedResults.Created($"/api/nodegroup/groups/{group.Id}", group);
            }
            catch (ArgumentException ex)
            {
                return TypedResults.BadRequest(new ApiErrorResponse { Error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.Conflict(new ApiErrorResponse { Error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "创建节点分组失败: {GroupName}", request.Name);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.createFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> UpdateNodeGroup(string id, DockerPanel.API.Services.UpdateNodeGroupRequest request, INodeGroupService nodeGroupService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var group = await nodeGroupService.UpdateNodeGroupAsync(id, request);
                return TypedResults.Ok(group);
            }
            catch (KeyNotFoundException ex)
            {
                return TypedResults.NotFound(new ApiErrorResponse { Error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return TypedResults.BadRequest(new ApiErrorResponse { Error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.Conflict(new ApiErrorResponse { Error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "更新节点分组失败: {GroupId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.updateFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> DeleteNodeGroup(string id, ILocalizationService localization, INodeGroupService nodeGroupService, ILogger<LoggingTag> logger)
        {
            try
            {
                var success = await nodeGroupService.DeleteNodeGroupAsync(id);
                if (!success)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.notFound") });
                }

                return TypedResults.NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return TypedResults.NotFound(new ApiErrorResponse { Error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new ApiErrorResponse { Error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "删除节点分组失败: {GroupId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.deleteFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> AddNodeToGroup(string groupId, string nodeId, INodeGroupService nodeGroupService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var success = await nodeGroupService.AddNodeToGroupAsync(groupId, nodeId);
                if (!success)
                {
                    return TypedResults.BadRequest(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.addNodeFailed") });
                }

                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("nodeGroup.addNodeSuccess") });
            }
            catch (KeyNotFoundException ex)
            {
                return TypedResults.NotFound(new ApiErrorResponse { Error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "添加节点到分组失败: {NodeId} -> {GroupId}", nodeId, groupId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.addNodeFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> RemoveNodeFromGroup(string groupId, string nodeId, ILocalizationService localization, ILogger<LoggingTag> logger, INodeGroupService nodeGroupService)
        {
            try
            {
                var success = await nodeGroupService.RemoveNodeFromGroupAsync(groupId, nodeId);
                if (!success)
                {
                    return TypedResults.BadRequest(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.removeNodeFailed") });
                }

                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("nodeGroup.removeNodeSuccess") });
            }
            catch (KeyNotFoundException ex)
            {
                return TypedResults.NotFound(new ApiErrorResponse { Error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "从分组移除节点失败: {NodeId} <- {GroupId}", nodeId, groupId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.removeNodeFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetNodesInGroup(string groupId, INodeGroupService nodeGroupService, ILogger<LoggingTag> logger, ILocalizationService localization)
        {
            try
            {
                var nodes = await nodeGroupService.GetNodesInGroupAsync(groupId);
                return TypedResults.Ok(nodes);
            }
            catch (KeyNotFoundException ex)
            {
                return TypedResults.NotFound(new ApiErrorResponse { Error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取分组节点失败: {GroupId}", groupId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.nodesFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetNodeGroupsForNode(string nodeId, INodeGroupService nodeGroupService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var groups = await nodeGroupService.GetNodeGroupsAsync(nodeId);
                return TypedResults.Ok(groups);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取节点分组失败: {NodeId}", nodeId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.listFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> BatchUpdateNodeGroups(BatchUpdateGroupsRequest request, INodeGroupService nodeGroupService, ILogger<LoggingTag> logger, ILocalizationService localization)
        {
            try
            {
                var success = await nodeGroupService.BatchUpdateNodeGroupsAsync(request.NodeIds, request.GroupIds);
                if (!success)
                {
                    return TypedResults.BadRequest(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.batchUpdateFailed") });
                }

                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("nodeGroup.batchUpdateSuccess") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "批量更新节点分组失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.batchUpdateFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetGroupStatistics(string groupId, ILocalizationService localization, ILogger<LoggingTag> logger, INodeGroupService nodeGroupService)
        {
            try
            {
                var statistics = await nodeGroupService.GetGroupStatisticsAsync(groupId);
                return TypedResults.Ok(statistics);
            }
            catch (KeyNotFoundException ex)
            {
                return TypedResults.NotFound(new ApiErrorResponse { Error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取分组统计失败: {GroupId}", groupId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.statsFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetAllTags(ILocalizationService localization, INodeGroupService nodeGroupService, ILogger<LoggingTag> logger)
        {
            try
            {
                var tags = await nodeGroupService.GetAllTagsAsync();
                return TypedResults.Ok(tags);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取标签失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.tagsFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> CreateTag(CreateTagRequest request, INodeGroupService nodeGroupService, ILogger<LoggingTag> logger, ILocalizationService localization)
        {
            try
            {
                var tag = await nodeGroupService.CreateTagAsync(request);
                return TypedResults.Created($"/api/nodegroup/tags/{tag.Id}", tag);
            }
            catch (ArgumentException ex)
            {
                return TypedResults.BadRequest(new ApiErrorResponse { Error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.Conflict(new ApiErrorResponse { Error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "创建标签失败: {TagName}", request.Name);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.tagCreateFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetTagById(string id, INodeGroupService nodeGroupService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var tags = await nodeGroupService.GetAllTagsAsync();
                var tag = tags.FirstOrDefault(t => t.Id == id);

                if (tag == null)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.tagNotFound") });
                }

                return TypedResults.Ok(tag);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取标签失败: {TagId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.tagGetFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> UpdateTag(string id, UpdateTagRequest request, ILogger<LoggingTag> logger, ILocalizationService localization, INodeGroupService nodeGroupService)
        {
            try
            {
                var tag = await nodeGroupService.UpdateTagAsync(id, request);
                return TypedResults.Ok(tag);
            }
            catch (KeyNotFoundException ex)
            {
                return TypedResults.NotFound(new ApiErrorResponse { Error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return TypedResults.BadRequest(new ApiErrorResponse { Error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.Conflict(new ApiErrorResponse { Error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "更新标签失败: {TagId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.tagUpdateFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> DeleteTag(ILocalizationService localization, string id, INodeGroupService nodeGroupService, ILogger<LoggingTag> logger)
        {
            try
            {
                var success = await nodeGroupService.DeleteTagAsync(id);
                if (!success)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.tagNotFound") });
                }

                return TypedResults.NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return TypedResults.NotFound(new ApiErrorResponse { Error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new ApiErrorResponse { Error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "删除标签失败: {TagId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.tagDeleteFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> AddTagToNode(string nodeId, string tagId, INodeGroupService nodeGroupService, ILogger<LoggingTag> logger, ILocalizationService localization)
        {
            try
            {
                var success = await nodeGroupService.AddTagToNodeAsync(nodeId, tagId);
                if (!success)
                {
                    return TypedResults.BadRequest(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.addTagFailed") });
                }

                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("nodeGroup.addTagSuccess") });
            }
            catch (KeyNotFoundException ex)
            {
                return TypedResults.NotFound(new ApiErrorResponse { Error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "为节点添加标签失败: {NodeId} + {TagId}", nodeId, tagId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.addTagFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> RemoveTagFromNode(string nodeId, string tagId, ILogger<LoggingTag> logger, INodeGroupService nodeGroupService, ILocalizationService localization)
        {
            try
            {
                var success = await nodeGroupService.RemoveTagFromNodeAsync(nodeId, tagId);
                if (!success)
                {
                    return TypedResults.BadRequest(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.removeTagFailed") });
                }

                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("nodeGroup.removeTagSuccess") });
            }
            catch (KeyNotFoundException ex)
            {
                return TypedResults.NotFound(new ApiErrorResponse { Error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "从节点移除标签失败: {NodeId} - {TagId}", nodeId, tagId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.removeTagFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetNodeTags(ILogger<LoggingTag> logger, string nodeId, INodeGroupService nodeGroupService, ILocalizationService localization)
        {
            try
            {
                var tags = await nodeGroupService.GetNodeTagsAsync(nodeId);
                return TypedResults.Ok(tags);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取节点标签失败: {NodeId}", nodeId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.nodeTagsFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetNodesByTag(INodeGroupService nodeGroupService, string tagId, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var nodes = await nodeGroupService.GetNodesByTagAsync(tagId);
                return TypedResults.Ok(nodes);
            }
            catch (KeyNotFoundException ex)
            {
                return TypedResults.NotFound(new ApiErrorResponse { Error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取标签节点失败: {TagId}", tagId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.tagNodesFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> BatchUpdateNodeTags(BatchUpdateTagsRequest request, ILocalizationService localization, ILogger<LoggingTag> logger, INodeGroupService nodeGroupService)
        {
            try
            {
                var success = await nodeGroupService.BatchUpdateNodeTagsAsync(request.NodeIds, request.TagIds);
                if (!success)
                {
                    return TypedResults.BadRequest(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.batchTagUpdateFailed") });
                }

                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("nodeGroup.batchTagUpdateSuccess") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "批量更新节点标签失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.batchTagUpdateFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetTagStatistics(INodeGroupService nodeGroupService, ILocalizationService localization, ILogger<LoggingTag> logger, string tagId)
        {
            try
            {
                var statistics = await nodeGroupService.GetTagStatisticsAsync(tagId);
                return TypedResults.Ok(statistics);
            }
            catch (KeyNotFoundException ex)
            {
                return TypedResults.NotFound(new ApiErrorResponse { Error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取标签统计失败: {TagId}", tagId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.tagStatsFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetOverview(ILogger<LoggingTag> logger, ILocalizationService localization, INodeGroupService nodeGroupService)
        {
            try
            {
                var groups = await nodeGroupService.GetNodeGroupsAsync();
                var tags = await nodeGroupService.GetAllTagsAsync();

                var overview = new NodeGroupOverview
                {
                    Groups = new NodeGroupOverviewGroupStats
                    {
                        Total = groups.Count(),
                        System = groups.Count(g => g.IsSystem),
                        Custom = groups.Count(g => !g.IsSystem),
                        Default = groups.Count(g => g.IsDefault)
                    },
                    Tags = new NodeGroupOverviewTagStats
                    {
                        Total = tags.Count(),
                        System = tags.Count(t => t.IsSystem),
                        Custom = tags.Count(t => !t.IsSystem),
                        Categories = tags.GroupBy(t => t.Category)
                                       .ToDictionary(g => g.Key, g => g.Count())
                    }
                };

                return TypedResults.Ok(overview);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取分组标签概览失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeGroup.overviewFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
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

    /// <summary>
    /// 节点分组和标签概览
    /// </summary>
    public sealed class NodeGroupOverview
    {
        public NodeGroupOverviewGroupStats Groups { get; set; } = new();
        public NodeGroupOverviewTagStats Tags { get; set; } = new();
    }

    /// <summary>
    /// 分组概览统计
    /// </summary>
    public sealed class NodeGroupOverviewGroupStats
    {
        public int Total { get; set; }
        public int System { get; set; }
        public int Custom { get; set; }
        public int Default { get; set; }
    }

    /// <summary>
    /// 标签概览统计
    /// </summary>
    public sealed class NodeGroupOverviewTagStats
    {
        public int Total { get; set; }
        public int System { get; set; }
        public int Custom { get; set; }
        public Dictionary<string, int> Categories { get; set; } = new();
    }
}
