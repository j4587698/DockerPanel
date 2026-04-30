using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace DockerPanel.API.Services.Acme.DnsProviders
{
    /// <summary>
    /// Cloudflare DNS API 提供商实现
    /// </summary>
    public class CloudflareDnsProvider : IDnsProvider
    {
        private readonly ILogger<CloudflareDnsProvider> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public CloudflareDnsProvider(
            ILogger<CloudflareDnsProvider> logger,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _httpClient = _httpClientFactory.CreateClient("Cloudflare");
            _httpClient.BaseAddress = new Uri("https://api.cloudflare.com/client/v4/");
        }

        public string Name => "cloudflare";
        public string DisplayName => "Cloudflare";
        public bool RequiresCredentials => true;

        /// <summary>
        /// 创建 DNS TXT 记录
        /// </summary>
        public async Task<DnsOperationResult> CreateTxtRecordAsync(string domain, string recordName, string recordValue, Dictionary<string, object>? credentials)
        {
            try
            {
                _logger.LogInformation("创建 Cloudflare DNS TXT 记录: {RecordName} = {RecordValue}", recordName, recordValue);

                var apiToken = GetApiToken(credentials);
                if (string.IsNullOrEmpty(apiToken))
                {
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = "Cloudflare API Token 未提供",
                        ErrorCode = "MISSING_API_TOKEN"
                    };
                }

                // 设置认证头
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

                // 获取域名信息
                var zoneId = await GetZoneId(domain);
                if (string.IsNullOrEmpty(zoneId))
                {
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = $"无法获取域名 {domain} 的 Zone ID",
                        ErrorCode = "ZONE_NOT_FOUND"
                    };
                }

                // 检查记录是否已存在
                var existingRecord = await GetExistingTxtRecord(zoneId, recordName);
                if (existingRecord != null)
                {
                    // 更新现有记录
                    return await UpdateTxtRecord(zoneId, existingRecord.Id, recordName, recordValue);
                }

                // 创建新记录
                var createRequest = new
                {
                    type = "TXT",
                    name = recordName,
                    content = recordValue,
                    ttl = 120 // 短 TTL 用于快速验证
                };

                var json = JsonSerializer.Serialize(createRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"zones/{zoneId}/dns_records", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("创建 Cloudflare DNS 记录失败: {StatusCode} - {Content}", response.StatusCode, responseContent);
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = $"创建 DNS 记录失败: {response.StatusCode}",
                        ErrorCode = "CREATE_FAILED",
                        Details = responseContent
                    };
                }

                var result = JsonSerializer.Deserialize<CloudflareDnsResponse>(responseContent);
                if (result?.Success == true)
                {
                    _logger.LogInformation("Cloudflare DNS TXT 记录创建成功: {RecordName}", recordName);
                    return new DnsOperationResult
                    {
                        Success = true,
                        Message = "DNS 记录创建成功",
                        RecordId = result.Result?.Id,
                        Details = $"记录 ID: {result.Result?.Id}"
                    };
                }

                return new DnsOperationResult
                {
                    Success = false,
                    Message = "创建 DNS 记录失败",
                    ErrorCode = "API_ERROR",
                    Details = responseContent
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建 Cloudflare DNS 记录异常: {RecordName}", recordName);
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
                _logger.LogInformation("删除 Cloudflare DNS TXT 记录: {RecordName}", recordName);

                var apiToken = GetApiToken(credentials);
                if (string.IsNullOrEmpty(apiToken))
                {
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = "Cloudflare API Token 未提供",
                        ErrorCode = "MISSING_API_TOKEN"
                    };
                }

                // 设置认证头
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

                // 获取域名信息
                var zoneId = await GetZoneId(domain);
                if (string.IsNullOrEmpty(zoneId))
                {
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = $"无法获取域名 {domain} 的 Zone ID",
                        ErrorCode = "ZONE_NOT_FOUND"
                    };
                }

                // 查找现有记录
                var existingRecord = await GetExistingTxtRecord(zoneId, recordName);
                if (existingRecord == null)
                {
                    _logger.LogWarning("未找到要删除的 DNS 记录: {RecordName}", recordName);
                    return new DnsOperationResult
                    {
                        Success = true,
                        Message = "记录不存在，无需删除"
                    };
                }

                // 删除记录
                var response = await _httpClient.DeleteAsync($"zones/{zoneId}/dns_records/{existingRecord.Id}");
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("删除 Cloudflare DNS 记录失败: {StatusCode} - {Content}", response.StatusCode, responseContent);
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = $"删除 DNS 记录失败: {response.StatusCode}",
                        ErrorCode = "DELETE_FAILED",
                        Details = responseContent
                    };
                }

                var result = JsonSerializer.Deserialize<CloudflareDnsResponse>(responseContent);
                if (result?.Success == true)
                {
                    _logger.LogInformation("Cloudflare DNS TXT 记录删除成功: {RecordName}", recordName);
                    return new DnsOperationResult
                    {
                        Success = true,
                        Message = "DNS 记录删除成功",
                        RecordId = existingRecord.Id
                    };
                }

                return new DnsOperationResult
                {
                    Success = false,
                    Message = "删除 DNS 记录失败",
                    ErrorCode = "API_ERROR",
                    Details = responseContent
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除 Cloudflare DNS 记录异常: {RecordName}", recordName);
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
                _logger.LogInformation("测试 Cloudflare DNS 连接");

                var apiToken = GetApiToken(credentials);
                if (string.IsNullOrEmpty(apiToken))
                {
                    return new DnsTestResult
                    {
                        Success = false,
                        Message = "Cloudflare API Token 未提供",
                        ErrorCode = "MISSING_API_TOKEN"
                    };
                }

                // 设置认证头
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

                // 测试获取用户信息
                var response = await _httpClient.GetAsync("user/tokens/verify");
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Cloudflare API 认证失败: {StatusCode} - {Content}", response.StatusCode, responseContent);
                    return new DnsTestResult
                    {
                        Success = false,
                        Message = $"API 认证失败: {response.StatusCode}",
                        ErrorCode = "AUTH_FAILED",
                        Details = responseContent
                    };
                }

                var result = JsonSerializer.Deserialize<CloudflareTokenVerifyResponse>(responseContent);
                if (result?.Success == true)
                {
                    return new DnsTestResult
                    {
                        Success = true,
                        Message = "Cloudflare API 连接测试成功",
                        Details = $"Token 状态: 有效, 权限: {string.Join(", ", result.Result?.Scope ?? Enumerable.Empty<string>())}"
                    };
                }

                return new DnsTestResult
                {
                    Success = false,
                    Message = "API 连接测试失败",
                    ErrorCode = "API_ERROR",
                    Details = responseContent
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "测试 Cloudflare DNS 连接异常");
                return new DnsTestResult
                {
                    Success = false,
                    Message = $"连接测试异常: {ex.Message}",
                    ErrorCode = "EXCEPTION",
                    Details = ex.ToString()
                };
            }
        }

        #region 私有方法

        private string? GetApiToken(Dictionary<string, object>? credentials)
        {
            return credentials?.GetValueOrDefault("api_token")?.ToString()
                   ?? _configuration["DnsProviders:Cloudflare:ApiToken"];
        }

        private async Task<string?> GetZoneId(string domain)
        {
            try
            {
                // 提取根域名
                var rootDomain = ExtractRootDomain(domain);

                var response = await _httpClient.GetAsync($"zones?name={rootDomain}");
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("获取 Zone ID 失败: {StatusCode} - {Content}", response.StatusCode, responseContent);
                    return null;
                }

                var result = JsonSerializer.Deserialize<CloudflareZonesResponse>(responseContent);
                var zone = result?.Result?.FirstOrDefault();

                return zone?.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取 Zone ID 异常: {Domain}", domain);
                return null;
            }
        }

        private async Task<CloudflareDnsRecord?> GetExistingTxtRecord(string zoneId, string recordName)
        {
            try
            {
                var response = await _httpClient.GetAsync($"zones/{zoneId}/dns_records?type=TXT&name={recordName}");
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("查询现有 DNS 记录失败: {StatusCode} - {Content}", response.StatusCode, responseContent);
                    return null;
                }

                var result = JsonSerializer.Deserialize<CloudflareDnsRecordsResponse>(responseContent);
                return result?.Result?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查询现有 DNS 记录异常: {RecordName}", recordName);
                return null;
            }
        }

        private async Task<DnsOperationResult> UpdateTxtRecord(string zoneId, string recordId, string recordName, string recordValue)
        {
            try
            {
                var updateRequest = new
                {
                    type = "TXT",
                    name = recordName,
                    content = recordValue,
                    ttl = 120
                };

                var json = JsonSerializer.Serialize(updateRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync($"zones/{zoneId}/dns_records/{recordId}", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("更新 Cloudflare DNS 记录失败: {StatusCode} - {Content}", response.StatusCode, responseContent);
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = $"更新 DNS 记录失败: {response.StatusCode}",
                        ErrorCode = "UPDATE_FAILED",
                        Details = responseContent
                    };
                }

                var result = JsonSerializer.Deserialize<CloudflareDnsResponse>(responseContent);
                if (result?.Success == true)
                {
                    _logger.LogInformation("Cloudflare DNS TXT 记录更新成功: {RecordName}", recordName);
                    return new DnsOperationResult
                    {
                        Success = true,
                        Message = "DNS 记录更新成功",
                        RecordId = recordId,
                        Details = $"记录 ID: {recordId}"
                    };
                }

                return new DnsOperationResult
                {
                    Success = false,
                    Message = "更新 DNS 记录失败",
                    ErrorCode = "API_ERROR",
                    Details = responseContent
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新 Cloudflare DNS 记录异常: {RecordName}", recordName);
                return new DnsOperationResult
                {
                    Success = false,
                    Message = $"更新 DNS 记录异常: {ex.Message}",
                    ErrorCode = "EXCEPTION",
                    Details = ex.ToString()
                };
            }
        }

        private string ExtractRootDomain(string domain)
        {
            // 简单的根域名提取逻辑
            // 实际应用中可能需要更复杂的逻辑来处理多级 TLD
            var parts = domain.Split('.');
            if (parts.Length >= 2)
            {
                return string.Join('.', parts[^2], parts[^1]);
            }
            return domain;
        }

        /// <summary>
        /// 删除所有匹配名称的 DNS TXT 记录（默认实现：逐个删除）
        /// </summary>
        public async Task<DnsOperationResult> DeleteAllTxtRecordsByNameAsync(string domain, string recordName, Dictionary<string, object>? credentials)
        {
            try
            {
                _logger.LogInformation("Cloudflare: 删除所有匹配的 DNS TXT 记录: {RecordName}", recordName);

                // 获取 API Token
                var apiToken = credentials?["api_token"]?.ToString();
                if (string.IsNullOrEmpty(apiToken))
                {
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = "缺少 API Token",
                        ErrorCode = "MISSING_TOKEN"
                    };
                }

                var email = credentials?["email"]?.ToString();
                var apiKey = credentials?["api_key"]?.ToString();

                // 获取 Zone ID
                var zoneId = await GetZoneId(domain, apiToken, email, apiKey);
                if (string.IsNullOrEmpty(zoneId))
                {
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = $"无法获取域名 {domain} 的 Zone ID",
                        ErrorCode = "ZONE_NOT_FOUND"
                    };
                }

                // 获取所有匹配的 TXT 记录
                var records = await ListTxtRecords(zoneId, recordName, apiToken, email, apiKey);
                if (records == null || records.Count == 0)
                {
                    return new DnsOperationResult
                    {
                        Success = true,
                        Message = "没有找到匹配的记录"
                    };
                }

                var deletedCount = 0;
                foreach (var record in records)
                {
                    var deleteResult = await DeleteRecord(zoneId, record.Id, apiToken, email, apiKey);
                    if (deleteResult)
                    {
                        deletedCount++;
                    }
                }

                return new DnsOperationResult
                {
                    Success = true,
                    Message = $"成功删除 {deletedCount} 条 DNS TXT 记录",
                    Details = $"已删除 {deletedCount} 条记录"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cloudflare 删除所有 DNS TXT 记录时发生错误: {RecordName}", recordName);
                return new DnsOperationResult
                {
                    Success = false,
                    Message = $"删除 DNS TXT 记录时发生错误: {ex.Message}",
                    ErrorCode = "EXCEPTION"
                };
            }
        }

        /// <summary>
        /// 获取 Zone ID
        /// </summary>
        private async Task<string?> GetZoneId(string domain, string apiToken, string? email, string? apiKey)
        {
            try
            {
                _logger.LogInformation("获取域名 {Domain} 的 Zone ID", domain);

                var response = await _httpClient.GetAsync($"zones?name={Uri.EscapeDataString(domain)}", CancellationToken.None);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Cloudflare Zone API 响应: {StatusCode}", response.StatusCode);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("获取 Zone ID 失败: {StatusCode} - {Content}", response.StatusCode, responseContent);
                    return null;
                }

                var result = JsonSerializer.Deserialize<CloudflareZonesResponse>(responseContent);
                if (result?.Result != null && result.Result.Count > 0)
                {
                    var zoneId = result.Result[0].Id;
                    _logger.LogInformation("找到 Zone ID: {ZoneId}", zoneId);
                    return zoneId;
                }

                _logger.LogWarning("未找到域名 {Domain} 的 Zone", domain);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取 Zone ID 时发生错误: {Domain}", domain);
                return null;
            }
        }

        /// <summary>
        /// 列出所有 TXT 记录
        /// </summary>
        private async Task<List<CloudflareDnsRecord>> ListTxtRecords(string zoneId, string recordName, string apiToken, string? email, string? apiKey)
        {
            try
            {
                var records = new List<CloudflareDnsRecord>();

                using var request = new HttpRequestMessage(HttpMethod.Get, $"zones/{zoneId}/dns_records?type=TXT&name={Uri.EscapeDataString(recordName)}");
                request.Headers.Add("Authorization", $"Bearer {apiToken}");

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<CloudflareDnsRecordsResponse>(responseContent);
                    if (result?.Result != null)
                    {
                        records = result.Result;
                    }
                }

                _logger.LogInformation("找到 {Count} 个 TXT 记录", records.Count);
                return records;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "列出 TXT 记录时发生错误: {RecordName}", recordName);
                return new List<CloudflareDnsRecord>();
            }
        }

        /// <summary>
        /// 删除 DNS 记录
        /// </summary>
        private async Task<bool> DeleteRecord(string zoneId, string recordId, string apiToken, string? email, string? apiKey)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Delete, $"zones/{zoneId}/dns_records/{recordId}");
                request.Headers.Add("Authorization", $"Bearer {apiToken}");

                var response = await _httpClient.SendAsync(request);

                _logger.LogInformation("删除 DNS 记录响应: {StatusCode}", response.StatusCode);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除 DNS 记录时发生错误: {RecordId}", recordId);
                return false;
            }
        }

        #endregion
    }

    #region Cloudflare API 响应模型

    public class CloudflareDnsResponse
    {
        public bool Success { get; set; }
        public CloudflareDnsRecord? Result { get; set; }
        public List<string>? Errors { get; set; }
        public List<string>? Messages { get; set; }
    }

    public class CloudflareDnsRecordsResponse
    {
        public bool Success { get; set; }
        public List<CloudflareDnsRecord>? Result { get; set; }
        public List<CloudflareErrorInfo>? Errors { get; set; }
    }

    public class CloudflareZonesResponse
    {
        public bool Success { get; set; }
        public List<CloudflareZone>? Result { get; set; }
        public List<CloudflareErrorInfo>? Errors { get; set; }
    }

    public class CloudflareTokenVerifyResponse
    {
        public bool Success { get; set; }
        public CloudflareTokenInfo? Result { get; set; }
        public List<CloudflareErrorInfo>? Errors { get; set; }
    }

    public class CloudflareDnsRecord
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public int Ttl { get; set; }
        public bool Proxied { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime ModifiedOn { get; set; }
    }

    public class CloudflareZone
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public Dictionary<string, object>? Plan { get; set; }
    }

    public class CloudflareTokenInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? ExpiresOn { get; set; }
        public List<string>? Scope { get; set; }
    }

    public class CloudflareErrorInfo
    {
        public int Code { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    #endregion
}