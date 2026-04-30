namespace DockerPanel.API.Models;

/// <summary>
/// 证书配置
/// </summary>
public class CertificateSettings
{
    /// <summary>
    /// 判断证书即将过期的天数阈值（默认15天）
    /// </summary>
    public int ExpiringSoonDays { get; set; } = 15;
    
    /// <summary>
    /// 默认续期提前天数（默认15天）
    /// </summary>
    public int DefaultRenewalDaysBeforeExpiry { get; set; } = 15;
    
    /// <summary>
    /// 证书有效期天数（Let's Encrypt 默认90天）
    /// </summary>
    public int CertificateValidityDays { get; set; } = 90;
    
    /// <summary>
    /// 自动续期检查间隔（小时）
    /// </summary>
    public int RenewalCheckIntervalHours { get; set; } = 6;
}
