using System.Collections.Generic;
using System.Threading.Tasks;

namespace DockerPanel.API.Services.Acme.DnsProviders
{
    /// <summary>
    /// DNS 提供商接口
    /// </summary>
    public interface IDnsProvider
    {
        /// <summary>
        /// 提供商名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 显示名称
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// 是否需要凭据
        /// </summary>
        bool RequiresCredentials { get; }

        /// <summary>
        /// 创建 DNS TXT 记录
        /// </summary>
        /// <param name="domain">域名</param>
        /// <param name="recordName">记录名称</param>
        /// <param name="recordValue">记录值</param>
        /// <param name="credentials">认证凭据</param>
        /// <returns>操作结果</returns>
        Task<DnsOperationResult> CreateTxtRecordAsync(string domain, string recordName, string recordValue, Dictionary<string, object>? credentials);

        /// <summary>
        /// 删除 DNS TXT 记录
        /// </summary>
        /// <param name="domain">域名</param>
        /// <param name="recordName">记录名称</param>
        /// <param name="recordValue">记录值</param>
        /// <param name="credentials">认证凭据</param>
        /// <returns>操作结果</returns>
        Task<DnsOperationResult> DeleteTxtRecordAsync(string domain, string recordName, string recordValue, Dictionary<string, object>? credentials);

        /// <summary>
        /// 测试连接
        /// </summary>
        /// <param name="credentials">认证凭据</param>
        /// <returns>测试结果</returns>
        Task<DnsTestResult> TestConnectionAsync(Dictionary<string, object>? credentials);

        /// <summary>
        /// 删除所有匹配名称的 DNS TXT 记录
        /// </summary>
        /// <param name="domain">域名</param>
        /// <param name="recordName">记录名称</param>
        /// <param name="credentials">认证凭据</param>
        /// <returns>操作结果</returns>
        Task<DnsOperationResult> DeleteAllTxtRecordsByNameAsync(string domain, string recordName, Dictionary<string, object>? credentials);
    }

    /// <summary>
    /// DNS 操作结果
    /// </summary>
    public class DnsOperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public string? RecordId { get; set; }
        public string? Details { get; set; }
        public System.DateTime OperatedAt { get; set; } = System.DateTime.UtcNow;
    }

    /// <summary>
    /// DNS 连接测试结果
    /// </summary>
    public class DnsTestResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public string? Details { get; set; }
        public System.DateTime TestedAt { get; set; } = System.DateTime.UtcNow;
    }
}