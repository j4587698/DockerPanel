using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DockerPanel.API.Models;
using DockerPanel.API.Serialization;
using DockerPanel.API.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// 网络管理 Minimal API 端点（原 NetworkController）。
    /// </summary>
    public static class NetworkEndpoints
    {
        /// <summary>
        /// 日志类别标记（静态类不能作泛型参数）。
        /// </summary>
        public sealed class LoggingTag
        {
        }

        /// <summary>
        /// 映射网络管理相关路由。
        /// </summary>
        public static IEndpointRouteBuilder MapNetworkEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/network");

            group.MapGet("", GetNetworks);
            group.MapGet("{networkId}", GetNetwork);
            group.MapPost("", CreateNetwork);
            group.MapDelete("{networkId}", DeleteNetwork);
            group.MapPost("{networkId}/connect/{containerId}", ConnectContainerToNetwork);
            group.MapPost("{networkId}/disconnect/{containerId}", DisconnectContainerFromNetwork);
            group.MapGet("{networkId}/containers", GetNetworkContainers);
            group.MapPost("prune", PruneNetworks);
            group.MapGet("statistics", GetNetworkStatistics);
            group.MapGet("{networkId}/exists", NetworkExists);
            group.MapGet("{networkId}/ipam", GetNetworkIpamInfo);
            group.MapPut("{networkId}", UpdateNetwork);

            return app;
        }

        private static async Task<IResult> GetNetworks(INetworkService networkService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                var networks = await networkService.GetNetworksAsync(nodeId);
                logger.LogDebug("获取网络列表: {Count} 个网络", networks?.Count() ?? 0);
                return TypedResults.Ok(networks);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取网络列表失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("network.listFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetNetwork(string networkId, INetworkService networkService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                var network = await networkService.GetNetworkByIdAsync(networkId, nodeId);
                if (network == null)
                {
                    return TypedResults.NotFound(new NetworkNotFoundResponse
                    {
                        Error = localization.GetMessage("network.notFound"),
                        NetworkId = networkId
                    });
                }
                return TypedResults.Ok(network);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取网络详情失败: {NetworkId}", networkId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("network.detailFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> CreateNetwork(CreateNetworkRequest request, INetworkService networkService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var network = await networkService.CreateNetworkAsync(request);
                return TypedResults.Created($"/api/network/{network.Id}", network);
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(ex, "创建网络参数错误: {Name}", request.Name);
                return TypedResults.BadRequest(new NetworkNameErrorResponse { Error = ex.Message, Name = request.Name });
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "创建网络操作失败: {Name}", request.Name);
                return TypedResults.Json(new NetworkNameErrorResponse { Error = ex.Message, Name = request.Name }, WebJsonContext.Default.NetworkNameErrorResponse, statusCode: 409);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "创建网络失败: {Name}", request.Name);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("network.createFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> DeleteNetwork(string networkId, INetworkService networkService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                var success = await networkService.DeleteNetworkAsync(networkId, nodeId);
                if (success)
                {
                    return TypedResults.Ok(new NetworkMessageResponse
                    {
                        Message = localization.GetMessage("network.deleteSuccess"),
                        NetworkId = networkId
                    });
                }
                else
                {
                    return TypedResults.NotFound(new NetworkNotFoundResponse
                    {
                        Error = localization.GetMessage("network.notFound"),
                        NetworkId = networkId
                    });
                }
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "删除网络操作失败: {NetworkId}", networkId);
                return TypedResults.Json(new NetworkConflictResponse { Error = ex.Message, NetworkId = networkId }, WebJsonContext.Default.NetworkConflictResponse, statusCode: 409);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "删除网络失败: {NetworkId}", networkId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("network.deleteFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ConnectContainerToNetwork(string networkId, string containerId, NetworkConnectionConfig? config, INetworkService networkService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                var success = await networkService.ConnectContainerToNetworkAsync(networkId, containerId,
                    config != null ? new NetworkConfig
                    {
                        Aliases = config.Aliases,
                        IPv4Address = config.IPv4Address,
                        IPv6Address = config.IPv6Address,
                        Links = config.Links
                    } : null);
                if (success)
                {
                    return TypedResults.Ok(new NetworkMessageResponse
                    {
                        Message = localization.GetMessage("network.connectSuccess"),
                        NetworkId = networkId,
                        ContainerId = containerId
                    });
                }
                else
                {
                    return TypedResults.BadRequest(new NetworkConnectErrorResponse
                    {
                        Error = localization.GetMessage("network.connectFailed"),
                        NetworkId = networkId,
                        ContainerId = containerId
                    });
                }
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(ex, "连接容器到网络参数错误: {NetworkId}, {ContainerId}", networkId, containerId);
                return TypedResults.BadRequest(new NetworkConnectErrorResponse { Error = ex.Message, NetworkId = networkId, ContainerId = containerId });
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "连接容器到网络操作失败: {NetworkId}, {ContainerId}", networkId, containerId);
                return TypedResults.Json(new NetworkConflictResponse { Error = ex.Message, NetworkId = networkId, ContainerId = containerId }, WebJsonContext.Default.NetworkConflictResponse, statusCode: 409);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "连接容器到网络失败: {NetworkId}, {ContainerId}", networkId, containerId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("network.connectFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> DisconnectContainerFromNetwork(string networkId, string containerId, INetworkService networkService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                var success = await networkService.DisconnectContainerFromNetworkAsync(networkId, containerId);
                if (success)
                {
                    return TypedResults.Ok(new NetworkMessageResponse
                    {
                        Message = localization.GetMessage("network.disconnectSuccess"),
                        NetworkId = networkId,
                        ContainerId = containerId
                    });
                }
                else
                {
                    return TypedResults.BadRequest(new NetworkConnectErrorResponse
                    {
                        Error = localization.GetMessage("network.disconnectFailed"),
                        NetworkId = networkId,
                        ContainerId = containerId
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "断开容器与网络的连接失败: {NetworkId}, {ContainerId}", networkId, containerId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("network.disconnectFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetNetworkContainers(string networkId, INetworkService networkService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                var containers = await networkService.GetNetworkContainersAsync(networkId, nodeId);
                return TypedResults.Ok(containers);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取网络容器列表失败: {NetworkId}", networkId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("network.containersFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> PruneNetworks(PruneNetworksRequest request, INetworkService networkService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var networksDeleted = await networkService.PruneNetworksAsync();
                var result = new NetworkPruneResult
                {
                    NetworksDeleted = networksDeleted,
                    SpaceReclaimed = 0,
                    Success = true
                };
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "清理网络失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("network.pruneFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetNetworkStatistics(INetworkService networkService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                var statistics = await networkService.GetNetworkStatisticsAsync(nodeId);
                return TypedResults.Ok(statistics);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取网络统计信息失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("network.statisticsFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> NetworkExists(string networkId, INetworkService networkService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                var exists = await networkService.NetworkExistsAsync(networkId, nodeId);
                return TypedResults.Ok(exists);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "检查网络是否存在失败: {NetworkId}", networkId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("network.existsCheckFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetNetworkIpamInfo(string networkId, INetworkService networkService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                var ipamInfo = await networkService.GetNetworkIpamInfoAsync(networkId, nodeId);
                return TypedResults.Ok(ipamInfo);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取网络IPAM信息失败: {NetworkId}", networkId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("network.ipamFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> UpdateNetwork(string networkId, UpdateNetworkRequest request, INetworkService networkService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                var success = await networkService.UpdateNetworkAsync(networkId, request);
                if (!success)
                {
                    return TypedResults.NotFound(new NetworkNotFoundResponse
                    {
                        Error = localization.GetMessage("network.notFound"),
                        NetworkId = networkId
                    });
                }

                // 获取更新后的网络信息
                var network = await networkService.GetNetworkByIdAsync(networkId, nodeId);
                return TypedResults.Ok(network);
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(ex, "更新网络配置参数错误: {NetworkId}", networkId);
                return TypedResults.BadRequest(new NetworkIdErrorResponse { Error = ex.Message, NetworkId = networkId });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "更新网络配置失败: {NetworkId}", networkId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("network.updateFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }
    }
}
