using DockerPanel.API.Models;

namespace DockerPanel.API.Services;

/// <summary>
/// 网络管理服务接口
/// </summary>
public interface INetworkService
{
    /// <summary>
    /// 获取所有网络
    /// </summary>
    Task<IEnumerable<NetworkInfo>> GetNetworksAsync(string? nodeId = null);

    /// <summary>
    /// 根据ID获取网络详情
    /// </summary>
    Task<NetworkDetailInfo?> GetNetworkByIdAsync(string networkId, string? nodeId = null);

    /// <summary>
    /// 获取网络详情
    /// </summary>
    Task<NetworkDetailInfo?> GetNetworkAsync(string networkId, string? nodeId = null);

    /// <summary>
    /// 创建网络
    /// </summary>
    Task<NetworkInfo> CreateNetworkAsync(CreateNetworkRequest request);

    /// <summary>
    /// 删除网络
    /// </summary>
    Task<bool> DeleteNetworkAsync(string networkId, string? nodeId = null);

    /// <summary>
    /// 连接容器到网络
    /// </summary>
    Task<bool> ConnectContainerToNetworkAsync(string networkId, string containerId, NetworkConfig? config = null);

    /// <summary>
    /// 断开容器与网络的连接
    /// </summary>
    Task<bool> DisconnectContainerFromNetworkAsync(string networkId, string containerId);

    /// <summary>
    /// 获取网络中的容器
    /// </summary>
    Task<IEnumerable<NetworkContainerInfo>> GetNetworkContainersAsync(string networkId, string? nodeId = null);

    /// <summary>
    /// 清理未使用的网络
    /// </summary>
    Task<int> PruneNetworksAsync();

    /// <summary>
    /// 获取网络统计信息
    /// </summary>
    Task<NetworkStatistics> GetNetworkStatisticsAsync(string? nodeId = null);

    /// <summary>
    /// 检查网络是否存在
    /// </summary>
    Task<bool> NetworkExistsAsync(string networkId, string? nodeId = null);

    /// <summary>
    /// 获取网络IPAM信息
    /// </summary>
    Task<NetworkIpamInfo?> GetNetworkIpamInfoAsync(string networkId, string? nodeId = null);

    /// <summary>
    /// 更新网络
    /// </summary>
    Task<bool> UpdateNetworkAsync(string networkId, UpdateNetworkRequest request);

    /// <summary>
    /// 移除网络
    /// </summary>
    Task RemoveNetworkAsync(string networkId);

    /// <summary>
    /// 连接容器
    /// </summary>
    Task ConnectContainerAsync(string networkId, string containerId, NetworkConfig? config = null);

    /// <summary>
    /// 断开容器连接
    /// </summary>
    Task DisconnectContainerAsync(string networkId, string containerId);

    /// <summary>
    /// 确保默认网络存在
    /// </summary>
    Task<NetworkInfo> EnsureDefaultNetworkAsync();

    /// <summary>
    /// 获取默认网络
    /// </summary>
    Task<NetworkInfo?> GetDefaultNetworkAsync();

    /// <summary>
    /// 检查是否为默认网络
    /// </summary>
    bool IsDefaultNetwork(string networkId);
}