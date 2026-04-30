using DockerPanel.API.Models;

namespace DockerPanel.API.Services;

/// <summary>
/// 设置服务接口
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// 获取系统设置
    /// </summary>
    Task<SystemSettings> GetSettingsAsync();

    /// <summary>
    /// 更新系统设置
    /// </summary>
    Task<bool> UpdateSettingsAsync(SystemSettings settings);

    /// <summary>
    /// 重置设置为默认值
    /// </summary>
    Task<bool> ResetSettingsAsync();

    /// <summary>
    /// 验证设置
    /// </summary>
    Task<SettingsValidationResult> ValidateSettingsAsync(SystemSettings settings);
}