using System;
using System.Collections.Generic;

namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// 导入Compose文件请求
    /// </summary>
    public sealed class ImportComposeFileRequest : DockerPanel.API.Models.INodeIdRequest
    {
        /// <summary>
        /// 文件内容
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 文件名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 描述
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 节点ID
        /// </summary>
        public string? NodeId { get; set; }
    }

    /// <summary>
    /// 根据模板创建请求
    /// </summary>
    public sealed class CreateFromTemplateRequest
    {
        /// <summary>
        /// 模板ID
        /// </summary>
        public string TemplateId { get; set; } = string.Empty;

        /// <summary>
        /// 变量值
        /// </summary>
        public Dictionary<string, object> Variables { get; set; } = new();

        /// <summary>
        /// 文件名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 描述
        /// </summary>
        public string? Description { get; set; }
    }

    /// <summary>
    /// 批量操作请求
    /// </summary>
    public sealed class BatchComposeOperationRequest
    {
        /// <summary>
        /// 文件ID列表
        /// </summary>
        public List<string> FileIds { get; set; } = new();

        /// <summary>
        /// 操作类型
        /// </summary>
        public string Operation { get; set; } = string.Empty;

        /// <summary>
        /// 操作参数
        /// </summary>
        public Dictionary<string, object>? Parameters { get; set; }
    }

    /// <summary>
    /// 恢复文件版本请求
    /// </summary>
    public sealed class RestoreFileVersionRequest
    {
        /// <summary>
        /// 版本ID
        /// </summary>
        public string VersionId { get; set; } = string.Empty;
    }
}
