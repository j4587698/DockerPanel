using System;
using System.Collections.Generic;

namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// 文件内容响应（替代 GetContainerFileContent / GetVolumeFileContent 匿名对象）。
    /// </summary>
    public sealed class FileContentResponse
    {
        public string Content { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;
    }

    /// <summary>
    /// 参数验证失败响应（替代 CreateContainer 的 { message, errors } 匿名对象）。
    /// </summary>
    public sealed class ValidationErrorResponse
    {
        public string Message { get; set; } = string.Empty;

        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// 容器重建响应（替代 RecreateContainer 匿名对象）。
    /// </summary>
    public sealed class ContainerRecreateResponse
    {
        public string Message { get; set; } = string.Empty;

        public string OldId { get; set; } = string.Empty;

        public string NewId { get; set; } = string.Empty;

        public string? Name { get; set; }
    }

    /// <summary>
    /// 删除运行中容器的冲突响应（替代 RemoveContainer 的 { message, error, needForce } 匿名对象）。
    /// </summary>
    public sealed class ContainerDeleteConflictResponse
    {
        public string Message { get; set; } = string.Empty;

        public string? Error { get; set; }

        public bool NeedForce { get; set; }
    }

    /// <summary>
    /// 写入文件内容请求
    /// </summary>
    public sealed class WriteFileContentRequest
    {
        /// <summary>
        /// 文件路径
        /// </summary>
        public string Path { get; set; } = "";

        /// <summary>
        /// 文件内容
        /// </summary>
        public string Content { get; set; } = "";
    }

    /// <summary>
    /// 修改权限请求
    /// </summary>
    public sealed class ChangePermissionsRequest
    {
        /// <summary>
        /// 文件路径
        /// </summary>
        public string Path { get; set; } = "";

        /// <summary>
        /// 权限（如 755, 644）
        /// </summary>
        public string Permissions { get; set; } = "";
    }
}
