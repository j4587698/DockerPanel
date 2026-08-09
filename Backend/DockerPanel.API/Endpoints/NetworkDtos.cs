using System;

namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// 网络未找到响应（替代 GetNetwork / DeleteNetwork 的 { error, networkId } 匿名对象）。
    /// </summary>
    public sealed class NetworkNotFoundResponse
    {
        public string Error { get; set; } = string.Empty;

        public string NetworkId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 网络名称相关错误响应（替代 CreateNetwork 的 { error, name } 匿名对象）。
    /// </summary>
    public sealed class NetworkNameErrorResponse
    {
        public string Error { get; set; } = string.Empty;

        public string? Name { get; set; }
    }

    /// <summary>
    /// 网络 ID 相关错误响应（替代 UpdateNetwork 的 { error, networkId } 匿名对象）。
    /// </summary>
    public sealed class NetworkIdErrorResponse
    {
        public string Error { get; set; } = string.Empty;

        public string NetworkId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 网络操作成功响应（替代 { message, networkId, containerId } 匿名对象）。
    /// </summary>
    public sealed class NetworkMessageResponse
    {
        public string Message { get; set; } = string.Empty;

        public string NetworkId { get; set; } = string.Empty;

        public string? ContainerId { get; set; }
    }

    /// <summary>
    /// 网络连接失败响应（替代 { error, networkId, containerId } 匿名对象）。
    /// </summary>
    public sealed class NetworkConnectErrorResponse
    {
        public string Error { get; set; } = string.Empty;

        public string NetworkId { get; set; } = string.Empty;

        public string ContainerId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 网络操作冲突响应（替代 { error, networkId[, containerId] } 匿名对象）。
    /// </summary>
    public sealed class NetworkConflictResponse
    {
        public string Error { get; set; } = string.Empty;

        public string NetworkId { get; set; } = string.Empty;

        public string? ContainerId { get; set; }
    }

    /// <summary>
    /// 清理网络请求
    /// </summary>
    public sealed class PruneNetworksRequest : DockerPanel.API.Models.INodeIdRequest
    {
        public bool Filters { get; set; } = false;

        public string? LabelFilter { get; set; }

        public string? NodeId { get; set; }
    }
}
