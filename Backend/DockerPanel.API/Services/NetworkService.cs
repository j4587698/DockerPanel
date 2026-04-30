using DockerPanel.API.Models;
using System.Diagnostics;
using System.Text.Json;
using System.Linq;

namespace DockerPanel.API.Services;

/// <summary>
/// 网络管理服务实现
/// </summary>
public class NetworkService : INetworkService
{
    private readonly ILogger<NetworkService> _logger;
    private readonly IContainerEngine _engine;
    private const string DEFAULT_NETWORK_NAME = "dockerpanel-network";

    public NetworkService(ILogger<NetworkService> logger, IContainerEngine engine)
    {
        _logger = logger;
        _engine = engine;
    }

    public async Task<IEnumerable<NetworkInfo>> GetNetworksAsync(string? nodeId = null)
    {
        _logger.LogInformation("获取网络列表");
        return await _engine.ListNetworksAsync(nodeId);
    }

    public async Task<NetworkInfo?> GetNetworkAsync(string id)
    {
        return await _engine.GetNetworkAsync(id);
    }

    public async Task<NetworkDetailInfo?> GetNetworkAsync(string id, string? nodeId = null)
    {
        // 从 Docker 引擎获取完整的网络信息
        var n = await _engine.GetNetworkAsync(id, nodeId);
        if (n == null) return null;

        // 不可删除的网络
        var defaultNetworkNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bridge", "host", "none", "dockerpanel-network"
        };

        return new NetworkDetailInfo
        {
            Id = n.Id,
            Name = n.Name,
            Driver = n.Driver,
            Scope = n.Scope,
            Internal = n.Internal,
            EnableIPv6 = n.EnableIPv6,
            Attachable = n.Attachable,
            Ingress = n.Ingress,
            Labels = n.Labels ?? new Dictionary<string, string>(),
            Options = n.Options ?? new Dictionary<string, string>(),
            CreatedAt = string.IsNullOrEmpty(n.CreatedAt) ? DateTime.MinValue : DateTime.Parse(n.CreatedAt),
            Containers = n.Containers ?? new List<NetworkContainer>()
        };
    }

    public async Task<NetworkInfo> CreateNetworkAsync(CreateNetworkRequest request)
    {
        _logger.LogInformation("创建网络: {Name}", request.Name);
        return await _engine.CreateNetworkAsync(request, request.NodeId);
    }

    public async Task<bool> DeleteNetworkAsync(string networkId, string? nodeId = null)
    {
        try
        {
            await _engine.RemoveNetworkAsync(networkId, nodeId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除网络失败: {Id}", networkId);
            return false;
        }
    }

    public async Task<bool> ConnectContainerToNetworkAsync(string networkId, string containerId, NetworkConnectionConfig? config = null, string? nodeId = null)
    {
        try
        {
            await _engine.ConnectContainerToNetworkAsync(networkId, containerId, config, nodeId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ConnectContainerToNetworkAsync(string networkId, string containerId, NetworkConfig? config = null)
    {
        return await ConnectContainerToNetworkAsync(networkId, containerId, config != null ? new NetworkConnectionConfig { Aliases = config.Aliases?.ToList() ?? new List<string>() } : null);
    }

    public async Task<bool> DisconnectContainerFromNetworkAsync(string networkId, string containerId, string? nodeId = null)
    {
        try
        {
            await _engine.DisconnectContainerFromNetworkAsync(networkId, containerId, nodeId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DisconnectContainerFromNetworkAsync(string networkId, string containerId)
    {
        return await DisconnectContainerFromNetworkAsync(networkId, containerId, null);
    }

    public async Task<NetworkInfo> EnsureDefaultNetworkAsync()
    {
        var networks = await GetNetworksAsync();
        var defaultNetwork = networks.FirstOrDefault(n => n.Name == DEFAULT_NETWORK_NAME);
        if (defaultNetwork != null) return defaultNetwork;

        return await CreateNetworkAsync(new CreateNetworkRequest { Name = DEFAULT_NETWORK_NAME, Driver = "bridge" });
    }

    public async Task<NetworkInfo?> GetDefaultNetworkAsync()
    {
        var networks = await GetNetworksAsync();
        return networks.FirstOrDefault(n => n.Name == DEFAULT_NETWORK_NAME);
    }

    public bool IsDefaultNetwork(string networkId)
    {
        return string.Equals(networkId, "bridge", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(networkId, "host", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(networkId, "none", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(networkId, DEFAULT_NETWORK_NAME, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<NetworkDetailInfo?> GetNetworkByIdAsync(string networkId, string? nodeId = null)
    {
        return await GetNetworkAsync(networkId, nodeId);
    }

    public async Task<IEnumerable<NetworkContainerInfo>> GetNetworkContainersAsync(string networkId, string? nodeId = null)
    {
        try
        {
            var network = await GetNetworkAsync(networkId, nodeId);
            if (network?.Containers == null) return new List<NetworkContainerInfo>();

            return network.Containers.Select(c => new NetworkContainerInfo
            {
                ContainerId = c.Id,
                ContainerName = c.Name,
                IpAddress = c.IpAddress,
                MacAddress = c.MacAddress
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取网络容器列表失败: {NetworkId}", networkId);
            return new List<NetworkContainerInfo>();
        }
    }

    public async Task<NetworkPruneResult> PruneNetworksAsync(NetworkPruneOptions options, string? nodeId = null)
    {
        var deleted = await _engine.PruneNetworksAsync(nodeId);
        return new NetworkPruneResult { Success = true, NetworksDeleted = deleted };
    }

    public async Task<NetworkStatistics> GetNetworkStatisticsAsync(string? nodeId = null)
    {
        var networks = (await _engine.ListNetworksAsync(nodeId)).ToList();
        return new NetworkStatistics
        {
            NetworkId = "all",
            NetworkName = "全部网络",
            Timestamp = DateTime.UtcNow,
            ConnectedContainers = networks.Sum(n => n.Containers?.Count ?? 0)
        };
    }

    public async Task<bool> NetworkExistsAsync(string networkId, string? nodeId = null)
    {
        var n = await _engine.GetNetworkAsync(networkId, nodeId);
        return n != null;
    }

    public async Task<NetworkIpamInfo?> GetNetworkIpamInfoAsync(string networkId, string? nodeId = null)
    {
        var network = await _engine.GetNetworkAsync(networkId, nodeId);
        if (network == null) return null;

        var pools = ParseIpamPools(network.IPAM, network.Containers ?? new List<NetworkContainer>());
        var total = pools.Sum(p => p.TotalIPs);
        var used = pools.Sum(p => p.UsedIPs.Count);
        return new NetworkIpamInfo
        {
            NetworkId = network.Id,
            NetworkName = network.Name,
            Pools = pools,
            Usage = new NetworkIpamUsage
            {
                TotalIPs = total,
                UsedIPs = used,
                AvailableIPs = Math.Max(0, total - used),
                UsagePercent = total > 0 ? used * 100.0 / total : 0,
                LastUpdated = DateTime.UtcNow
            },
            LastUpdated = DateTime.UtcNow
        };
    }

    public async Task<NetworkInfo> UpdateNetworkAsync(string networkId, UpdateNetworkRequest request, string? nodeId = null)
    {
        var network = await _engine.GetNetworkAsync(networkId, nodeId) ?? throw new Exception("Not found");
        _logger.LogWarning("Docker 不支持直接更新已有网络配置，已返回当前网络信息: {NetworkId}", networkId);
        return network;
    }

    public async Task<bool> UpdateNetworkAsync(string networkId, UpdateNetworkRequest request)
    {
        await UpdateNetworkAsync(networkId, request, null);
        return true;
    }

    public async Task RemoveNetworkAsync(string networkId)
    {
        await _engine.RemoveNetworkAsync(networkId);
    }

    public async Task ConnectContainerAsync(string networkId, string containerId, NetworkConfig? config = null)
    {
        await ConnectContainerToNetworkAsync(networkId, containerId, config);
    }

    public async Task DisconnectContainerAsync(string networkId, string containerId)
    {
        await DisconnectContainerFromNetworkAsync(networkId, containerId);
    }

    public async Task<int> PruneNetworksAsync()
    {
        return await _engine.PruneNetworksAsync();
    }

    private static List<NetworkIpamPool> ParseIpamPools(string? ipamJson, List<NetworkContainer> containers)
    {
        if (string.IsNullOrWhiteSpace(ipamJson)) return new List<NetworkIpamPool>();

        try
        {
            using var document = JsonDocument.Parse(ipamJson);
            if (!document.RootElement.TryGetProperty("Config", out var config) || config.ValueKind != JsonValueKind.Array)
            {
                return new List<NetworkIpamPool>();
            }

            return config.EnumerateArray().Select(entry =>
            {
                var subnet = entry.TryGetProperty("Subnet", out var subnetElement) ? subnetElement.GetString() ?? string.Empty : string.Empty;
                var used = containers.Select(c => NormalizeIpAddress(c.IpAddress)).Where(ip => !string.IsNullOrWhiteSpace(ip)).Distinct().ToList();
                var total = EstimateIpv4Capacity(subnet);
                return new NetworkIpamPool
                {
                    Subnet = subnet,
                    IPRange = entry.TryGetProperty("IPRange", out var rangeElement) ? rangeElement.GetString() : null,
                    Gateway = entry.TryGetProperty("Gateway", out var gatewayElement) ? gatewayElement.GetString() : null,
                    TotalIPs = total,
                    AvailableIPs = Math.Max(0, total - used.Count),
                    UsagePercent = total > 0 ? used.Count * 100.0 / total : 0,
                    UsedIPs = used
                };
            }).ToList();
        }
        catch
        {
            return new List<NetworkIpamPool>();
        }
    }

    private static string NormalizeIpAddress(string ipAddress)
    {
        var slashIndex = ipAddress.IndexOf('/');
        return slashIndex >= 0 ? ipAddress[..slashIndex] : ipAddress;
    }

    private static long EstimateIpv4Capacity(string subnet)
    {
        var slashIndex = subnet.IndexOf('/');
        if (slashIndex < 0 || !int.TryParse(subnet[(slashIndex + 1)..], out var prefix) || prefix < 0 || prefix > 32)
        {
            return 0;
        }

        var total = 1L << (32 - prefix);
        return Math.Max(0, total - 2);
    }
}