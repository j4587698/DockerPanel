using System;

namespace DockerPanel.API.Services
{
    /// <summary>
    /// 卷不存在异常（用于文件操作前校验，避免 Docker 自动创建空卷）。
    /// </summary>
    public class VolumeNotFoundException : Exception
    {
        public VolumeNotFoundException(string volumeId)
            : base($"卷不存在: {volumeId}")
        {
            VolumeId = volumeId;
        }

        public string VolumeId { get; }
    }
}
