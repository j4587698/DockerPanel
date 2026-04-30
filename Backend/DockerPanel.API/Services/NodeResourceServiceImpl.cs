using DockerPanel.API.Data;
using DockerPanel.API.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.Json;

namespace DockerPanel.API.Services;

/// <summary>
/// 简单节点资源服务实现
/// </summary>
public class NodeResourceServiceImpl : INodeResourceService
{
    private readonly ILogger<NodeResourceServiceImpl> _logger;
    private readonly IMemoryCache _cache;
    private readonly INodeService _nodeService;
    private readonly ISshService _sshService;
    private readonly IContainerEngine _dockerEngine;
    private readonly TinyDbContext _dbContext;

    public NodeResourceServiceImpl(
        ILogger<NodeResourceServiceImpl> logger,
        IMemoryCache cache,
        INodeService nodeService,
        ISshService sshService,
        IContainerEngine dockerEngine,
        TinyDbContext dbContext)
    {
        _logger = logger;
        _cache = cache;
        _nodeService = nodeService;
        _sshService = sshService;
        _dockerEngine = dockerEngine;
        _dbContext = dbContext;
    }

    public async Task<NodeResourceOverview?> GetNodeResourceOverviewAsync(string nodeId)
    {
        try
        {
            _logger.LogInformation("获取节点资源概览: {NodeId}", nodeId);

            var node = await _nodeService.GetNodeByIdAsync(nodeId);
            if (node == null)
            {
                _logger.LogWarning("节点不存在: {NodeId}", nodeId);
                return null;
            }

            // 转换NodeInfo到Node
            var nodeObj = ConvertToNode(node);
            var overview = await GetNodeResourceOverviewAsync(nodeObj);

            _logger.LogInformation("成功获取节点资源概览: {NodeId}", nodeId);
            return overview;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取节点资源概览失败: {NodeId}", nodeId);
            return null;
        }
    }

    public async Task<IEnumerable<NodeResourceOverview>> GetNodesResourceOverviewAsync()
    {
        try
        {
            _logger.LogInformation("获取所有节点资源概览");

            var nodes = await _nodeService.GetNodesAsync();
            var overviews = new List<NodeResourceOverview>();

            foreach (var node in nodes)
            {
                var overview = await GetNodeResourceOverviewAsync(ConvertToNode(node));
                if (overview != null)
                {
                    overviews.Add(overview);
                }
            }

            _logger.LogInformation("成功获取 {Count} 个节点的资源概览", overviews.Count);
            return overviews;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取节点资源概览失败");
            throw;
        }
    }

    public async Task<NodeResourceDetails?> GetNodeResourceDetailsAsync(string nodeId)
    {
        try
        {
            _logger.LogInformation("获取节点详细资源信息: {NodeId}", nodeId);

            var cacheKey = $"node_resource_details_{nodeId}";
            if (_cache.TryGetValue(cacheKey, out NodeResourceDetails? cachedDetails))
            {
                _logger.LogDebug("从缓存获取节点资源详情: {NodeId}", nodeId);
                return cachedDetails;
            }

            var nodeInfo = await _nodeService.GetNodeByIdAsync(nodeId);
            if (nodeInfo == null)
            {
                _logger.LogWarning("节点不存在: {NodeId}", nodeId);
                return null;
            }

            var node = ConvertToNode(nodeInfo);
            var details = await CollectNodeResourceDetailsAsync(node);

            // 缓存5分钟
            _cache.Set(cacheKey, details, TimeSpan.FromMinutes(5));

            _logger.LogInformation("成功获取节点资源详情: {NodeId}", nodeId);
            return details;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取节点资源详情失败: {NodeId}", nodeId);
            throw;
        }
    }

    public async Task<NodeResourceTrend?> GetNodeResourceTrendAsync(string nodeId, TimeSpan timeRange)
    {
        try
        {
            _logger.LogInformation("获取节点资源趋势: {NodeId} 时间范围: {TimeRange}", nodeId, timeRange);

            var nodeInfo = await _nodeService.GetNodeByIdAsync(nodeId);
            if (nodeInfo == null)
            {
                _logger.LogWarning("节点不存在: {NodeId}", nodeId);
                return null;
            }

            var node = ConvertToNode(nodeInfo);
            var endTime = DateTime.UtcNow;
            var startTime = endTime - timeRange;
            var interval = TimeSpan.FromMinutes(5); // 5分钟间隔

            var trend = await CollectResourceTrendAsync(node, startTime, endTime, interval);

            _logger.LogInformation("成功获取节点资源趋势: {NodeId} 数据点: {DataPoints}",
                nodeId, trend?.CpuTrend.Count ?? 0);

            return trend;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取节点资源趋势失败: {NodeId}", nodeId);
            throw;
        }
    }

    public async Task<ClusterResourceStats> GetClusterResourceStatsAsync()
    {
        try
        {
            _logger.LogInformation("获取集群资源统计");

            var cacheKey = "cluster_resource_stats";
            if (_cache.TryGetValue(cacheKey, out ClusterResourceStats? cachedStats))
            {
                _logger.LogDebug("从缓存获取集群资源统计");
                return cachedStats!;
            }

            var nodes = await _nodeService.GetNodesAsync();
            var stats = new ClusterResourceStats
            {
                LastUpdated = DateTime.UtcNow,
                CriticalAlerts = new List<string>()
            };

            var totalCpuUsed = 0.0;
            var totalCpuTotal = 0.0;
            var totalMemoryUsed = 0L;
            var totalMemoryTotal = 0L;
            var totalDiskUsed = 0L;
            var totalDiskTotal = 0L;

            foreach (var node in nodes)
            {
                var overview = await GetNodeResourceOverviewAsync(ConvertToNode(node));
                if (overview != null)
                {
                    stats.TotalNodes++;

                    switch (overview.Status)
                    {
                        case NodeResourceStatus.Online:
                            stats.OnlineNodes++;
                            break;
                        case NodeResourceStatus.Offline:
                            // stats.OfflineNodes++; // 属性是只读的，不能直接赋值
                            stats.CriticalAlerts.Add($"节点 {overview.NodeName} 离线");
                            break;
                        case NodeResourceStatus.Warning:
                            stats.WarningNodes++;
                            break;
                        case NodeResourceStatus.Error:
                            stats.ErrorNodes++;
                            stats.CriticalAlerts.Add($"节点 {overview.NodeName} 错误");
                            break;
                    }

                    totalCpuUsed += overview.CpuUsage.Used;
                    totalCpuTotal += overview.CpuUsage.Total;
                    totalMemoryUsed += (long)overview.MemoryUsage.Used;
                    totalMemoryTotal += (long)overview.MemoryUsage.Total;
                    totalDiskUsed += (long)overview.DiskUsage.Used;
                    totalDiskTotal += (long)overview.DiskUsage.Total;

                    stats.TotalContainers += overview.ContainerUsage.TotalCount;
                    stats.RunningContainers += overview.ContainerUsage.RunningCount;
                    stats.StoppedContainers += overview.ContainerUsage.StoppedCount;
                }
            }

            // 计算集群资源使用情况
            if (totalCpuTotal > 0)
            {
                stats.ClusterCpuUsage = new ClusterCpuUsage
                {
                    Used = totalCpuUsed,
                    Total = totalCpuTotal,
                    Percentage = (totalCpuUsed / totalCpuTotal) * 100,
                    AverageUsage = stats.OnlineNodes > 0 ? totalCpuUsed / stats.OnlineNodes : 0,
                    MaxUsage = totalCpuUsed, // 简化处理
                    MinUsage = 0
                };
            }

            if (totalMemoryTotal > 0)
            {
                stats.ClusterMemoryUsage = new ClusterMemoryUsage
                {
                    Used = totalMemoryUsed,
                    Total = totalMemoryTotal,
                    Percentage = ((double)totalMemoryUsed / totalMemoryTotal) * 100,
                    AverageUsage = stats.OnlineNodes > 0 ? (double)totalMemoryUsed / stats.OnlineNodes : 0,
                    MaxUsage = (double)totalMemoryUsed,
                    MinUsage = 0
                };
            }

            if (totalDiskTotal > 0)
            {
                stats.ClusterDiskUsage = new ClusterDiskUsage
                {
                    Used = totalDiskUsed,
                    Total = totalDiskTotal,
                    Percentage = ((double)totalDiskUsed / totalDiskTotal) * 100,
                    AverageUsage = stats.OnlineNodes > 0 ? (double)totalDiskUsed / stats.OnlineNodes : 0,
                    MaxUsage = (double)totalDiskUsed,
                    MinUsage = 0
                };
            }

            // 计算集群利用率评分
            stats.ClusterUtilizationScore = CalculateClusterUtilizationScore(stats);

            // Ensure all numeric values are valid (not Infinity or NaN)
            if (stats.ClusterCpuUsage != null)
            {
                stats.ClusterCpuUsage.Percentage = SanitizeDouble(stats.ClusterCpuUsage.Percentage);
                stats.ClusterCpuUsage.AverageUsage = SanitizeDouble(stats.ClusterCpuUsage.AverageUsage);
                stats.ClusterCpuUsage.Used = SanitizeDouble(stats.ClusterCpuUsage.Used);
                stats.ClusterCpuUsage.Total = SanitizeDouble(stats.ClusterCpuUsage.Total);
            }
            if (stats.ClusterMemoryUsage != null)
            {
                stats.ClusterMemoryUsage.Percentage = SanitizeDouble(stats.ClusterMemoryUsage.Percentage);
                stats.ClusterMemoryUsage.AverageUsage = SanitizeDouble(stats.ClusterMemoryUsage.AverageUsage);
            }
            if (stats.ClusterDiskUsage != null)
            {
                stats.ClusterDiskUsage.Percentage = SanitizeDouble(stats.ClusterDiskUsage.Percentage);
                stats.ClusterDiskUsage.AverageUsage = SanitizeDouble(stats.ClusterDiskUsage.AverageUsage);
            }
            stats.ClusterUtilizationScore = SanitizeDouble(stats.ClusterUtilizationScore);

            // 缓存2分钟
            _cache.Set(cacheKey, stats, TimeSpan.FromMinutes(2));

            _logger.LogInformation("成功获取集群资源统计: 总节点 {TotalNodes} 在线节点 {OnlineNodes}",
                stats.TotalNodes, stats.OnlineNodes);

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取集群资源统计失败");
            throw;
        }
    }

    public async Task<IEnumerable<DockerPanel.API.Models.ResourceAlert>> GetResourceAlertsAsync()
    {
        try
        {
            _logger.LogInformation("获取资源告警信息");

            var cacheKey = "resource_alerts";
            if (_cache.TryGetValue(cacheKey, out List<DockerPanel.API.Models.ResourceAlert>? cachedAlerts))
            {
                _logger.LogDebug("从缓存获取资源告警");
                return cachedAlerts!;
            }

            var alerts = new List<DockerPanel.API.Models.ResourceAlert>();
            var nodes = await _nodeService.GetNodesAsync();

            foreach (var node in nodes)
            {
                var nodeAlerts = await CheckNodeResourceAlertsAsync(ConvertToNode(node));
                alerts.AddRange(nodeAlerts);
            }

            // 按严重程度和时间排序
            alerts = alerts.OrderByDescending(a => a.Severity)
                          .ThenByDescending(a => a.CreatedAt)
                          .ToList();

            // 缓存1分钟
            _cache.Set(cacheKey, alerts, TimeSpan.FromMinutes(1));

            _logger.LogInformation("成功获取资源告警: 告警数量 {AlertCount}", alerts.Count);
            return alerts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取资源告警失败");
            throw;
        }
    }

    public async Task<DockerPanel.API.Models.ResourceAlertRule> CreateAlertRuleAsync(DockerPanel.API.Models.CreateResourceAlertRuleRequest request)
    {
        try
        {
            _logger.LogInformation("创建资源告警规则: {Name}", request.Name);

            var nodeName = string.Empty;
            if (!string.IsNullOrWhiteSpace(request.NodeId))
            {
                var node = await _nodeService.GetNodeByIdAsync(request.NodeId);
                nodeName = node?.Name ?? string.Empty;
            }

            var rule = new DockerPanel.API.Models.ResourceAlertRule
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                Description = request.Description,
                NodeId = request.NodeId ?? string.Empty,
                NodeName = nodeName,
                ResourceType = request.ResourceType,
                MetricType = string.IsNullOrWhiteSpace(request.MetricType) ? "usage" : request.MetricType,
                ThresholdValue = request.ThresholdValue,
                ComparisonOperator = request.ComparisonOperator ?? ">",
                EvaluationPeriod = request.EvaluationPeriod,
                ConsecutiveEvaluations = request.ConsecutiveEvaluations,
                Severity = request.Severity,
                IsEnabled = request.IsEnabled,
                NotificationEmails = request.NotificationEmails,
                NotificationWebhooks = request.NotificationWebhooks,
                Metadata = request.Metadata,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Status = new DockerPanel.API.Models.AlertRuleStatus
                {
                    State = "normal",
                    LastEvaluation = DateTime.UtcNow,
                    CurrentConsecutive = 0,
                    CurrentValue = 0,
                    IsTriggered = false
                }
            };

            var rulesCollection = _dbContext.GetCollection<DockerPanel.API.Models.ResourceAlertRule>("resource_alert_rules");
            rulesCollection.Insert(rule);
            _cache.Remove("resource_alert_rules");
            _cache.Remove("resource_alerts");

            _logger.LogInformation("成功创建资源告警规则: {RuleId}", rule.Id);
            return rule;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建资源告警规则失败: {Name}", request.Name);
            throw;
        }
    }

    public async Task<DockerPanel.API.Models.ResourceAlertRule> UpdateAlertRuleAsync(string id, DockerPanel.API.Models.UpdateResourceAlertRuleRequest request)
    {
        try
        {
            _logger.LogInformation("更新资源告警规则: {RuleId}", id);

            var rulesCollection = _dbContext.GetCollection<DockerPanel.API.Models.ResourceAlertRule>("resource_alert_rules");
            var rule = rulesCollection.FindById(id);
            if (rule == null)
            {
                throw new KeyNotFoundException($"资源告警规则不存在: {id}");
            }

            if (request.Name != null) rule.Name = request.Name;
            if (request.Description != null) rule.Description = request.Description;
            if (request.ResourceType != null) rule.ResourceType = request.ResourceType;
            if (request.MetricType != null) rule.MetricType = request.MetricType;
            if (request.ThresholdValue.HasValue) rule.ThresholdValue = request.ThresholdValue.Value;
            if (request.ComparisonOperator != null) rule.ComparisonOperator = request.ComparisonOperator;
            if (request.EvaluationPeriod.HasValue) rule.EvaluationPeriod = request.EvaluationPeriod.Value;
            if (request.ConsecutiveEvaluations.HasValue) rule.ConsecutiveEvaluations = request.ConsecutiveEvaluations.Value;
            if (request.Severity.HasValue) rule.Severity = request.Severity.Value;
            if (request.IsEnabled.HasValue) rule.IsEnabled = request.IsEnabled.Value;
            if (request.NotificationEmails != null) rule.NotificationEmails = request.NotificationEmails;
            if (request.NotificationWebhooks != null) rule.NotificationWebhooks = request.NotificationWebhooks;
            if (request.Metadata != null) rule.Metadata = request.Metadata;
            rule.UpdatedAt = DateTime.UtcNow;

            rulesCollection.Update(rule);
            _cache.Remove("resource_alert_rules");
            _cache.Remove("resource_alerts");

            _logger.LogInformation("成功更新资源告警规则: {RuleId}", id);
            return rule;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新资源告警规则失败: {RuleId}", id);
            throw;
        }
    }

    public async Task<bool> DeleteAlertRuleAsync(string id)
    {
        try
        {
            _logger.LogInformation("删除资源告警规则: {RuleId}", id);

            var rulesCollection = _dbContext.GetCollection<DockerPanel.API.Models.ResourceAlertRule>("resource_alert_rules");
            var deleted = rulesCollection.Delete(id) > 0;
            _cache.Remove("resource_alert_rules");
            _cache.Remove("resource_alerts");

            _logger.LogInformation("删除资源告警规则完成: {RuleId}, Deleted={Deleted}", id, deleted);
            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除资源告警规则失败: {RuleId}", id);
            return false;
        }
    }

    public async Task<IEnumerable<DockerPanel.API.Models.ResourceAlertRule>> GetAlertRulesAsync()
    {
        try
        {
            _logger.LogInformation("获取资源告警规则列表");

            var cacheKey = "resource_alert_rules";
            if (_cache.TryGetValue(cacheKey, out List<DockerPanel.API.Models.ResourceAlertRule>? cachedRules))
            {
                return cachedRules?.ToList() ?? Enumerable.Empty<DockerPanel.API.Models.ResourceAlertRule>();
            }

            var rulesCollection = _dbContext.GetCollection<DockerPanel.API.Models.ResourceAlertRule>("resource_alert_rules");
            var rules = rulesCollection.FindAll()
                .OrderByDescending(r => r.UpdatedAt)
                .ToList();

            _cache.Set(cacheKey, rules, TimeSpan.FromMinutes(1));

            _logger.LogInformation("成功获取资源告警规则: 规则数量 {RuleCount}", rules.Count);
            return rules;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取资源告警规则失败");
            throw;
        }
    }

    private async Task<NodeResourceOverview?> GetNodeResourceOverviewAsync(Node node)
    {
        try
        {
            var cacheKey = $"node_overview_{node.Id}";
            if (_cache.TryGetValue(cacheKey, out NodeResourceOverview? cachedOverview))
            {
                return cachedOverview;
            }

            var overview = new NodeResourceOverview
            {
                NodeId = node.Id,
                NodeName = node.Name,
                Host = node.Host,
                Status = await CheckNodeResourceStatusAsync(node),
                LastUpdated = DateTime.UtcNow,
                Alerts = new List<string>()
            };

            // 获取资源使用情况
            if (overview.Status == NodeResourceStatus.Online)
            {
                await CollectResourceUsageAsync(node, overview);
            }
            else
            {
                // 离线节点设置默认值
                overview.CpuUsage = new ResourceUsage { Used = 0, Total = 100, Percentage = 0, Unit = "%" };
                overview.MemoryUsage = new ResourceUsage { Used = 0, Total = 1024, Percentage = 0, Unit = "MB" };
                overview.DiskUsage = new ResourceUsage { Used = 0, Total = 10240, Percentage = 0, Unit = "MB" };
                overview.NetworkUsage = new NetworkUsage();
                overview.ContainerUsage = new ContainerUsage();
                overview.Alerts.Add("节点离线");
            }

            // Ensure all numeric values are valid (not Infinity or NaN)
            if (overview.CpuUsage != null)
            {
                overview.CpuUsage.Percentage = SanitizeDouble(overview.CpuUsage.Percentage);
                overview.CpuUsage.Used = SanitizeDouble(overview.CpuUsage.Used);
                overview.CpuUsage.Total = SanitizeDouble(overview.CpuUsage.Total);
            }
            if (overview.MemoryUsage != null)
            {
                overview.MemoryUsage.Percentage = SanitizeDouble(overview.MemoryUsage.Percentage);
                overview.MemoryUsage.Used = SanitizeDouble(overview.MemoryUsage.Used);
                overview.MemoryUsage.Total = SanitizeDouble(overview.MemoryUsage.Total);
            }
            if (overview.DiskUsage != null)
            {
                overview.DiskUsage.Percentage = SanitizeDouble(overview.DiskUsage.Percentage);
                overview.DiskUsage.Used = SanitizeDouble(overview.DiskUsage.Used);
                overview.DiskUsage.Total = SanitizeDouble(overview.DiskUsage.Total);
            }
            if (overview.NetworkUsage != null)
            {
                overview.NetworkUsage.BandwidthUsed = SanitizeDouble(overview.NetworkUsage.BandwidthUsed);
                overview.NetworkUsage.BandwidthTotal = SanitizeDouble(overview.NetworkUsage.BandwidthTotal);
                overview.NetworkUsage.PacketsIn = SanitizeDouble(overview.NetworkUsage.PacketsIn);
                overview.NetworkUsage.PacketsOut = SanitizeDouble(overview.NetworkUsage.PacketsOut);
                overview.NetworkUsage.ErrorsIn = SanitizeDouble(overview.NetworkUsage.ErrorsIn);
                overview.NetworkUsage.ErrorsOut = SanitizeDouble(overview.NetworkUsage.ErrorsOut);
            }
            if (overview.ContainerUsage != null)
            {
                overview.ContainerUsage.ResourceUtilizationScore = SanitizeDouble(overview.ContainerUsage.ResourceUtilizationScore);
            }

            // 缓存2分钟
            _cache.Set(cacheKey, overview, TimeSpan.FromMinutes(2));

            return overview;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取节点资源概览失败: {NodeId}", node.Id);
            return null;
        }
    }

    private async Task<NodeResourceStatus> CheckNodeResourceStatusAsync(Node node)
    {
        try
        {
            // 本地节点特殊处理 - 直接使用 Docker 连接检查
            if (IsLocalNode(node))
            {
                // 检查 Docker 是否可用
                try
                {
                    var dockerVersion = await _dockerEngine.GetSystemInfoAsync();
                    return dockerVersion != null ? NodeResourceStatus.Online : NodeResourceStatus.Offline;
                }
                catch
                {
                    return NodeResourceStatus.Offline;
                }
            }

            // 远程节点使用 SSH 检查
            var result = await _sshService.TestSshConnectionAsync(
                node.Host,
                node.SshPort,
                node.SshUsername,
                node.SshPassword,
                node.SshPrivateKeyPath);

            return result ? NodeResourceStatus.Online : NodeResourceStatus.Offline;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "检查节点状态失败: {NodeId}", node.Id);
            return NodeResourceStatus.Unknown;
        }
    }

    private async Task CollectResourceUsageAsync(Node node, NodeResourceOverview overview)
    {
        try
        {
            // 本地节点特殊处理 - 使用本地系统 API
            if (IsLocalNode(node))
            {
                await CollectLocalResourceUsageAsync(node, overview);
                return;
            }

            // 获取CPU使用率
            var cpuCommand = "top -bn1 | grep 'Cpu(s)' | awk '{print $2}' | sed 's/%us,//'";
            var cpuResult = await _sshService.ExecuteCommandAsync(
                node.Host, node.SshPort, node.SshUsername, cpuCommand,
                node.SshPassword, node.SshPrivateKeyPath);

            if (cpuResult.Success && double.TryParse(cpuResult.Output.Trim(), out var cpuUsage))
            {
                overview.CpuUsage = new ResourceUsage
                {
                    Used = cpuUsage,
                    Total = 100,
                    Percentage = cpuUsage,
                    Unit = "%",
                    Trend = CalculateTrend($"cpu_trend_{node.Id}", cpuUsage)
                };
            }

            // 获取内存使用情况
            var memoryCommand = "free -m | awk 'NR==2{printf \"%.2f\", $3*100/$2}'";
            var memoryResult = await _sshService.ExecuteCommandAsync(
                node.Host, node.SshPort, node.SshUsername, memoryCommand,
                node.SshPassword, node.SshPrivateKeyPath);

            if (memoryResult.Success && double.TryParse(memoryResult.Output.Trim(), out var memoryUsage))
            {
                var totalMemoryCommand = "free -m | awk 'NR==2{print $2}'";
                var totalMemoryResult = await _sshService.ExecuteCommandAsync(
                    node.Host, node.SshPort, node.SshUsername, totalMemoryCommand,
                    node.SshPassword, node.SshPrivateKeyPath);

                if (totalMemoryResult.Success && long.TryParse(totalMemoryResult.Output.Trim(), out var totalMemory))
                {
                    var usedMemory = (totalMemory * memoryUsage) / 100;
                    overview.MemoryUsage = new ResourceUsage
                    {
                        Used = usedMemory,
                        Total = totalMemory,
                        Percentage = memoryUsage,
                        Unit = "MB",
                        Trend = CalculateTrend($"memory_trend_{node.Id}", memoryUsage)
                    };
                }
            }

            // 获取磁盘使用情况
            var diskCommand = "df -h / | awk 'NR==2{print $5}' | sed 's/%//'";
            var diskResult = await _sshService.ExecuteCommandAsync(
                node.Host, node.SshPort, node.SshUsername, diskCommand,
                node.SshPassword, node.SshPrivateKeyPath);

            if (diskResult.Success && double.TryParse(diskResult.Output.Trim(), out var diskUsage))
            {
                var totalDiskCommand = "df -h / | awk 'NR==2{print $2}'";
                var totalDiskResult = await _sshService.ExecuteCommandAsync(
                    node.Host, node.SshPort, node.SshUsername, totalDiskCommand,
                    node.SshPassword, node.SshPrivateKeyPath);

                if (totalDiskResult.Success)
                {
                    var totalDiskStr = totalDiskResult.Output.Trim();
                    var totalDisk = ParseDiskSize(totalDiskStr);
                    var usedDisk = (totalDisk * diskUsage) / 100;

                    overview.DiskUsage = new ResourceUsage
                    {
                        Used = usedDisk,
                        Total = totalDisk,
                        Percentage = diskUsage,
                        Unit = "GB",
                        Trend = CalculateTrend($"disk_trend_{node.Id}", diskUsage)
                    };
                }
            }

            // 获取容器统计信息（如果节点是Docker主机）
            try
            {
                var containerCommand = "docker ps -a --format '{{.Status}}' | grep -c .";
                var containerResult = await _sshService.ExecuteCommandAsync(
                    node.Host, node.SshPort, node.SshUsername, containerCommand,
                    node.SshPassword, node.SshPrivateKeyPath);

                if (containerResult.Success && int.TryParse(containerResult.Output.Trim(), out var totalContainers))
                {
                    var runningCommand = "docker ps --format '{{.Status}}' | grep -c .";
                    var runningResult = await _sshService.ExecuteCommandAsync(
                        node.Host, node.SshPort, node.SshUsername, runningCommand,
                        node.SshPassword, node.SshPrivateKeyPath);

                    var runningContainers = 0;
                    if (runningResult.Success)
                    {
                        int.TryParse(runningResult.Output.Trim(), out runningContainers);
                    }

                    overview.ContainerUsage = new ContainerUsage
                    {
                        TotalCount = totalContainers,
                        RunningCount = runningContainers,
                        StoppedCount = totalContainers - runningContainers,
                        PausedCount = 0,
                        ResourceUtilizationScore = CalculateContainerUtilizationScore(runningContainers, totalContainers)
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "获取容器信息失败，可能节点不支持Docker: {NodeId}", node.Id);
                overview.ContainerUsage = new ContainerUsage();
            }

            // 获取网络使用情况
            await CollectNetworkUsageAsync(node, overview);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "收集资源使用情况失败: {NodeId}", node.Id);
            overview.Status = NodeResourceStatus.Error;
            overview.Alerts.Add("资源监控异常");
        }
    }

    /// <summary>
    /// 收集本地节点资源使用情况（不使用 SSH）
    /// </summary>
    private async Task CollectLocalResourceUsageAsync(Node node, NodeResourceOverview overview)
    {
        try
        {
            // 获取 CPU 使用率（使用 Process 类）
            var cpuUsage = await GetCpuUsageAsync();

            overview.CpuUsage = new ResourceUsage
            {
                Used = Math.Round(cpuUsage, 2),
                Total = 100,
                Percentage = Math.Round(cpuUsage, 2),
                Unit = "%",
                Trend = CalculateTrend($"cpu_trend_{node.Id}", cpuUsage)
            };

            // 获取内存使用情况
            var gcmem = GC.GetGCMemoryInfo();
            var totalMemory = (long)(gcmem.TotalAvailableMemoryBytes / 1024 / 1024);
            var usedMemory = (long)(gcmem.MemoryLoadBytes / 1024 / 1024);
            var memoryPercentage = totalMemory > 0 ? (double)usedMemory / totalMemory * 100 : 0;

            overview.MemoryUsage = new ResourceUsage
            {
                Used = usedMemory,
                Total = totalMemory,
                Percentage = Math.Round(memoryPercentage, 2),
                Unit = "MB",
                Trend = CalculateTrend($"memory_trend_{node.Id}", memoryPercentage)
            };

            // 获取磁盘使用情况
            var drive = new System.IO.DriveInfo("C");
            var totalDisk = drive.TotalSize / 1024.0 / 1024 / 1024; // GB
            var freeDisk = drive.AvailableFreeSpace / 1024.0 / 1024 / 1024; // GB
            var usedDisk = totalDisk - freeDisk;
            var diskPercentage = totalDisk > 0 ? usedDisk / totalDisk * 100 : 0;

            overview.DiskUsage = new ResourceUsage
            {
                Used = Math.Round(usedDisk, 2),
                Total = Math.Round(totalDisk, 2),
                Percentage = Math.Round(diskPercentage, 2),
                Unit = "GB",
                Trend = CalculateTrend($"disk_trend_{node.Id}", diskPercentage)
            };

            // 获取容器统计信息
            try
            {
                var containers = await _dockerEngine.ListContainersAsync(true);
                var totalContainers = containers.Count();
                var runningContainers = containers.Count(c => c.State == "running");

                overview.ContainerUsage = new ContainerUsage
                {
                    TotalCount = totalContainers,
                    RunningCount = runningContainers,
                    StoppedCount = totalContainers - runningContainers,
                    PausedCount = 0,
                    ResourceUtilizationScore = CalculateContainerUtilizationScore(runningContainers, totalContainers)
                };
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "获取本地容器信息失败: {NodeId}", node.Id);
                overview.ContainerUsage = new ContainerUsage();
            }

            overview.NetworkUsage = CollectLocalNetworkUsage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "收集本地资源使用情况失败: {NodeId}", node.Id);
            overview.Status = NodeResourceStatus.Error;
            overview.Alerts.Add("资源监控异常");
        }
    }

    /// <summary>
    /// 获取 CPU 使用率
    /// </summary>
    private async Task<double> GetCpuUsageAsync()
    {
        try
        {
            // 使用 WMI 获取 CPU 使用率
            var startCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
            var startRealTime = DateTime.UtcNow;
            
            await Task.Delay(500);
            
            var endCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
            var endRealTime = DateTime.UtcNow;
            
            var cpuUsedMs = (endCpuTime - startCpuTime).TotalMilliseconds;
            var totalMsPassed = (endRealTime - startRealTime).TotalMilliseconds;
            
            // 系统 CPU 使用率估算（简化版本，基于进程的 CPU 时间）
            var cpuUsage = Environment.ProcessorCount > 0 
                ? Math.Min(100, (cpuUsedMs / totalMsPassed) * 100 / Environment.ProcessorCount)
                : 0;
            
            // 获取系统整体 CPU 使用率
            var systemCpuUsage = GetSystemCpuUsage();
            return systemCpuUsage > 0 ? systemCpuUsage : cpuUsage;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// 获取系统 CPU 使用率（通过系统信息）
    /// </summary>
    private double GetSystemCpuUsage()
    {
        try
        {
            // 使用操作系统提供的 API
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "wmic",
                    Arguments = "cpu get loadpercentage /value",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            // 解析输出: LoadPercentage=XX
            var lines = output.Split('\n');
            foreach (var line in lines)
            {
                if (line.StartsWith("LoadPercentage="))
                {
                    var value = line.Substring("LoadPercentage=".Length).Trim();
                    if (double.TryParse(value, out var cpuUsage))
                    {
                        return cpuUsage;
                    }
                }
            }
        }
        catch
        {
            // 忽略错误
        }
        return 0;
    }

    private async Task CollectNetworkUsageAsync(Node node, NodeResourceOverview overview)
    {
        try
        {
            var networkCommand = "cat /proc/net/dev | grep eth0 | awk '{print $2, $10}'";
            var networkResult = await _sshService.ExecuteCommandAsync(
                node.Host, node.SshPort, node.SshUsername, networkCommand,
                node.SshPassword, node.SshPrivateKeyPath);

            if (networkResult.Success)
            {
                var parts = networkResult.Output.Trim().Split();
                if (parts.Length >= 2)
                {
                    var packetsIn = double.TryParse(parts[0], out var pIn) ? pIn : 0;
                    var packetsOut = double.TryParse(parts[1], out var pOut) ? pOut : 0;

                    overview.NetworkUsage = new NetworkUsage
                    {
                        PacketsIn = packetsIn,
                        PacketsOut = packetsOut,
                        BandwidthUsed = (packetsIn + packetsOut) / 1024, // KB
                        BandwidthTotal = 1024 * 100, // 100MB
                        ConnectionsCount = await GetActiveConnectionsAsync(node),
                        ErrorsIn = 0,
                        ErrorsOut = 0
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "获取网络使用情况失败: {NodeId}", node.Id);
            overview.NetworkUsage = new NetworkUsage();
        }
    }

    private async Task<int> GetActiveConnectionsAsync(Node node)
    {
        try
        {
            var command = "netstat -an | grep ESTABLISHED | wc -l";
            var result = await _sshService.ExecuteCommandAsync(
                node.Host, node.SshPort, node.SshUsername, command,
                node.SshPassword, node.SshPrivateKeyPath);

            if (result.Success && int.TryParse(result.Output.Trim(), out var connections))
            {
                return connections;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "获取活跃连接数失败: {NodeId}", node.Id);
        }

        return 0;
    }

    private async Task<NodeResourceDetails> CollectNodeResourceDetailsAsync(Node node)
    {
        var details = new NodeResourceDetails
        {
            Overview = await GetNodeResourceOverviewAsync(node) ?? new NodeResourceOverview
            {
                NodeId = node.Id,
                NodeName = node.Name,
                Host = node.Host,
                Status = NodeResourceStatus.Offline
            }
        };

        // 获取系统信息
        await CollectSystemInfoAsync(node, details);

        // 获取Docker信息
        await CollectDockerInfoAsync(node, details);

        // 获取容器详细信息
        await CollectContainerInfoAsync(node, details);

        // 获取网络信息
        await CollectNetworkInfoAsync(node, details);

        // 获取卷信息
        await CollectVolumeInfoAsync(node, details);

        // 获取性能指标
        await CollectPerformanceMetricsAsync(node, details);

        return details;
    }

    private async Task CollectSystemInfoAsync(Node node, NodeResourceDetails details)
    {
        try
        {
            var osCommand = "uname -s";
            var osResult = await _sshService.ExecuteCommandAsync(
                node.Host, node.SshPort, node.SshUsername, osCommand,
                node.SshPassword, node.SshPrivateKeyPath);

            var kernelCommand = "uname -r";
            var kernelResult = await _sshService.ExecuteCommandAsync(
                node.Host, node.SshPort, node.SshUsername, kernelCommand,
                node.SshPassword, node.SshPrivateKeyPath);

            var archCommand = "uname -m";
            var archResult = await _sshService.ExecuteCommandAsync(
                node.Host, node.SshPort, node.SshUsername, archCommand,
                node.SshPassword, node.SshPrivateKeyPath);

            var cpuCommand = "nproc";
            var cpuResult = await _sshService.ExecuteCommandAsync(
                node.Host, node.SshPort, node.SshUsername, cpuCommand,
                node.SshPassword, node.SshPrivateKeyPath);

            var memoryCommand = "free -b | awk 'NR==2{print $2}'";
            var memoryResult = await _sshService.ExecuteCommandAsync(
                node.Host, node.SshPort, node.SshUsername, memoryCommand,
                node.SshPassword, node.SshPrivateKeyPath);

            var uptimeCommand = "cat /proc/uptime | awk '{print $1}'";
            var uptimeResult = await _sshService.ExecuteCommandAsync(
                node.Host, node.SshPort, node.SshUsername, uptimeCommand,
                node.SshPassword, node.SshPrivateKeyPath);

            details.SystemInfo = new SystemInfo
            {
                OsType = osResult.Success ? osResult.Output.Trim() : "Unknown",
                KernelVersion = kernelResult.Success ? kernelResult.Output.Trim() : "Unknown",
                Architecture = archResult.Success ? archResult.Output.Trim() : "Unknown",
                CpuCores = cpuResult.Success && int.TryParse(cpuResult.Output.Trim(), out var cores) ? cores : 0,
                TotalMemory = memoryResult.Success && long.TryParse(memoryResult.Output.Trim(), out var memory) ? memory : 0,
                TotalDisk = (long)details.Overview.DiskUsage.Total,
                Uptime = uptimeResult.Success && double.TryParse(uptimeResult.Output.Trim(), out var uptime) ? uptime : 0,
                BootTime = DateTime.UtcNow.AddSeconds(-(uptimeResult.Success && double.TryParse(uptimeResult.Output.Trim(), out var ut) ? ut : 0))
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "收集系统信息失败: {NodeId}", node.Id);
            details.SystemInfo = new SystemInfo();
        }
    }

    private async Task CollectDockerInfoAsync(Node node, NodeResourceDetails details)
    {
        try
        {
            var versionCommand = "docker version --format '{{.Server.Version}}'";
            var versionResult = await _sshService.ExecuteCommandAsync(
                node.Host, node.SshPort, node.SshUsername, versionCommand,
                node.SshPassword, node.SshPrivateKeyPath);

            var apiCommand = "docker version --format '{{.Server.APIVersion}}'";
            var apiResult = await _sshService.ExecuteCommandAsync(
                node.Host, node.SshPort, node.SshUsername, apiCommand,
                node.SshPassword, node.SshPrivateKeyPath);

            var containersCommand = "docker info --format '{{.Containers}}'";
            var containersResult = await _sshService.ExecuteCommandAsync(
                node.Host, node.SshPort, node.SshUsername, containersCommand,
                node.SshPassword, node.SshPrivateKeyPath);

            var imagesCommand = "docker info --format '{{.Images}}'";
            var imagesResult = await _sshService.ExecuteCommandAsync(
                node.Host, node.SshPort, node.SshUsername, imagesCommand,
                node.SshPassword, node.SshPrivateKeyPath);

            details.DockerInfo = new DockerEngineInfo
            {
                Version = versionResult.Success ? versionResult.Output.Trim() : "Unknown",
                ApiVersion = apiResult.Success ? apiResult.Output.Trim() : "Unknown",
                Containers = containersResult.Success && int.TryParse(containersResult.Output.Trim(), out var cont) ? cont : 0,
                Images = imagesResult.Success && int.TryParse(imagesResult.Output.Trim(), out var imgs) ? imgs : 0,
                Networks = 0, // 需要额外命令获取
                Volumes = 0,  // 需要额外命令获取
                ServerVersion = versionResult.Success ? versionResult.Output.Trim() : "Unknown",
                SystemTime = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "收集Docker信息失败: {NodeId}", node.Id);
            details.DockerInfo = new DockerEngineInfo();
        }
    }

    private async Task CollectContainerInfoAsync(Node node, NodeResourceDetails details)
    {
        try
        {
            var command = "docker ps -a --format '{{.ID}}\t{{.Names}}\t{{.Status}}\t{{.Image}}'";
            var result = await _sshService.ExecuteCommandAsync(
                node.Host, node.SshPort, node.SshUsername, command,
                node.SshPassword, node.SshPrivateKeyPath);

            if (result.Success)
            {
                var lines = result.Output.Trim().Split('\n');
                foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
                {
                    var parts = line.Split('\t');
                    if (parts.Length >= 4)
                    {
                        details.Containers.Add(new ContainerResourceInfo
                        {
                            Id = parts[0],
                            Name = parts[1],
                            Status = parts[2],
                            CpuUsage = new ResourceUsage { Unit = "%" },
                            MemoryUsage = new ResourceUsage { Unit = "MB" },
                            DiskUsage = new ResourceUsage { Unit = "MB" },
                            NetworkUsage = new NetworkUsage(),
                            CreatedAt = DateTime.UtcNow, // 需要解析实际创建时间
                            Labels = new List<string>()
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "收集容器信息失败: {NodeId}", node.Id);
        }
    }

    private async Task CollectNetworkInfoAsync(Node node, NodeResourceDetails details)
    {
        try
        {
            var command = "docker network ls --format '{{.ID}}\t{{.Name}}\t{{.Driver}}\t{{.Scope}}'";
            var result = await _sshService.ExecuteCommandAsync(
                node.Host, node.SshPort, node.SshUsername, command,
                node.SshPassword, node.SshPrivateKeyPath);

            if (result.Success)
            {
                var lines = result.Output.Trim().Split('\n');
                foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
                {
                    var parts = line.Split('\t');
                    if (parts.Length >= 4)
                    {
                        details.Networks.Add(new NetworkResourceInfo
                        {
                            Id = parts[0],
                            Name = parts[1],
                            Driver = parts[2],
                            Scope = parts[3],
                            Internal = false,
                            ContainersCount = 0, // 需要额外查询
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "收集网络信息失败: {NodeId}", node.Id);
        }
    }

    private async Task CollectVolumeInfoAsync(Node node, NodeResourceDetails details)
    {
        try
        {
            var command = "docker volume ls --format '{{.Name}}\t{{.Driver}}'";
            var result = await _sshService.ExecuteCommandAsync(
                node.Host, node.SshPort, node.SshUsername, command,
                node.SshPassword, node.SshPrivateKeyPath);

            if (result.Success)
            {
                var lines = result.Output.Trim().Split('\n');
                foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
                {
                    var parts = line.Split('\t');
                    if (parts.Length >= 2)
                    {
                        details.Volumes.Add(new VolumeInfo
                        {
                            Name = parts[0],
                            Driver = parts[1],
                            Mountpoint = $"/var/lib/docker/volumes/{parts[0]}",
                            Size = 0, // 需要额外计算
                            CreatedAt = DateTime.UtcNow,
                            Created = DateTime.UtcNow,
                            Status = "available"
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "收集卷信息失败: {NodeId}", node.Id);
        }
    }

    private async Task CollectPerformanceMetricsAsync(Node node, NodeResourceDetails details)
    {
        try
        {
            var loadCommand = "cat /proc/loadavg | awk '{print $1}'";
            var loadResult = await _sshService.ExecuteCommandAsync(
                node.Host, node.SshPort, node.SshUsername, loadCommand,
                node.SshPassword, node.SshPrivateKeyPath);

            var processCommand = "ps aux | wc -l";
            var processResult = await _sshService.ExecuteCommandAsync(
                node.Host, node.SshPort, node.SshUsername, processCommand,
                node.SshPassword, node.SshPrivateKeyPath);

            var contextCommand = "cat /proc/stat | grep 'ctxt' | awk '{print $2}'";
            var contextResult = await _sshService.ExecuteCommandAsync(
                node.Host, node.SshPort, node.SshUsername, contextCommand,
                node.SshPassword, node.SshPrivateKeyPath);

            details.PerformanceMetrics = new PerformanceMetrics
            {
                CpuLoadAverage = loadResult.Success && double.TryParse(loadResult.Output.Trim(), out var load) ? load : 0,
                MemoryPressure = details.Overview.MemoryUsage.Percentage,
                DiskIoWait = 0, // 需要额外命令获取
                NetworkLatency = 0, // 需要ping测试
                ProcessCount = processResult.Success && int.TryParse(processResult.Output.Trim(), out var processes) ? processes : 0,
                ThreadCount = 0, // 需要额外计算
                ContextSwitches = contextResult.Success && double.TryParse(contextResult.Output.Trim(), out var context) ? context : 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "收集性能指标失败: {NodeId}", node.Id);
            details.PerformanceMetrics = new PerformanceMetrics();
        }
    }

    private async Task<NodeResourceTrend> CollectResourceTrendAsync(Node node, DateTime startTime, DateTime endTime, TimeSpan interval)
    {
        var trend = new NodeResourceTrend
        {
            NodeId = node.Id,
            StartTime = startTime,
            EndTime = endTime,
            Interval = interval
        };

        var overview = await GetNodeResourceOverviewAsync(node);
        if (overview == null)
        {
            return trend;
        }

        trend.CpuTrend.Add(new ResourceDataPoint
        {
            Timestamp = endTime,
            Value = overview.CpuUsage.Percentage,
            Unit = "%"
        });

        trend.MemoryTrend.Add(new ResourceDataPoint
        {
            Timestamp = endTime,
            Value = overview.MemoryUsage.Percentage,
            Unit = "%"
        });

        trend.DiskTrend.Add(new ResourceDataPoint
        {
            Timestamp = endTime,
            Value = overview.DiskUsage.Percentage,
            Unit = "%"
        });

        trend.NetworkTrend.Add(new ResourceDataPoint
        {
            Timestamp = endTime,
            Value = overview.NetworkUsage.BandwidthUsed,
            Unit = "KB"
        });

        return trend;
    }

    private async Task<List<DockerPanel.API.Models.ResourceAlert>> CheckNodeResourceAlertsAsync(Node node)
    {
        var alerts = new List<DockerPanel.API.Models.ResourceAlert>();
        var overview = await GetNodeResourceOverviewAsync(node);

        if (overview == null) return alerts;

        // CPU告警
        if (overview.CpuUsage.Percentage > 90)
        {
            alerts.Add(new DockerPanel.API.Models.ResourceAlert
            {
                Id = Guid.NewGuid().ToString(),
                RuleId = "cpu-threshold-rule",
                RuleName = "CPU使用率告警",
                NodeId = node.Id,
                NodeName = overview.NodeName,
                ResourceType = "CPU",
                MetricType = "usage",
                CurrentValue = overview.CpuUsage.Percentage,
                ThresholdValue = 90,
                Severity = DockerPanel.API.Models.AlertSeverity.Critical,
                State = "fired",
                Title = "CPU使用率过高",
                Message = $"节点 {overview.NodeName} CPU使用率达到 {overview.CpuUsage.Percentage:F2}%",
                StartedAt = DateTime.UtcNow,
                Notifications = new List<DockerPanel.API.Models.AlertNotification>(),
                Metadata = new Dictionary<string, object>
                {
                    ["alertType"] = "Threshold",
                    ["threshold"] = 90
                }
            });
        }

        // 内存告警
        if (overview.MemoryUsage.Percentage > 85)
        {
            alerts.Add(new DockerPanel.API.Models.ResourceAlert
            {
                Id = Guid.NewGuid().ToString(),
                RuleId = "memory-threshold-rule",
                RuleName = "内存使用率告警",
                NodeId = node.Id,
                NodeName = overview.NodeName,
                ResourceType = "Memory",
                MetricType = "usage",
                CurrentValue = overview.MemoryUsage.Percentage,
                ThresholdValue = 85,
                Severity = DockerPanel.API.Models.AlertSeverity.Warning,
                State = "fired",
                Title = "内存使用率过高",
                Message = $"节点 {overview.NodeName} 内存使用率达到 {overview.MemoryUsage.Percentage:F2}%",
                StartedAt = DateTime.UtcNow,
                Notifications = new List<DockerPanel.API.Models.AlertNotification>(),
                Metadata = new Dictionary<string, object>
                {
                    ["alertType"] = "Threshold",
                    ["threshold"] = 85
                }
            });
        }

        // 磁盘告警
        if (overview.DiskUsage.Percentage > 80)
        {
            alerts.Add(new DockerPanel.API.Models.ResourceAlert
            {
                Id = Guid.NewGuid().ToString(),
                RuleId = "disk-threshold-rule",
                RuleName = "磁盘使用率告警",
                NodeId = node.Id,
                NodeName = overview.NodeName,
                ResourceType = "Disk",
                MetricType = "usage",
                CurrentValue = overview.DiskUsage.Percentage,
                ThresholdValue = 80,
                Severity = DockerPanel.API.Models.AlertSeverity.Warning,
                State = "fired",
                Title = "磁盘使用率过高",
                Message = $"节点 {overview.NodeName} 磁盘使用率达到 {overview.DiskUsage.Percentage:F2}%",
                StartedAt = DateTime.UtcNow,
                Notifications = new List<DockerPanel.API.Models.AlertNotification>(),
                Metadata = new Dictionary<string, object>
                {
                    ["alertType"] = "Threshold",
                    ["threshold"] = 80
                }
            });
        }

        // 节点状态告警
        if (overview.Status == NodeResourceStatus.Offline)
        {
            alerts.Add(new DockerPanel.API.Models.ResourceAlert
            {
                Id = Guid.NewGuid().ToString(),
                RuleId = "node-status-rule",
                RuleName = "节点状态告警",
                NodeId = node.Id,
                NodeName = overview.NodeName,
                ResourceType = "Node",
                MetricType = "status",
                CurrentValue = 0,
                ThresholdValue = 0,
                Severity = DockerPanel.API.Models.AlertSeverity.Critical,
                State = "fired",
                Title = "节点离线",
                Message = $"节点 {overview.NodeName} 无法连接",
                StartedAt = DateTime.UtcNow,
                Notifications = new List<DockerPanel.API.Models.AlertNotification>(),
                Metadata = new Dictionary<string, object>
                {
                    ["alertType"] = "ConnectionLost"
                }
            });
        }

        return alerts;
    }

    private Trend CalculateTrend(string cacheKey, double currentValue)
    {
        try
        {
            if (_cache.TryGetValue(cacheKey, out List<double>? history))
            {
                if (history != null && history.Count >= 2)
                {
                    var previous = history[^1];
                    var beforePrevious = history[^2];

                    if (currentValue > previous && previous > beforePrevious)
                    {
                        return Trend.Increasing;
                    }
                    else if (currentValue < previous && previous < beforePrevious)
                    {
                        return Trend.Decreasing;
                    }
                    else
                    {
                        return Trend.Fluctuating;
                    }
                }
            }
            else
            {
                history = new List<double>();
            }

            if (history == null)
            {
                history = new List<double>();
            }

            history.Add(currentValue);
            if (history.Count > 10) // 保留最近10个数据点
            {
                history.RemoveAt(0);
            }

            _cache.Set(cacheKey, history, TimeSpan.FromMinutes(30));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "计算趋势失败: {CacheKey}", cacheKey);
        }

        return Trend.Stable;
    }

    private static bool IsLocalNode(Node node)
    {
        return node.Id.Equals("local", StringComparison.OrdinalIgnoreCase) ||
               node.Id.Equals("local-node", StringComparison.OrdinalIgnoreCase) ||
               node.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
               node.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
               string.IsNullOrEmpty(node.SshUsername);
    }

    private NetworkUsage CollectLocalNetworkUsage()
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                             ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                             ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .ToList();

            double bytesIn = 0;
            double bytesOut = 0;
            double packetsIn = 0;
            double packetsOut = 0;
            double errorsIn = 0;
            double errorsOut = 0;

            foreach (var networkInterface in interfaces)
            {
                var stats = networkInterface.GetIPv4Statistics();
                bytesIn += stats.BytesReceived;
                bytesOut += stats.BytesSent;
                packetsIn += stats.UnicastPacketsReceived + stats.NonUnicastPacketsReceived;
                packetsOut += stats.UnicastPacketsSent + stats.NonUnicastPacketsSent;
                errorsIn += stats.IncomingPacketsWithErrors;
                errorsOut += stats.OutgoingPacketsWithErrors;
            }

            return new NetworkUsage
            {
                PacketsIn = packetsIn,
                PacketsOut = packetsOut,
                BandwidthUsed = Math.Round((bytesIn + bytesOut) / 1024, 2),
                BandwidthTotal = interfaces.Sum(ni => Math.Max(0, ni.Speed)) / 8.0 / 1024,
                ConnectionsCount = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections().Length,
                ErrorsIn = errorsIn,
                ErrorsOut = errorsOut
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "获取本地网络统计失败");
            return new NetworkUsage();
        }
    }

    /// <summary>
    /// Ensure a double value is valid (not NaN or Infinity)
    /// </summary>
    private double SanitizeDouble(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0;
        }
        return value;
    }

    private double CalculateContainerUtilizationScore(int runningContainers, int totalContainers)
    {
        if (totalContainers == 0) return 0;
        return (double)runningContainers / totalContainers * 100;
    }

    private double CalculateClusterUtilizationScore(ClusterResourceStats stats)
    {
        if (stats.TotalNodes == 0) return 0;

        var cpuScore = stats.ClusterCpuUsage?.Percentage ?? 0;
        var memoryScore = stats.ClusterMemoryUsage?.Percentage ?? 0;
        var diskScore = stats.ClusterDiskUsage?.Percentage ?? 0;

        var score = (cpuScore + memoryScore + diskScore) / 3;
        
        // Ensure the score is a valid number
        if (double.IsNaN(score) || double.IsInfinity(score))
        {
            return 0;
        }
        
        return score;
    }

    private long ParseDiskSize(string sizeStr)
    {
        try
        {
            sizeStr = sizeStr.ToUpper();
            if (sizeStr.EndsWith("G"))
            {
                var value = double.Parse(sizeStr[..^1]);
                return (long)(value * 1024); // 转换为MB
            }
            else if (sizeStr.EndsWith("T"))
            {
                var value = double.Parse(sizeStr[..^1]);
                return (long)(value * 1024 * 1024); // 转换为MB
            }
            else if (sizeStr.EndsWith("M"))
            {
                return long.Parse(sizeStr[..^1]);
            }
            else
            {
                return long.Parse(sizeStr);
            }
        }
        catch
        {
            return 10240; // 默认10GB
        }
    }

    /// <summary>
    /// 将NodeInfo转换为Node
    /// </summary>
    private static Node ConvertToNode(NodeInfo nodeInfo)
    {
        return new Node
        {
            Id = nodeInfo.Id,
            Name = nodeInfo.Name,
            Host = nodeInfo.Host,
            Port = nodeInfo.Port,
            EngineType = nodeInfo.EngineType,
            Username = nodeInfo.UseSsh ? nodeInfo.SshUsername : null,
            Password = null, // NodeInfo中没有Password字段，设置为null
            PrivateKeyPath = nodeInfo.UseSsh ? nodeInfo.SshPrivateKeyPath : null,
            UseSsh = nodeInfo.UseSsh,
            SshPort = nodeInfo.SshPort ?? 22,
            SshUsername = nodeInfo.SshUsername ?? string.Empty,
            SshPassword = string.Empty, // NodeInfo中没有SshPassword字段，设置为空字符串
            SshPrivateKeyPath = nodeInfo.SshPrivateKeyPath ?? string.Empty,
            Labels = nodeInfo.Labels,
            IsOnline = nodeInfo.IsOnline,
            LastConnected = nodeInfo.LastConnected,
            CreatedAt = nodeInfo.CreatedAt,
            UpdatedAt = nodeInfo.UpdatedAt,
            Resources = nodeInfo.Resources,
            Status = nodeInfo.Status
        };
    }
}