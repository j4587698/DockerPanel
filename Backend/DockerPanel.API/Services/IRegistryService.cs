using DockerPanel.API.Models;

namespace DockerPanel.API.Services;

/// <summary>
/// 镜像仓库管理服务接口
/// </summary>
public interface IRegistryService
{
    /// <summary>
    /// 获取所有镜像仓库
    /// </summary>
    Task<IEnumerable<ImageRegistry>> GetRegistriesAsync();

    /// <summary>
    /// 根据ID获取镜像仓库
    /// </summary>
    Task<ImageRegistry?> GetRegistryByIdAsync(string id);

    /// <summary>
    /// 创建镜像仓库
    /// </summary>
    Task<ImageRegistry> CreateRegistryAsync(CreateRegistryRequest request);

    /// <summary>
    /// 更新镜像仓库
    /// </summary>
    Task<ImageRegistry> UpdateRegistryAsync(string id, UpdateRegistryRequest request);

    /// <summary>
    /// 删除镜像仓库
    /// </summary>
    Task<bool> DeleteRegistryAsync(string id);

    /// <summary>
    /// 测试仓库连接
    /// </summary>
    Task<RegistryTestResult> TestRegistryConnectionAsync(string registryId);

    /// <summary>
    /// 测试仓库配置连接（无需保存）
    /// </summary>
    Task<RegistryTestResult> TestRegistryConfigAsync(TestRegistryConfigRequest request);

    /// <summary>
    /// 获取仓库中的镜像列表
    /// </summary>
    Task<IEnumerable<RegistryImage>> GetRegistryImagesAsync(string registryId, string? search = null);

    /// <summary>
    /// 设置默认仓库
    /// </summary>
    Task<bool> SetDefaultRegistryAsync(string registryId);

    /// <summary>
    /// 登录到私有仓库
    /// </summary>
    Task<bool> LoginToRegistryAsync(string registryId, string? username = null, string? password = null);

    /// <summary>
    /// 从私有仓库登出
    /// </summary>
    Task<bool> LogoutFromRegistryAsync(string registryId);

    /// <summary>
    /// 验证仓库认证信息
    /// </summary>
    Task<RegistryAuthResult> ValidateRegistryAuthAsync(string registryId);

    /// <summary>
    /// 同步仓库镜像信息
    /// </summary>
    Task<RegistrySyncResult> SyncRegistryImagesAsync(string registryId);

    /// <summary>
    /// 获取仓库统计数据
    /// </summary>
    Task<RegistryStatistics> GetRegistryStatisticsAsync(string? registryId = null);

    /// <summary>
    /// 搜索仓库镜像
    /// </summary>
    Task<RegistrySearchResponse> SearchRegistryImagesAsync(string registryId, string query, int limit = 20, int offset = 0);
}

/// <summary>
/// 仓库搜索响应
/// </summary>
public class RegistrySearchResponse
{
    public List<RegistrySearchResultItem> Results { get; set; } = new();
    public int Total { get; set; }
    public string Query { get; set; } = string.Empty;
    public string RegistryId { get; set; } = string.Empty;
    public string RegistryName { get; set; } = string.Empty;
}

/// <summary>
/// 搜索结果项
/// </summary>
public class RegistrySearchResultItem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public bool IsOfficial { get; set; }
    public bool IsAutomated { get; set; }
}

/// <summary>
/// 仓库连接测试结果
/// </summary>
public class RegistryTestResult
{
    public bool IsConnected { get; set; }
    public string Message { get; set; } = string.Empty;
    public string RegistryUrl { get; set; } = string.Empty;
    public DateTime TestTime { get; set; }
    public long ResponseTimeMs { get; set; }
    public Dictionary<string, object> Details { get; set; } = new();
}


/// <summary>
/// 仓库认证结果
/// </summary>
public class RegistryAuthResult
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public string AuthType { get; set; } = string.Empty;
    public DateTime TestTime { get; set; }
    public string? Token { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// 仓库同步结果
/// </summary>
public class RegistrySyncResult
{
    public string RegistryId { get; set; } = string.Empty;
    public string RegistryName { get; set; } = string.Empty;
    public int TotalImages { get; set; }
    public int SyncedImages { get; set; }
    public int NewImages { get; set; }
    public int UpdatedImages { get; set; }
    public int SkippedImages { get; set; }
    public List<SyncError> Errors { get; set; } = new();
    public DateTime SyncTime { get; set; }
    public TimeSpan SyncDuration { get; set; }
    public bool IsSuccess { get; set; }
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// 同步错误
/// </summary>
public class SyncError
{
    public string ImageName { get; set; } = string.Empty;
    public string ImageTag { get; set; } = string.Empty;
    public string ErrorType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime ErrorTime { get; set; }
    public string? StackTrace { get; set; }
}

/// <summary>
/// 仓库统计数据
/// </summary>
public class RegistryStatistics
{
    public string RegistryId { get; set; } = string.Empty;
    public string RegistryName { get; set; } = string.Empty;
    public int TotalImages { get; set; }
    public long TotalSize { get; set; }
    public int Repositories { get; set; }
    public int OfficialImages { get; set; }
    public int PrivateImages { get; set; }
    public DateTime LastSync { get; set; }
    public int SyncCount { get; set; }
    public bool IsHealthy { get; set; }
    public double SyncSuccessRate { get; set; }
    public List<string> TopRepositories { get; set; } = new();
    public Dictionary<string, int> ImageCountByTag { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}