using DockerPanel.API.Models;
using Microsoft.Extensions.Primitives;
using System.Threading;
using Yarp.ReverseProxy.Configuration;

namespace DockerPanel.API.Services;

/// <summary>
/// 反向代理工厂接口
/// </summary>
public interface IReverseProxyFactory
{
    /// <summary>
    /// 获取当前代理配置
    /// </summary>
    YarpProxyConfig GetConfig();

    /// <summary>
    /// 重新加载配置
    /// </summary>
    Task ReloadConfigAsync();

    /// <summary>
    /// 添加路由
    /// </summary>
    Task<bool> AddRouteAsync(ProxyRouteConfig route);

    /// <summary>
    /// 更新路由
    /// </summary>
    Task<bool> UpdateRouteAsync(ProxyRouteConfig route);

    /// <summary>
    /// 删除路由
    /// </summary>
    Task<bool> RemoveRouteAsync(string routeId);

    /// <summary>
    /// 添加集群
    /// </summary>
    Task<bool> AddClusterAsync(ProxyClusterConfig cluster);

    /// <summary>
    /// 更新集群
    /// </summary>
    Task<bool> UpdateClusterAsync(ProxyClusterConfig cluster);

    /// <summary>
    /// 删除集群
    /// </summary>
    Task<bool> RemoveClusterAsync(string clusterId);

    /// <summary>
    /// 添加域名映射
    /// </summary>
    Task<bool> AddDomainMappingAsync(DomainMapping mapping);

    /// <summary>
    /// 更新域名映射
    /// </summary>
    Task<bool> UpdateDomainMappingAsync(DomainMapping mapping);

    /// <summary>
    /// 删除域名映射
    /// </summary>
    Task<bool> RemoveDomainMappingAsync(string mappingId);

    /// <summary>
    /// 获取所有域名映射
    /// </summary>
    Task<List<DomainMapping>> GetAllDomainMappingsAsync();

    /// <summary>
    /// 根据容器ID获取域名映射
    /// </summary>
    Task<List<DomainMapping>> GetDomainMappingsByContainerIdAsync(string containerId);

    /// <summary>
    /// 从数据库构建YARP配置
    /// </summary>
    Task<YarpProxyConfig> BuildConfigFromDatabaseAsync();

    /// <summary>
    /// 更新域名映射的证书绑定
    /// </summary>
    /// <param name="mappingId">域名映射ID</param>
    /// <param name="certificateId">证书ID（为null时取消绑定）</param>
    Task<bool> UpdateDomainMappingCertificateAsync(string mappingId, string? certificateId);
}

/// <summary>
/// YARP代理配置（内存中配置）
/// </summary>
public class YarpProxyConfig : IProxyConfig
{
    public List<RouteConfig> Routes { get; set; } = new();
    public List<ClusterConfig> Clusters { get; set; } = new();
    public CancellationToken ChangeToken { get; set; }

    IReadOnlyList<RouteConfig> IProxyConfig.Routes => Routes;
    IReadOnlyList<ClusterConfig> IProxyConfig.Clusters => Clusters;

    /// <summary>
    /// IProxyConfig.ChangeToken 属性
    /// </summary>
    IChangeToken IProxyConfig.ChangeToken
    {
        get
        {
            if (ChangeToken == default || !ChangeToken.CanBeCanceled)
            {
                return NullChangeToken.Instance;
            }
            return new CancellationChangeToken(ChangeToken);
        }
    }
}

/// <summary>
/// 空变更令牌（用于禁用变更通知）
/// </summary>
public class NullChangeToken : IChangeToken
{
    public static readonly NullChangeToken Instance = new();

    private NullChangeToken() { }

    public bool HasChanged => false;
    public bool ActiveChangeCallbacks => false;
    public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => EmptyDisposable.Instance;
}

internal class EmptyDisposable : IDisposable
{
    public static readonly EmptyDisposable Instance = new();
    public void Dispose() { }
}
