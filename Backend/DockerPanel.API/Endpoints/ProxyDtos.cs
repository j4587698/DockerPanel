using System;

namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// 添加域名映射成功响应（替代 AddDomainMapping 匿名对象）。
    /// </summary>
    public sealed class ProxyMappingAddResponse
    {
        public string Message { get; set; } = string.Empty;

        public bool CertificateRequested { get; set; }
    }

    /// <summary>
    /// YARP 代理状态响应（替代 GetYarpStatus 匿名对象）。
    /// </summary>
    public sealed class YarpStatusResponse
    {
        public bool IsHealthy { get; set; }

        public int TotalRoutes { get; set; }

        public int TotalClusters { get; set; }

        public int TotalDomainMappings { get; set; }

        public int ActiveMappings { get; set; }

        public int SslEnabledMappings { get; set; }

        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// 更新证书绑定请求
    /// </summary>
    public sealed class UpdateCertificateRequest
    {
        /// <summary>
        /// 证书ID（为null时取消绑定）
        /// </summary>
        public string? CertificateId { get; set; }
    }

    /// <summary>
    /// 更新域名映射请求（支持部分更新）
    /// </summary>
    public sealed class UpdateDomainMappingRequest
    {
        public string? Domain { get; set; }

        public string? ContainerId { get; set; }

        public string? ContainerName { get; set; }

        public string? DestinationAddress { get; set; }

        public int? ContainerPort { get; set; }

        public string? PathPrefix { get; set; }

        public string? Protocol { get; set; }

        public bool? EnableSsl { get; set; }

        public string? CertificateId { get; set; }

        public string? AccountId { get; set; }

        public bool? AutoRequestCertificate { get; set; }

        public int? Priority { get; set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool? Enabled { get; set; }

        public int? ActivityTimeoutSeconds { get; set; }

        public int? RequestTimeoutSeconds { get; set; }

        public bool? ForceHttps { get; set; }

        public string? HttpVersion { get; set; }

        public bool? EnableWebSocketOptimization { get; set; }

        /// <summary>
        /// 标记是否从表单更新，用于处理高级设置的清除操作
        /// </summary>
        public bool? UpdateAdvancedSettings { get; set; }
    }
}
