using System.ComponentModel.DataAnnotations;
using TinyDb.Attributes;
using Yarp.ReverseProxy.Configuration;

namespace DockerPanel.API.Models;

/// <summary>
/// 代理路由配置
/// </summary>
[Entity]
public class ProxyRouteConfig
{
    [Id]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 路由ID
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string RouteId { get; set; } = string.Empty;

    /// <summary>
    /// 域名
    /// </summary>
    [MaxLength(512)]
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// 路径匹配模式
    /// </summary>
    [MaxLength(512)]
    public string? PathPattern { get; set; }

    /// <summary>
    /// 关联的集群ID
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string ClusterId { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 优先级（数值越大优先级越高）
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// 响应头转换
    /// </summary>
    public List<ProxyHeaderTransformConfig>? ResponseHeaderTransforms { get; set; }

    /// <summary>
    /// 请求头转换
    /// </summary>
    public List<ProxyHeaderTransformConfig>? RequestHeaderTransforms { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 代理集群配置
/// </summary>
[Entity]
public class ProxyClusterConfig
{
    [Id]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 集群ID
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string ClusterId { get; set; } = string.Empty;

    /// <summary>
    /// 负载均衡策略
    /// </summary>
    [MaxLength(64)]
    public string LoadBalancingPolicy { get; set; } = "RoundRobin";

    /// <summary>
    /// 健康检查配置
    /// </summary>
    public ProxyHealthCheckConfig? HealthCheck { get; set; }

    /// <summary>
    /// 目的地列表
    /// </summary>
    public List<ProxyDestinationConfig> Destinations { get; set; } = new();

    /// <summary>
    /// 会话亲和性配置
    /// </summary>
    public ProxySessionAffinityConfig? SessionAffinity { get; set; }

    /// <summary>
    /// 超时配置
    /// </summary>
    public ProxyTimeoutConfig? Timeout { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 代理目的地配置
/// </summary>
public class ProxyDestinationConfig
{
    /// <summary>
    /// 目的地ID
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string DestinationId { get; set; } = string.Empty;

    /// <summary>
    /// 地址 (http://host:port)
    /// </summary>
    [Required]
    [MaxLength(512)]
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// 头信息
    /// </summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// 健康状态
    /// </summary>
    public bool IsHealthy { get; set; } = true;
}

/// <summary>
/// 代理健康检查配置
/// </summary>
public class ProxyHealthCheckConfig
{
    /// <summary>
    /// 是否启用主动健康检查
    /// </summary>
    public bool ActiveEnabled { get; set; } = false;

    /// <summary>
    /// 健康检查路径
    /// </summary>
    [MaxLength(512)]
    public string? ActivePath { get; set; }

    /// <summary>
    /// 健康检查间隔
    /// </summary>
    public int ActiveIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// 健康检查超时
    /// </summary>
    public int ActiveTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// 连续失败次数阈值
    /// </summary>
    public int ConsecutiveFailureThreshold { get; set; } = 3;

    /// <summary>
    /// 初始状态
    /// </summary>
    public string? InitialStatus { get; set; }
}

/// <summary>
/// 代理会话亲和性配置
/// </summary>
public class ProxySessionAffinityConfig
{
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// 亲和性策略
    /// </summary>
    [MaxLength(64)]
    public string Policy { get; set; } = "Cookie";

    /// <summary>
    /// Cookie 名称
    /// </summary>
    [MaxLength(128)]
    public string? CookieName { get; set; }

    /// <summary>
    /// Cookie 路径
    /// </summary>
    [MaxLength(256)]
    public string? CookiePath { get; set; }

    /// <summary>
    /// Cookie 是否 HttpOnly
    /// </summary>
    public bool CookieHttpOnly { get; set; } = true;

    /// <summary>
    /// Cookie 是否 SameSite
    /// </summary>
    [MaxLength(32)]
    public string CookieSameSite { get; set; } = "Lax";

    /// <summary>
    /// Cookie 过期时间（分钟）
    /// </summary>
    public int CookieExpirationMinutes { get; set; } = 60;
}

/// <summary>
/// 代理超时配置
/// </summary>
public class ProxyTimeoutConfig
{
    /// <summary>
    /// 请求超时（秒）
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 100;

    /// <summary>
    /// 活动连接超时（秒）
    /// </summary>
    public int ActiveConnectionTimeoutSeconds { get; set; } = 100;
}

/// <summary>
/// 代理头信息转换配置
/// </summary>
public class ProxyHeaderTransformConfig
{
    /// <summary>
    /// 头名称
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string HeaderName { get; set; } = string.Empty;

    /// <summary>
    /// 操作类型
    /// </summary>
    [MaxLength(32)]
    public string Action { get; set; } = "Set"; // Set, Remove, Append

    /// <summary>
    /// 值（用于 Set/Append）
    /// </summary>
    [MaxLength(512)]
    public string? Value { get; set; }
}

/// <summary>
/// 域名映射配置（扩展自 DomainMappingConfig）
/// </summary>
[Entity]
public class DomainMapping
{
    [Id]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 关联的容器ID
    /// </summary>
    [Required]
    [MaxLength(256)]
    [Index]
    public string ContainerId { get; set; } = string.Empty;

    /// <summary>
    /// 关联的容器名称
    /// </summary>
    [MaxLength(256)]
    public string ContainerName { get; set; } = string.Empty;

    /// <summary>
    /// 域名
    /// </summary>
    [Required]
    [MaxLength(512)]
    [Index]
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// 目标地址 (host:port 或 ip:port)
    /// </summary>
    [Required]
    [MaxLength(512)]
    public string DestinationAddress { get; set; } = string.Empty;

    /// <summary>
    /// 容器端口
    /// </summary>
    public int ContainerPort { get; set; } = 80;

    /// <summary>
    /// 路径前缀
    /// </summary>
    [MaxLength(512)]
    public string? PathPrefix { get; set; }

    /// <summary>
    /// 协议 (http/https)
    /// </summary>
    [MaxLength(8)]
    public string Protocol { get; set; } = "http";

    /// <summary>
    /// 是否启用SSL
    /// </summary>
    public bool EnableSsl { get; set; } = false;

    /// <summary>
    /// SSL证书ID
    /// </summary>
    [MaxLength(256)]
    public string? CertificateId { get; set; }

    /// <summary>
    /// ACME账户ID（用于自动申请证书）
    /// </summary>
    [MaxLength(256)]
    public string? AccountId { get; set; }

    /// <summary>
    /// 是否自动申请证书
    /// </summary>
    public bool AutoRequestCertificate { get; set; } = false;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 优先级
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// 标签
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
