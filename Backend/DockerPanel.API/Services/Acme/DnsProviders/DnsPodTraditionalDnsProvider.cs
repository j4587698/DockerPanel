using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace DockerPanel.API.Services.Acme.DnsProviders
{
    /// <summary>
    /// DNSPod 传统API (dnsapi.cn) 提供商实现
    /// </summary>
    public class DnsPodTraditionalDnsProvider : IDnsProvider
    {
        private readonly ILogger<DnsPodTraditionalDnsProvider> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public DnsPodTraditionalDnsProvider(
            ILogger<DnsPodTraditionalDnsProvider> logger,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _httpClient = _httpClientFactory.CreateClient("DnsPodTraditional");
            _httpClient.BaseAddress = new Uri("https://dnsapi.cn/");
        }

        public string Name => "dnspod-traditional";
        public string DisplayName => "DNSPod (传统API)";
        public bool RequiresCredentials => true;

        /// <summary>
        /// 创建 DNS TXT 记录
        /// </summary>
        public async Task<DnsOperationResult> CreateTxtRecordAsync(string domain, string recordName, string recordValue, Dictionary<string, object>? credentials)
        {
            try
            {
                _logger.LogInformation("创建 DNSPod 传统 API DNS TXT 记录: {RecordName} = {RecordValue}", recordName, recordValue);

                var loginToken = GetLoginToken(credentials);
                var secretId = GetSecretId(credentials);
                var secretKey = GetSecretKey(credentials);

                if (string.IsNullOrEmpty(loginToken) && (string.IsNullOrEmpty(secretId) || string.IsNullOrEmpty(secretKey)))
                {
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = "DNSPod 登录令牌或 Secret 未提供",
                        ErrorCode = "MISSING_CREDENTIALS"
                    };
                }

                // 获取域名信息
                var domainName = ExtractRootDomain(domain);
                var domainId = await GetDomainId(domainName, loginToken, secretId, secretKey);

                if (string.IsNullOrEmpty(domainId))
                {
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = $"无法获取域名 {domainName} 的 ID",
                        ErrorCode = "DOMAIN_NOT_FOUND"
                    };
                }

                // 创建子域名
                var subDomain = ExtractSubDomain(recordName, domainId);

                // 先检查TXT记录是否已存在
                var existingRecordId = await GetRecordId(domainId, subDomain, recordValue, loginToken, secretId, secretKey);
                if (!string.IsNullOrEmpty(existingRecordId))
                {
                    _logger.LogInformation("DNS TXT 记录已存在，跳过创建: {RecordName} = {RecordValue}, RecordId: {RecordId}", recordName, recordValue, existingRecordId);
                    return new DnsOperationResult
                    {
                        Success = true,
                        Message = $"DNS TXT 记录已存在: {recordName} = {recordValue}",
                        RecordId = existingRecordId
                    };
                }

                // 创建TXT记录
                var recordId = await CreateRecord(domainId, subDomain, recordValue, loginToken, secretId, secretKey);

                if (!string.IsNullOrEmpty(recordId))
                {
                    return new DnsOperationResult
                    {
                        Success = true,
                        Message = $"DNS TXT 记录创建成功: {recordName} = {recordValue}",
                        RecordId = recordId
                    };
                }
                else
                {
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = "创建 DNS TXT 记录失败",
                        ErrorCode = "CREATE_FAILED"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建 DNS TXT 记录时发生错误: {RecordName}", recordName);
                return new DnsOperationResult
                {
                    Success = false,
                    Message = $"创建 DNS TXT 记录时发生错误: {ex.Message}",
                    ErrorCode = "EXCEPTION"
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
                _logger.LogInformation("测试 DNSPod 传统 API 连接");

                var loginToken = GetLoginToken(credentials);
                var secretId = GetSecretId(credentials);
                var secretKey = GetSecretKey(credentials);

                if (string.IsNullOrEmpty(loginToken) && (string.IsNullOrEmpty(secretId) || string.IsNullOrEmpty(secretKey)))
                {
                    return new DnsTestResult
                    {
                        Success = false,
                        Message = "DNSPod 登录令牌或 Secret 未提供",
                        ErrorCode = "MISSING_CREDENTIALS"
                    };
                }

                // 尝试获取域名列表来测试连接
                var parameters = new Dictionary<string, string>();

                if (!string.IsNullOrEmpty(loginToken))
                {
                    parameters["login_token"] = loginToken;
                }
                else
                {
                    parameters["login_token"] = $"{secretId},{secretKey}";
                }

                var response = await SendRequest("Domain.List", parameters);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("DNSPod 传统 API 测试响应状态: {StatusCode}, 内容: {Content}", response.StatusCode, responseContent);

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);
                    if (result != null && result.ContainsKey("status"))
                    {
                        var status = result["status"].ToString();
                        if (status?.Equals("success", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            return new DnsTestResult
                            {
                                Success = true,
                                Message = "DNSPod 传统 API 连接测试成功"
                            };
                        }
                    }
                }

                return new DnsTestResult
                {
                    Success = false,
                    Message = "DNSPod 传统 API 连接测试失败",
                    ErrorCode = "CONNECTION_FAILED",
                    Details = responseContent
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "测试 DNSPod 传统 API 连接时发生错误");
                return new DnsTestResult
                {
                    Success = false,
                    Message = $"测试连接时发生错误: {ex.Message}",
                    ErrorCode = "EXCEPTION"
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
                _logger.LogInformation("删除 DNSPod 传统 API DNS TXT 记录: {RecordName}", recordName);

                var loginToken = GetLoginToken(credentials);
                var secretId = GetSecretId(credentials);
                var secretKey = GetSecretKey(credentials);

                var domainName = ExtractRootDomain(domain);
                var domainId = await GetDomainId(domainName, loginToken, secretId, secretKey);

                if (string.IsNullOrEmpty(domainId))
                {
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = $"无法获取域名 {domainName} 的 ID",
                        ErrorCode = "DOMAIN_NOT_FOUND"
                    };
                }

                var subDomain = ExtractSubDomain(recordName, domainId);
                var recordId = await GetRecordId(domainId, subDomain, recordValue, loginToken, secretId, secretKey);

                if (string.IsNullOrEmpty(recordId))
                {
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = "找不到要删除的 DNS TXT 记录",
                        ErrorCode = "RECORD_NOT_FOUND"
                    };
                }

                var success = await DeleteRecord(domainId, recordId, loginToken, secretId, secretKey);

                if (success)
                {
                    return new DnsOperationResult
                    {
                        Success = true,
                        Message = $"DNS TXT 记录删除成功: {recordName}",
                        RecordId = recordId
                    };
                }
                else
                {
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = "删除 DNS TXT 记录失败",
                        ErrorCode = "DELETE_FAILED"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除 DNS TXT 记录时发生错误: {RecordName}", recordName);
                return new DnsOperationResult
                {
                    Success = false,
                    Message = $"删除 DNS TXT 记录时发生错误: {ex.Message}",
                    ErrorCode = "EXCEPTION"
                };
            }
        }

        private string? GetLoginToken(Dictionary<string, object>? credentials)
        {
            return credentials?.GetValueOrDefault("loginToken")?.ToString()
                   ?? credentials?.GetValueOrDefault("login_token")?.ToString()
                   ?? credentials?.GetValueOrDefault("login_token")?.ToString()
                   ?? _configuration["DnsProviders:DnsPod:LoginToken"];
        }

        private string? GetSecretId(Dictionary<string, object>? credentials)
        {
            return credentials?.GetValueOrDefault("secretId")?.ToString()
                   ?? credentials?.GetValueOrDefault("SecretId")?.ToString()
                   ?? credentials?.GetValueOrDefault("secret_id")?.ToString()
                   ?? _configuration["DnsProviders:DnsPod:SecretId"];
        }

        private string? GetSecretKey(Dictionary<string, object>? credentials)
        {
            return credentials?.GetValueOrDefault("secretKey")?.ToString()
                   ?? credentials?.GetValueOrDefault("SecretKey")?.ToString()
                   ?? credentials?.GetValueOrDefault("secret_key")?.ToString()
                   ?? _configuration["DnsProviders:DnsPod:SecretKey"];
        }

        private async Task<string?> GetDomainId(string domain, string? loginToken, string? secretId, string? secretKey)
        {
            try
            {
                _logger.LogInformation("开始获取域名列表，查找域名: {Domain}", domain);

                var parameters = new Dictionary<string, string>();

                if (!string.IsNullOrEmpty(loginToken))
                {
                    parameters["login_token"] = loginToken;
                }
                else
                {
                    parameters["login_token"] = $"{secretId},{secretKey}";
                }

                var response = await SendRequest("Domain.List", parameters);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("DNSPod 传统 API 响应: {Content}", responseContent);

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);
                    if (result != null && result.ContainsKey("domains"))
                    {
                        var domainsJson = result["domains"].ToString();
                        if (!string.IsNullOrEmpty(domainsJson))
                        {
                            var domains = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(domainsJson);
                            if (domains != null)
                            {
                                var targetDomain = domains.FirstOrDefault(d =>
                                    d.ContainsKey("name") && d["name"].ToString()?.Equals(domain, StringComparison.OrdinalIgnoreCase) == true);

                                if (targetDomain != null && targetDomain.ContainsKey("id"))
                                {
                                    var domainId = targetDomain["id"].ToString();
                                    _logger.LogInformation("找到域名 ID: {DomainId} for {Domain}", domainId, domain);
                                    return domainId;
                                }
                            }
                        }
                    }
                }

                _logger.LogWarning("未找到域名: {Domain}", domain);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取域名 ID 时发生错误: {Domain}", domain);
                return null;
            }
        }

        private async Task<string?> CreateRecord(string domainId, string subDomain, string recordValue, string? loginToken, string? secretId, string? secretKey)
        {
            try
            {
                _logger.LogInformation("创建 TXT 记录: 域名ID={DomainId}, 子域名={SubDomain}, 值={RecordValue}", domainId, subDomain, recordValue);

                var parameters = new Dictionary<string, string>
                {
                    ["domain_id"] = domainId,
                    ["sub_domain"] = subDomain,
                    ["record_type"] = "TXT",
                    ["record_line"] = "默认",
                    ["value"] = recordValue,
                    ["ttl"] = "600"
                };

                if (!string.IsNullOrEmpty(loginToken))
                {
                    parameters["login_token"] = loginToken;
                }
                else
                {
                    parameters["login_token"] = $"{secretId},{secretKey}";
                }

                var response = await SendRequest("Record.Create", parameters);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("DNSPod 创建记录响应: {Content}", responseContent);

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);
                    if (result != null && result.ContainsKey("record"))
                    {
                        var recordJson = result["record"].ToString();
                        if (!string.IsNullOrEmpty(recordJson))
                        {
                            var record = JsonSerializer.Deserialize<Dictionary<string, object>>(recordJson);
                            if (record != null && record.ContainsKey("id"))
                            {
                                var recordId = record["id"].ToString();
                                _logger.LogInformation("记录创建成功，ID: {RecordId}", recordId);
                                return recordId;
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建记录时发生错误");
                return null;
            }
        }

        private async Task<string?> GetRecordId(string domainId, string subDomain, string recordValue, string? loginToken, string? secretId, string? secretKey)
        {
            try
            {
                var parameters = new Dictionary<string, string>
                {
                    ["domain_id"] = domainId,
                    ["sub_domain"] = subDomain,
                    ["record_type"] = "TXT"
                };

                if (!string.IsNullOrEmpty(loginToken))
                {
                    parameters["login_token"] = loginToken;
                }
                else
                {
                    parameters["login_token"] = $"{secretId},{secretKey}";
                }

                var response = await SendRequest("Record.List", parameters);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);
                    if (result != null && result.ContainsKey("records"))
                    {
                        var recordsJson = result["records"].ToString();
                        if (!string.IsNullOrEmpty(recordsJson))
                        {
                            var records = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(recordsJson);
                            if (records != null)
                            {
                                var targetRecord = records.FirstOrDefault(r =>
                                    r.ContainsKey("value") && r["value"].ToString()?.Equals(recordValue, StringComparison.OrdinalIgnoreCase) == true);

                                if (targetRecord != null && targetRecord.ContainsKey("id"))
                                {
                                    return targetRecord["id"].ToString();
                                }
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取记录 ID 时发生错误");
                return null;
            }
        }

        private async Task<bool> DeleteRecord(string domainId, string recordId, string? loginToken, string? secretId, string? secretKey)
        {
            try
            {
                var parameters = new Dictionary<string, string>
                {
                    ["domain_id"] = domainId,
                    ["record_id"] = recordId
                };

                if (!string.IsNullOrEmpty(loginToken))
                {
                    parameters["login_token"] = loginToken;
                }
                else
                {
                    parameters["login_token"] = $"{secretId},{secretKey}";
                }

                var response = await SendRequest("Record.Delete", parameters);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);
                    if (result != null && result.ContainsKey("status"))
                    {
                        var status = result["status"].ToString();
                        return status?.Equals("success", StringComparison.OrdinalIgnoreCase) == true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除记录时发生错误");
                return false;
            }
        }

        private async Task<HttpResponseMessage> SendRequest(string action, Dictionary<string, string> parameters)
        {
            var content = new FormUrlEncodedContent(parameters);

            // DNSPod API requires User-Agent header
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            var request = new HttpRequestMessage(HttpMethod.Post, action)
            {
                Content = content
            };
            request.Headers.UserAgent.ParseAdd("DockerPanel-ACME/1.0");

            var response = await _httpClient.SendAsync(request);

            _logger.LogInformation("DNSPod 传统 API 请求: Action={Action}, Status={StatusCode}",
                action, response.StatusCode);

            return response;
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

        private string ExtractSubDomain(string recordName, string domainId)
        {
            // 从完整记录名中提取子域名部分
            // 例如：_acme-challenge.test.example.com -> _acme-challenge.test
            var domainName = recordName;

            // 简单实现：移除最后两个部分（根域名和TLD）
            var parts = domainName.Split('.');
            if (parts.Length > 2)
            {
                return string.Join('.', parts[..^2]);
            }

            return domainName;
        }

        /// <summary>
        /// 删除所有匹配名称的 DNS TXT 记录
        /// </summary>
        public async Task<DnsOperationResult> DeleteAllTxtRecordsByNameAsync(string domain, string recordName, Dictionary<string, object>? credentials)
        {
            try
            {
                _logger.LogInformation("删除所有匹配的 DNS TXT 记录: {RecordName}", recordName);

                var loginToken = GetLoginToken(credentials);
                var secretId = GetSecretId(credentials);
                var secretKey = GetSecretKey(credentials);

                var domainName = ExtractRootDomain(domain);
                var domainId = await GetDomainId(domainName, loginToken, secretId, secretKey);

                if (string.IsNullOrEmpty(domainId))
                {
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = $"无法获取域名 {domainName} 的 ID",
                        ErrorCode = "DOMAIN_NOT_FOUND"
                    };
                }

                var subDomain = ExtractSubDomain(recordName, domainId);

                // 获取所有匹配的 TXT 记录
                var parameters = new Dictionary<string, string>
                {
                    ["domain_id"] = domainId,
                    ["sub_domain"] = subDomain,
                    ["record_type"] = "TXT"
                };

                if (!string.IsNullOrEmpty(loginToken))
                {
                    parameters["login_token"] = loginToken;
                }
                else
                {
                    parameters["login_token"] = $"{secretId},{secretKey}";
                }

                var response = await SendRequest("Record.List", parameters);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);
                    if (result != null && result.ContainsKey("records"))
                    {
                        var recordsJson = result["records"].ToString();
                        if (!string.IsNullOrEmpty(recordsJson))
                        {
                            var records = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(recordsJson);
                            if (records != null && records.Count > 0)
                            {
                                var deletedCount = 0;
                                foreach (var record in records)
                                {
                                    if (record.ContainsKey("id"))
                                    {
                                        var recordId = record["id"].ToString();
                                        if (!string.IsNullOrEmpty(recordId))
                                        {
                                            var deleteResult = await DeleteRecord(domainId, recordId, loginToken, secretId, secretKey);
                                            if (deleteResult)
                                            {
                                                deletedCount++;
                                                _logger.LogInformation("已删除 DNS TXT 记录: {RecordId}", recordId);
                                            }
                                        }
                                    }
                                }

                                return new DnsOperationResult
                                {
                                    Success = true,
                                    Message = $"成功删除 {deletedCount} 条 DNS TXT 记录",
                                    Details = $"已删除 {deletedCount} 条记录"
                                };
                            }
                        }
                    }
                }

                return new DnsOperationResult
                {
                    Success = true,
                    Message = "没有找到匹配的记录或已全部删除"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除所有 DNS TXT 记录时发生错误: {RecordName}", recordName);
                return new DnsOperationResult
                {
                    Success = false,
                    Message = $"删除 DNS TXT 记录时发生错误: {ex.Message}",
                    ErrorCode = "EXCEPTION"
                };
            }
        }
    }
}