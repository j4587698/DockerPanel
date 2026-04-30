using DockerPanel.API.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DockerPanel.API.Services;

/// <summary>
/// 简单节点分组和标签管理服务实现
/// </summary>
public class NodeGroupServiceImpl : INodeGroupService
{
    private readonly ILogger<NodeGroupServiceImpl> _logger;
    private readonly IMemoryCache _cache;
    private readonly INodeService _nodeService;
    private readonly INodeResourceService _resourceService;

    // 内存数据存储（实际项目中应该使用数据库）
    private static readonly Dictionary<string, NodeGroup> _nodeGroups = new();
    private static readonly Dictionary<string, NodeTag> _nodeTags = new();
    private static readonly Dictionary<string, HashSet<string>> _nodeToGroups = new();
    private static readonly Dictionary<string, HashSet<string>> _nodeToTags = new();

    public NodeGroupServiceImpl(
        ILogger<NodeGroupServiceImpl> logger,
        IMemoryCache cache,
        INodeService nodeService,
        INodeResourceService resourceService)
    {
        _logger = logger;
        _cache = cache;
        _nodeService = nodeService;
        _resourceService = resourceService;

        // 初始化默认数据
        InitializeDefaultData();
    }

    public async Task<IEnumerable<NodeGroup>> GetNodeGroupsAsync()
    {
        try
        {
            _logger.LogInformation("获取所有节点分组");

            var cacheKey = "node_groups_all";
            if (_cache.TryGetValue(cacheKey, out List<NodeGroup>? cachedGroups))
            {
                return cachedGroups!;
            }

            var groups = _nodeGroups.Values
                .OrderBy(g => g.SortOrder)
                .ThenBy(g => g.Name)
                .ToList();

            // 缓存5分钟
            _cache.Set(cacheKey, groups, TimeSpan.FromMinutes(5));

            _logger.LogInformation("成功获取 {Count} 个节点分组", groups.Count);
            return groups;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取节点分组失败");
            throw;
        }
    }

    public async Task<NodeGroup?> GetNodeGroupByIdAsync(string id)
    {
        try
        {
            await Task.CompletedTask;
            _logger.LogInformation("获取节点分组: {GroupId}", id);

            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            _nodeGroups.TryGetValue(id, out var group);
            if (group == null)
            {
                _logger.LogWarning("节点分组不存在: {GroupId}", id);
            }

            return group;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取节点分组失败: {GroupId}", id);
            throw;
        }
    }

    public async Task<NodeGroup> CreateNodeGroupAsync(CreateNodeGroupRequest request)
    {
        try
        {
            _logger.LogInformation("创建节点分组: {GroupName}", request.Name);

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ArgumentException("分组名称不能为空", nameof(request.Name));
            }

            // 检查名称是否已存在
            if (_nodeGroups.Values.Any(g => g.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"分组名称 '{request.Name}' 已存在");
            }

            var group = new NodeGroup
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                Description = request.Description,
                Color = request.Color ?? "#1890ff",
                Icon = request.Icon ?? "cluster",
                NodeIds = request.NodeIds ?? new List<string>(),
                Metadata = request.Metadata ?? new Dictionary<string, object>(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = "system", // 实际项目中应该从当前用户获取
                UpdatedBy = "system"
            };

            _nodeGroups[group.Id] = group;

            // 更新节点到分组的映射
            foreach (var nodeId in group.NodeIds)
            {
                if (!_nodeToGroups.ContainsKey(nodeId))
                {
                    _nodeToGroups[nodeId] = new HashSet<string>();
                }
                _nodeToGroups[nodeId].Add(group.Id);
            }

            // 清除缓存
            InvalidateCache();

            _logger.LogInformation("成功创建节点分组: {GroupId} {GroupName}", group.Id, group.Name);
            return group;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建节点分组失败: {GroupName}", request.Name);
            throw;
        }
    }

    public async Task<NodeGroup> UpdateNodeGroupAsync(string id, UpdateNodeGroupRequest request)
    {
        try
        {
            _logger.LogInformation("更新节点分组: {GroupId}", id);

            if (!_nodeGroups.TryGetValue(id, out var group))
            {
                throw new KeyNotFoundException($"节点分组不存在: {id}");
            }

            // 更新字段
            if (!string.IsNullOrEmpty(request.Name) && request.Name != group.Name)
            {
                // 检查名称是否已存在
                if (_nodeGroups.Values.Any(g => g.Id != id && g.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException($"分组名称 '{request.Name}' 已存在");
                }
                group.Name = request.Name;
            }

            if (request.Description != null)
                group.Description = request.Description;

            if (request.Color != null)
                group.Color = request.Color;

            if (request.Icon != null)
                group.Icon = request.Icon;

            if (request.Metadata != null)
                group.Metadata = request.Metadata;

            // 更新节点列表
            if (request.NodeIds != null)
            {
                // 移除旧的节点映射
                foreach (var nodeId in group.NodeIds)
                {
                    if (_nodeToGroups.ContainsKey(nodeId))
                    {
                        _nodeToGroups[nodeId].Remove(id);
                    }
                }

                // 添加新的节点映射
                group.NodeIds = request.NodeIds;
                foreach (var nodeId in group.NodeIds)
                {
                    if (!_nodeToGroups.ContainsKey(nodeId))
                    {
                        _nodeToGroups[nodeId] = new HashSet<string>();
                    }
                    _nodeToGroups[nodeId].Add(id);
                }
            }

            group.UpdatedAt = DateTime.UtcNow;
            group.UpdatedBy = "system";

            // 清除缓存
            InvalidateCache();

            _logger.LogInformation("成功更新节点分组: {GroupId} {GroupName}", group.Id, group.Name);
            return group;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新节点分组失败: {GroupId}", id);
            throw;
        }
    }

    public async Task<bool> DeleteNodeGroupAsync(string id)
    {
        try
        {
            _logger.LogInformation("删除节点分组: {GroupId}", id);

            if (!_nodeGroups.TryGetValue(id, out var group))
            {
                _logger.LogWarning("节点分组不存在: {GroupId}", id);
                return false;
            }

            // 不能删除系统分组
            if (group.IsSystem)
            {
                throw new InvalidOperationException("不能删除系统分组");
            }

            // 移除节点映射
            foreach (var nodeId in group.NodeIds)
            {
                if (_nodeToGroups.ContainsKey(nodeId))
                {
                    _nodeToGroups[nodeId].Remove(id);
                }
            }

            // 删除分组
            _nodeGroups.Remove(id);

            // 清除缓存
            InvalidateCache();

            _logger.LogInformation("成功删除节点分组: {GroupId} {GroupName}", group.Id, group.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除节点分组失败: {GroupId}", id);
            throw;
        }
    }

    public async Task<bool> AddNodeToGroupAsync(string groupId, string nodeId)
    {
        try
        {
            _logger.LogInformation("将节点添加到分组: {NodeId} -> {GroupId}", nodeId, groupId);

            if (!_nodeGroups.TryGetValue(groupId, out var group))
            {
                throw new KeyNotFoundException($"节点分组不存在: {groupId}");
            }

            if (!group.NodeIds.Contains(nodeId))
            {
                group.NodeIds.Add(nodeId);
                group.UpdatedAt = DateTime.UtcNow;

                if (!_nodeToGroups.ContainsKey(nodeId))
                {
                    _nodeToGroups[nodeId] = new HashSet<string>();
                }
                _nodeToGroups[nodeId].Add(groupId);

                // 清除缓存
                InvalidateCache();
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "将节点添加到分组失败: {NodeId} -> {GroupId}", nodeId, groupId);
            throw;
        }
    }

    public async Task<bool> RemoveNodeFromGroupAsync(string groupId, string nodeId)
    {
        try
        {
            _logger.LogInformation("从分组中移除节点: {NodeId} <- {GroupId}", nodeId, groupId);

            if (!_nodeGroups.TryGetValue(groupId, out var group))
            {
                throw new KeyNotFoundException($"节点分组不存在: {groupId}");
            }

            if (group.NodeIds.Remove(nodeId))
            {
                group.UpdatedAt = DateTime.UtcNow;

                if (_nodeToGroups.ContainsKey(nodeId))
                {
                    _nodeToGroups[nodeId].Remove(groupId);
                }

                // 清除缓存
                InvalidateCache();
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从分组中移除节点失败: {NodeId} <- {GroupId}", nodeId, groupId);
            throw;
        }
    }

    public async Task<IEnumerable<NodeInfo>> GetNodesInGroupAsync(string groupId)
    {
        try
        {
            _logger.LogInformation("获取分组中的节点: {GroupId}", groupId);

            if (!_nodeGroups.TryGetValue(groupId, out var group))
            {
                throw new KeyNotFoundException($"节点分组不存在: {groupId}");
            }

            var nodes = new List<NodeInfo>();
            foreach (var nodeId in group.NodeIds)
            {
                var node = await _nodeService.GetNodeByIdAsync(nodeId);
                if (node != null)
                {
                    nodes.Add(node);
                }
            }

            _logger.LogInformation("成功获取分组节点: {GroupId} 节点数量 {Count}", groupId, nodes.Count);
            return nodes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取分组节点失败: {GroupId}", groupId);
            throw;
        }
    }

    public async Task<IEnumerable<NodeGroup>> GetNodeGroupsAsync(string nodeId)
    {
        try
        {
            _logger.LogInformation("获取节点所属分组: {NodeId}", nodeId);

            var groups = new List<NodeGroup>();
            if (_nodeToGroups.TryGetValue(nodeId, out var groupIds))
            {
                foreach (var groupId in groupIds)
                {
                    if (_nodeGroups.TryGetValue(groupId, out var group))
                    {
                        groups.Add(group);
                    }
                }
            }

            _logger.LogInformation("成功获取节点分组: {NodeId} 分组数量 {Count}", nodeId, groups.Count);
            return groups;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取节点分组失败: {NodeId}", nodeId);
            throw;
        }
    }

    public async Task<IEnumerable<NodeTag>> GetAllTagsAsync()
    {
        try
        {
            _logger.LogInformation("获取所有标签");

            var cacheKey = "node_tags_all";
            if (_cache.TryGetValue(cacheKey, out List<NodeTag>? cachedTags))
            {
                return cachedTags!;
            }

            var tags = _nodeTags.Values
                .OrderBy(t => t.Category)
                .ThenBy(t => t.Name)
                .ToList();

            // 缓存5分钟
            _cache.Set(cacheKey, tags, TimeSpan.FromMinutes(5));

            _logger.LogInformation("成功获取 {Count} 个标签", tags.Count);
            return tags;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取标签失败");
            throw;
        }
    }

    public async Task<NodeTag> CreateTagAsync(CreateTagRequest request)
    {
        try
        {
            _logger.LogInformation("创建标签: {TagName}", request.Name);

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ArgumentException("标签名称不能为空", nameof(request.Name));
            }

            // 检查名称是否已存在
            if (_nodeTags.Values.Any(t => t.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"标签名称 '{request.Name}' 已存在");
            }

            var tag = new NodeTag
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                Description = request.Description,
                Color = request.Color ?? "#87d068",
                Category = request.Category ?? "custom",
                Metadata = request.Metadata ?? new Dictionary<string, object>(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = "system",
                UpdatedBy = "system"
            };

            _nodeTags[tag.Id] = tag;

            // 清除缓存
            InvalidateCache();

            _logger.LogInformation("成功创建标签: {TagId} {TagName}", tag.Id, tag.Name);
            return tag;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建标签失败: {TagName}", request.Name);
            throw;
        }
    }

    public async Task<NodeTag> UpdateTagAsync(string id, UpdateTagRequest request)
    {
        try
        {
            _logger.LogInformation("更新标签: {TagId}", id);

            if (!_nodeTags.TryGetValue(id, out var tag))
            {
                throw new KeyNotFoundException($"标签不存在: {id}");
            }

            // 更新字段
            if (!string.IsNullOrEmpty(request.Name) && request.Name != tag.Name)
            {
                // 检查名称是否已存在
                if (_nodeTags.Values.Any(t => t.Id != id && t.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException($"标签名称 '{request.Name}' 已存在");
                }
                tag.Name = request.Name;
            }

            if (request.Description != null)
                tag.Description = request.Description;

            if (request.Color != null)
                tag.Color = request.Color;

            if (request.Category != null)
                tag.Category = request.Category;

            if (request.Metadata != null)
                tag.Metadata = request.Metadata;

            tag.UpdatedAt = DateTime.UtcNow;
            tag.UpdatedBy = "system";

            // 清除缓存
            InvalidateCache();

            _logger.LogInformation("成功更新标签: {TagId} {TagName}", tag.Id, tag.Name);
            return tag;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新标签失败: {TagId}", id);
            throw;
        }
    }

    public async Task<bool> DeleteTagAsync(string id)
    {
        try
        {
            _logger.LogInformation("删除标签: {TagId}", id);

            if (!_nodeTags.TryGetValue(id, out var tag))
            {
                _logger.LogWarning("标签不存在: {TagId}", id);
                return false;
            }

            // 不能删除系统标签
            if (tag.IsSystem)
            {
                throw new InvalidOperationException("不能删除系统标签");
            }

            // 移除节点映射
            foreach (var nodeId in tag.NodeIds)
            {
                if (_nodeToTags.ContainsKey(nodeId))
                {
                    _nodeToTags[nodeId].Remove(id);
                }
            }

            // 删除标签
            _nodeTags.Remove(id);

            // 清除缓存
            InvalidateCache();

            _logger.LogInformation("成功删除标签: {TagId} {TagName}", tag.Id, tag.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除标签失败: {TagId}", id);
            throw;
        }
    }

    public async Task<bool> AddTagToNodeAsync(string nodeId, string tagId)
    {
        try
        {
            _logger.LogInformation("为节点添加标签: {NodeId} + {TagId}", nodeId, tagId);

            if (!_nodeTags.TryGetValue(tagId, out var tag))
            {
                throw new KeyNotFoundException($"标签不存在: {tagId}");
            }

            if (!tag.NodeIds.Contains(nodeId))
            {
                tag.NodeIds.Add(nodeId);
                tag.UpdatedAt = DateTime.UtcNow;

                if (!_nodeToTags.ContainsKey(nodeId))
                {
                    _nodeToTags[nodeId] = new HashSet<string>();
                }
                _nodeToTags[nodeId].Add(tagId);

                // 清除缓存
                InvalidateCache();
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "为节点添加标签失败: {NodeId} + {TagId}", nodeId, tagId);
            throw;
        }
    }

    public async Task<bool> RemoveTagFromNodeAsync(string nodeId, string tagId)
    {
        try
        {
            _logger.LogInformation("从节点移除标签: {NodeId} - {TagId}", nodeId, tagId);

            if (!_nodeTags.TryGetValue(tagId, out var tag))
            {
                throw new KeyNotFoundException($"标签不存在: {tagId}");
            }

            if (tag.NodeIds.Remove(nodeId))
            {
                tag.UpdatedAt = DateTime.UtcNow;

                if (_nodeToTags.ContainsKey(nodeId))
                {
                    _nodeToTags[nodeId].Remove(tagId);
                }

                // 清除缓存
                InvalidateCache();
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从节点移除标签失败: {NodeId} - {TagId}", nodeId, tagId);
            throw;
        }
    }

    public async Task<IEnumerable<NodeTag>> GetNodeTagsAsync(string nodeId)
    {
        try
        {
            _logger.LogInformation("获取节点标签: {NodeId}", nodeId);

            var tags = new List<NodeTag>();
            if (_nodeToTags.TryGetValue(nodeId, out var tagIds))
            {
                foreach (var tagId in tagIds)
                {
                    if (_nodeTags.TryGetValue(tagId, out var tag))
                    {
                        tags.Add(tag);
                    }
                }
            }

            _logger.LogInformation("成功获取节点标签: {NodeId} 标签数量 {Count}", nodeId, tags.Count);
            return tags;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取节点标签失败: {NodeId}", nodeId);
            throw;
        }
    }

    public async Task<IEnumerable<NodeInfo>> GetNodesByTagAsync(string tagId)
    {
        try
        {
            _logger.LogInformation("获取使用标签的节点: {TagId}", tagId);

            if (!_nodeTags.TryGetValue(tagId, out var tag))
            {
                throw new KeyNotFoundException($"标签不存在: {tagId}");
            }

            var nodes = new List<NodeInfo>();
            foreach (var nodeId in tag.NodeIds)
            {
                var node = await _nodeService.GetNodeByIdAsync(nodeId);
                if (node != null)
                {
                    nodes.Add(node);
                }
            }

            _logger.LogInformation("成功获取标签节点: {TagId} 节点数量 {Count}", tagId, nodes.Count);
            return nodes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取标签节点失败: {TagId}", tagId);
            throw;
        }
    }

    public async Task<bool> BatchUpdateNodeGroupsAsync(string nodeIds, string groupIds)
    {
        try
        {
            _logger.LogInformation("批量更新节点分组");

            var nodeIdList = nodeIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var groupIdList = groupIds.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var nodeId in nodeIdList)
            {
                // 获取节点当前的所有分组
                var currentGroups = _nodeToGroups.ContainsKey(nodeId)
                    ? _nodeToGroups[nodeId].ToList()
                    : new List<string>();

                // 移除不再需要的分组
                foreach (var groupId in currentGroups)
                {
                    if (!groupIdList.Contains(groupId))
                    {
                        await RemoveNodeFromGroupAsync(groupId, nodeId);
                    }
                }

                // 添加新的分组
                foreach (var groupId in groupIdList)
                {
                    if (!currentGroups.Contains(groupId))
                    {
                        await AddNodeToGroupAsync(groupId, nodeId);
                    }
                }
            }

            _logger.LogInformation("成功批量更新节点分组");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量更新节点分组失败");
            throw;
        }
    }

    public async Task<bool> BatchUpdateNodeTagsAsync(string nodeIds, string tagIds)
    {
        try
        {
            _logger.LogInformation("批量更新节点标签");

            var nodeIdList = nodeIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var tagIdList = tagIds.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var nodeId in nodeIdList)
            {
                // 获取节点当前的所有标签
                var currentTags = _nodeToTags.ContainsKey(nodeId)
                    ? _nodeToTags[nodeId].ToList()
                    : new List<string>();

                // 移除不再需要的标签
                foreach (var tagId in currentTags)
                {
                    if (!tagIdList.Contains(tagId))
                    {
                        await RemoveTagFromNodeAsync(nodeId, tagId);
                    }
                }

                // 添加新的标签
                foreach (var tagId in tagIdList)
                {
                    if (!currentTags.Contains(tagId))
                    {
                        await AddTagToNodeAsync(nodeId, tagId);
                    }
                }
            }

            _logger.LogInformation("成功批量更新节点标签");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量更新节点标签失败");
            throw;
        }
    }

    public async Task<GroupStatistics> GetGroupStatisticsAsync(string groupId)
    {
        try
        {
            _logger.LogInformation("获取分组统计信息: {GroupId}", groupId);

            if (!_nodeGroups.TryGetValue(groupId, out var group))
            {
                throw new KeyNotFoundException($"节点分组不存在: {groupId}");
            }

            var statistics = new GroupStatistics
            {
                GroupId = group.Id,
                GroupName = group.Name,
                LastUpdated = DateTime.UtcNow
            };

            // 获取分组中的所有节点
            var nodes = await GetNodesInGroupAsync(groupId);
            statistics.TotalNodes = nodes.Count();

            // 统计节点状态
            foreach (var node in nodes)
            {
                switch (node.Status)
                {
                    case NodeResourceStatus.Online:
                        statistics.OnlineNodes++;
                        break;
                    case NodeResourceStatus.Offline:
                        statistics.OfflineNodes++;
                        break;
                    case NodeResourceStatus.Warning:
                        statistics.WarningNodes++;
                        break;
                    case NodeResourceStatus.Error:
                        statistics.ErrorNodes++;
                        break;
                }
            }

            // 计算资源使用率平均值
            var totalCpu = 0.0;
            var totalMemory = 0.0;
            var totalDisk = 0.0;
            var totalContainers = 0;
            var runningContainers = 0;

            foreach (var node in nodes)
            {
                try
                {
                    var overview = await _resourceService.GetNodeResourceOverviewAsync(node.NodeId);
                    if (overview != null)
                    {
                        totalCpu += overview.CpuUsage.Percentage;
                        totalMemory += overview.MemoryUsage.Percentage;
                        totalDisk += overview.DiskUsage.Percentage;
                        totalContainers += overview.ContainerUsage.TotalCount;
                        runningContainers += overview.ContainerUsage.RunningCount;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "获取节点资源概览失败: {NodeId}", node.NodeId);
                }
            }

            if (statistics.TotalNodes > 0)
            {
                statistics.AverageCpuUsage = totalCpu / statistics.TotalNodes;
                statistics.AverageMemoryUsage = totalMemory / statistics.TotalNodes;
                statistics.AverageDiskUsage = totalDisk / statistics.TotalNodes;
            }

            statistics.TotalContainers = totalContainers;
            statistics.RunningContainers = runningContainers;

            _logger.LogInformation("成功获取分组统计: {GroupId} 节点数 {TotalNodes}", groupId, statistics.TotalNodes);
            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取分组统计失败: {GroupId}", groupId);
            throw;
        }
    }

    public async Task<TagStatistics> GetTagStatisticsAsync(string tagId)
    {
        try
        {
            _logger.LogInformation("获取标签统计信息: {TagId}", tagId);

            if (!_nodeTags.TryGetValue(tagId, out var tag))
            {
                throw new KeyNotFoundException($"标签不存在: {tagId}");
            }

            var statistics = new TagStatistics
            {
                TagId = tag.Id,
                TagName = tag.Name,
                Category = tag.Category,
                UsageCount = tag.NodeIds.Count,
                NodeIds = tag.NodeIds.ToList(),
                CreatedAt = tag.CreatedAt,
                LastUsed = tag.UpdatedAt
            };

            // 统计节点状态分布
            foreach (var nodeId in tag.NodeIds)
            {
                try
                {
                    var node = await _nodeService.GetNodeByIdAsync(nodeId);
                    if (node != null)
                    {
                        var statusName = node.Status.ToString();
                        if (statistics.NodeStatusDistribution.ContainsKey(statusName))
                        {
                            statistics.NodeStatusDistribution[statusName]++;
                        }
                        else
                        {
                            statistics.NodeStatusDistribution[statusName] = 1;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "获取节点信息失败: {NodeId}", nodeId);
                }
            }

            _logger.LogInformation("成功获取标签统计: {TagId} 使用次数 {UsageCount}", tagId, statistics.UsageCount);
            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取标签统计失败: {TagId}", tagId);
            throw;
        }
    }

    private void InitializeDefaultData()
    {
        try
        {
            // 创建默认分组
            if (!_nodeGroups.Any())
            {
                var defaultGroup = new NodeGroup
                {
                    Id = "default",
                    Name = "默认分组",
                    Description = "系统默认分组，所有未分组的节点都属于此分组",
                    Color = "#d9d9d9",
                    Icon = "inbox",
                    IsDefault = true,
                    IsSystem = true,
                    SortOrder = 999,
                    NodeIds = new List<string>(),
                    Metadata = new Dictionary<string, object>
                    {
                        ["auto_assign"] = true,
                        ["description"] = "自动分配给未分组的节点"
                    },
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = "system",
                    UpdatedBy = "system"
                };

                _nodeGroups[defaultGroup.Id] = defaultGroup;
            }

            // 创建默认标签
            if (!_nodeTags.Any())
            {
                var defaultTags = new[]
                {
                    new NodeTag
                    {
                        Id = "production",
                        Name = "生产环境",
                        Description = "生产环境节点",
                        Color = "#f5222d",
                        Category = "environment",
                        IsSystem = true,
                        NodeIds = new List<string>(),
                        Metadata = new Dictionary<string, object>
                        {
                            ["environment"] = "production",
                            ["priority"] = "high"
                        },
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedBy = "system",
                        UpdatedBy = "system"
                    },
                    new NodeTag
                    {
                        Id = "staging",
                        Name = "测试环境",
                        Description = "测试环境节点",
                        Color = "#fa8c16",
                        Category = "environment",
                        IsSystem = true,
                        NodeIds = new List<string>(),
                        Metadata = new Dictionary<string, object>
                        {
                            ["environment"] = "staging",
                            ["priority"] = "medium"
                        },
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedBy = "system",
                        UpdatedBy = "system"
                    },
                    new NodeTag
                    {
                        Id = "development",
                        Name = "开发环境",
                        Description = "开发环境节点",
                        Color = "#52c41a",
                        Category = "environment",
                        IsSystem = true,
                        NodeIds = new List<string>(),
                        Metadata = new Dictionary<string, object>
                        {
                            ["environment"] = "development",
                            ["priority"] = "low"
                        },
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedBy = "system",
                        UpdatedBy = "system"
                    },
                    new NodeTag
                    {
                        Id = "high-performance",
                        Name = "高性能",
                        Description = "高性能节点",
                        Color = "#722ed1",
                        Category = "performance",
                        IsSystem = true,
                        NodeIds = new List<string>(),
                        Metadata = new Dictionary<string, object>
                        {
                            ["performance_level"] = "high"
                        },
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedBy = "system",
                        UpdatedBy = "system"
                    }
                };

                foreach (var tag in defaultTags)
                {
                    _nodeTags[tag.Id] = tag;
                }
            }

            _logger.LogInformation("完成默认节点分组和标签数据初始化");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化默认数据失败");
        }
    }

    private void InvalidateCache()
    {
        _cache.Remove("node_groups_all");
        _cache.Remove("node_tags_all");
    }
}