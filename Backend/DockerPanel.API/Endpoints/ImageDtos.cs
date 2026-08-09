using System;
using System.Collections.Generic;

namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// 镜像未找到响应（替代 GetImage / InspectImage / ExportImage 的 { error, imageId } 匿名对象）。
    /// </summary>
    public sealed class ImageNotFoundResponse
    {
        public string Error { get; set; } = string.Empty;

        public string ImageId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 拉取镜像启动响应（替代 PullImage 匿名对象）。
    /// </summary>
    public sealed class ImagePullStartedResponse
    {
        public string Message { get; set; } = string.Empty;

        public string PullId { get; set; } = string.Empty;

        public string ImageName { get; set; } = string.Empty;

        public string? Tag { get; set; }
    }

    /// <summary>
    /// 删除镜像成功响应（替代 { message, imageId, force } 匿名对象）。
    /// </summary>
    public sealed class ImageDeleteResponse
    {
        public string Message { get; set; } = string.Empty;

        public string ImageId { get; set; } = string.Empty;

        public bool Force { get; set; }
    }

    /// <summary>
    /// 标记镜像成功响应（替代 TagImage 匿名对象）。
    /// </summary>
    public sealed class ImageTagResponse
    {
        public string Message { get; set; } = string.Empty;

        public string SourceImageId { get; set; } = string.Empty;

        public string TargetRepository { get; set; } = string.Empty;

        public string? TargetTag { get; set; }
    }

    /// <summary>
    /// 推送镜像启动响应（替代 PushImage 匿名对象）。
    /// </summary>
    public sealed class ImagePushStartedResponse
    {
        public string PushId { get; set; } = string.Empty;

        public string ImageName { get; set; } = string.Empty;

        public string Tag { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// 构建镜像提交响应（替代 BuildImage 匿名对象）。
    /// </summary>
    public sealed class ImageBuildSubmittedResponse
    {
        public string Message { get; set; } = string.Empty;

        public string BuildId { get; set; } = string.Empty;

        public string Tag { get; set; } = string.Empty;

        public string Mode { get; set; } = string.Empty;
    }

    /// <summary>
    /// 导入镜像成功响应（替代 { message, images } 匿名对象）。
    /// </summary>
    public sealed class ImageImportResponse
    {
        public string Message { get; set; } = string.Empty;

        public List<string> Images { get; set; } = new();
    }

    /// <summary>
    /// 镜像历史条目
    /// </summary>
    public sealed class ImageHistoryEntry
    {
        public string Id { get; set; } = string.Empty;

        public DateTime Created { get; set; }

        public string CreatedBy { get; set; } = string.Empty;

        public string[] Tags { get; set; } = Array.Empty<string>();

        public long Size { get; set; }

        public string Comment { get; set; } = string.Empty;
    }

    /// <summary>
    /// 拉取镜像请求
    /// </summary>
    public sealed class PullImageRequest : DockerPanel.API.Models.INodeIdRequest
    {
        public string ImageName { get; set; } = string.Empty;

        public string? Tag { get; set; }

        public string? NodeId { get; set; }

        public string? ConnectionId { get; set; }

        /// <summary>
        /// 镜像加速器ID（可选），指定后使用加速器拉取镜像
        /// </summary>
        public string? Registry { get; set; }
    }

    /// <summary>
    /// 标记镜像请求
    /// </summary>
    public sealed class TagImageRequest : DockerPanel.API.Models.INodeIdRequest
    {
        public string TargetRepository { get; set; } = string.Empty;

        public string? TargetTag { get; set; }

        public string? NodeId { get; set; }
    }

    /// <summary>
    /// 推送镜像请求
    /// </summary>
    public sealed class PushImageRequest : DockerPanel.API.Models.INodeIdRequest
    {
        public string? Tag { get; set; }

        public string? NodeId { get; set; }
    }
}
