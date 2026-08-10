using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DockerPanel.API.Models;
using DockerPanel.API.Serialization;
using DockerPanel.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// 节点资源监控 Minimal API 端点（原 NodeResourceController）。
    /// </summary>
    public static class NodeResourceEndpoints
    {
        /// <summary>
        /// 日志类别标记（静态类不能作泛型参数）。
        /// </summary>
        public sealed class LoggingTag
        {
        }

        /// <summary>
        /// 映射节点资源监控相关路由。
        /// </summary>
        public static IEndpointRouteBuilder MapNodeResourceEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/noderesource");

            group.MapGet("overview", GetNodesOverview);
            group.MapGet("{nodeId}/details", GetNodeDetails);
            group.MapGet("{nodeId}/trend", GetNodeTrend);
            group.MapGet("cluster/stats", GetClusterStats);
            group.MapGet("alerts", GetResourceAlerts);
            group.MapPost("alert-rules", CreateAlertRule);
            group.MapGet("alert-rules/{id}", GetAlertRule);
            group.MapGet("alert-rules", GetAlertRules);
            group.MapPut("alert-rules/{id}", UpdateAlertRule);
            group.MapDelete("alert-rules/{id}", DeleteAlertRule);
            group.MapGet("{nodeId}/realtime", GetNodeRealTimeUsage);
            group.MapGet("dashboard", GetClusterDashboard);
            group.MapGet("{nodeId}/performance", GetNodePerformance);

            return app;
        }

        private static async Task<IResult> GetNodesOverview(INodeResourceService nodeResourceService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var overviews = await nodeResourceService.GetNodesResourceOverviewAsync();
                return TypedResults.Ok(overviews);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取节点资源概览失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeResource.overviewFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetNodeDetails(string nodeId, INodeResourceService nodeResourceService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var details = await nodeResourceService.GetNodeResourceDetailsAsync(nodeId);
                if (details == null)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("nodeResource.nodeNotFound") });
                }
                return TypedResults.Ok(details);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取节点资源详情失败: {NodeId}", nodeId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeResource.detailFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetNodeTrend(string nodeId, int hours, INodeResourceService nodeResourceService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var timeRange = TimeSpan.FromHours(hours);
                var trend = await nodeResourceService.GetNodeResourceTrendAsync(nodeId, timeRange);
                if (trend == null)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("nodeResource.nodeNotFound") });
                }
                return TypedResults.Ok(trend);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取节点资源趋势失败: {NodeId}", nodeId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeResource.trendFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetClusterStats(ILocalizationService localization, INodeResourceService nodeResourceService, ILogger<LoggingTag> logger)
        {
            try
            {
                var stats = await nodeResourceService.GetClusterResourceStatsAsync();
                return TypedResults.Ok(stats);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取集群资源统计失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeResource.clusterStatsFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetResourceAlerts(ILogger<LoggingTag> logger, INodeResourceService nodeResourceService, ILocalizationService localization)
        {
            try
            {
                var alerts = await nodeResourceService.GetResourceAlertsAsync();
                return TypedResults.Ok(alerts);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取资源告警失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeResource.alertsFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> CreateAlertRule(CreateResourceAlertRuleRequest request, INodeResourceService nodeResourceService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var rule = await nodeResourceService.CreateAlertRuleAsync(request);
                return TypedResults.Created($"/api/noderesource/alert-rules/{rule.Id}", rule);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "创建资源告警规则失败: {Name}", request.Name);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeResource.alertCreateFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetAlertRule(string id, ILocalizationService localization, ILogger<LoggingTag> logger, INodeResourceService nodeResourceService)
        {
            try
            {
                var rules = await nodeResourceService.GetAlertRulesAsync();
                var rule = rules.FirstOrDefault(r => r.Id == id);

                if (rule == null)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("nodeResource.alertNotFound") });
                }

                return TypedResults.Ok(rule);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取资源告警规则失败: {RuleId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeResource.alertGetFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetAlertRules(INodeResourceService nodeResourceService, ILogger<LoggingTag> logger, ILocalizationService localization)
        {
            try
            {
                var rules = await nodeResourceService.GetAlertRulesAsync();
                return TypedResults.Ok(rules);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取资源告警规则列表失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeResource.alertListFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> UpdateAlertRule(string id, DockerPanel.API.Models.UpdateResourceAlertRuleRequest request, ILogger<LoggingTag> logger, INodeResourceService nodeResourceService, ILocalizationService localization)
        {
            try
            {
                var rule = await nodeResourceService.UpdateAlertRuleAsync(id, request);
                return TypedResults.Ok(rule);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "更新资源告警规则失败: {RuleId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeResource.alertUpdateFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> DeleteAlertRule(string id, ILogger<LoggingTag> logger, ILocalizationService localization, INodeResourceService nodeResourceService)
        {
            try
            {
                var success = await nodeResourceService.DeleteAlertRuleAsync(id);
                if (!success)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("nodeResource.alertNotFound") });
                }

                return TypedResults.NoContent();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "删除资源告警规则失败: {RuleId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeResource.alertDeleteFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetNodeRealTimeUsage(string nodeId, INodeResourceService nodeResourceService, ILogger<LoggingTag> logger, ILocalizationService localization)
        {
            try
            {
                var details = await nodeResourceService.GetNodeResourceDetailsAsync(nodeId);
                if (details == null)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("nodeResource.nodeNotFound") });
                }

                var realTimeData = new NodeRealTimeUsage
                {
                    NodeId = nodeId,
                    NodeName = details.Overview.NodeName,
                    Status = details.Overview.Status.ToString(),
                    Cpu = new NodeRealTimeUsageMetric
                    {
                        Usage = details.Overview.CpuUsage.Percentage,
                        Trend = details.Overview.CpuUsage.Trend.ToString(),
                        Unit = details.Overview.CpuUsage.Unit
                    },
                    Memory = new NodeRealTimeUsageMemory
                    {
                        Usage = details.Overview.MemoryUsage.Percentage,
                        Used = details.Overview.MemoryUsage.Used,
                        Total = details.Overview.MemoryUsage.Total,
                        Trend = details.Overview.MemoryUsage.Trend.ToString(),
                        Unit = details.Overview.MemoryUsage.Unit
                    },
                    Disk = new NodeRealTimeUsageMemory
                    {
                        Usage = details.Overview.DiskUsage.Percentage,
                        Used = details.Overview.DiskUsage.Used,
                        Total = details.Overview.DiskUsage.Total,
                        Trend = details.Overview.DiskUsage.Trend.ToString(),
                        Unit = details.Overview.DiskUsage.Unit
                    },
                    Network = new NodeRealTimeUsageNetwork
                    {
                        BandwidthUsed = details.Overview.NetworkUsage.BandwidthUsed,
                        Connections = details.Overview.NetworkUsage.ConnectionsCount,
                        PacketsIn = details.Overview.NetworkUsage.PacketsIn,
                        PacketsOut = details.Overview.NetworkUsage.PacketsOut
                    },
                    Containers = new NodeRealTimeUsageContainers
                    {
                        Total = details.Overview.ContainerUsage.TotalCount,
                        Running = details.Overview.ContainerUsage.RunningCount,
                        Stopped = details.Overview.ContainerUsage.StoppedCount,
                        UtilizationScore = details.Overview.ContainerUsage.ResourceUtilizationScore
                    },
                    LastUpdated = details.Overview.LastUpdated,
                    Alerts = details.Overview.Alerts
                };

                return TypedResults.Ok(realTimeData);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取节点实时资源使用率失败: {NodeId}", nodeId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeResource.realtimeFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetClusterDashboard(ILocalizationService localization, ILogger<LoggingTag> logger, INodeResourceService nodeResourceService)
        {
            try
            {
                var clusterStats = await nodeResourceService.GetClusterResourceStatsAsync();
                var alerts = await nodeResourceService.GetResourceAlertsAsync();
                var activeAlerts = alerts.Where(a => a.IsActive).Take(10).ToList();

                var dashboard = new ClusterDashboard
                {
                    Cluster = new ClusterDashboardCluster
                    {
                        TotalNodes = clusterStats.TotalNodes,
                        OnlineNodes = clusterStats.OnlineNodes,
                        OfflineNodes = clusterStats.OfflineNodes,
                        WarningNodes = clusterStats.WarningNodes,
                        ErrorNodes = clusterStats.ErrorNodes,
                        UtilizationScore = clusterStats.ClusterUtilizationScore,
                        LastUpdated = clusterStats.LastUpdated
                    },
                    Resources = new ClusterDashboardResources
                    {
                        Cpu = new ClusterDashboardResourceMetric
                        {
                            Used = clusterStats.ClusterCpuUsage?.Used ?? 0,
                            Total = clusterStats.ClusterCpuUsage?.Total ?? 0,
                            Percentage = clusterStats.ClusterCpuUsage?.Percentage ?? 0,
                            AverageUsage = clusterStats.ClusterCpuUsage?.AverageUsage ?? 0
                        },
                        Memory = new ClusterDashboardResourceMetric
                        {
                            Used = clusterStats.ClusterMemoryUsage?.Used ?? 0,
                            Total = clusterStats.ClusterMemoryUsage?.Total ?? 0,
                            Percentage = clusterStats.ClusterMemoryUsage?.Percentage ?? 0,
                            AverageUsage = clusterStats.ClusterMemoryUsage?.AverageUsage ?? 0
                        },
                        Disk = new ClusterDashboardResourceMetric
                        {
                            Used = clusterStats.ClusterDiskUsage?.Used ?? 0,
                            Total = clusterStats.ClusterDiskUsage?.Total ?? 0,
                            Percentage = clusterStats.ClusterDiskUsage?.Percentage ?? 0,
                            AverageUsage = clusterStats.ClusterDiskUsage?.AverageUsage ?? 0
                        }
                    },
                    Containers = new ClusterDashboardContainers
                    {
                        Total = clusterStats.TotalContainers,
                        Running = clusterStats.RunningContainers,
                        Stopped = clusterStats.StoppedContainers
                    },
                    Alerts = new ClusterDashboardAlerts
                    {
                        Total = alerts.Count(a => a.IsActive),
                        Critical = alerts.Count(a => a.IsActive && a.Severity == DockerPanel.API.Models.AlertSeverity.Critical),
                        Warning = alerts.Count(a => a.IsActive && a.Severity == DockerPanel.API.Models.AlertSeverity.Warning),
                        Recent = activeAlerts.Select(a => new ClusterDashboardAlertItem
                        {
                            Id = a.Id,
                            NodeId = a.NodeId,
                            Title = a.Title,
                            Severity = a.Severity,
                            CreatedAt = a.CreatedAt,
                            CurrentValue = a.CurrentValue,
                            Threshold = a.Threshold
                        }).ToList()
                    },
                    CriticalAlerts = clusterStats.CriticalAlerts
                };

                return TypedResults.Ok(dashboard);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取集群仪表盘数据失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeResource.dashboardFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetNodePerformance(string nodeId, ILocalizationService localization, INodeResourceService nodeResourceService, ILogger<LoggingTag> logger)
        {
            try
            {
                var details = await nodeResourceService.GetNodeResourceDetailsAsync(nodeId);
                if (details == null)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("nodeResource.nodeNotFound") });
                }

                var performance = new NodePerformance
                {
                    NodeId = nodeId,
                    NodeName = details.Overview.NodeName,
                    Metrics = new NodePerformanceMetricsBlock
                    {
                        CpuLoadAverage = details.PerformanceMetrics.CpuLoadAverage,
                        MemoryPressure = details.PerformanceMetrics.MemoryPressure,
                        DiskIoWait = details.PerformanceMetrics.DiskIoWait,
                        NetworkLatency = details.PerformanceMetrics.NetworkLatency,
                        ProcessCount = details.PerformanceMetrics.ProcessCount,
                        ThreadCount = details.PerformanceMetrics.ThreadCount,
                        ContextSwitches = details.PerformanceMetrics.ContextSwitches
                    },
                    System = new NodePerformanceSystem
                    {
                        OsType = details.SystemInfo.OsType,
                        KernelVersion = details.SystemInfo.KernelVersion,
                        Architecture = details.SystemInfo.Architecture,
                        CpuCores = details.SystemInfo.CpuCores,
                        TotalMemory = details.SystemInfo.TotalMemory,
                        TotalDisk = details.SystemInfo.TotalDisk,
                        Uptime = details.SystemInfo.Uptime,
                        BootTime = details.SystemInfo.BootTime
                    },
                    Docker = new NodePerformanceDocker
                    {
                        Version = details.DockerInfo.Version,
                        ApiVersion = details.DockerInfo.ApiVersion,
                        Containers = details.DockerInfo.Containers,
                        Images = details.DockerInfo.Images,
                        Networks = details.DockerInfo.Networks,
                        Volumes = details.DockerInfo.Volumes,
                        ServerVersion = details.DockerInfo.ServerVersion
                    },
                    LastUpdated = DateTime.UtcNow
                };

                return TypedResults.Ok(performance);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取节点性能指标失败: {NodeId}", nodeId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("nodeResource.performanceFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }
    }

    /// <summary>
    /// 节点实时资源使用率
    /// </summary>
    public sealed class NodeRealTimeUsage
    {
        public string NodeId { get; set; } = string.Empty;
        public string NodeName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public NodeRealTimeUsageMetric Cpu { get; set; } = new();
        public NodeRealTimeUsageMemory Memory { get; set; } = new();
        public NodeRealTimeUsageMemory Disk { get; set; } = new();
        public NodeRealTimeUsageNetwork Network { get; set; } = new();
        public NodeRealTimeUsageContainers Containers { get; set; } = new();
        public DateTime LastUpdated { get; set; }
        public List<string> Alerts { get; set; } = new();
    }

    /// <summary>
    /// 实时资源指标
    /// </summary>
    public sealed class NodeRealTimeUsageMetric
    {
        public double Usage { get; set; }
        public string Trend { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
    }

    /// <summary>
    /// 实时内存/磁盘指标
    /// </summary>
    public sealed class NodeRealTimeUsageMemory
    {
        public double Usage { get; set; }
        public double Used { get; set; }
        public double Total { get; set; }
        public string Trend { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
    }

    /// <summary>
    /// 实时网络指标
    /// </summary>
    public sealed class NodeRealTimeUsageNetwork
    {
        public double BandwidthUsed { get; set; }
        public int Connections { get; set; }
        public double PacketsIn { get; set; }
        public double PacketsOut { get; set; }
    }

    /// <summary>
    /// 实时容器指标
    /// </summary>
    public sealed class NodeRealTimeUsageContainers
    {
        public int Total { get; set; }
        public int Running { get; set; }
        public int Stopped { get; set; }
        public double UtilizationScore { get; set; }
    }

    /// <summary>
    /// 集群仪表盘数据
    /// </summary>
    public sealed class ClusterDashboard
    {
        public ClusterDashboardCluster Cluster { get; set; } = new();
        public ClusterDashboardResources Resources { get; set; } = new();
        public ClusterDashboardContainers Containers { get; set; } = new();
        public ClusterDashboardAlerts Alerts { get; set; } = new();
        public List<string> CriticalAlerts { get; set; } = new();
    }

    /// <summary>
    /// 集群概览
    /// </summary>
    public sealed class ClusterDashboardCluster
    {
        public int TotalNodes { get; set; }
        public int OnlineNodes { get; set; }
        public int OfflineNodes { get; set; }
        public int WarningNodes { get; set; }
        public int ErrorNodes { get; set; }
        public double UtilizationScore { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// 集群资源块
    /// </summary>
    public sealed class ClusterDashboardResources
    {
        public ClusterDashboardResourceMetric Cpu { get; set; } = new();
        public ClusterDashboardResourceMetric Memory { get; set; } = new();
        public ClusterDashboardResourceMetric Disk { get; set; } = new();
    }

    /// <summary>
    /// 集群资源指标
    /// </summary>
    public sealed class ClusterDashboardResourceMetric
    {
        public double Used { get; set; }
        public double Total { get; set; }
        public double Percentage { get; set; }
        public double AverageUsage { get; set; }
    }

    /// <summary>
    /// 集群容器统计
    /// </summary>
    public sealed class ClusterDashboardContainers
    {
        public int Total { get; set; }
        public int Running { get; set; }
        public int Stopped { get; set; }
    }

    /// <summary>
    /// 集群告警统计
    /// </summary>
    public sealed class ClusterDashboardAlerts
    {
        public int Total { get; set; }
        public int Critical { get; set; }
        public int Warning { get; set; }
        public List<ClusterDashboardAlertItem> Recent { get; set; } = new();
    }

    /// <summary>
    /// 集群告警条目
    /// </summary>
    public sealed class ClusterDashboardAlertItem
    {
        public string Id { get; set; } = string.Empty;
        public string NodeId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DockerPanel.API.Models.AlertSeverity Severity { get; set; }
        public DateTime CreatedAt { get; set; }
        public double CurrentValue { get; set; }
        public double Threshold { get; set; }
    }

    /// <summary>
    /// 节点性能指标
    /// </summary>
    public sealed class NodePerformance
    {
        public string NodeId { get; set; } = string.Empty;
        public string NodeName { get; set; } = string.Empty;
        public NodePerformanceMetricsBlock Metrics { get; set; } = new();
        public NodePerformanceSystem System { get; set; } = new();
        public NodePerformanceDocker Docker { get; set; } = new();
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// 性能指标块
    /// </summary>
    public sealed class NodePerformanceMetricsBlock
    {
        public double CpuLoadAverage { get; set; }
        public double MemoryPressure { get; set; }
        public double DiskIoWait { get; set; }
        public double NetworkLatency { get; set; }
        public int ProcessCount { get; set; }
        public double ThreadCount { get; set; }
        public double ContextSwitches { get; set; }
    }

    /// <summary>
    /// 系统信息块
    /// </summary>
    public sealed class NodePerformanceSystem
    {
        public string OsType { get; set; } = string.Empty;
        public string KernelVersion { get; set; } = string.Empty;
        public string Architecture { get; set; } = string.Empty;
        public int CpuCores { get; set; }
        public long TotalMemory { get; set; }
        public long TotalDisk { get; set; }
        public double Uptime { get; set; }
        public DateTime BootTime { get; set; }
    }

    /// <summary>
    /// Docker 信息块
    /// </summary>
    public sealed class NodePerformanceDocker
    {
        public string Version { get; set; } = string.Empty;
        public string ApiVersion { get; set; } = string.Empty;
        public int Containers { get; set; }
        public int Images { get; set; }
        public int Networks { get; set; }
        public int Volumes { get; set; }
        public string ServerVersion { get; set; } = string.Empty;
    }
}
