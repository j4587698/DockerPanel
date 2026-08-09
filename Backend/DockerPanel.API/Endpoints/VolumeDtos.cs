using System;

namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// 卷未找到响应（替代 GetVolume / DeleteVolume 的 { error, volumeId } 匿名对象）。
    /// </summary>
    public sealed class VolumeNotFoundResponse
    {
        public string Error { get; set; } = string.Empty;

        public string VolumeId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 卷名称相关错误响应（替代 CreateVolume 的 { error, name } 匿名对象）。
    /// </summary>
    public sealed class VolumeNameErrorResponse
    {
        public string Error { get; set; } = string.Empty;

        public string? Name { get; set; }
    }

    /// <summary>
    /// 卷 ID 相关错误响应（替代 DeleteVolume / UpdateVolume 的 { error, volumeId } 匿名对象）。
    /// </summary>
    public sealed class VolumeIdErrorResponse
    {
        public string Error { get; set; } = string.Empty;

        public string VolumeId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 删除卷成功响应（替代 { message, volumeId, force } 匿名对象）。
    /// </summary>
    public sealed class VolumeDeleteResponse
    {
        public string Message { get; set; } = string.Empty;

        public string VolumeId { get; set; } = string.Empty;

        public bool Force { get; set; }
    }

    /// <summary>
    /// 上传卷文件成功响应（替代 { message, fileName } 匿名对象）。
    /// </summary>
    public sealed class VolumeUploadResponse
    {
        public string Message { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;
    }

    /// <summary>
    /// 删除卷备份成功响应（替代 { message, volumeId, backupId } 匿名对象）。
    /// </summary>
    public sealed class VolumeBackupDeleteResponse
    {
        public string Message { get; set; } = string.Empty;

        public string VolumeId { get; set; } = string.Empty;

        public string BackupId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 卷备份未找到响应（替代 { error, volumeId, backupId } 匿名对象）。
    /// </summary>
    public sealed class VolumeBackupNotFoundResponse
    {
        public string Error { get; set; } = string.Empty;

        public string VolumeId { get; set; } = string.Empty;

        public string BackupId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 清理卷请求
    /// </summary>
    public sealed class PruneVolumesRequest : DockerPanel.API.Models.INodeIdRequest
    {
        public bool Filters { get; set; } = false;

        public string? LabelFilter { get; set; }

        public bool All { get; set; } = false;

        public string? NodeId { get; set; }
    }

    /// <summary>
    /// 保存文件内容请求
    /// </summary>
    public sealed class SaveFileContentRequest
    {
        public string Path { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;
    }
}
