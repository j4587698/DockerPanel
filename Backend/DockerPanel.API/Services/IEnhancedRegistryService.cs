using DockerPanel.API.Models;

namespace DockerPanel.API.Services;

/// <summary>
/// 增强的镜像仓库管理服务接口
/// </summary>
public interface IEnhancedRegistryService : IRegistryService
{
    /// <summary>
    /// 登录到私有仓库（增强版本）
    /// </summary>
    /// <param name="request">登录请求</param>
    /// <returns>登录结果</returns>
    Task<RegistryAuthResult> LoginToRegistryAsync(RegistryLoginRequest request);

    /// <summary>
    /// 推送镜像到仓库
    /// </summary>
    /// <param name="request">推送请求</param>
    /// <returns>推送结果</returns>
    Task<RegistryOperationResult> PushImageAsync(PushImageRequest request);

    /// <summary>
    /// 从仓库拉取镜像
    /// </summary>
    /// <param name="request">拉取请求</param>
    /// <returns>拉取结果</returns>
    Task<RegistryOperationResult> PullImageAsync(PullImageRequest request);

    /// <summary>
    /// 搜索仓库中的镜像
    /// </summary>
    /// <param name="request">搜索请求</param>
    /// <returns>搜索结果</returns>
    Task<RegistrySearchResult> SearchRegistryImagesAsync(SearchRegistryRequest request);

    /// <summary>
    /// 获取镜像详细信息
    /// </summary>
    /// <param name="registryId">仓库ID</param>
    /// <param name="imageName">镜像名称</param>
    /// <param name="tag">标签（可选，默认latest）</param>
    /// <returns>镜像详情</returns>
    Task<RegistryImageDetail?> GetImageDetailAsync(string registryId, string imageName, string? tag = "latest");

    /// <summary>
    /// 获取镜像标签列表
    /// </summary>
    /// <param name="registryId">仓库ID</param>
    /// <param name="imageName">镜像名称</param>
    /// <returns>标签列表</returns>
    Task<IEnumerable<RegistryImageTag>> GetImageTagsAsync(string registryId, string imageName);

    /// <summary>
    /// 删除仓库中的镜像标签
    /// </summary>
    /// <param name="registryId">仓库ID</param>
    /// <param name="imageName">镜像名称</param>
    /// <param name="tag">标签</param>
    /// <returns>删除结果</returns>
    Task<RegistryOperationResult> DeleteImageTagAsync(string registryId, string imageName, string tag);

    /// <summary>
    /// 获取仓库健康状态
    /// </summary>
    /// <param name="registryId">仓库ID</param>
    /// <returns>健康检查结果</returns>
    Task<RegistryHealthCheckResult> GetRegistryHealthAsync(string registryId);

    /// <summary>
    /// 批量健康检查所有仓库
    /// </summary>
    /// <returns>所有仓库的健康检查结果</returns>
    Task<Dictionary<string, RegistryHealthCheckResult>> CheckAllRegistriesHealthAsync();

    /// <summary>
    /// 扫描镜像安全漏洞
    /// </summary>
    /// <param name="registryId">仓库ID</param>
    /// <param name="imageName">镜像名称</param>
    /// <param name="tag">标签</param>
    /// <returns>扫描结果</returns>
    Task<RegistryImageScanResult> ScanImageSecurityAsync(string registryId, string imageName, string tag);

    /// <summary>
    /// 获取仓库访问日志
    /// </summary>
    /// <param name="registryId">仓库ID</param>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="action">操作类型（可选）</param>
    /// <param name="limit">限制数量</param>
    /// <returns>访问日志</returns>
    Task<IEnumerable<RegistryAccessLog>> GetRegistryAccessLogsAsync(string registryId, DateTime? startTime = null, DateTime? endTime = null, string? action = null, int limit = 100);

    /// <summary>
    /// 管理仓库凭证
    /// </summary>
    /// <param name="registryId">仓库ID</param>
    /// <param name="credential">凭证信息</param>
    /// <returns>操作结果</returns>
    Task<RegistryCredential> SaveRegistryCredentialAsync(string registryId, RegistryCredential credential);

    /// <summary>
    /// 删除仓库凭证
    /// </summary>
    /// <param name="credentialId">凭证ID</param>
    /// <returns>删除是否成功</returns>
    Task<bool> DeleteRegistryCredentialAsync(string credentialId);

    /// <summary>
    /// 获取仓库凭证列表
    /// </summary>
    /// <param name="registryId">仓库ID</param>
    /// <returns>凭证列表</returns>
    Task<IEnumerable<RegistryCredential>> GetRegistryCredentialsAsync(string registryId);

    /// <summary>
    /// 刷新仓库认证令牌
    /// </summary>
    /// <param name="registryId">仓库ID</param>
    /// <returns>刷新结果</returns>
    Task<RegistryAuthResult> RefreshRegistryTokenAsync(string registryId);

    /// <summary>
    /// 同步仓库镜像元数据
    /// </summary>
    /// <param name="registryId">仓库ID</param>
    /// <param name="forceSync">是否强制同步</param>
    /// <returns>同步结果</returns>
    Task<RegistrySyncResult> SyncRegistryMetadataAsync(string registryId, bool forceSync = false);

    /// <summary>
    /// 获取仓库配置模板
    /// </summary>
    /// <param name="registryType">仓库类型</param>
    /// <returns>配置模板</returns>
    Task<RegistryConfigTemplate> GetRegistryConfigTemplateAsync(string registryType);

    /// <summary>
    /// 测试仓库连接配置
    /// </summary>
    /// <param name="config">仓库配置</param>
    /// <returns>测试结果</returns>
    Task<RegistryTestResult> TestRegistryConfigAsync(RegistryConfig config);

    /// <summary>
    /// 镜像仓库间复制
    /// </summary>
    /// <param name="sourceRegistryId">源仓库ID</param>
    /// <param name="targetRegistryId">目标仓库ID</param>
    /// <param name="imageName">镜像名称</param>
    /// <param name="tag">标签</param>
    /// <returns>复制结果</returns>
    Task<RegistryOperationResult> CopyImageBetweenRegistriesAsync(string sourceRegistryId, string targetRegistryId, string imageName, string tag);

    /// <summary>
    /// 批量镜像操作
    /// </summary>
    /// <param name="request">批量操作请求</param>
    /// <returns>批量操作结果</returns>
    Task<BatchRegistryOperationResult> BatchRegistryOperationAsync(BatchRegistryOperationRequest request);

    /// <summary>
    /// 获取仓库使用统计
    /// </summary>
    /// <param name="registryId">仓库ID</param>
    /// <param name="period">统计周期</param>
    /// <returns>使用统计</returns>
    Task<RegistryUsageStats> GetRegistryUsageStatsAsync(string registryId, string period = "7d");

    /// <summary>
    /// 清理仓库中的过期镜像
    /// </summary>
    /// <param name="registryId">仓库ID</param>
    /// <param name="policy">清理策略</param>
    /// <returns>清理结果</returns>
    Task<RegistryCleanupResult> CleanupRegistryAsync(string registryId, RegistryCleanupPolicy policy);

    /// <summary>
    /// 设置仓库镜像自动同步
    /// </summary>
    /// <param name="registryId">仓库ID</param>
    /// <param name="config">同步配置</param>
    /// <returns>设置结果</returns>
    Task<RegistrySyncConfig> SetRegistryAutoSyncAsync(string registryId, RegistrySyncConfig config);

    /// <summary>
    /// 获取仓库镜像自动同步配置
    /// </summary>
    /// <param name="registryId">仓库ID</param>
    /// <returns>同步配置</returns>
    Task<RegistrySyncConfig?> GetRegistryAutoSyncConfigAsync(string registryId);

    /// <summary>
    /// 获取仓库镜像安全扫描配置
    /// </summary>
    /// <param name="registryId">仓库ID</param>
    /// <returns>扫描配置</returns>
    Task<RegistryScanConfig?> GetRegistryScanConfigAsync(string registryId);

    /// <summary>
    /// 设置仓库镜像安全扫描配置
    /// </summary>
    /// <param name="registryId">仓库ID</param>
    /// <param name="config">扫描配置</param>
    /// <returns>设置结果</returns>
    Task<RegistryScanConfig> SetRegistryScanConfigAsync(string registryId, RegistryScanConfig config);
}

