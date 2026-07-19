using DockerPanel.API.Models;
using System.Text.Json;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;
using DockerPanel.API.Services.Acme;
using DockerPanel.API.Models.Acme;

namespace DockerPanel.API.Services;

/// <summary>
/// 域名映射服务
/// </summary>
public class DomainMappingService
{
    private readonly ILogger<DomainMappingService> _logger;
    private readonly INetworkService _networkService;
    private readonly IContainerService _containerService;
    private readonly IReverseProxyFactory _reverseProxyFactory;
    private readonly IAcmeService _acmeService;

    public DomainMappingService(
        ILogger<DomainMappingService> logger,
        INetworkService networkService,
        IContainerService containerService,
        IReverseProxyFactory reverseProxyFactory,
        IAcmeService acmeService)
    {
        _logger = logger;
        _networkService = networkService;
        _containerService = containerService;
        _reverseProxyFactory = reverseProxyFactory;
        _acmeService = acmeService;
    }

    /// <summary>
    /// 处理容器创建时的域名映射
    /// </summary>
    /// <summary>
    /// 处理容器创建时的域名映射
    /// </summary>
    public async Task ProcessContainerDomainMappingAsync(string containerId, CreateContainerRequest request)
    {
        try
        {
            // 如果是 none 网络模式，跳过域名映射
            if (request.NetworkMode == "none")
            {
                _logger.LogInformation("容器使用 none 网络模式，跳过域名映射: {ContainerId}", containerId);
                return;
            }

            // 2. 如果配置了域名映射，则创建YARP路由
            if (request.DomainMapping != null && !string.IsNullOrEmpty(request.DomainMapping.Domain))
            {
                await CreateDomainMappingAsync(containerId, request);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理容器域名映射失败: {ContainerId}", containerId);
            throw;
        }
    }

    /// <summary>
    /// 确保容器连接到默认网络
    /// </summary>
    private async Task EnsureContainerInDefaultNetworkAsync(string containerId, string? containerName = null)
    {
        const string DEFAULT_NETWORK_NAME = "dockerpanel-network";

        try
        {
            // 获取或创建默认网络
            var defaultNetwork = await _networkService.GetDefaultNetworkAsync();
            if (defaultNetwork == null)
            {
                defaultNetwork = await _networkService.EnsureDefaultNetworkAsync();
                _logger.LogInformation("已创建默认网络: {NetworkName}", DEFAULT_NETWORK_NAME);
            }

            // 获取网络详情（包含容器列表）
            var networkDetail = await _networkService.GetNetworkAsync(defaultNetwork.Id);
            var isInDefaultNetwork = networkDetail?.Containers?.Any(c => c.Id == containerId) ?? false;

            if (!isInDefaultNetwork)
            {
                // 如果提供了容器名称，将其设置为网络别名以增强DNS解析稳定性
                NetworkConfig? networkConfig = null;
                if (!string.IsNullOrEmpty(containerName))
                {
                    networkConfig = new NetworkConfig
                    {
                        Aliases = new List<string> { containerName }
                    };
                }

                var connected = await _networkService.ConnectContainerToNetworkAsync(
                    defaultNetwork.Id, containerId, networkConfig);

                if (connected)
                {
                    _logger.LogInformation("容器已连接到默认网络: {ContainerId} -> {NetworkName} (Alias: {Alias})",
                        containerId, DEFAULT_NETWORK_NAME, containerName ?? "none");
                }
                else
                {
                    _logger.LogWarning("连接容器到默认网络失败: {ContainerId} -> {NetworkName}",
                        containerId, DEFAULT_NETWORK_NAME);
                }
            }
            else
            {
                _logger.LogDebug("容器已在默认网络中: {ContainerId}", containerId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "确保容器连接默认网络失败: {ContainerId}", containerId);
        }
    }

    /// <summary>
    /// 创建域名映射
    /// </summary>
    private async Task CreateDomainMappingAsync(string containerId, CreateContainerRequest request)
    {
        var domainMapping = request.DomainMapping;
        if (domainMapping == null)
        {
            _logger.LogWarning("域名映射配置为空，跳过创建");
            return;
        }

        try
        {
            // 获取容器信息
            var container = await _containerService.GetContainerAsync(containerId);
            if (container == null)
            {
                _logger.LogWarning("容器不存在，无法创建域名映射: {ContainerId}", containerId);
                return;
            }

            // 处理证书自动申请
            string? certificateId = domainMapping.CertificateId;
            if (domainMapping.AutoRequestCertificate)
            {
                try
                {
                    certificateId = await AutoRequestCertificateAsync(domainMapping.Domain!, domainMapping.AccountId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "自动申请证书失败: {Domain}", domainMapping.Domain);
                    // 即使证书申请失败，也继续创建域名映射，只是不带证书
                }
            }

            // 根据网络模式确定目标地址。
            string destinationAddress;
            if (request.NetworkMode == "host")
            {
                // host 模式：使用 host.docker.internal 访问宿主机上的服务
                destinationAddress = $"host.docker.internal:{domainMapping.ContainerPort}";
                _logger.LogInformation("使用 host 网络模式，目标地址: {Address}", destinationAddress);
            }
            else
            {
                // bridge 模式：使用容器名称
                // DockerPanel 在 dockerpanel-network 内，可以通过 Docker DNS 解析容器名
                destinationAddress = $"{container.Name}:{domainMapping.ContainerPort}";
                _logger.LogInformation("使用 bridge 网络模式，目标地址: {Address}", destinationAddress);
                
                // 确保容器连接到默认网络，并将容器名作为网络别名
                await EnsureContainerInDefaultNetworkAsync(containerId, container.Name);
            }

            // 创建域名映射对象
            var mapping = new DomainMapping
            {
                Id = Guid.NewGuid().ToString(),
                ContainerId = containerId,
                ContainerName = container.Name ?? "unknown",
                Domain = domainMapping.Domain!,
                DestinationAddress = destinationAddress,
                ContainerPort = domainMapping.ContainerPort,
                PathPrefix = domainMapping.PathPrefix ?? "/",
                Protocol = "http", // 容器内部通常是HTTP
                EnableSsl = !string.IsNullOrEmpty(certificateId),
                CertificateId = certificateId,
                AccountId = domainMapping.AccountId,
                Enabled = true,
                Priority = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // 使用工厂添加映射
            var result = await _reverseProxyFactory.AddDomainMappingAsync(mapping);
            if (result)
            {
                _logger.LogInformation("域名映射创建成功: {Domain} -> {ContainerId}:{Port}",
                    domainMapping.Domain, containerId, domainMapping.ContainerPort);
            }
            else
            {
                _logger.LogError("域名映射创建失败: {Domain}", domainMapping.Domain);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建域名映射失败: {ContainerId} -> {Domain}",
                containerId, domainMapping.Domain);
            throw;
        }
    }

    private async Task<string?> AutoRequestCertificateAsync(string domain, string? accountId = null)
    {
        AcmeAccount? account = null;

        // 优先使用指定的账户ID
        if (!string.IsNullOrEmpty(accountId))
        {
            var allAccounts = await _acmeService.GetAccountsAsync();
            account = allAccounts.FirstOrDefault(a => a.Id == accountId);
            if (account == null)
            {
                _logger.LogWarning("指定的ACME账户不存在: {AccountId}，将尝试使用默认账户", accountId);
            }
        }

        // 如果没有指定账户或指定的账户不存在，获取第一个可用账户
        if (account == null)
        {
            var accounts = await _acmeService.GetAccountsAsync();
            account = accounts.FirstOrDefault();
        }

        // 如果没有账户，抛出异常
        if (account == null)
        {
            throw new InvalidOperationException("未找到可用的 ACME 账户，无法自动申请证书。请先配置 ACME 账户。");
        }

        var request = new AcmeCertificateRequest
        {
            AccountId = account.Id,
            Domains = new List<string> { domain },
            KeyType = "ECDSA256", // 现代化默认值
            UseWildcard = false,
            ChallengeTypes = new List<string> { "http-01" },
            AcmeProvider = account.Provider == "letsencrypt" ? "letsencrypt" : account.Provider,
            Metadata = new Dictionary<string, object>
            {
                ["autoRequested"] = true,
                ["challengeType"] = "http-01"
            },
            AccountKey = account.AccountKey
        };

        var order = await _acmeService.OrderCertificateAsync(request);
        if (order != null)
        {
            _logger.LogInformation("已启动证书自动申请流程: OrderId={OrderId}, Domain={Domain}, AccountId={AccountId}", 
                order.Id, domain, account.Id);
            return order.Id; // 返回订单ID作为临时证书ID
        }

        return null;
    }

    /// <summary>
    /// 删除域名映射
    /// </summary>
    /// <summary>
    /// 删除域名映射
    /// </summary>
    public async Task RemoveDomainMappingAsync(string containerId)
    {
        try
        {
            var container = await _containerService.GetContainerAsync(containerId);
            var mappings = new List<DomainMapping>();
            
            if (container != null && !string.IsNullOrEmpty(container.Name))
            {
                mappings = await _reverseProxyFactory.GetDomainMappingsByContainerNameAsync(container.Name);
            }
            
            if (mappings.Count == 0)
            {
                mappings = await _reverseProxyFactory.GetDomainMappingsByContainerIdAsync(containerId);
            }

            foreach (var mapping in mappings)
            {
                await _reverseProxyFactory.RemoveDomainMappingAsync(mapping.Id);

                // 同时删除关联的路由和集群
                var routeId = $"{mapping.Domain}-{containerId}";
                var clusterId = $"cluster-{containerId}";

                await _reverseProxyFactory.RemoveRouteAsync(routeId);
                await _reverseProxyFactory.RemoveClusterAsync(clusterId);

                _logger.LogInformation("已删除域名映射及其关联配置: {MappingId}, {Domain}", mapping.Id, mapping.Domain);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除域名映射失败: {ContainerId}", containerId);
            throw;
        }
    }

    /// <summary>
    /// 获取容器的域名映射
    /// </summary>
    public async Task<List<DomainMappingInfo>> GetContainerDomainMappingsAsync(string containerId)
    {
        var result = new List<DomainMappingInfo>();

        try
        {
            var container = await _containerService.GetContainerAsync(containerId);
            var mappings = new List<DomainMapping>();
            
            if (container != null && !string.IsNullOrEmpty(container.Name))
            {
                mappings = await _reverseProxyFactory.GetDomainMappingsByContainerNameAsync(container.Name);
                
                // 自动修复外部重建导致的 ContainerId 变更
                foreach (var mapping in mappings)
                {
                    if (mapping.ContainerId != containerId)
                    {
                        mapping.ContainerId = containerId;
                        await _reverseProxyFactory.UpdateDomainMappingAsync(mapping);
                        _logger.LogInformation("已自动修复映射 {Id} 的 ContainerId 为 {NewId}", mapping.Id, containerId);
                    }
                }
            }
            
            if (mappings.Count == 0)
            {
                mappings = await _reverseProxyFactory.GetDomainMappingsByContainerIdAsync(containerId);
            }

            foreach (var mapping in mappings)
            {
                result.Add(new DomainMappingInfo
                {
                    ContainerId = mapping.ContainerId,
                    ContainerName = mapping.ContainerName,
                    Domain = mapping.Domain,
                    PathPrefix = mapping.PathPrefix,
                    ContainerPort = ParsePortFromAddress(mapping.DestinationAddress),
                    EnableSsl = mapping.EnableSsl,
                    RouteId = $"{mapping.Domain}-{mapping.ContainerId}",
                    ClusterId = $"cluster-{mapping.ContainerId}",
                    CreatedAt = mapping.CreatedAt
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取容器域名映射失败: {ContainerId}", containerId);
        }

        return result;
    }

    private int ParsePortFromAddress(string address)
    {
        if (string.IsNullOrEmpty(address)) return 80;

        var parts = address.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[1], out var port))
        {
            return port;
        }
        return 80;
    }

    /// <summary>
    /// 从集群配置中提取端口号
    /// </summary>
    private int ExtractPortFromDestination(ClusterConfig cluster)
    {
        try
        {
            var destination = cluster.Destinations?.Values.FirstOrDefault();
            if (destination?.Address != null)
            {
                // 从 "http://container-name:port" 中提取端口
                var parts = destination.Address.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[1], out var port))
                {
                    return port;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提取端口号失败");
        }

        return 80; // 默认端口
    }
}

/// <summary>
/// 域名映射信息
/// </summary>
public class DomainMappingInfo
{
    public string ContainerId { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string? PathPrefix { get; set; }
    public int ContainerPort { get; set; }
    public bool EnableSsl { get; set; }
    public string RouteId { get; set; } = string.Empty;
    public string ClusterId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// YARP路径前缀转换
/// </summary>
public class PathPrefixTransform
{
    public string Pattern { get; set; } = string.Empty;

    public PathPrefixTransform(string pattern)
    {
        Pattern = pattern;
    }
}