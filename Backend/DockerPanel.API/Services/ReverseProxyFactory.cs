using System.Collections.Concurrent;
using DockerPanel.API.Data;
using DockerPanel.API.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace DockerPanel.API.Services;

/// <summary>
/// 反向代理工厂实现
/// </summary>
public class ReverseProxyFactory : IReverseProxyFactory, IProxyConfigProvider, IDisposable
{
    private readonly ILogger<ReverseProxyFactory> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<string, RouteConfig> _routes;
    private readonly ConcurrentDictionary<string, ClusterConfig> _clusters;
    private readonly ConcurrentDictionary<string, DomainMapping> _domainMappings;
    private readonly SemaphoreSlim _configLock;
    private CancellationTokenSource _changeTokenSource;
    private bool _disposed;

    public ReverseProxyFactory(
        ILogger<ReverseProxyFactory> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _routes = new ConcurrentDictionary<string, RouteConfig>();
        _clusters = new ConcurrentDictionary<string, ClusterConfig>();
        _domainMappings = new ConcurrentDictionary<string, DomainMapping>();
        _configLock = new SemaphoreSlim(1, 1);
        _changeTokenSource = new CancellationTokenSource();

        // 初始化时从数据库加载配置
        LoadConfigFromDatabaseAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// 获取TinyDbContext实例（每次调用创建新的作用域）
    /// </summary>
    private TinyDbContext GetDbContext()
    {
        var scope = _scopeFactory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TinyDbContext>();
    }

    #region IProxyConfigProvider Implementation

    /// <summary>
    /// 获取当前代理配置（实现 IProxyConfigProvider 接口）
    /// 依赖 ConcurrentDictionary 的线程安全性，不额外加锁避免死锁
    /// </summary>
    IProxyConfig IProxyConfigProvider.GetConfig()
    {
        // 如果 token 已被取消，需要创建新的
        // 否则 YARP 会在注册监听时发现 token 已取消，再次触发重载
        if (_changeTokenSource.IsCancellationRequested)
        {
            _changeTokenSource.Dispose();
            _changeTokenSource = new CancellationTokenSource();
        }

        // ConcurrentDictionary 本身是线程安全的
        // ToList() 会创建快照，即使同时有修改也不会抛出异常
        return new YarpProxyConfig
        {
            Routes = _routes.Values.ToList(),
            Clusters = _clusters.Values.ToList(),
            ChangeToken = _changeTokenSource.Token
        };
    }

    #endregion

    #region IReverseProxyFactory Implementation

    /// <summary>
    /// 获取当前代理配置
    /// </summary>
    public YarpProxyConfig GetConfig()
    {
        return new YarpProxyConfig
        {
            Routes = _routes.Values.ToList(),
            Clusters = _clusters.Values.ToList(),
            ChangeToken = _changeTokenSource.Token
        };
    }

    /// <summary>
    /// 重新加载配置
    /// </summary>
    public async Task ReloadConfigAsync()
    {
        bool shouldTriggerChange = false;
        int routeCount = 0, clusterCount = 0;
        
        await _configLock.WaitAsync();
        try
        {
            _logger.LogInformation("重新加载YARP配置...");

            await LoadConfigFromDatabaseAsync();

            shouldTriggerChange = true;
            routeCount = _routes.Count;
            clusterCount = _clusters.Count;
        }
        finally
        {
            _configLock.Release();
        }
        
        // 在锁外触发配置变更通知
        if (shouldTriggerChange)
        {
            _changeTokenSource.Cancel();
            _changeTokenSource.Dispose();
            _changeTokenSource = new CancellationTokenSource();
            
            _logger.LogInformation("YARP配置已重新加载: {RouteCount} 路由, {ClusterCount} 集群",
                routeCount, clusterCount);
        }
    }

    /// <summary>
    /// 添加路由
    /// </summary>
    public async Task<bool> AddRouteAsync(ProxyRouteConfig route)
    {
        bool shouldTriggerChange = false;
        try
        {
            await _configLock.WaitAsync();
            try
            {
                var routeConfig = ConvertToRouteConfig(route);

                // 保存到数据库
                var collection = GetDbContext().GetCollection<ProxyRouteConfig>("proxy_routes");
                collection.Insert(route);

                // 添加到内存配置
                _routes[route.RouteId] = routeConfig;

                shouldTriggerChange = true;

                _logger.LogInformation("路由已添加: {RouteId} -> {Host}", route.RouteId, route.Host);
            }
            finally
            {
                _configLock.Release();
            }
            
            // 在锁外触发配置变更
            if (shouldTriggerChange)
            {
                TriggerConfigChange();
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加路由失败: {RouteId}", route.RouteId);
            return false;
        }
    }

    /// <summary>
    /// 更新路由
    /// </summary>
    public async Task<bool> UpdateRouteAsync(ProxyRouteConfig route)
    {
        bool shouldTriggerChange = false;
        try
        {
            await _configLock.WaitAsync();
            try
            {
                var routeConfig = ConvertToRouteConfig(route);

                // 更新到数据库
                var collection = GetDbContext().GetCollection<ProxyRouteConfig>("proxy_routes");
                collection.Update(route);

                // 更新内存配置
                _routes[route.RouteId] = routeConfig;

                shouldTriggerChange = true;

                _logger.LogInformation("路由已更新: {RouteId}", route.RouteId);
            }
            finally
            {
                _configLock.Release();
            }
            
            // 在锁外触发配置变更
            if (shouldTriggerChange)
            {
                TriggerConfigChange();
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新路由失败: {RouteId}", route.RouteId);
            return false;
        }
    }

    /// <summary>
    /// 删除路由
    /// </summary>
    public async Task<bool> RemoveRouteAsync(string routeId)
    {
        bool shouldTriggerChange = false;
        try
        {
            await _configLock.WaitAsync();
            try
            {
                // 从数据库删除
                var collection = GetDbContext().GetCollection<ProxyRouteConfig>("proxy_routes");
                collection.DeleteMany(r => r.RouteId == routeId);

                // 从内存配置删除
                _routes.TryRemove(routeId, out _);

                shouldTriggerChange = true;

                _logger.LogInformation("路由已删除: {RouteId}", routeId);
            }
            finally
            {
                _configLock.Release();
            }
            
            // 在锁外触发配置变更
            if (shouldTriggerChange)
            {
                TriggerConfigChange();
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除路由失败: {RouteId}", routeId);
            return false;
        }
    }

    /// <summary>
    /// 添加集群
    /// </summary>
    public async Task<bool> AddClusterAsync(ProxyClusterConfig cluster)
    {
        bool shouldTriggerChange = false;
        try
        {
            await _configLock.WaitAsync();
            try
            {
                var clusterConfig = ConvertToClusterConfig(cluster);

                // 保存到数据库
                var collection = GetDbContext().GetCollection<ProxyClusterConfig>("proxy_clusters");
                collection.Insert(cluster);

                // 添加到内存配置
                _clusters[cluster.ClusterId] = clusterConfig;

                shouldTriggerChange = true;

                _logger.LogInformation("集群已添加: {ClusterId}", cluster.ClusterId);
            }
            finally
            {
                _configLock.Release();
            }
            
            // 在锁外触发配置变更
            if (shouldTriggerChange)
            {
                TriggerConfigChange();
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加集群失败: {ClusterId}", cluster.ClusterId);
            return false;
        }
    }

    /// <summary>
    /// 更新集群
    /// </summary>
    public async Task<bool> UpdateClusterAsync(ProxyClusterConfig cluster)
    {
        bool shouldTriggerChange = false;
        try
        {
            await _configLock.WaitAsync();
            try
            {
                var clusterConfig = ConvertToClusterConfig(cluster);

                // 更新到数据库
                var collection = GetDbContext().GetCollection<ProxyClusterConfig>("proxy_clusters");
                collection.Update(cluster);

                // 更新内存配置
                _clusters[cluster.ClusterId] = clusterConfig;

                shouldTriggerChange = true;

                _logger.LogInformation("集群已更新: {ClusterId}", cluster.ClusterId);
            }
            finally
            {
                _configLock.Release();
            }
            
            // 在锁外触发配置变更
            if (shouldTriggerChange)
            {
                TriggerConfigChange();
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新集群失败: {ClusterId}", cluster.ClusterId);
            return false;
        }
    }

    /// <summary>
    /// 删除集群
    /// </summary>
    public async Task<bool> RemoveClusterAsync(string clusterId)
    {
        bool shouldTriggerChange = false;
        try
        {
            await _configLock.WaitAsync();
            try
            {
                // 从数据库删除
                var collection = GetDbContext().GetCollection<ProxyClusterConfig>("proxy_clusters");
                collection.DeleteMany(c => c.ClusterId == clusterId);

                // 从内存配置删除
                _clusters.TryRemove(clusterId, out _);

                shouldTriggerChange = true;

                _logger.LogInformation("集群已删除: {ClusterId}", clusterId);
            }
            finally
            {
                _configLock.Release();
            }
            
            // 在锁外触发配置变更
            if (shouldTriggerChange)
            {
                TriggerConfigChange();
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除集群失败: {ClusterId}", clusterId);
            return false;
        }
    }

    /// <summary>
    /// 添加域名映射
    /// </summary>
    public async Task<bool> AddDomainMappingAsync(DomainMapping mapping)
    {
        bool shouldTriggerChange = false;
        try
        {
            await _configLock.WaitAsync();
            try
            {
                // 保存到数据库
                var collection = GetDbContext().GetCollection<DomainMapping>("domain_mappings");
                collection.Insert(mapping);

                // 添加到内存配置
                _domainMappings[mapping.Id] = mapping;

                // 重新生成路由和集群配置
                await RebuildRoutesAndClustersFromMappingsAsync();

                // 标记需要在锁外触发变更
                shouldTriggerChange = true;

                _logger.LogInformation("域名映射已添加: {Domain} -> {Destination}", mapping.Domain, mapping.DestinationAddress);
            }
            finally
            {
                _configLock.Release();
            }
            
            // 在锁外触发配置变更，避免死锁
            if (shouldTriggerChange)
            {
                TriggerConfigChange();
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加域名映射失败: {Domain}", mapping.Domain);
            return false;
        }
    }

    /// <summary>
    /// 更新域名映射
    /// </summary>
    public async Task<bool> UpdateDomainMappingAsync(DomainMapping mapping)
    {
        bool shouldTriggerChange = false;
        try
        {
            await _configLock.WaitAsync();
            try
            {
                // 更新到数据库
                var collection = GetDbContext().GetCollection<DomainMapping>("domain_mappings");
                mapping.UpdatedAt = DateTime.UtcNow;
                collection.Update(mapping);

                // 更新内存配置
                _domainMappings[mapping.Id] = mapping;

                // 重新生成路由和集群配置
                await RebuildRoutesAndClustersFromMappingsAsync();

                shouldTriggerChange = true;

                _logger.LogInformation("域名映射已更新: {Domain}", mapping.Domain);
            }
            finally
            {
                _configLock.Release();
            }
            
            // 在锁外触发配置变更
            if (shouldTriggerChange)
            {
                TriggerConfigChange();
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新域名映射失败: {Domain}", mapping.Domain);
            return false;
        }
    }

    /// <summary>
    /// 删除域名映射
    /// </summary>
    public async Task<bool> RemoveDomainMappingAsync(string mappingId)
    {
        bool shouldTriggerChange = false;
        try
        {
            await _configLock.WaitAsync();
            try
            {
                // 从数据库删除
                var collection = GetDbContext().GetCollection<DomainMapping>("domain_mappings");
                collection.Delete(mappingId);

                // 从内存配置删除
                _domainMappings.TryRemove(mappingId, out _);

                // 重新生成路由和集群配置
                await RebuildRoutesAndClustersFromMappingsAsync();

                shouldTriggerChange = true;

                _logger.LogInformation("域名映射已删除: {MappingId}", mappingId);
            }
            finally
            {
                _configLock.Release();
            }
            
            // 在锁外触发配置变更
            if (shouldTriggerChange)
            {
                TriggerConfigChange();
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除域名映射失败: {MappingId}", mappingId);
            return false;
        }
    }

    /// <summary>
    /// 获取所有域名映射（包括禁用的）
    /// </summary>
    public Task<List<DomainMapping>> GetAllDomainMappingsAsync()
    {
        var mappings = _domainMappings.Values
            .OrderByDescending(m => m.Priority)
            .ThenBy(m => m.Domain)
            .ToList();

        return Task.FromResult(mappings);
    }

    /// <summary>
    /// 根据容器ID获取域名映射
    /// </summary>
    public Task<List<DomainMapping>> GetDomainMappingsByContainerIdAsync(string containerId)
    {
        var mappings = _domainMappings.Values
            .Where(m => m.ContainerId == containerId && m.Enabled)
            .OrderByDescending(m => m.Priority)
            .ToList();

        return Task.FromResult(mappings);
    }

    /// <summary>
    /// 从数据库构建YARP配置
    /// </summary>
    public async Task<YarpProxyConfig> BuildConfigFromDatabaseAsync()
    {
        await _configLock.WaitAsync();
        try
        {
            await LoadConfigFromDatabaseAsync();

            return new YarpProxyConfig
            {
                Routes = _routes.Values.ToList(),
                Clusters = _clusters.Values.ToList(),
                ChangeToken = _changeTokenSource.Token
            };
        }
        finally
        {
            _configLock.Release();
        }
    }

    /// <summary>
    /// 更新域名映射的证书绑定
    /// </summary>
    public async Task<bool> UpdateDomainMappingCertificateAsync(string mappingId, string? certificateId)
    {
        bool shouldTriggerChange = false;
        try
        {
            await _configLock.WaitAsync();
            try
            {
                // 检查映射是否存在
                if (!_domainMappings.TryGetValue(mappingId, out var existingMapping))
                {
                    _logger.LogWarning("域名映射不存在: {MappingId}", mappingId);
                    return false;
                }

                // 更新证书ID
                existingMapping.CertificateId = certificateId;
                existingMapping.EnableSsl = !string.IsNullOrEmpty(certificateId);
                existingMapping.UpdatedAt = DateTime.UtcNow;

                // 保存到数据库
                var collection = GetDbContext().GetCollection<DomainMapping>("domain_mappings");
                collection.Update(existingMapping);

                // 更新内存配置
                _domainMappings[mappingId] = existingMapping;

                // 重新生成路由和集群配置
                await RebuildRoutesAndClustersFromMappingsAsync();

                shouldTriggerChange = true;

                _logger.LogInformation("域名映射证书已更新: {MappingId} -> {CertificateId}",
                    mappingId, certificateId ?? "(无证书)");
            }
            finally
            {
                _configLock.Release();
            }
            
            // 在锁外触发配置变更
            if (shouldTriggerChange)
            {
                TriggerConfigChange();
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新域名映射证书失败: {MappingId}", mappingId);
            return false;
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// 从数据库加载配置
    /// </summary>
    private async Task LoadConfigFromDatabaseAsync()
    {
        try
        {
            _logger.LogInformation("从数据库加载YARP配置...");

            // 加载路由配置
            var routesCollection = GetDbContext().GetCollection<ProxyRouteConfig>("proxy_routes");
            var routes = routesCollection.FindAll().Where(r => r.Enabled).ToList();

            _routes.Clear();
            foreach (var route in routes)
            {
                _routes[route.RouteId] = ConvertToRouteConfig(route);
            }

            // 加载集群配置
            var clustersCollection = GetDbContext().GetCollection<ProxyClusterConfig>("proxy_clusters");
            var clusters = clustersCollection.FindAll().ToList();

            _clusters.Clear();
            foreach (var cluster in clusters)
            {
                _clusters[cluster.ClusterId] = ConvertToClusterConfig(cluster);
            }

            // 加载所有域名映射（包括禁用的，用于管理界面显示）
            var mappingsCollection = GetDbContext().GetCollection<DomainMapping>("domain_mappings");
            var mappings = mappingsCollection.FindAll().ToList();

            _domainMappings.Clear();
            foreach (var mapping in mappings)
            {
                _domainMappings[mapping.Id] = mapping;
            }

            // 如果没有手动配置的路由/集群，从启用的域名映射生成
            if (_routes.IsEmpty && _clusters.IsEmpty && _domainMappings.Values.Any(m => m.Enabled))
            {
                await RebuildRoutesAndClustersFromMappingsAsync();
            }

            // 添加默认系统路由（ACME挑战处理等）
            AddDefaultRoutes();

            _logger.LogInformation("YARP配置加载完成: {RouteCount} 路由, {ClusterCount} 集群, {MappingCount} 域名映射",
                _routes.Count, _clusters.Count, _domainMappings.Count);
        }
        catch (Exception ex)
        {
            _routes.Clear();
            _clusters.Clear();
            _domainMappings.Clear();
            AddDefaultRoutes();

            _logger.LogError(ex, "从数据库加载YARP配置失败，已跳过数据库代理配置并使用空代理配置继续启动");
        }
    }

    /// <summary>
    /// 从域名映射重新生成路由和集群配置
    /// 支持同域名多容器负载均衡
    /// </summary>
    private async Task RebuildRoutesAndClustersFromMappingsAsync()
    {
        _routes.Clear();
        _clusters.Clear();

        // 按域名分组，支持同域名多容器负载均衡
        var domainGroups = _domainMappings.Values
            .Where(m => !string.IsNullOrWhiteSpace(m.DestinationAddress))
            .GroupBy(m => $"{m.Domain}|{m.PathPrefix ?? "/"}");

        foreach (var group in domainGroups)
        {
            var mappings = group.ToList();
            var firstMapping = mappings[0];
            var domain = firstMapping.Domain;
            var pathPrefix = firstMapping.PathPrefix ?? "/";

            // 确保协议有效，默认为 http
            var protocol = string.IsNullOrWhiteSpace(firstMapping.Protocol) ? "http" : firstMapping.Protocol.ToLowerInvariant();
            if (protocol != "http" && protocol != "https")
            {
                protocol = "http";
            }

            // 生成集群 ID：基于域名而非容器 ID，这样同域名可以共享集群
            var clusterId = $"cluster-{domain.Replace(".", "-").Replace(":", "-")}";

            // 收集所有目标地址（同域名的多个容器）
            var destinations = new List<ProxyDestinationConfig>();
            for (int i = 0; i < mappings.Count; i++)
            {
                var mapping = mappings[i];
                destinations.Add(new ProxyDestinationConfig
                {
                    DestinationId = $"dest-{mapping.ContainerId}",
                    Address = $"{protocol}://{mapping.DestinationAddress}"
                });
            }

            // 创建集群配置
            var cluster = new ProxyClusterConfig
            {
                ClusterId = clusterId,
                LoadBalancingPolicy = "RoundRobin",
                HealthCheck = new ProxyHealthCheckConfig
                {
                    ActiveEnabled = false, // 禁用主动健康检查，避免对容器造成压力
                    ActivePath = "/health",
                    ActiveIntervalSeconds = 30,
                    ActiveTimeoutSeconds = 10,
                    ConsecutiveFailureThreshold = 3
                },
                Destinations = destinations
            };
            _clusters[clusterId] = ConvertToClusterConfig(cluster);

            // 生成路由配置
            var routeId = $"route-{domain.Replace(".", "-").Replace(":", "-")}";
            
            // 确保 PathPattern 使用 catch-all 模式以匹配所有子路径
            var pathPattern = pathPrefix;
            if (pathPattern == "/") 
            {
                pathPattern = "/{**catch-all}";
            }
            else if (!pathPattern.EndsWith("{**catch-all}"))
            {
                pathPattern = pathPattern.EndsWith("/") 
                    ? $"{pathPattern}{{**catch-all}}" 
                    : $"{pathPattern}/{{**catch-all}}";
            }

            var route = new ProxyRouteConfig
            {
                RouteId = routeId,
                Host = domain,
                PathPattern = pathPattern,
                ClusterId = clusterId,
                Enabled = true,
                Priority = firstMapping.Priority
            };
            _routes[routeId] = ConvertToRouteConfig(route);

            _logger.LogInformation("域名 {Domain} 映射到 {Count} 个容器: {Destinations}",
                domain, mappings.Count, string.Join(", ", destinations.Select(d => d.Address)));
        }

        _logger.LogInformation("从域名映射生成配置: {RouteCount} 路由, {ClusterCount} 集群",
            _routes.Count, _clusters.Count);
    }

    /// <summary>
    /// 添加默认系统路由（ACME挑战、API路由等）
    /// </summary>
    private void AddDefaultRoutes()
    {
        // 注意：ACME挑战已通过 MinimalAPI 端点在 Program.cs 中直接处理
        // 这里不需要添加 ACME 路由，因为 MapGet 端点会优先匹配

        // 如果需要，可以添加其他默认路由
        // 例如：健康检查路由、API网关路由等

        _logger.LogDebug("已检查默认系统路由配置");
    }

    /// <summary>
    /// 触发配置变更通知
    /// 只取消当前 token，新 token 由 GetConfig 创建
    /// </summary>
    private void TriggerConfigChange()
    {
        try
        {
            if (!_changeTokenSource.IsCancellationRequested)
            {
                _logger.LogDebug("触发 YARP 配置变更通知");
                _changeTokenSource.Cancel();
            }
        }
        catch (ObjectDisposedException)
        {
            // Token 已被释放，忽略
        }
    }

    /// <summary>
    /// 转换为 YARP 路由配置
    /// </summary>
    private RouteConfig ConvertToRouteConfig(ProxyRouteConfig config)
    {
        return new RouteConfig
        {
            RouteId = config.RouteId,
            ClusterId = config.ClusterId,
            Match = new RouteMatch
            {
                Hosts = new[] { config.Host }.Where(h => !string.IsNullOrEmpty(h)).ToArray(),
                Path = config.PathPattern
            }
        };
    }

    /// <summary>
    /// 转换为 YARP 集群配置
    /// </summary>
    private ClusterConfig ConvertToClusterConfig(ProxyClusterConfig config)
    {
        var destinations = config.Destinations.ToDictionary(
            d => d.DestinationId,
            d => new DestinationConfig
            {
                Address = d.Address,
                Health = d.IsHealthy ? "Healthy" : "Unhealthy"
            },
            StringComparer.OrdinalIgnoreCase
        );

        Yarp.ReverseProxy.Configuration.HealthCheckConfig? healthCheck = null;
        if (config.HealthCheck != null)
        {
            healthCheck = new Yarp.ReverseProxy.Configuration.HealthCheckConfig
            {
                Active = new Yarp.ReverseProxy.Configuration.ActiveHealthCheckConfig
                {
                    Enabled = config.HealthCheck.ActiveEnabled,
                    Path = config.HealthCheck.ActivePath ?? "/health",
                    Interval = TimeSpan.FromSeconds(config.HealthCheck.ActiveIntervalSeconds),
                    Timeout = TimeSpan.FromSeconds(config.HealthCheck.ActiveTimeoutSeconds)
                },
                Passive = new Yarp.ReverseProxy.Configuration.PassiveHealthCheckConfig
                {
                    Enabled = false
                }
            };
        }

        Yarp.ReverseProxy.Configuration.SessionAffinityConfig? sessionAffinity = null;
        if (config.SessionAffinity?.Enabled == true)
        {
            sessionAffinity = new Yarp.ReverseProxy.Configuration.SessionAffinityConfig
            {
                Enabled = true,
                Policy = config.SessionAffinity.Policy,
                FailurePolicy = "Redirect"
            };
        }

        return new ClusterConfig
        {
            ClusterId = config.ClusterId,
            Destinations = destinations,
            HealthCheck = healthCheck,
            SessionAffinity = sessionAffinity
        };
    }

    #endregion

    #region IDisposable Implementation

    public void Dispose()
    {
        if (_disposed) return;

        _configLock.Dispose();
        _changeTokenSource.Dispose();

        _routes.Clear();
        _clusters.Clear();
        _domainMappings.Clear();

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #endregion
}
