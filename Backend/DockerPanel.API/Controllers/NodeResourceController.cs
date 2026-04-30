using Microsoft.AspNetCore.Mvc;
using DockerPanel.API.Services;
using DockerPanel.API.Models;

namespace DockerPanel.API.Controllers;

/// <summary>
/// 节点资源监控控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class NodeResourceController : ControllerBase
{
    private readonly INodeResourceService _nodeResourceService;
    private readonly ILogger<NodeResourceController> _logger;
    private readonly ILocalizationService _localization;

    public NodeResourceController(INodeResourceService nodeResourceService, ILogger<NodeResourceController> logger, ILocalizationService localization)
    {
        _nodeResourceService = nodeResourceService;
        _logger = logger;
        _localization = localization;
    }

    /// <summary>
    /// 获取所有节点的资源概览
    /// </summary>
    [HttpGet("overview")]
    public async Task<ActionResult<IEnumerable<NodeResourceOverview>>> GetNodesOverview()
    {
        try
        {
            var overviews = await _nodeResourceService.GetNodesResourceOverviewAsync();
            return Ok(overviews);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取节点资源概览失败");
            return StatusCode(500, new { message = _localization.GetMessage("nodeResource.overviewFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 获取指定节点的详细资源信息
    /// </summary>
    [HttpGet("{nodeId}/details")]
    public async Task<ActionResult<NodeResourceDetails>> GetNodeDetails(string nodeId)
    {
        try
        {
            var details = await _nodeResourceService.GetNodeResourceDetailsAsync(nodeId);
            if (details == null)
            {
                return NotFound(new { message = _localization.GetMessage("nodeResource.nodeNotFound") });
            }
            return Ok(details);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取节点资源详情失败: {NodeId}", nodeId);
            return StatusCode(500, new { message = _localization.GetMessage("nodeResource.detailFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 获取节点的历史资源使用趋势
    /// </summary>
    [HttpGet("{nodeId}/trend")]
    public async Task<ActionResult<NodeResourceTrend>> GetNodeTrend(
        string nodeId,
        [FromQuery] int hours = 24)
    {
        try
        {
            var timeRange = TimeSpan.FromHours(hours);
            var trend = await _nodeResourceService.GetNodeResourceTrendAsync(nodeId, timeRange);
            if (trend == null)
            {
                return NotFound(new { message = _localization.GetMessage("nodeResource.nodeNotFound") });
            }
            return Ok(trend);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取节点资源趋势失败: {NodeId}", nodeId);
            return StatusCode(500, new { message = _localization.GetMessage("nodeResource.trendFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 获取集群资源统计
    /// </summary>
    [HttpGet("cluster/stats")]
    public async Task<ActionResult<ClusterResourceStats>> GetClusterStats()
    {
        try
        {
            var stats = await _nodeResourceService.GetClusterResourceStatsAsync();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取集群资源统计失败");
            return StatusCode(500, new { message = _localization.GetMessage("nodeResource.clusterStatsFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 获取资源告警信息
    /// </summary>
    [HttpGet("alerts")]
    public async Task<ActionResult<IEnumerable<DockerPanel.API.Models.ResourceAlert>>> GetResourceAlerts()
    {
        try
        {
            var alerts = await _nodeResourceService.GetResourceAlertsAsync();
            return Ok(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取资源告警失败");
            return StatusCode(500, new { message = _localization.GetMessage("nodeResource.alertsFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 创建资源告警规则
    /// </summary>
    [HttpPost("alert-rules")]
    public async Task<ActionResult<DockerPanel.API.Models.ResourceAlertRule>> CreateAlertRule([FromBody] DockerPanel.API.Models.CreateResourceAlertRuleRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var rule = await _nodeResourceService.CreateAlertRuleAsync(request);
            return CreatedAtAction(nameof(GetAlertRule), new { id = rule.Id }, rule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建资源告警规则失败: {Name}", request.Name);
            return StatusCode(500, new { message = _localization.GetMessage("nodeResource.alertCreateFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 获取指定的资源告警规则
    /// </summary>
    [HttpGet("alert-rules/{id}")]
    public async Task<ActionResult<DockerPanel.API.Models.ResourceAlertRule>> GetAlertRule(string id)
    {
        try
        {
            var rules = await _nodeResourceService.GetAlertRulesAsync();
            var rule = rules.FirstOrDefault(r => r.Id == id);

            if (rule == null)
            {
                return NotFound(new { message = _localization.GetMessage("nodeResource.alertNotFound") });
            }

            return Ok(rule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取资源告警规则失败: {RuleId}", id);
            return StatusCode(500, new { message = _localization.GetMessage("nodeResource.alertGetFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 获取所有资源告警规则
    /// </summary>
    [HttpGet("alert-rules")]
    public async Task<ActionResult<IEnumerable<DockerPanel.API.Models.ResourceAlertRule>>> GetAlertRules()
    {
        try
        {
            var rules = await _nodeResourceService.GetAlertRulesAsync();
            return Ok(rules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取资源告警规则列表失败");
            return StatusCode(500, new { message = _localization.GetMessage("nodeResource.alertListFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 更新资源告警规则
    /// </summary>
    [HttpPut("alert-rules/{id}")]
    public async Task<ActionResult<DockerPanel.API.Models.ResourceAlertRule>> UpdateAlertRule(string id, [FromBody] DockerPanel.API.Models.UpdateResourceAlertRuleRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var rule = await _nodeResourceService.UpdateAlertRuleAsync(id, request);
            return Ok(rule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新资源告警规则失败: {RuleId}", id);
            return StatusCode(500, new { message = _localization.GetMessage("nodeResource.alertUpdateFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 删除资源告警规则
    /// </summary>
    [HttpDelete("alert-rules/{id}")]
    public async Task<ActionResult> DeleteAlertRule(string id)
    {
        try
        {
            var success = await _nodeResourceService.DeleteAlertRuleAsync(id);
            if (!success)
            {
                return NotFound(new { message = _localization.GetMessage("nodeResource.alertNotFound") });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除资源告警规则失败: {RuleId}", id);
            return StatusCode(500, new { message = _localization.GetMessage("nodeResource.alertDeleteFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 获取节点实时资源使用率（用于仪表盘）
    /// </summary>
    [HttpGet("{nodeId}/realtime")]
    public async Task<ActionResult> GetNodeRealTimeUsage(string nodeId)
    {
        try
        {
            var details = await _nodeResourceService.GetNodeResourceDetailsAsync(nodeId);
            if (details == null)
            {
                return NotFound(new { message = _localization.GetMessage("nodeResource.nodeNotFound") });
            }

            var realTimeData = new
            {
                nodeId = nodeId,
                nodeName = details.Overview.NodeName,
                status = details.Overview.Status,
                cpu = new
                {
                    usage = details.Overview.CpuUsage.Percentage,
                    trend = details.Overview.CpuUsage.Trend.ToString(),
                    unit = details.Overview.CpuUsage.Unit
                },
                memory = new
                {
                    usage = details.Overview.MemoryUsage.Percentage,
                    used = details.Overview.MemoryUsage.Used,
                    total = details.Overview.MemoryUsage.Total,
                    trend = details.Overview.MemoryUsage.Trend.ToString(),
                    unit = details.Overview.MemoryUsage.Unit
                },
                disk = new
                {
                    usage = details.Overview.DiskUsage.Percentage,
                    used = details.Overview.DiskUsage.Used,
                    total = details.Overview.DiskUsage.Total,
                    trend = details.Overview.DiskUsage.Trend.ToString(),
                    unit = details.Overview.DiskUsage.Unit
                },
                network = new
                {
                    bandwidthUsed = details.Overview.NetworkUsage.BandwidthUsed,
                    connections = details.Overview.NetworkUsage.ConnectionsCount,
                    packetsIn = details.Overview.NetworkUsage.PacketsIn,
                    packetsOut = details.Overview.NetworkUsage.PacketsOut
                },
                containers = new
                {
                    total = details.Overview.ContainerUsage.TotalCount,
                    running = details.Overview.ContainerUsage.RunningCount,
                    stopped = details.Overview.ContainerUsage.StoppedCount,
                    utilizationScore = details.Overview.ContainerUsage.ResourceUtilizationScore
                },
                lastUpdated = details.Overview.LastUpdated,
                alerts = details.Overview.Alerts
            };

            return Ok(realTimeData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取节点实时资源使用率失败: {NodeId}", nodeId);
            return StatusCode(500, new { message = _localization.GetMessage("nodeResource.realtimeFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 获取集群仪表盘数据
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult> GetClusterDashboard()
    {
        try
        {
            var clusterStats = await _nodeResourceService.GetClusterResourceStatsAsync();
            var alerts = await _nodeResourceService.GetResourceAlertsAsync();
            var activeAlerts = alerts.Where(a => a.IsActive).Take(10).ToList();

            var dashboard = new
            {
                cluster = new
                {
                    totalNodes = clusterStats.TotalNodes,
                    onlineNodes = clusterStats.OnlineNodes,
                    offlineNodes = clusterStats.OfflineNodes,
                    warningNodes = clusterStats.WarningNodes,
                    errorNodes = clusterStats.ErrorNodes,
                    utilizationScore = clusterStats.ClusterUtilizationScore,
                    lastUpdated = clusterStats.LastUpdated
                },
                resources = new
                {
                    cpu = new
                    {
                        used = clusterStats.ClusterCpuUsage?.Used ?? 0,
                        total = clusterStats.ClusterCpuUsage?.Total ?? 0,
                        percentage = clusterStats.ClusterCpuUsage?.Percentage ?? 0,
                        averageUsage = clusterStats.ClusterCpuUsage?.AverageUsage ?? 0
                    },
                    memory = new
                    {
                        used = clusterStats.ClusterMemoryUsage?.Used ?? 0,
                        total = clusterStats.ClusterMemoryUsage?.Total ?? 0,
                        percentage = clusterStats.ClusterMemoryUsage?.Percentage ?? 0,
                        averageUsage = clusterStats.ClusterMemoryUsage?.AverageUsage ?? 0
                    },
                    disk = new
                    {
                        used = clusterStats.ClusterDiskUsage?.Used ?? 0,
                        total = clusterStats.ClusterDiskUsage?.Total ?? 0,
                        percentage = clusterStats.ClusterDiskUsage?.Percentage ?? 0,
                        averageUsage = clusterStats.ClusterDiskUsage?.AverageUsage ?? 0
                    }
                },
                containers = new
                {
                    total = clusterStats.TotalContainers,
                    running = clusterStats.RunningContainers,
                    stopped = clusterStats.StoppedContainers
                },
                alerts = new
                {
                    total = alerts.Count(a => a.IsActive),
                    critical = alerts.Count(a => a.IsActive && a.Severity == DockerPanel.API.Models.AlertSeverity.Critical),
                    warning = alerts.Count(a => a.IsActive && a.Severity == DockerPanel.API.Models.AlertSeverity.Warning),
                    recent = activeAlerts.Select(a => new
                    {
                        id = a.Id,
                        nodeId = a.NodeId,
                        title = a.Title,
                        severity = a.Severity,
                        createdAt = a.CreatedAt,
                        currentValue = a.CurrentValue,
                        threshold = a.Threshold
                    }).ToList()
                },
                criticalAlerts = clusterStats.CriticalAlerts
            };

            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取集群仪表盘数据失败");
            return StatusCode(500, new { message = _localization.GetMessage("nodeResource.dashboardFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 获取节点性能指标
    /// </summary>
    [HttpGet("{nodeId}/performance")]
    public async Task<ActionResult> GetNodePerformance(string nodeId)
    {
        try
        {
            var details = await _nodeResourceService.GetNodeResourceDetailsAsync(nodeId);
            if (details == null)
            {
                return NotFound(new { message = _localization.GetMessage("nodeResource.nodeNotFound") });
            }

            var performance = new
            {
                nodeId = nodeId,
                nodeName = details.Overview.NodeName,
                metrics = new
                {
                    cpuLoadAverage = details.PerformanceMetrics.CpuLoadAverage,
                    memoryPressure = details.PerformanceMetrics.MemoryPressure,
                    diskIoWait = details.PerformanceMetrics.DiskIoWait,
                    networkLatency = details.PerformanceMetrics.NetworkLatency,
                    processCount = details.PerformanceMetrics.ProcessCount,
                    threadCount = details.PerformanceMetrics.ThreadCount,
                    contextSwitches = details.PerformanceMetrics.ContextSwitches
                },
                system = new
                {
                    osType = details.SystemInfo.OsType,
                    kernelVersion = details.SystemInfo.KernelVersion,
                    architecture = details.SystemInfo.Architecture,
                    cpuCores = details.SystemInfo.CpuCores,
                    totalMemory = details.SystemInfo.TotalMemory,
                    totalDisk = details.SystemInfo.TotalDisk,
                    uptime = details.SystemInfo.Uptime,
                    bootTime = details.SystemInfo.BootTime
                },
                docker = new
                {
                    version = details.DockerInfo.Version,
                    apiVersion = details.DockerInfo.ApiVersion,
                    containers = details.DockerInfo.Containers,
                    images = details.DockerInfo.Images,
                    networks = details.DockerInfo.Networks,
                    volumes = details.DockerInfo.Volumes,
                    serverVersion = details.DockerInfo.ServerVersion
                },
                lastUpdated = DateTime.UtcNow
            };

            return Ok(performance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取节点性能指标失败: {NodeId}", nodeId);
            return StatusCode(500, new { message = _localization.GetMessage("nodeResource.performanceFailed"), error = ex.Message });
        }
    }
}