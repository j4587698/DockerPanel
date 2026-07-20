using System.ComponentModel.DataAnnotations;
using TinyDb.Attributes;

namespace DockerPanel.API.Models;

/// <summary>
/// 镜像信息
/// </summary>
[Entity]
public class ImageInfo
{
    [Id]
    [IdGeneration(IdGenerationStrategy.ObjectId)]
    public string Id { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string Digest { get; set; } = string.Empty;
    public long Size { get; set; }
    [Index]
    public DateTime CreatedAt { get; set; }
    public DateTime Created { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string[] RepoTags { get; set; } = Array.Empty<string>();
    [Index]
    public string[] Tags { get; set; } = Array.Empty<string>();
    public Dictionary<string, string> Labels { get; set; } = new();
    public string NodeId { get; set; } = string.Empty;
    public string NodeName { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string Os { get; set; } = string.Empty;
    
    /// <summary>
    /// 使用此镜像的容器数量
    /// </summary>
    public int ContainersCount { get; set; }
    
    /// <summary>
    /// 是否正在被使用
    /// </summary>
    public bool IsUsed => ContainersCount > 0;
}

/// <summary>
/// 镜像详情信息
/// </summary>
public class ImageDetailInfo : ImageInfo
{
    public new string Architecture { get; set; } = string.Empty;
    public new string Os { get; set; } = string.Empty;
    public string Variant { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string Config { get; set; } = string.Empty;
    public string Parent { get; set; } = string.Empty;
    public string[] RootFS { get; set; } = Array.Empty<string>();
    public long VirtualSize { get; set; }
    public long SizeDelta { get; set; }
    
    // 镜像配置信息 - 用于创建容器时预填充
    /// <summary>
    /// 暴露的端口
    /// </summary>
    public List<string> ExposedPorts { get; set; } = new();
    
    /// <summary>
    /// 定义的卷
    /// </summary>
    public List<string> Volumes { get; set; } = new();
    
    /// <summary>
    /// 环境变量
    /// </summary>
    public List<string> Env { get; set; } = new();
    
    /// <summary>
    /// 工作目录
    /// </summary>
    public string? WorkingDir { get; set; }
    
    /// <summary>
    /// 入口点
    /// </summary>
    public List<string>? Entrypoint { get; set; }
    
    /// <summary>
    /// 默认命令
    /// </summary>
    public List<string>? Cmd { get; set; }
    
    /// <summary>
    /// 用户
    /// </summary>
    public string? User { get; set; }
}

/// <summary>
/// 镜像搜索结果
/// </summary>
public class ImageSearchResult
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Stars { get; set; }
    public bool IsOfficial { get; set; }
    public bool IsAutomated { get; set; }
}

/// <summary>
/// 镜像历史条目
/// </summary>
public class ImageHistory
{
    public string Id { get; set; } = string.Empty;
    public DateTime Created { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string[] Tags { get; set; } = Array.Empty<string>();
    public long Size { get; set; }
    public string Comment { get; set; } = string.Empty;
}

/// <summary>
/// 构建镜像请求
/// </summary>
public class BuildImageRequest
{
    [Required]
    public string DockerfileName { get; set; } = string.Empty;

    [Required]
    public string ContextPath { get; set; } = string.Empty;

    [Required]
    public string Repository { get; set; } = string.Empty;

    [Required]
    public string Tag { get; set; } = string.Empty;

    public Dictionary<string, string> BuildArgs { get; set; } = new();
    public Dictionary<string, string> Labels { get; set; } = new();
    public bool NoCache { get; set; } = false;
    public bool RemoveIntermediateContainers { get; set; } = true;
    public string NodeId { get; set; } = string.Empty;
}

/// <summary>
/// 镜像层级信息
/// </summary>
public class ImageLayersInfo
{
    public string ImageId { get; set; } = string.Empty;
    public List<ImageLayer> Layers { get; set; } = new();
    public long TotalSize { get; set; }
    public int LayerCount { get; set; }
}

/// <summary>
/// 镜像层级
/// </summary>
public class ImageLayer
{
    public string Id { get; set; } = string.Empty;
    public string Digest { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string[] Tags { get; set; } = Array.Empty<string>();
    public string Comment { get; set; } = string.Empty;
}

/// <summary>
/// 镜像批量操作结果
/// </summary>
public class ImageBatchOperationResult
{
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<ImageBatchOperationItem> Results { get; set; } = new();
    public bool Success { get; set; }
    public int SuccessfulCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Successful { get; set; } = new();
    public List<BatchOperationError> Failed { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// 容器批量操作结果
/// </summary>
public class ContainerBatchOperationResult
{
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<ContainerBatchOperationItem> Results { get; set; } = new();
    public bool Success { get; set; }
    public int SuccessfulCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Successful { get; set; } = new();
    public List<BatchOperationError> Failed { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// 批量操作错误
/// </summary>
public class BatchOperationError
{
    public string ContainerId { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 批量操作项
/// </summary>
public class ContainerBatchOperationItem
{
    public string ContainerId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// 镜像批量操作项
/// </summary>
public class ImageBatchOperationItem
{
    public string ImageId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// 操作错误
/// </summary>
public class OperationError
{
    public string Id { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string ErrorType { get; set; } = string.Empty;
}

/// <summary>
/// 镜像统计信息
/// </summary>
public class ImageStatistics
{
    public int TotalImages { get; set; }
    public long TotalSize { get; set; }
    public int ImagesWithTags { get; set; }
    public int DanglingImages { get; set; }
    public Dictionary<string, int> ImagesByRepository { get; set; } = new();
    public Dictionary<string, long> SizeByRepository { get; set; } = new();
    public DateTime LastUpdated { get; set; }
    public string NodeId { get; set; } = string.Empty;
}

/// <summary>
/// 清理选项
/// </summary>
public class PruneOptions
{
    public bool Dangling { get; set; }
    public bool All { get; set; }
    public string? Filter { get; set; }
    public bool KeepUntil { get; set; }
    public string? KeepUntilDuration { get; set; }
}

/// <summary>
/// 清理结果
/// </summary>
public class PruneResult
{
    public int ImagesDeleted { get; set; }
    public long SpaceReclaimed { get; set; }
    public List<string> DeletedImageIds { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// 镜像仓库
/// </summary>
[Entity]
public class ImageRegistry
{
    [Id]
    [IdGeneration(IdGenerationStrategy.ObjectId)]
    public string Id { get; set; } = string.Empty;
    
    [Index]
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// 仓库域名，如 registry-1.docker.io、registry.cn-hangzhou.aliyuncs.com
    /// </summary>
    [Index]
    public string Domain { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    [Index]
    public bool IsDefault { get; set; }
    /// <summary>
    /// 是否使用 HTTPS，默认 true
    /// </summary>
    public bool IsSecure { get; set; } = true;
    public bool IsPublic { get; set; }
    [Index]
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string UpdatedBy { get; set; } = string.Empty;
    public RegistryAuthConfig AuthConfig { get; set; } = new();
    /// <summary>
    /// 仓库类型：Private=私有仓库，Mirror=镜像加速器，DockerHub=Docker Hub
    /// </summary>
    [Index]
    public string Type { get; set; } = "DockerHub"; // DockerHub, Mirror, Private, 或厂商名(Harbor/Nexus/Aliyun/...)
    [Index]
    public string Status { get; set; } = "Active"; // Active, Inactive, Error
    public string Description { get; set; } = string.Empty;
    public RegistryConfig Configuration { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    /// <summary>
    /// 获取完整的仓库 URL（根据 IsSecure 自动添加协议前缀）
    /// </summary>
    public string Url => IsSecure ? $"https://{Domain}" : $"http://{Domain}";
}

/// <summary>
/// 仓库认证配置
/// </summary>
public class RegistryAuthConfig
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? ServerAddress { get; set; }
    public string? IdentityToken { get; set; }
    public string? RegistryToken { get; set; }
    public Dictionary<string, string> AuthParameters { get; set; } = new();
}

/// <summary>
/// 创建仓库请求
/// </summary>
public class CreateRegistryRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 仓库域名，如 registry-1.docker.io、registry.cn-hangzhou.aliyuncs.com
    /// 不要包含 http:// 或 https://
    /// </summary>
    [Required]
    public string Domain { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 是否使用 HTTPS，默认 true
    /// </summary>
    public bool IsSecure { get; set; } = true;
    public bool IsDefault { get; set; } = false;
    public bool IsPublic { get; set; } = false;
    
    /// <summary>
    /// 仓库类型：Mirror=镜像加速器，DockerHub=Docker Hub，Private=私有仓库，或厂商名(Harbor/Nexus/...)
    /// 为空时由域名推断
    /// </summary>
    public string? Type { get; set; }
}

/// <summary>
/// 更新仓库请求
/// </summary>
public class UpdateRegistryRequest
{
    public string? Name { get; set; }
    public string? Domain { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool? IsSecure { get; set; }
    public bool? IsDefault { get; set; }
    public string? Type { get; set; }
}

/// <summary>
/// 测试仓库配置请求（无需保存）
/// </summary>
public class TestRegistryConfigRequest
{
    public string Domain { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool IsSecure { get; set; } = true;
}


/// <summary>
/// 镜像构建选项
/// </summary>
public class ImageBuildOptions
{
    public string DockerfileName { get; set; } = "Dockerfile";
    public string ContextPath { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public string Tag { get; set; } = "latest";
    public Dictionary<string, string> BuildArgs { get; set; } = new();
    public Dictionary<string, string> Labels { get; set; } = new();
    public bool NoCache { get; set; } = false;
    public bool RemoveIntermediateContainers { get; set; } = true;
    public bool ForceRm { get; set; } = false;
    public bool Pull { get; set; } = false;
    public string? Platform { get; set; }
    public Dictionary<string, string> BuildLabels { get; set; } = new();
}

/// <summary>
/// 镜像清理选项
/// </summary>
public class ImagePruneOptions
{
    public bool Dangling { get; set; } = true;
    public bool All { get; set; } = false;
    public string? Filter { get; set; }
    public bool KeepUntil { get; set; } = false;
    public string? KeepUntilDuration { get; set; }
}

/// <summary>
/// 批量删除镜像请求
/// </summary>
public class BatchRemoveImagesRequest
{
    public List<string> ImageIds { get; set; } = new();
    public bool Force { get; set; } = false;
    public string? NodeId { get; set; }
}

/// <summary>
/// 清理镜像请求
/// </summary>
public class PruneImagesRequest
{
    public bool Dangling { get; set; } = true;
    public bool All { get; set; } = false;
    public string? Filter { get; set; }
    public bool KeepUntil { get; set; } = false;
    public string? KeepUntilDuration { get; set; }
    public string? NodeId { get; set; }
}

/// <summary>
/// 镜像检查信息
/// </summary>
public class ImageInspect
{
    public string Id { get; set; } = string.Empty;
    public List<string> RepoTags { get; set; } = new();
    public List<string> RepoDigests { get; set; } = new();
    public string Parent { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public DateTime Created { get; set; }
    public DockerContainerConfig Config { get; set; } = new();
    public string Architecture { get; set; } = string.Empty;
    public string Os { get; set; } = string.Empty;
    public long Size { get; set; }
    public long VirtualSize { get; set; }
    public long SharedSize { get; set; }
    public Dictionary<string, object> RootFS { get; set; } = new();
}

/// <summary>
/// Docker容器配置
/// </summary>
public class DockerContainerConfig
{
    public string Hostname { get; set; } = string.Empty;
    public string Domainname { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public List<string> Env { get; set; } = new();
    public List<string> Cmd { get; set; } = new();
    public List<string> Entrypoint { get; set; } = new();
    public List<string> ExposedPorts { get; set; } = new();
    public List<string> Volumes { get; set; } = new();
    public string WorkingDir { get; set; } = string.Empty;
    public Dictionary<string, object> Labels { get; set; } = new();
}

/// <summary>
/// 镜像清理结果
/// </summary>
public class ImagePruneResult
{
    public List<string> DeletedImages { get; set; } = new();
    public long SpaceReclaimed { get; set; }
}

/// <summary>
/// 镜像导入结果
/// </summary>
public class ImageImportResult
{
    public string ImageId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long Size { get; set; }
}

/// <summary>
/// 镜像清单
/// </summary>
public class ImageManifest
{
    public string Name { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string Digest { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public long Size { get; set; }
    public List<ImageManifestLayer> Layers { get; set; } = new();
}

/// <summary>
/// 镜像清单层
/// </summary>
public class ImageManifestLayer
{
    public string Digest { get; set; } = string.Empty;
    public long Size { get; set; }
    public string MediaType { get; set; } = string.Empty;
}

/// <summary>
/// 镜像配置
/// </summary>
public class ImageConfig
{
    public string Id { get; set; } = string.Empty;
    public string Parent { get; set; } = string.Empty;
    public List<string> RepoTags { get; set; } = new();
    public List<string> RepoDigests { get; set; } = new();
    public DateTime Created { get; set; }
    public DockerContainerConfig Config { get; set; } = new();
    public string Architecture { get; set; } = string.Empty;
    public string Os { get; set; } = string.Empty;
    public long Size { get; set; }
}

/// <summary>
/// 镜像层信息
/// </summary>
public class ImageLayerInfo
{
    public string ImageId { get; set; } = string.Empty;
    public List<ImageLayer> Layers { get; set; } = new();
}


/// <summary>
/// 镜像大小信息
/// </summary>
public class ImageSizeInfo
{
    public string ImageId { get; set; } = string.Empty;
    public long Size { get; set; }
    public long SharedSize { get; set; }
    public long VirtualSize { get; set; }
}

/// <summary>
/// 镜像差异
/// </summary>
public class ImageDiff
{
    public string ImageId { get; set; } = string.Empty;
    public List<string> Additions { get; set; } = new();
    public List<string> Deletions { get; set; } = new();
    public List<string> Modifications { get; set; } = new();
}

/// <summary>
/// 提交镜像请求
/// </summary>
public class CommitImageRequest
{
    public string ContainerId { get; set; } = string.Empty;
    public string? Repository { get; set; }
    public string? Tag { get; set; }
    public string? Message { get; set; }
    public string? Author { get; set; }
    public bool Pause { get; set; } = false;
    public Dictionary<string, string>? Labels { get; set; }
}

/// <summary>
/// 镜像提交结果
/// </summary>
public class ImageCommitResult
{
    public string ImageId { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? Digest { get; set; }
    public long Size { get; set; }
}

/// <summary>
/// 镜像复制结果
/// </summary>
public class ImageCopyResult
{
    public bool Success { get; set; }
    public string ImageId { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 签名镜像请求
/// </summary>
public class SignImageRequest
{
    public string KeyId { get; set; } = string.Empty;
    public string Passphrase { get; set; } = string.Empty;
    public Dictionary<string, string>? Annotations { get; set; }
}

/// <summary>
/// 镜像签名结果
/// </summary>
public class ImageSignResult
{
    public bool Success { get; set; }
    public string Signature { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 镜像验证结果
/// </summary>
public class ImageVerifyResult
{
    public bool Valid { get; set; }
    public string Signature { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime VerifiedAt { get; set; } = DateTime.UtcNow;
}

