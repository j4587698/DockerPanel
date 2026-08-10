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
    /// 节点管理 Minimal API 端点（原 NodesController）。
    /// </summary>
    public static class NodeEndpoints
    {
        /// <summary>
        /// 日志类别标记（静态类不能作泛型参数）。
        /// </summary>
        public sealed class LoggingTag
        {
        }

        /// <summary>
        /// 映射节点管理相关路由。
        /// </summary>
        public static IEndpointRouteBuilder MapNodeEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/nodes");

            group.MapGet("", GetNodes);
            group.MapGet("{id}", GetNode);
            group.MapGet("default", GetDefaultNode);
            group.MapPost("", AddNode).RequireAuthorization(new AuthorizeAttribute { Roles = AuthRoles.Admin });
            group.MapPut("{id}", UpdateNode).RequireAuthorization(new AuthorizeAttribute { Roles = AuthRoles.Admin });
            group.MapDelete("{id}", DeleteNode).RequireAuthorization(new AuthorizeAttribute { Roles = AuthRoles.Admin });
            group.MapPost("{id}/test-connection", TestNodeConnection).RequireAuthorization(new AuthorizeAttribute { Roles = AuthRoles.Admin });
            group.MapPost("test-connection", TestConnection).RequireAuthorization(new AuthorizeAttribute { Roles = AuthRoles.Admin });
            group.MapGet("{id}/stats", GetNodeStats);
            group.MapGet("{id}/info", GetNodeInfo);
            group.MapGet("{id}/health", GetNodeHealthStatus);
            group.MapPost("{id}/set-default", SetDefaultNode).RequireAuthorization(new AuthorizeAttribute { Roles = AuthRoles.Admin });
            group.MapPost("batch", BatchOperation).RequireAuthorization(new AuthorizeAttribute { Roles = AuthRoles.Admin });
            group.MapGet("groups", GetGroups);
            group.MapGet("groups/{id}", GetGroup);
            group.MapPost("groups", CreateGroup).RequireAuthorization(new AuthorizeAttribute { Roles = AuthRoles.Admin });
            group.MapPut("groups/{id}", UpdateGroup).RequireAuthorization(new AuthorizeAttribute { Roles = AuthRoles.Admin });
            group.MapDelete("groups/{id}", DeleteGroup).RequireAuthorization(new AuthorizeAttribute { Roles = AuthRoles.Admin });

            return app;
        }

        private static async Task<IResult> GetNodes(INodeService nodeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var nodes = await nodeService.GetNodesAsync();
                return TypedResults.Ok(nodes);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取节点列表失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("node.listFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetNode(string id, INodeService nodeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var node = await nodeService.GetNodeAsync(id);
                if (node == null)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("node.notFound") });
                }
                return TypedResults.Ok(node);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取节点 {Id} 失败", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("node.getFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetDefaultNode(ILocalizationService localization, INodeService nodeService, ILogger<LoggingTag> logger)
        {
            try
            {
                var node = await nodeService.GetDefaultNodeAsync();
                if (node == null)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = "未设置默认节点" });
                }
                return TypedResults.Ok(node);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取默认节点失败");
                return TypedResults.Json(new ApiErrorResponse { Error = "获取默认节点失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> AddNode(AddNodeRequest request, INodeService nodeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var nodeId = await nodeService.AddNodeAsync(request);
                return TypedResults.Created($"/api/nodes/{nodeId}", nodeId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "添加节点失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("node.addFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> UpdateNode(string id, UpdateNodeRequest request, INodeService nodeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                await nodeService.UpdateNodeAsync(id, request);
                return TypedResults.NoContent();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "更新节点 {Id} 失败", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("node.updateFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> DeleteNode(string id, ILocalizationService localization, INodeService nodeService, ILogger<LoggingTag> logger)
        {
            try
            {
                await nodeService.RemoveNodeAsync(id);
                return TypedResults.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "删除节点 {Id} 被拒绝", id);
                return TypedResults.BadRequest(new ApiErrorResponse { Error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "删除节点 {Id} 失败", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("node.deleteFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> TestNodeConnection(string id, ILogger<LoggingTag> logger, INodeService nodeService, ILocalizationService localization)
        {
            try
            {
                var result = await nodeService.TestNodeConnectionAsync(id);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "测试节点 {Id} 连接失败", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("node.testConnectionFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> TestConnection(TestNodeConnectionRequest request, INodeService nodeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var result = await nodeService.TestConnectionAsync(request);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "测试连接失败");
                return TypedResults.Json(new ApiErrorResponse { Error = "连接测试失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetNodeStats(string id, ILocalizationService localization, ILogger<LoggingTag> logger, INodeService nodeService)
        {
            try
            {
                var stats = await nodeService.GetNodeStatsAsync(id);
                if (stats == null)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("node.notFound") });
                }
                return TypedResults.Ok(stats);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取节点 {Id} 统计信息失败", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("node.statsFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetNodeInfo(string id, ILogger<LoggingTag> logger, ILocalizationService localization, INodeService nodeService)
        {
            try
            {
                var info = await nodeService.GetNodeInfoAsync(id);
                if (info == null)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("node.notFound") });
                }
                return TypedResults.Ok(info);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取节点 {Id} 详细信息失败", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("node.detailFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetNodeHealthStatus(string id, INodeService nodeService, ILogger<LoggingTag> logger, ILocalizationService localization)
        {
            try
            {
                var healthStatus = await nodeService.GetNodeHealthStatusAsync(id);
                return TypedResults.Ok(healthStatus);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取节点 {Id} 健康状态失败", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("node.healthFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> SetDefaultNode(ILocalizationService localization, string id, INodeService nodeService, ILogger<LoggingTag> logger)
        {
            try
            {
                await nodeService.SetDefaultNodeAsync(id);
                return TypedResults.NoContent();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "设置默认节点失败");
                return TypedResults.Json(new ApiErrorResponse { Error = "设置默认节点失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> BatchOperation(BatchNodeOperationRequest request, ILocalizationService localization, INodeService nodeService, ILogger<LoggingTag> logger)
        {
            try
            {
                var results = new List<NodeBatchOperationResult>();

                foreach (var nodeId in request.NodeIds)
                {
                    try
                    {
                        switch (request.Operation.ToLower())
                        {
                            case "test-connection":
                                var isConnected = await nodeService.TestNodeConnectionAsync(nodeId);
                                results.Add(new NodeBatchOperationResult { NodeId = nodeId, Success = true, Connected = isConnected });
                                break;
                            case "remove":
                                await nodeService.RemoveNodeAsync(nodeId);
                                results.Add(new NodeBatchOperationResult { NodeId = nodeId, Success = true });
                                break;
                            default:
                                results.Add(new NodeBatchOperationResult { NodeId = nodeId, Success = false, Error = localization.GetMessage("node.unsupportedOperation") });
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        results.Add(new NodeBatchOperationResult { NodeId = nodeId, Success = false, Error = ex.Message });
                    }
                }

                return TypedResults.Ok(new NodeBatchOperationResponse { Results = results });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "批量节点操作失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("node.batchOperationFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetGroups(ILogger<LoggingTag> logger, INodeService nodeService, ILocalizationService localization)
        {
            try
            {
                var groups = await nodeService.GetGroupsAsync();
                return TypedResults.Ok(groups);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取节点分组失败");
                return TypedResults.Json(new ApiErrorResponse { Error = "获取节点分组失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetGroup(ILogger<LoggingTag> logger, string id, INodeService nodeService, ILocalizationService localization)
        {
            try
            {
                var group = await nodeService.GetGroupAsync(id);
                if (group == null)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = "分组不存在" });
                }
                return TypedResults.Ok(group);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取分组 {Id} 失败", id);
                return TypedResults.Json(new ApiErrorResponse { Error = "获取分组失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> CreateGroup(DockerPanel.API.Models.CreateNodeGroupRequest request, INodeService nodeService, ILogger<LoggingTag> logger, ILocalizationService localization)
        {
            try
            {
                var group = await nodeService.CreateGroupAsync(request);
                return TypedResults.Created($"/api/nodes/groups/{group.Id}", group);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "创建分组失败");
                return TypedResults.Json(new ApiErrorResponse { Error = "创建分组失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> UpdateGroup(string id, DockerPanel.API.Models.UpdateNodeGroupRequest request, ILocalizationService localization, ILogger<LoggingTag> logger, INodeService nodeService)
        {
            try
            {
                await nodeService.UpdateGroupAsync(id, request);
                return TypedResults.NoContent();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "更新分组 {Id} 失败", id);
                return TypedResults.Json(new ApiErrorResponse { Error = "更新分组失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> DeleteGroup(INodeService nodeService, string id, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                await nodeService.DeleteGroupAsync(id);
                return TypedResults.NoContent();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "删除分组 {Id} 失败", id);
                return TypedResults.Json(new ApiErrorResponse { Error = "删除分组失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }
    }

    /// <summary>
    /// 批量节点操作单项结果
    /// </summary>
    public sealed class NodeBatchOperationResult
    {
        public string NodeId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public bool? Connected { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// 批量节点操作响应
    /// </summary>
    public sealed class NodeBatchOperationResponse
    {
        public List<NodeBatchOperationResult> Results { get; set; } = new();
    }
}
