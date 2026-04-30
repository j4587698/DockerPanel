using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace DockerPanel.API.Services.Acme.DnsProviders
{
    /// <summary>
    /// GoDaddy DNS API 提供商实现
    /// </summary>
    public class GoDaddyDnsProvider : IDnsProvider
    {
        private readonly ILogger<GoDaddyDnsProvider> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public GoDaddyDnsProvider(
            ILogger<GoDaddyDnsProvider> logger,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _httpClient = _httpClientFactory.CreateClient("GoDaddy");
            _httpClient.BaseAddress = new Uri("https://api.godaddy.com/v1/");
        }

        public string Name => "godaddy";
        public string DisplayName => "GoDaddy";
        public bool RequiresCredentials => true;

        /// <summary>
        /// 创建 DNS TXT 记录
        /// </summary>
        public async Task<DnsOperationResult> CreateTxtRecordAsync(string domain, string recordName, string recordValue, Dictionary<string, object>? credentials)
        {
            try
            {
                _logger.LogInformation("创建 GoDaddy DNS TXT 记录: {RecordName} = {RecordValue}", recordName, recordValue);

                var apiKey = GetApiKey(credentials);
                var apiSecret = GetApiSecret(credentials);

                if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
                {
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = "GoDaddy API Key 或 Secret 未提供",
                        ErrorCode = "MISSING_CREDENTIALS"
                    };
                }

                // 提取域名和记录名
                var rootDomain = ExtractRootDomain(domain);
                var relativeName = GetRelativeRecordName(recordName, rootDomain);

                // 设置认证头
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("sso-key", $"{apiKey}:{apiSecret}");

                // 检查记录是否已存在
                var existingRecords = await GetExistingTxtRecords(rootDomain, relativeName, apiKey, apiSecret);

                // 构建新的记录列表
                var records = existingRecords.ToList();
                records.Add(new GoDaddyDnsRecord
                {
                    Name = relativeName,
                    Type = "TXT",
                    Data = recordValue,
                    Ttl = 120
                });

                // 使用 PATCH 方法添加记录
                var json = JsonSerializer.Serialize(records);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PatchAsync($"domains/{rootDomain}/records", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("创建 GoDaddy DNS 记录失败: {StatusCode} - {Content}", response.StatusCode, responseContent);
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = $"创建 DNS 记录失败: {response.StatusCode}",
                        ErrorCode = "CREATE_FAILED",
                        Details = responseContent
                    };
                }

                _logger.LogInformation("GoDaddy DNS TXT 记录创建成功: {RecordName}", recordName);
                return new DnsOperationResult
                {
                    Success = true,
                    Message = "DNS 记录创建成功",
                    Details = $"Domain: {rootDomain}, Record: {relativeName}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建 GoDaddy DNS 记录异常: {RecordName}", recordName);
                return new DnsOperationResult
                {
                    Success = false,
                    Message = $"创建 DNS 记录异常: {ex.Message}",
                    ErrorCode = "EXCEPTION",
                    Details = ex.ToString()
                };
            }
        }

        /// <summary>
        /// 删除 DNS TXT 记录
        /// </summary>
        public async Task<DnsOperationResult> DeleteTxtRecordAsync(string domain, string recordName, string recordValue, Dictionary<string, object>? credentials)
        {
            try
            {
                _logger.LogInformation("删除 GoDaddy DNS TXT 记录: {RecordName}", recordName);

                var apiKey = GetApiKey(credentials);
                var apiSecret = GetApiSecret(credentials);

                if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
                {
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = "GoDaddy API Key 或 Secret 未提供",
                        ErrorCode = "MISSING_CREDENTIALS"
                    };
                }

                // 提取域名和记录名
                var rootDomain = ExtractRootDomain(domain);
                var relativeName = GetRelativeRecordName(recordName, rootDomain);

                // 设置认证头
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("sso-key", $"{apiKey}:{apiSecret}");

                // 获取现有记录
                var existingRecords = await GetExistingTxtRecords(rootDomain, relativeName, apiKey, apiSecret);
                var recordsToDelete = existingRecords.Where(r => r.Data == recordValue).ToList();

                if (recordsToDelete.Count == 0)
                {
                    _logger.LogWarning("未找到要删除的 GoDaddy DNS 记录: {RecordName}", recordName);
                    return new DnsOperationResult
                    {
                        Success = true,
                        Message = "记录不存在，无需删除"
                    };
                }

                // 过滤掉要删除的记录
                var remainingRecords = existingRecords.Where(r => r.Data != recordValue).ToList();

                // 使用 PUT 方法更新记录（替换所有同类型同名称的记录）
                // 或者使用 DELETE 方法删除特定记录
                var response = await _httpClient.DeleteAsync($"domains/{rootDomain}/records/TXT/{relativeName}");
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogError("删除 GoDaddy DNS 记录失败: {StatusCode} - {Content}", response.StatusCode, responseContent);
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = $"删除 DNS 记录失败: {response.StatusCode}",
                        ErrorCode = "DELETE_FAILED",
                        Details = responseContent
                    };
                }

                // 如果还有剩余记录，需要重新添加
                if (remainingRecords.Count > 0)
                {
                    var json = JsonSerializer.Serialize(remainingRecords);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    await _httpClient.PatchAsync($"domains/{rootDomain}/records", content);
                }

                _logger.LogInformation("GoDaddy DNS TXT 记录删除成功: {RecordName}", recordName);
                return new DnsOperationResult
                {
                    Success = true,
                    Message = "DNS 记录删除成功"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除 GoDaddy DNS 记录异常: {RecordName}", recordName);
                return new DnsOperationResult
                {
                    Success = false,
                    Message = $"删除 DNS 记录异常: {ex.Message}",
                    ErrorCode = "EXCEPTION",
                    Details = ex.ToString()
                };
            }
        }

        /// <summary>
        /// 测试连接
        /// </summary>
        public async Task<DnsTestResult> TestConnectionAsync(Dictionary<string, object>? credentials)
        {
            try
            {
                _logger.LogInformation("测试 GoDaddy DNS 连接");

                var apiKey = GetApiKey(credentials);
                var apiSecret = GetApiSecret(credentials);

                if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
                {
                    return new DnsTestResult
                    {
                        Success = false,
                        Message = "GoDaddy API Key 或 Secret 未提供",
                        ErrorCode = "MISSING_CREDENTIALS"
                    };
                }

                // 设置认证头
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("sso-key", $"{apiKey}:{apiSecret}");

                // 测试获取域名列表
                var response = await _httpClient.GetAsync("domains");
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("GoDaddy API 认证失败: {StatusCode} - {Content}", response.StatusCode, responseContent);
                    return new DnsTestResult
                    {
                        Success = false,
                        Message = $"API 认证失败: {response.StatusCode}",
                        ErrorCode = "AUTH_FAILED",
                        Details = responseContent
                    };
                }

                // 解析响应
                var domains = JsonSerializer.Deserialize<List<GoDaddyDomain>>(responseContent);
                var domainCount = domains?.Count ?? 0;

                return new DnsTestResult
                {
                    Success = true,
                    Message = "GoDaddy API 连接测试成功",
                    Details = $"找到 {domainCount} 个域名"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "测试 GoDaddy DNS 连接异常");
                return new DnsTestResult
                {
                    Success = false,
                    Message = $"连接测试异常: {ex.Message}",
                    ErrorCode = "EXCEPTION",
                    Details = ex.ToString()
                };
            }
        }

        /// <summary>
        /// 删除所有匹配名称的 DNS TXT 记录
        /// </summary>
        public async Task<DnsOperationResult> DeleteAllTxtRecordsByNameAsync(string domain, string recordName, Dictionary<string, object>? credentials)
        {
            try
            {
                _logger.LogInformation("GoDaddy: 删除所有匹配的 DNS TXT 记录: {RecordName}", recordName);

                var apiKey = GetApiKey(credentials);
                var apiSecret = GetApiSecret(credentials);

                if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
                {
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = "缺少 GoDaddy API 凭据",
                        ErrorCode = "MISSING_CREDENTIALS"
                    };
                }

                // 提取域名和记录名
                var rootDomain = ExtractRootDomain(domain);
                var relativeName = GetRelativeRecordName(recordName, rootDomain);

                // 设置认证头
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("sso-key", $"{apiKey}:{apiSecret}");

                // 删除所有匹配的记录
                var response = await _httpClient.DeleteAsync($"domains/{rootDomain}/records/TXT/{relativeName}");
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogError("GoDaddy 删除 DNS 记录失败: {StatusCode} - {Content}", response.StatusCode, responseContent);
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = $"删除 DNS 记录失败: {response.StatusCode}",
                        ErrorCode = "DELETE_FAILED",
                        Details = responseContent
                    };
                }

                return new DnsOperationResult
                {
                    Success = true,
                    Message = "DNS TXT 记录已删除",
                    Details = $"已删除 {relativeName} 的所有 TXT 记录"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GoDaddy 删除所有 DNS TXT 记录时发生错误: {RecordName}", recordName);
                return new DnsOperationResult
                {
                    Success = false,
                    Message = $"删除 DNS TXT 记录时发生错误: {ex.Message}",
                    ErrorCode = "EXCEPTION"
                };
            }
        }

        #region 私有方法

        private string? GetApiKey(Dictionary<string, object>? credentials)
        {
            return credentials?.GetValueOrDefault("apiKey")?.ToString()
                   ?? credentials?.GetValueOrDefault("api_key")?.ToString()
                   ?? _configuration["DnsProviders:GoDaddy:ApiKey"];
        }

        private string? GetApiSecret(Dictionary<string, object>? credentials)
        {
            return credentials?.GetValueOrDefault("apiSecret")?.ToString()
                   ?? credentials?.GetValueOrDefault("api_secret")?.ToString()
                   ?? _configuration["DnsProviders:GoDaddy:ApiSecret"];
        }

        private async Task<IEnumerable<GoDaddyDnsRecord>> GetExistingTxtRecords(string domain, string recordName, string apiKey, string apiSecret)
        {
            try
            {
                var response = await _httpClient.GetAsync($"domains/{domain}/records/TXT/{recordName}");
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return Enumerable.Empty<GoDaddyDnsRecord>();
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("获取 GoDaddy DNS 记录失败: {StatusCode} - {Content}", response.StatusCode, responseContent);
                    return Enumerable.Empty<GoDaddyDnsRecord>();
                }

                var records = JsonSerializer.Deserialize<List<GoDaddyDnsRecord>>(responseContent);
                return records ?? Enumerable.Empty<GoDaddyDnsRecord>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取 GoDaddy DNS 记录异常");
                return Enumerable.Empty<GoDaddyDnsRecord>();
            }
        }

        private string ExtractRootDomain(string domain)
        {
            var parts = domain.Split('.');
            if (parts.Length >= 2)
            {
                return string.Join('.', parts[^2], parts[^1]);
            }
            return domain;
        }

        private string GetRelativeRecordName(string recordName, string rootDomain)
        {
            // 移除域名后缀
            if (recordName.EndsWith($".{rootDomain}", StringComparison.OrdinalIgnoreCase))
            {
                return recordName[..^(rootDomain.Length + 1)];
            }
            if (recordName.Equals(rootDomain, StringComparison.OrdinalIgnoreCase))
            {
                return "@";
            }
            // 处理 _acme-challenge 格式
            if (recordName.StartsWith("_acme-challenge.", StringComparison.OrdinalIgnoreCase))
            {
                return recordName;
            }
            return recordName;
        }

        #endregion
    }

    #region GoDaddy API 响应模型

    public class GoDaddyDomain
    {
        [System.Text.Json.Serialization.JsonPropertyName("domain")]
        public string DomainName { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }

    public class GoDaddyDnsRecord
    {
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("data")]
        public string Data { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("ttl")]
        public int Ttl { get; set; } = 120;
    }

    #endregion
}
