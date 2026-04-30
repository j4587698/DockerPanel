using DockerPanel.API.Models;

namespace DockerPanel.API.Services;

/// <summary>
/// 容器服务接口
/// </summary>
public interface IContainerService
{
    /// <summary>
    /// 获取容器列表
    /// </summary>
    /// <param name="nodeId">节点ID</param>
    /// <param name="all">是否显示所有容器</param>
    /// <param name="limit">限制数量</param>
    /// <returns>容器列表</returns>
    Task<IEnumerable<ContainerInfo>> GetContainersAsync(string? nodeId = null, bool all = false, int limit = 100);

    /// <summary>
    /// 根据ID获取容器
    /// </summary>
    /// <param name="id">容器ID</param>
    /// <returns>容器信息</returns>
    Task<ContainerInfo?> GetContainerAsync(string id, string? nodeId = null);

    /// <summary>
    /// 创建容器
    /// </summary>
    /// <param name="request">创建请求</param>
    /// <param name="progress">进度汇报</param>
    /// <returns>创建的容器信息</returns>
    Task<ContainerInfo> CreateContainerAsync(CreateContainerRequest request, IProgress<ImagePullProgress>? progress = null);

    /// <summary>
    /// 启动容器
    /// </summary>
    /// <param name="id">容器ID</param>
    Task StartContainerAsync(string id, string? nodeId = null);

    /// <summary>
    /// 停止容器
    /// </summary>
    /// <param name="id">容器ID</param>
    /// <param name="timeout">超时时间</param>
    Task StopContainerAsync(string id, int timeout = 30, string? nodeId = null);

    /// <summary>
    /// 重启容器
    /// </summary>
    /// <param name="id">容器ID</param>
    /// <param name="timeout">超时时间</param>
    Task RestartContainerAsync(string id, int timeout = 30, string? nodeId = null);

    /// <summary>
    /// 删除容器
    /// </summary>
    /// <param name="id">容器ID</param>
    /// <param name="force">是否强制删除</param>
    /// <param name="removeVolumes">是否删除关联卷</param>
    Task RemoveContainerAsync(string id, bool force = false, bool removeVolumes = false, string? nodeId = null);

    /// <summary>
    /// 获取容器日志
    /// </summary>
    /// <param name="id">容器ID</param>
    /// <param name="since">起始时间</param>
    /// <param name="until">结束时间</param>
    /// <param name="tail">显示最后N行</param>
    /// <param name="follow">是否跟踪日志</param>
    /// <returns>容器日志</returns>
    Task<ContainerLogs> GetContainerLogsAsync(string id, DateTime? since = null, DateTime? until = null, int tail = 100, bool follow = false, string? nodeId = null);

    /// <summary>
    /// 获取容器统计信息
    /// </summary>
    /// <param name="id">容器ID</param>
    /// <returns>统计信息</returns>
    Task<ContainerStats> GetContainerStatsAsync(string id, string? nodeId = null);

    /// <summary>
    /// 执行容器命令
    /// </summary>
    /// <param name="id">容器ID</param>
    /// <param name="command">执行命令请求</param>
    /// <returns>执行结果</returns>
    Task<ExecResult> ExecuteCommandAsync(string id, ExecCommandRequest command, string? nodeId = null);

    /// <summary>
    /// 暂停容器
    /// </summary>
    Task PauseContainerAsync(string id);

    /// <summary>
    /// 恢复容器
    /// </summary>
    Task UnpauseContainerAsync(string id);

    /// <summary>
    /// 重命名容器
    /// </summary>
    Task RenameContainerAsync(string id, string newName);

    /// <summary>
    /// 获取容器进程列表
    /// </summary>
    Task<IEnumerable<ContainerProcess>> GetContainerProcessesAsync(string id);

    /// <summary>
    /// 获取容器文件系统变更
    /// </summary>
    Task<IEnumerable<FileSystemChange>> GetContainerChangesAsync(string id);

    /// <summary>
    /// 导出容器
    /// </summary>
    Task<byte[]> ExportContainerAsync(string id);

    /// <summary>
    /// 更新容器配置
    /// </summary>
    Task UpdateContainerAsync(string id, UpdateContainerRequest request);

    /// <summary>
    /// 获取容器资源使用情况
    /// </summary>
    Task<ContainerResourceUsage> GetContainerResourceUsageAsync(string id);

    /// <summary>
    /// 批量操作容器
    /// </summary>
    Task<ContainerBatchOperationResult> BatchOperationAsync(BatchContainerOperationRequest request);

    /// <summary>
    /// 获取容器健康检查状态
    /// </summary>
    /// <param name="id">容器ID</param>
    /// <returns>健康检查状态</returns>
    Task<HealthCheckStatus?> GetContainerHealthStatusAsync(string id);

    /// <summary>
    /// 获取容器健康检查日志
    /// </summary>
    /// <param name="id">容器ID</param>
    /// <param name="since">起始时间</param>
    /// <param name="until">结束时间</param>
    /// <param name="limit">限制数量</param>
    /// <returns>健康检查日志</returns>
    Task<IEnumerable<HealthCheckLog>> GetContainerHealthLogsAsync(string id, DateTime? since = null, DateTime? until = null, int limit = 100);

    /// <summary>
    /// 获取容器健康检查统计
    /// </summary>
    /// <param name="id">容器ID</param>
    /// <returns>健康检查统计</returns>
    Task<HealthCheckStats?> GetContainerHealthStatsAsync(string id);

    /// <summary>
    /// 更新容器健康检查配置
    /// </summary>
    /// <param name="id">容器ID</param>
    /// <param name="config">健康检查配置</param>
    Task UpdateContainerHealthCheckAsync(string id, HealthCheckConfig config);

    /// <summary>
    /// 移除容器健康检查配置
    /// </summary>
    /// <param name="id">容器ID</param>
    Task RemoveContainerHealthCheckAsync(string id);

    /// <summary>
    /// 更新容器资源限制
    /// </summary>
    /// <param name="id">容器ID</param>
    /// <param name="request">资源限制配置</param>
    Task UpdateContainerResourcesAsync(string id, UpdateContainerResourcesRequest request);

    /// <summary>
    /// 获取容器当前资源限制配置
    /// </summary>
    /// <param name="id">容器ID</param>
    /// <returns>资源限制配置</returns>
    Task<ResourceLimits?> GetContainerResourcesAsync(string id);

    /// <summary>
    /// 获取容器文件列表
    /// </summary>
    /// <param name="id">容器ID</param>
    /// <param name="path">目录路径</param>
    /// <returns>文件列表响应</returns>
    Task<ContainerFileListResponse> GetContainerFilesAsync(string id, string path, string? nodeId = null);

    /// <summary>
    /// 下载容器文件
    /// </summary>
    /// <param name="id">容器ID</param>
    /// <param name="path">文件路径</param>
    /// <returns>文件内容</returns>
    Task<byte[]> DownloadContainerFileAsync(string id, string path, string? nodeId = null);

    /// <summary>
    /// 上传文件到容器
    /// </summary>
    /// <param name="id">容器ID</param>
    /// <param name="path">目标目录路径</param>
    /// <param name="fileName">文件名</param>
    /// <param name="content">文件内容</param>
    Task UploadContainerFileAsync(string id, string path, string fileName, byte[] content, string? nodeId = null);

    /// <summary>
    /// 在容器中创建文件夹
    /// </summary>
    /// <param name="id">容器ID</param>
    /// <param name="path">目录路径</param>
    /// <param name="name">文件夹名称</param>
    Task CreateContainerFolderAsync(string id, string path, string name, string? nodeId = null);

    /// <summary>
    /// 重命名容器中的文件
    /// </summary>
    /// <param name="id">容器ID</param>
    /// <param name="path">文件所在目录</param>
    /// <param name="oldName">原文件名</param>
    /// <param name="newName">新文件名</param>
    Task RenameContainerFileAsync(string id, string path, string oldName, string newName, string? nodeId = null);

    /// <summary>
    /// 删除容器中的文件
    /// </summary>
    /// <param name="id">容器ID</param>
    /// <param name="path">文件路径</param>
    /// <param name="recursive">是否递归删除</param>
    Task DeleteContainerFileAsync(string id, string path, bool recursive = false, string? nodeId = null);

    /// <summary>
    /// 获取容器挂载点信息
    /// </summary>
    /// <param name="id">容器ID</param>
    /// <returns>挂载点列表</returns>
    Task<List<ContainerMountInfo>> GetContainerMountsAsync(string id, string? nodeId = null);

    /// <summary>
    /// 获取容器文件内容
    /// </summary>
    /// <param name="id">容器ID</param>
    /// <param name="path">文件路径</param>
    /// <returns>文件内容</returns>
    Task<string> GetContainerFileContentAsync(string id, string path, string? nodeId = null);

    /// <summary>
    /// 写入容器文件内容
    /// </summary>
    /// <param name="id">容器ID</param>
    /// <param name="path">文件路径</param>
    /// <param name="content">文件内容</param>
    Task WriteContainerFileContentAsync(string id, string path, string content, string? nodeId = null);

    /// <summary>
    /// 修改容器文件权限
        /// </summary>
        /// <param name="id">容器ID</param>
        /// <param name="path">文件路径</param>
        /// <param name="permissions">权限字符串（如 755, 644）</param>
        Task ChangeContainerFilePermissionsAsync(string id, string path, string permissions, string? nodeId = null);
    }
    
    /// <summary>
    /// 镜像服务接口
    /// </summary>
    public interface IImageService
    {
        /// <summary>
        /// 获取镜像列表
        /// </summary>
        Task<IEnumerable<ImageInfo>> GetImagesAsync(string? nodeId = null);
    
        /// <summary>
        /// 根据ID获取镜像
        /// </summary>
        Task<ImageInfo?> GetImageAsync(string id);
    
        /// <summary>
        /// 拉取镜像
        /// </summary>
        /// <param name="name">镜像名称</param>
        /// <param name="tag">标签</param>
        /// <param name="nodeId">节点ID</param>
        /// <param name="progress">进度回调</param>
        /// <param name="registryId">镜像加速器ID（可选）</param>
        Task PullImageAsync(string name, string? tag = null, string? nodeId = null, IProgress<ImagePullProgress>? progress = null, string? registryId = null);
    
        /// <summary>
        /// 删除镜像
        /// </summary>
        Task RemoveImageAsync(string id, bool force = false);
    
        /// <summary>
        /// 从 Dockerfile 内容构建镜像（Dockerfile 模式，无代码上下文）
        /// </summary>
        Task<string> BuildImageFromDockerfileAsync(string dockerfileContent, BuildImageParams parameters, IProgress<ImageBuildProgress>? progress = null);
    
        /// <summary>
        /// 从构建上下文构建镜像（压缩包模式）
        /// </summary>
        Task<string> BuildImageFromContextAsync(Stream contextStream, BuildImageParams parameters, IProgress<ImageBuildProgress>? progress = null);
    
        /// <summary>
        /// 推送镜像
        /// </summary>
        Task PushImageAsync(string name, string? tag = null, IProgress<ImagePushProgress>? progress = null);
    
        /// <summary>
        /// 给镜像打标签
        /// </summary>
        Task TagImageAsync(string sourceId, string targetName);
    
        /// <summary>
        /// 获取镜像历史
        /// </summary>
        Task<IEnumerable<ImageHistory>> GetImageHistoryAsync(string id);
    
        /// <summary>
        /// 搜索镜像
        /// </summary>
        Task<IEnumerable<ImageSearchResult>> SearchImagesAsync(string term);
    
        /// <summary>
        /// 保存镜像到文件
        /// </summary>
        Task<byte[]> SaveImageAsync(string id);
    
        /// <summary>
        /// 从 tar 文件加载镜像
        /// </summary>
        /// <param name="data">tar 文件流</param>
        /// <returns>导入的镜像名称列表</returns>
        Task<List<string>> LoadImageAsync(Stream data);
    
        
        /// <summary>
        /// 清理未使用的镜像
        /// </summary>
        Task<int> PruneImagesAsync();
    
        /// <summary>
        /// 获取镜像层级信息
        /// </summary>
        Task<ImageLayersInfo> GetImageLayersAsync(string imageId, string? nodeId = null);
    
        /// <summary>
        /// 批量删除镜像
        /// </summary>
        Task<ImageBatchOperationResult> BatchRemoveImagesAsync(BatchRemoveImagesRequest request);
    
        /// <summary>
        /// 获取镜像统计信息
        /// </summary>
        Task<ImageStatistics> GetImageStatisticsAsync(string? nodeId = null);
    
        /// <summary>
        /// 清理未使用的镜像（扩展版本）
        /// </summary>
        Task<PruneResult> PruneImagesAsync(PruneOptions options, string? nodeId = null);
    
        /// <summary>
        /// 获取镜像详细信息（Docker inspect）
        /// </summary>
        Task<ImageInspect?> InspectImageAsync(string imageId);
    }
    
    
    /// <summary>
    /// 存储卷服务接口
    /// </summary>
    public interface IVolumeService
    {
        /// <summary>
        /// 获取存储卷列表
        /// </summary>
        Task<IEnumerable<VolumeInfo>> GetVolumesAsync(string? nodeId = null);
    
        /// <summary>
        /// 根据名称获取存储卷
        /// </summary>
        Task<VolumeInfo?> GetVolumeAsync(string name);
    
        /// <summary>
        /// 根据ID获取存储卷详情
        /// </summary>
        Task<VolumeDetailInfo?> GetVolumeByIdAsync(string volumeId, string? nodeId = null);
    
        /// <summary>
        /// 创建存储卷
        /// </summary>
        Task<string> CreateVolumeAsync(CreateVolumeRequest request);
    
        /// <summary>
        /// 删除存储卷
        /// </summary>
        Task RemoveVolumeAsync(string name, bool force = false);
    
        /// <summary>
        /// 删除存储卷
        /// </summary>
        Task<bool> DeleteVolumeAsync(string volumeId, bool force = false, string? nodeId = null);
    
        /// <summary>
        /// 更新存储卷
        /// </summary>
        Task<VolumeInfo> UpdateVolumeAsync(string volumeId, UpdateVolumeRequest request, string? nodeId = null);
    
        /// <summary>
        /// 清理未使用的存储卷
        /// </summary>
        Task<int> PruneVolumesAsync();
    
        /// <summary>
        /// 清理未使用的存储卷
        /// </summary>
        Task<VolumePruneResult> PruneVolumesAsync(VolumePruneOptions options, string? nodeId = null);
    
        /// <summary>
        /// 获取存储卷统计信息
        /// </summary>
        Task<VolumeStatistics> GetVolumeStatisticsAsync(string? nodeId = null);
    
        /// <summary>
        /// 检查存储卷是否存在
        /// </summary>
        Task<bool> VolumeExistsAsync(string volumeId, string? nodeId = null);
    
        /// <summary>
        /// 获取存储卷使用情况
        /// </summary>
        Task<VolumeUsageInfo> GetVolumeUsageAsync(string volumeId, string? nodeId = null);
    
        /// <summary>
        /// 备份存储卷
        /// </summary>
        Task<byte[]> BackupVolumeAsync(string name);
    
        /// <summary>
        /// 备份存储卷
        /// </summary>
        Task<VolumeBackupResult> BackupVolumeAsync(string volumeId, VolumeBackupRequest request);
    
        /// <summary>
        /// 恢复存储卷
        /// </summary>
        Task RestoreVolumeAsync(string name, byte[] data);
    
        /// <summary>
        /// 恢复存储卷
        /// </summary>
        Task<VolumeRestoreResult> RestoreVolumeAsync(VolumeRestoreRequest request);
    
        /// <summary>
        /// 获取存储卷备份列表
        /// </summary>
        Task<IEnumerable<VolumeBackupInfo>> GetVolumeBackupsAsync(string volumeId);
    
        /// <summary>
        /// 删除存储卷备份
        /// </summary>
        Task<bool> DeleteVolumeBackupAsync(string volumeId, string backupId);
    
        #region 文件操作
    
        /// <summary>
        /// 获取卷文件列表
        /// </summary>
        Task<ContainerFileListResponse> GetVolumeFilesAsync(string volumeId, string path, string? nodeId = null);
    
        /// <summary>
        /// 下载卷文件
        /// </summary>
        Task<byte[]> DownloadVolumeFileAsync(string volumeId, string path, string? nodeId = null);
    
        /// <summary>
        /// 打包卷文件
        /// </summary>
        Task<(byte[] content, string fileName)> ArchiveVolumeFilesAsync(string volumeId, string path, string? nodeId = null);
    
        /// <summary>
        /// 上传文件到卷
        /// </summary>
        Task UploadVolumeFileAsync(string volumeId, string path, string fileName, Stream content, string? nodeId = null);
    
        /// <summary>
        /// 在卷中创建文件夹
        /// </summary>
        Task CreateVolumeFolderAsync(string volumeId, string path, string name, string? nodeId = null);
    
        /// <summary>
        /// 重命名卷文件
        /// </summary>
        Task RenameVolumeFileAsync(string volumeId, string path, string oldName, string newName, string? nodeId = null);
    
        /// <summary>
        /// 删除卷文件
        /// </summary>
        Task DeleteVolumeFileAsync(string volumeId, string path, bool recursive = false, string? nodeId = null);
    
        /// <summary>
        /// 获取卷文件内容
        /// </summary>
        Task<string> GetVolumeFileContentAsync(string volumeId, string path, string? nodeId = null);
    
        /// <summary>
        /// 保存卷文件内容
        /// </summary>
        Task SaveVolumeFileContentAsync(string volumeId, string path, string content, string? nodeId = null);
    
        /// <summary>
        /// 从 tar.gz 归档文件恢复创建新卷
        /// </summary>
        Task<VolumeInfo> RestoreVolumeFromArchiveAsync(string? volumeName, Stream archiveStream, string? nodeId = null);
    
        #endregion
    }
