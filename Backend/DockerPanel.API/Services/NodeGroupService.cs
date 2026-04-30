using DockerPanel.API.Models;

namespace DockerPanel.API.Services;

/// <summary>
/// 节点分组和标签管理服务接口
/// </summary>
public interface INodeGroupService
{
    /// <summary>
    /// 获取所有节点分组
    /// </summary>
    Task<IEnumerable<NodeGroup>> GetNodeGroupsAsync();

    /// <summary>
    /// 根据ID获取节点分组
    /// </summary>
    Task<NodeGroup?> GetNodeGroupByIdAsync(string id);

    /// <summary>
    /// 创建节点分组
    /// </summary>
    Task<NodeGroup> CreateNodeGroupAsync(CreateNodeGroupRequest request);

    /// <summary>
    /// 更新节点分组
    /// </summary>
    Task<NodeGroup> UpdateNodeGroupAsync(string id, UpdateNodeGroupRequest request);

    /// <summary>
    /// 删除节点分组
    /// </summary>
    Task<bool> DeleteNodeGroupAsync(string id);

    /// <summary>
    /// 将节点添加到分组
    /// </summary>
    Task<bool> AddNodeToGroupAsync(string groupId, string nodeId);

    /// <summary>
    /// 从分组中移除节点
    /// </summary>
    Task<bool> RemoveNodeFromGroupAsync(string groupId, string nodeId);

    /// <summary>
    /// 获取分组中的节点列表
    /// </summary>
    Task<IEnumerable<NodeInfo>> GetNodesInGroupAsync(string groupId);

    /// <summary>
    /// 获取节点所属的所有分组
    /// </summary>
    Task<IEnumerable<NodeGroup>> GetNodeGroupsAsync(string nodeId);

    /// <summary>
    /// 获取所有可用的标签
    /// </summary>
    Task<IEnumerable<NodeTag>> GetAllTagsAsync();

    /// <summary>
    /// 创建标签
    /// </summary>
    Task<NodeTag> CreateTagAsync(CreateTagRequest request);

    /// <summary>
    /// 更新标签
    /// </summary>
    Task<NodeTag> UpdateTagAsync(string id, UpdateTagRequest request);

    /// <summary>
    /// 删除标签
    /// </summary>
    Task<bool> DeleteTagAsync(string id);

    /// <summary>
    /// 为节点添加标签
    /// </summary>
    Task<bool> AddTagToNodeAsync(string nodeId, string tagId);

    /// <summary>
    /// 从节点移除标签
    /// </summary>
    Task<bool> RemoveTagFromNodeAsync(string nodeId, string tagId);

    /// <summary>
    /// 获取节点的所有标签
    /// </summary>
    Task<IEnumerable<NodeTag>> GetNodeTagsAsync(string nodeId);

    /// <summary>
    /// 获取使用指定标签的所有节点
    /// </summary>
    Task<IEnumerable<NodeInfo>> GetNodesByTagAsync(string tagId);

    /// <summary>
    /// 批量操作节点分组
    /// </summary>
    Task<bool> BatchUpdateNodeGroupsAsync(string nodeIds, string groupIds);

    /// <summary>
    /// 批量操作节点标签
    /// </summary>
    Task<bool> BatchUpdateNodeTagsAsync(string nodeIds, string tagIds);

    /// <summary>
    /// 获取分组统计信息
    /// </summary>
    Task<GroupStatistics> GetGroupStatisticsAsync(string groupId);

    /// <summary>
    /// 获取标签使用统计
    /// </summary>
    Task<TagStatistics> GetTagStatisticsAsync(string tagId);
}

/// <summary>
/// 节点分组实体
/// </summary>
public class NodeGroup
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Color { get; set; } = "#1890ff"; // 默认蓝色
    public string Icon { get; set; } = "cluster"; // 默认图标
    public bool IsDefault { get; set; } = false;
    public bool IsSystem { get; set; } = false;
    public int SortOrder { get; set; } = 0;
    public List<string> NodeIds { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string UpdatedBy { get; set; } = string.Empty;
}

/// <summary>
/// 创建节点分组请求
/// </summary>
public class CreateNodeGroupRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Color { get; set; } = "#1890ff";
    public string Icon { get; set; } = "cluster";
    public List<string> NodeIds { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// 更新节点分组请求
/// </summary>
public class UpdateNodeGroupRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public List<string>? NodeIds { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// 节点标签实体
/// </summary>
public class NodeTag
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Color { get; set; } = "#87d068"; // 默认绿色
    public string Category { get; set; } = "custom"; // 标签分类
    public bool IsSystem { get; set; } = false;
    public List<string> NodeIds { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string UpdatedBy { get; set; } = string.Empty;
}

/// <summary>
/// 创建标签请求
/// </summary>
public class CreateTagRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Color { get; set; } = "#87d068";
    public string Category { get; set; } = "custom";
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// 更新标签请求
/// </summary>
public class UpdateTagRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? Category { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// 分组统计信息
/// </summary>
public class GroupStatistics
{
    public string GroupId { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public int TotalNodes { get; set; }
    public int OnlineNodes { get; set; }
    public int OfflineNodes { get; set; }
    public int WarningNodes { get; set; }
    public int ErrorNodes { get; set; }
    public double AverageCpuUsage { get; set; }
    public double AverageMemoryUsage { get; set; }
    public double AverageDiskUsage { get; set; }
    public int TotalContainers { get; set; }
    public int RunningContainers { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// 标签统计信息
/// </summary>
public class TagStatistics
{
    public string TagId { get; set; } = string.Empty;
    public string TagName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public List<string> NodeIds { get; set; } = new();
    public Dictionary<string, int> NodeStatusDistribution { get; set; } = new();
    public DateTime LastUsed { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 节点分组查询选项
/// </summary>
public class NodeGroupQueryOptions
{
    public bool IncludeNodes { get; set; } = false;
    public bool IncludeNodeDetails { get; set; } = false;
    public bool IncludeStatistics { get; set; } = false;
    public string? SearchTerm { get; set; }
    public List<string>? TagIds { get; set; }
    public List<string>? NodeStatuses { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}

/// <summary>
/// 节点标签查询选项
/// </summary>
public class NodeTagQueryOptions
{
    public bool IncludeNodes { get; set; } = false;
    public bool IncludeStatistics { get; set; } = false;
    public string? SearchTerm { get; set; }
    public string? Category { get; set; }
    public int? MinUsageCount { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}