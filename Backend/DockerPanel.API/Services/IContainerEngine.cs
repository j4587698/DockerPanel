using DockerPanel.API.Models;

namespace DockerPanel.API.Services;

/// <summary>
/// 容器引擎接口
/// </summary>
public interface IContainerEngine
{
    /// <summary>
    /// 引擎名称
    /// </summary>
    string EngineName { get; }

    /// <summary>
    /// 检查引擎是否可用
    /// </summary>
    Task<bool> IsAvailableAsync(string? nodeId = null);

    // 容器操作
    Task<IEnumerable<ContainerInfo>> ListContainersAsync(bool all = false, string? nodeId = null);
    Task<ContainerInfo?> GetContainerAsync(string id, string? nodeId = null);
    Task<string> CreateContainerAsync(CreateContainerRequest request, string? nodeId = null);
    Task StartContainerAsync(string id, string? nodeId = null);
    Task StopContainerAsync(string id, int timeout = 30, string? nodeId = null);
    Task RestartContainerAsync(string id, int timeout = 30, string? nodeId = null);
    Task RemoveContainerAsync(string id, bool force = false, string? nodeId = null);
    Task<ContainerLogs> GetContainerLogsAsync(string id, DateTime? since = null, DateTime? until = null, int tail = 100, bool follow = false, string? nodeId = null);
    Task<ContainerStats> GetContainerStatsAsync(string id, string? nodeId = null);
    Task<ExecResult> ExecuteCommandAsync(string id, ExecCommandRequest command, string? nodeId = null);

    // 镜像操作
    Task<IEnumerable<ImageInfo>> ListImagesAsync(string? nodeId = null);
    Task<ImageInfo?> GetImageAsync(string id, string? nodeId = null);
    /// <summary>
    /// 拉取镜像
    /// </summary>
    /// <param name="name">镜像名称</param>
    /// <param name="tag">标签</param>
    /// <param name="progress">进度回调</param>
    /// <param name="registryId">镜像加速器ID（可选）</param>
    /// <param name="nodeId">节点ID（可选）</param>
    Task PullImageAsync(string name, string? tag = null, IProgress<ImagePullProgress>? progress = null, string? registryId = null, string? nodeId = null);
    Task RemoveImageAsync(string id, bool force = false, string? nodeId = null);
    Task<IEnumerable<ImageSearchResult>> SearchImagesAsync(string term, string? nodeId = null);
    Task<IEnumerable<ImageHistory>> GetImageHistoryAsync(string id, string? nodeId = null);
    
    /// <summary>
    /// 导出镜像为 tar 文件
    /// </summary>
    /// <param name="imageId">镜像ID或名称</param>
    /// <param name="nodeId">节点ID（可选）</param>
    /// <returns>tar 文件流</returns>
    Task<Stream> SaveImageAsync(string imageId, string? nodeId = null);
    
    /// <summary>
    /// 从 tar 文件导入镜像
    /// </summary>
    /// <param name="imageStream">tar 文件流</param>
    /// <param name="nodeId">节点ID（可选）</param>
    /// <returns>导入的镜像名称列表</returns>
    Task<List<string>> LoadImageAsync(Stream imageStream, string? nodeId = null);

    /// <summary>
    /// 从构建上下文构建镜像（压缩包模式）
    /// </summary>
    /// <param name="contextStream">构建上下文 tar 文件流</param>
    /// <param name="parameters">构建参数</param>
    /// <param name="progress">进度回调</param>
    /// <param name="nodeId">节点ID（可选）</param>
    /// <returns>构建的镜像ID</returns>
    Task<string> BuildImageFromContextAsync(Stream contextStream, BuildImageParams parameters, IProgress<ImageBuildProgress>? progress = null, string? nodeId = null);

    /// <summary>
    /// 从 Dockerfile 内容构建镜像（Dockerfile 模式，无代码上下文）
    /// </summary>
    /// <param name="dockerfileContent">Dockerfile 内容</param>
    /// <param name="parameters">构建参数</param>
    /// <param name="progress">进度回调</param>
    /// <param name="nodeId">节点ID（可选）</param>
    /// <returns>构建的镜像ID</returns>
    Task<string> BuildImageFromDockerfileAsync(string dockerfileContent, BuildImageParams parameters, IProgress<ImageBuildProgress>? progress = null, string? nodeId = null);

    /// <summary>
    /// 给镜像打标签
    /// </summary>
    /// <param name="sourceImage">源镜像ID或名称</param>
    /// <param name="targetName">目标镜像名称（包含标签）</param>
    /// <param name="nodeId">节点ID（可选）</param>
    Task TagImageAsync(string sourceImage, string targetName, string? nodeId = null);
    
    /// <summary>
    /// 推送镜像到仓库
    /// </summary>
    /// <param name="imageName">镜像名称</param>
    /// <param name="tag">标签</param>
    /// <param name="progress">进度回调</param>
    /// <param name="registryId">仓库ID（可选）</param>
    /// <param name="nodeId">节点ID（可选）</param>
    Task PushImageAsync(string imageName, string? tag = null, IProgress<ImagePushProgress>? progress = null, string? registryId = null, string? nodeId = null);

    // 资源管理
    Task UpdateContainerResourcesAsync(string id, UpdateContainerResourcesRequest request, string? nodeId = null);
    Task<ResourceLimits?> GetContainerResourcesAsync(string id, string? nodeId = null);

    // 健康检查
    Task<HealthCheckStatus?> GetContainerHealthStatusAsync(string id, string? nodeId = null);
    Task<IEnumerable<HealthCheckLog>> GetContainerHealthLogsAsync(string id, DateTime? since = null, DateTime? until = null, int limit = 100, string? nodeId = null);
    Task<HealthCheckStats?> GetContainerHealthStatsAsync(string id, string? nodeId = null);
    Task UpdateContainerHealthCheckAsync(string id, HealthCheckConfig config, string? nodeId = null);
    Task RemoveContainerHealthCheckAsync(string id, string? nodeId = null);

    // 系统操作
    Task<EngineVersionInfo> GetVersionAsync(string? nodeId = null);
    Task<EngineSystemInfo> GetSystemInfoAsync(string? nodeId = null);

    // 存储卷操作
    Task<IEnumerable<VolumeInfo>> ListVolumesAsync(string? nodeId = null);
    Task<VolumeInfo?> GetVolumeAsync(string name, string? nodeId = null);
    Task<string> CreateVolumeAsync(CreateVolumeRequest request, string? nodeId = null);
    Task RemoveVolumeAsync(string name, bool force = false, string? nodeId = null);

    // 网络操作
    Task<IEnumerable<NetworkInfo>> ListNetworksAsync(string? nodeId = null);
    Task<NetworkInfo?> GetNetworkAsync(string id, string? nodeId = null);
    Task<NetworkInfo> CreateNetworkAsync(CreateNetworkRequest request, string? nodeId = null);
    Task RemoveNetworkAsync(string id, string? nodeId = null);
    Task ConnectContainerToNetworkAsync(string networkId, string containerId, NetworkConnectionConfig? config = null, string? nodeId = null);
    Task DisconnectContainerFromNetworkAsync(string networkId, string containerId, string? nodeId = null);
    Task<int> PruneNetworksAsync(string? nodeId = null);
    Task<PruneResult> PruneImagesAsync(bool danglingOnly = true, string? nodeId = null);

    // 仓库认证
    /// <summary>
    /// 验证仓库凭据
    /// </summary>
    /// <param name="serverAddress">仓库地址</param>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    /// <param name="nodeId">节点ID（可选）</param>
    /// <returns>认证结果</returns>
    Task<RegistryAuthResult> AuthenticateRegistryAsync(string serverAddress, string username, string password, string? nodeId = null);

    // 文件管理
    Task<ContainerFileListResponse> GetContainerFilesAsync(string containerId, string path, string? nodeId = null);
    Task<List<ContainerMountInfo>> GetContainerMountsAsync(string containerId, string? nodeId = null);
    Task<byte[]> DownloadContainerFileAsync(string containerId, string path, string? nodeId = null);
    Task UploadContainerFileAsync(string containerId, string path, string fileName, byte[] content, string? nodeId = null);
    Task CreateContainerFolderAsync(string containerId, string path, string name, string? nodeId = null);
    Task RenameContainerFileAsync(string containerId, string path, string oldName, string newName, string? nodeId = null);
    Task DeleteContainerFileAsync(string containerId, string path, bool recursive, string? nodeId = null);
    Task<string> GetContainerFileContentAsync(string containerId, string path, string? nodeId = null);
    Task WriteContainerFileContentAsync(string containerId, string path, string content, string? nodeId = null);
    Task ChangeContainerFilePermissionsAsync(string containerId, string path, string permissions, string? nodeId = null);
    
    /// <summary>
    /// 获取容器文件变更列表（docker diff）
    /// </summary>
    /// <param name="containerId">容器ID</param>
    /// <param name="nodeId">节点ID（可选）</param>
    /// <returns>文件路径到变更状态的映射 (A=新增, C=修改, D=删除)</returns>
    Task<Dictionary<string, string>> GetContainerChangesAsync(string containerId, string? nodeId = null);
}

/// <summary>
/// Docker主机配置
/// </summary>
public class DockerHostConfiguration
{
    /// <summary>
    /// Docker主机地址
    /// </summary>
    public string Host { get; set; } = "unix:///var/run/docker.sock";

    /// <summary>
    /// 端口
    /// </summary>
    public int Port { get; set; } = 2375;

    /// <summary>
    /// 是否使用TLS
    /// </summary>
    public bool UseTls { get; set; } = false;

    /// <summary>
    /// TLS证书路径
    /// </summary>
    public string? TlsCertPath { get; set; }

    /// <summary>
    /// API版本
    /// </summary>
    public string ApiVersion { get; set; } = "1.41";

    /// <summary>
    /// 连接超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// 容器引擎配置
/// </summary>
public class ContainerEngineConfiguration
{
    /// <summary>
    /// 默认引擎类型
    /// </summary>
    public string DefaultEngine { get; set; } = "docker";

    /// <summary>
    /// Docker配置
    /// </summary>
    public DockerHostConfiguration Docker { get; set; } = new();

    /// <summary>
    /// Podman配置
    /// </summary>
    public PodmanConfiguration Podman { get; set; } = new();
}

/// <summary>
/// Podman配置
/// </summary>
public class PodmanConfiguration
{
    /// <summary>
    /// Podman主机地址
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// 端口
    /// </summary>
    public int Port { get; set; } = 80;

    /// <summary>
    /// API版本
    /// </summary>
    public string ApiVersion { get; set; } = "v4.0.0";

    /// <summary>
    /// 是否使用TLS
    /// </summary>
    public bool UseTls { get; set; } = false;

    /// <summary>
    /// 连接超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}