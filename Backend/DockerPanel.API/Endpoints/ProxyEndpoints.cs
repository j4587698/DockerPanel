using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DockerPanel.API.Models;
using DockerPanel.API.Models.Acme;
using DockerPanel.API.Serialization;
using DockerPanel.API.Services;
using DockerPanel.API.Services.Acme;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// 反向代理(YARP)管理 Minimal API 端点（原 ProxyController）。
    /// </summary>
    public static class ProxyEndpoints
    {
        /// <summary>
        /// 日志类别标记（静态类不能作泛型参数）。
        /// </summary>
        public sealed class LoggingTag
        {
        }

        /// <summary>
        /// 映射反向代理管理相关路由。
        /// </summary>
        public static IEndpointRouteBuilder MapProxyEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/proxy");

            group.MapGet("config", GetConfig);
            group.MapPost("reload", ReloadConfig);
            group.MapPost("routes", AddRoute);
            group.MapPut("routes/{routeId}", UpdateRoute);
            group.MapDelete("routes/{routeId}", RemoveRoute);
            group.MapPost("clusters", AddCluster);
            group.MapPut("clusters/{clusterId}", UpdateCluster);
            group.MapDelete("clusters/{clusterId}", RemoveCluster);
            group.MapPost("mappings", AddDomainMapping);
            group.MapGet("mappings", GetDomainMappings);
            group.MapPut("mappings/{mappingId}", UpdateDomainMapping);
            group.MapDelete("mappings/{mappingId}", RemoveDomainMapping);
            group.MapPut("mappings/{mappingId}/certificate", UpdateMappingCertificate);
            group.MapGet("status", GetYarpStatus);

            return app;
        }

        private static IResult GetConfig(IReverseProxyFactory proxyFactory, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var config = proxyFactory.GetConfig();
                return TypedResults.Ok(config);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取代理配置失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("proxy.configFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ReloadConfig(IReverseProxyFactory proxyFactory, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                await proxyFactory.ReloadConfigAsync();
                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("proxy.reloadSuccess") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "重新加载配置失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("proxy.reloadFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> AddRoute(ProxyRouteConfig route, IReverseProxyFactory proxyFactory, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var success = await proxyFactory.AddRouteAsync(route);
                if (success)
                    return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("proxy.routeAddSuccess") });
                return TypedResults.BadRequest(new ApiErrorResponse { Error = localization.GetMessage("proxy.routeAddFailed") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "添加路由失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("proxy.routeAddFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> UpdateRoute(string routeId, ProxyRouteConfig route, IReverseProxyFactory proxyFactory, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                if (route.RouteId != routeId)
                    return TypedResults.BadRequest(new ApiErrorResponse { Error = localization.GetMessage("proxy.routeIdMismatch") });

                var success = await proxyFactory.UpdateRouteAsync(route);
                if (success)
                    return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("proxy.routeUpdateSuccess") });
                return TypedResults.BadRequest(new ApiErrorResponse { Error = localization.GetMessage("proxy.routeUpdateFailed") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "更新路由失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("proxy.routeUpdateFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> RemoveRoute(string routeId, IReverseProxyFactory proxyFactory, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var success = await proxyFactory.RemoveRouteAsync(routeId);
                if (success)
                    return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("proxy.routeDeleteSuccess") });
                return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("proxy.routeNotFound") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "删除路由失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("proxy.routeDeleteFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> AddCluster(ProxyClusterConfig cluster, IReverseProxyFactory proxyFactory, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var success = await proxyFactory.AddClusterAsync(cluster);
                if (success)
                    return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("proxy.clusterAddSuccess") });
                return TypedResults.BadRequest(new ApiErrorResponse { Error = localization.GetMessage("proxy.clusterAddFailed") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "添加集群失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("proxy.clusterAddFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> UpdateCluster(string clusterId, ProxyClusterConfig cluster, IReverseProxyFactory proxyFactory, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                if (cluster.ClusterId != clusterId)
                    return TypedResults.BadRequest(new ApiErrorResponse { Error = localization.GetMessage("proxy.clusterIdMismatch") });

                var success = await proxyFactory.UpdateClusterAsync(cluster);
                if (success)
                    return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("proxy.clusterUpdateSuccess") });
                return TypedResults.BadRequest(new ApiErrorResponse { Error = localization.GetMessage("proxy.clusterUpdateFailed") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "更新集群失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("proxy.clusterUpdateFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> RemoveCluster(string clusterId, IReverseProxyFactory proxyFactory, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var success = await proxyFactory.RemoveClusterAsync(clusterId);
                if (success)
                    return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("proxy.clusterDeleteSuccess") });
                return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("proxy.clusterNotFound") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "删除集群失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("proxy.clusterDeleteFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> AddDomainMapping(DomainMapping mapping, IReverseProxyFactory proxyFactory, IAcmeService acmeService, ILocalizationService localization, ILogger<LoggingTag> logger)
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
                            var allAccounts = await acmeService.GetAccountsAsync();
                            account = allAccounts.FirstOrDefault(a => a.Id == mapping.AccountId);
                            if (account == null)
                            {
                                logger.LogWarning("指定的ACME账户不存在: {AccountId}", mapping.AccountId);
                            }
                        }

                        // 如果没有指定账户或指定的账户不存在，获取第一个可用账户
                        if (account == null)
                        {
                            var accounts = await acmeService.GetAccountsAsync();
                            account = accounts.FirstOrDefault();
                        }

                        if (account == null)
                        {
                            return TypedResults.BadRequest(new ApiErrorResponse { Error = localization.GetMessage("proxy.noAcmeAccount") });
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

                        var order = await acmeService.OrderCertificateAsync(request);
                        if (order != null)
                        {
                            certificateMessage = $"证书申请已启动（订单ID: {order.Id}），证书将在域名验证通过后自动颁发。验证完成后请手动绑定证书。";
                            logger.LogInformation("已启动证书自动申请流程: OrderId={OrderId}, Domain={Domain}, AccountId={AccountId}",
                                order.Id, mapping.Domain, account.Id);
                        }

                        // 不立即启用 SSL，等证书申请完成后再绑定
                        sslEnabled = false;
                        certificateId = null;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "启动证书申请失败: {Domain}", mapping.Domain);
                        return TypedResults.BadRequest(new ApiErrorResponse { Error = $"启动证书申请失败: {ex.Message}" });
                    }
                }

                // 更新映射配置
                mapping.EnableSsl = sslEnabled;
                mapping.CertificateId = certificateId;

                var success = await proxyFactory.AddDomainMappingAsync(mapping);
                if (success)
                {
                    return TypedResults.Ok(new ProxyMappingAddResponse
                    {
                        Message = certificateMessage ?? localization.GetMessage("proxy.domainMappingAddSuccess"),
                        CertificateRequested = mapping.AutoRequestCertificate && certificateMessage != null
                    });
                }
                return TypedResults.BadRequest(new ApiErrorResponse { Error = localization.GetMessage("proxy.domainMappingAddFailed") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "添加域名映射失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("proxy.domainMappingAddFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetDomainMappings(IReverseProxyFactory proxyFactory, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                // 直接从数据库获取所有映射（包括禁用的）
                var allMappings = await proxyFactory.GetAllDomainMappingsAsync();
                return TypedResults.Ok(allMappings);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取域名映射失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("proxy.getMappingsFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> UpdateDomainMapping(string mappingId, UpdateDomainMappingRequest request, IReverseProxyFactory proxyFactory, IAcmeService acmeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("更新域名映射: mappingId={MappingId}, enabled={Enabled}", mappingId, request.Enabled);

                // 先重新从数据库加载配置，确保内存中有最新的映射
                await proxyFactory.BuildConfigFromDatabaseAsync();

                // 获取所有映射
                var allMappings = await proxyFactory.GetAllDomainMappingsAsync();
                logger.LogInformation("获取到 {Count} 个映射", allMappings.Count);

                var existingMapping = allMappings.FirstOrDefault(m => m.Id == mappingId);

                if (existingMapping == null)
                {
                    logger.LogWarning("映射不存在: {MappingId}", mappingId);
                    return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("proxy.domainMappingNotFound") });
                }

                logger.LogInformation("找到映射: {Domain}, 当前状态: {CurrentEnabled}, 新状态: {NewEnabled}",
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

                // 启用SSL且无证书时，自动触发证书申请
                if (request.EnableSsl == true && string.IsNullOrEmpty(existingMapping.CertificateId) && request.AutoRequestCertificate == true)
                {
                    try
                    {
                        AcmeAccount? account = null;
                        if (!string.IsNullOrEmpty(existingMapping.AccountId))
                        {
                            var allAccounts = await acmeService.GetAccountsAsync();
                            account = allAccounts.FirstOrDefault(a => a.Id == existingMapping.AccountId);
                        }
                        account ??= (await acmeService.GetAccountsAsync()).FirstOrDefault();

                        if (account != null)
                        {
                            var certRequest = new AcmeCertificateRequest
                            {
                                AccountId = account.Id,
                                Domains = new List<string> { existingMapping.Domain },
                                KeyType = "ECDSA256",
                                UseWildcard = false,
                                ChallengeTypes = new List<string> { "http-01" },
                                AcmeProvider = account.Provider == "letsencrypt" ? "letsencrypt" : account.Provider,
                                AccountKey = account.AccountKey,
                                Metadata = new Dictionary<string, object>
                                {
                                    ["autoRequested"] = true,
                                    ["autoRenew"] = true,
                                    ["challengeType"] = "http-01"
                                }
                            };

                            existingMapping.AccountId = account.Id;
                            var order = await acmeService.OrderCertificateAsync(certRequest);
                            if (order != null)
                            {
                                existingMapping.CertificateId = order.Id;
                                existingMapping.EnableSsl = true;
                                logger.LogInformation("已自动申请证书: OrderId={OrderId}, Domain={Domain}",
                                    order.Id, existingMapping.Domain);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "自动申请证书失败: {Domain}", existingMapping.Domain);
                    }
                }

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

                var success = await proxyFactory.UpdateDomainMappingAsync(existingMapping);
                if (success)
                {
                    logger.LogInformation("映射更新成功: {MappingId}", mappingId);
                    return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("proxy.domainMappingUpdateSuccess") });
                }

                logger.LogWarning("映射更新失败: {MappingId}", mappingId);
                return TypedResults.BadRequest(new ApiErrorResponse { Error = localization.GetMessage("proxy.domainMappingUpdateFailed") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "更新域名映射失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("proxy.domainMappingUpdateFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> RemoveDomainMapping(string mappingId, IReverseProxyFactory proxyFactory, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var success = await proxyFactory.RemoveDomainMappingAsync(mappingId);
                if (success)
                    return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("proxy.domainMappingDeleteSuccess") });
                return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("proxy.domainMappingNotFound") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "删除域名映射失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("proxy.domainMappingDeleteFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> UpdateMappingCertificate(string mappingId, UpdateCertificateRequest request, IReverseProxyFactory proxyFactory, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var success = await proxyFactory.UpdateDomainMappingCertificateAsync(mappingId, request.CertificateId);
                if (success)
                    return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("proxy.certificateBindingUpdated") });
                return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("proxy.domainMappingNotFound") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "更新证书绑定失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("proxy.certificateBindingUpdateFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static IResult GetYarpStatus(IReverseProxyFactory proxyFactory, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var config = proxyFactory.GetConfig();
                var mappings = proxyFactory.GetAllDomainMappingsAsync().GetAwaiter().GetResult();

                return TypedResults.Ok(new YarpStatusResponse
                {
                    IsHealthy = true,
                    TotalRoutes = config.Routes.Count,
                    TotalClusters = config.Clusters.Count,
                    TotalDomainMappings = mappings.Count,
                    ActiveMappings = mappings.Count(m => m.Enabled),
                    SslEnabledMappings = mappings.Count(m => m.EnableSsl),
                    LastUpdated = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取代理状态失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("proxy.statusFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }
    }
}
