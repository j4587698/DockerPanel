using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace DockerPanel.API.Services.Acme.DnsProviders
{
    /// <summary>
    /// DNSPod 传统API (dnsapi.cn) 提供商实现
    /// 使用旧版 DNSPod API 格式
    /// </summary>
    public class DnsPodDnsProvider : IDnsProvider
    {
        private readonly ILogger<DnsPodDnsProvider> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        // 域名ID到域名的映射缓存
        private readonly Dictionary<string, string> _domainIdToNameMap = new();

        public DnsPodDnsProvider(
            ILogger<DnsPodDnsProvider> logger,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _httpClient = _httpClientFactory.CreateClient("DnsPod");
            // 使用传统 DNSPod API
            _httpClient.BaseAddress = new Uri("https://dnsapi.cn/");
        }

        public string Name => "dnspod";
        public string DisplayName => "DNSPod";
        public bool RequiresCredentials => true;

        /// <summary>
        /// 创建 DNS TXT 记录
        /// </summary>
        public async Task<DnsOperationResult> CreateTxtRecordAsync(string domain, string recordName, string recordValue, Dictionary<string, object>? credentials)
        {
            try
            {
                _logger.LogInformation("创建 DNSPod DNS TXT 记录: {RecordName} = {RecordValue}", recordName, recordValue);

                var secretId = GetSecretId(credentials);
                var secretKey = GetSecretKey(credentials);

                if (string.IsNullOrEmpty(secretId) || string.IsNullOrEmpty(secretKey))
                {
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = "DNSPod Secret 未提供",
                        ErrorCode = "MISSING_CREDENTIALS"
                    };
                }

                // 构建 login_token
                var loginToken = $"{secretId},{secretKey}";

                // 获取域名信息
                var domainName = ExtractRootDomain(domain);
                var domainId = await GetDomainId(domainName, loginToken);

                if (string.IsNullOrEmpty(domainId))
                {
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = $"无法获取域名 {domainName} 的 ID",
                        ErrorCode = "DOMAIN_NOT_FOUND"
                    };
                }

                // 缓存域名ID到域名的映射
                if (!_domainIdToNameMap.ContainsKey(domainId))
                {
                    _domainIdToNameMap[domainId] = domainName;
                }

                // 提取子域名
                var subDomain = ExtractSubDomain(recordName, domainName);

                // 检查记录是否已存在
                var existingRecordId = await GetRecordId(domainId, subDomain, recordValue, loginToken);
                if (!string.IsNullOrEmpty(existingRecordId))
                {
                    _logger.LogInformation("DNS TXT 记录已存在，跳过创建: {RecordName} = {RecordValue}, RecordId: {RecordId}",
                        recordName, recordValue, existingRecordId);
                    return new DnsOperationResult
                    {
                        Success = true,
                        Message = $"DNS TXT 记录已存在: {recordName} = {recordValue}",
                        RecordId = existingRecordId
                    };
                }

                // 创建新记录
                var recordId = await CreateRecord(domainId, subDomain, recordValue, loginToken);

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
        /// 删除 DNS TXT 记录
        /// </summary>
        public async Task<DnsOperationResult> DeleteTxtRecordAsync(string domain, string recordName, string recordValue, Dictionary<string, object>? credentials)
        {
            try
            {
                _logger.LogInformation("删除 DNSPod DNS TXT 记录: {RecordName}", recordName);

                var secretId = GetSecretId(credentials);
                var secretKey = GetSecretKey(credentials);
                var loginToken = $"{secretId},{secretKey}";

                var domainName = ExtractRootDomain(domain);
                var domainId = await GetDomainId(domainName, loginToken);

                if (string.IsNullOrEmpty(domainId))
                {
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = $"无法获取域名 {domainName} 的 ID",
                        ErrorCode = "DOMAIN_NOT_FOUND"
                    };
                }

                var subDomain = ExtractSubDomain(recordName, domainName);
                var recordId = await GetRecordId(domainId, subDomain, recordValue, loginToken);

                if (string.IsNullOrEmpty(recordId))
                {
                    return new DnsOperationResult
                    {
                        Success = true,
                        Message = "记录不存在，无需删除"
                    };
                }

                var success = await DeleteRecord(domainId, recordId, loginToken);

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

        /// <summary>
        /// 删除所有匹配名称的 DNS TXT 记录
        /// </summary>
        public async Task<DnsOperationResult> DeleteAllTxtRecordsByNameAsync(string domain, string recordName, Dictionary<string, object>? credentials)
        {
            try
            {
                _logger.LogInformation("删除所有匹配的 DNS TXT 记录: {RecordName}", recordName);

                var secretId = GetSecretId(credentials);
                var secretKey = GetSecretKey(credentials);
                var loginToken = $"{secretId},{secretKey}";

                var domainName = ExtractRootDomain(domain);
                var domainId = await GetDomainId(domainName, loginToken);

                if (string.IsNullOrEmpty(domainId))
                {
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = $"无法获取域名 {domainName} 的 ID",
                        ErrorCode = "DOMAIN_NOT_FOUND"
                    };
                }

                var subDomain = ExtractSubDomain(recordName, domainName);

                // 获取所有匹配的 TXT 记录
                var parameters = new Dictionary<string, string>
                {
                    ["domain_id"] = domainId,
                    ["sub_domain"] = subDomain,
                    ["record_type"] = "TXT",
                    ["login_token"] = loginToken
                };

                var response = await SendRequest("Record.List", parameters);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);
                    if (result != null && result.ContainsKey("records"))
                    {
                        var records = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(result["records"].ToString()!);
                        if (records != null && records.Count > 0)
                        {
                            var deletedCount = 0;
                            foreach (var record in records)
                            {
                                if (record.ContainsKey("id"))
                                {
                                    var recordId = record["id"].ToString()!;
                                    var deleteResult = await DeleteRecord(domainId, recordId, loginToken);
                                    if (deleteResult)
                                    {
                                        deletedCount++;
                                        _logger.LogInformation("已删除 DNS TXT 记录: {RecordId}", recordId);
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

        /// <summary>
        /// 测试连接
        /// </summary>
        public async Task<DnsTestResult> TestConnectionAsync(Dictionary<string, object>? credentials)
        {
            try
            {
                _logger.LogInformation("测试 DNSPod API 连接");

                var secretId = GetSecretId(credentials);
                var secretKey = GetSecretKey(credentials);

                if (string.IsNullOrEmpty(secretId) || string.IsNullOrEmpty(secretKey))
                {
                    return new DnsTestResult
                    {
                        Success = false,
                        Message = "DNSPod Secret 未提供",
                        ErrorCode = "MISSING_CREDENTIALS"
                    };
                }

                var loginToken = $"{secretId},{secretKey}";

                // 尝试获取域名列表来测试连接
                var parameters = new Dictionary<string, string>
                {
                    ["login_token"] = loginToken
                };

                var response = await SendRequest("Domain.List", parameters);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("DNSPod API 测试响应状态: {StatusCode}, 内容: {Content}", response.StatusCode, responseContent);

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);
                    if (result != null && result.ContainsKey("status"))
                    {
                        var statusJson = result["status"].ToString();
                        if (statusJson != null && statusJson.Contains("success", StringComparison.OrdinalIgnoreCase))
                        {
                            return new DnsTestResult
                            {
                                Success = true,
                                Message = "DNSPod API 连接测试成功"
                            };
                        }
                    }
                }

                return new DnsTestResult
                {
                    Success = false,
                    Message = "DNSPod API 连接测试失败",
                    ErrorCode = "CONNECTION_FAILED",
                    Details = responseContent
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "测试 DNSPod API 连接时发生错误");
                return new DnsTestResult
                {
                    Success = false,
                    Message = $"测试连接时发生错误: {ex.Message}",
                    ErrorCode = "EXCEPTION"
                };
            }
        }

        #region 私有方法

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

        private async Task<string?> GetDomainId(string domain, string loginToken)
        {
            try
            {
                _logger.LogInformation("开始获取域名列表，查找域名: {Domain}", domain);

                var parameters = new Dictionary<string, string>
                {
                    ["login_token"] = loginToken
                };

                var response = await SendRequest("Domain.List", parameters);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogDebug("DNSPod API 响应: {Content}", responseContent);

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);
                    if (result != null && result.ContainsKey("domains"))
                    {
                        var domains = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(result["domains"].ToString()!);
                        if (domains != null)
                        {
                            var targetDomain = domains.FirstOrDefault(d =>
                                d.ContainsKey("name") && d["name"].ToString()!.Equals(domain, StringComparison.OrdinalIgnoreCase));

                            if (targetDomain != null && targetDomain.ContainsKey("id"))
                            {
                                var domainId = targetDomain["id"].ToString();
                                _logger.LogInformation("找到域名 ID: {DomainId} for {Domain}", domainId, domain);

                                // 缓存映射
                                if (!string.IsNullOrEmpty(domainId))
                                {
                                    _domainIdToNameMap[domainId] = domain;
                                }

                                return domainId;
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

        private async Task<string?> CreateRecord(string domainId, string subDomain, string recordValue, string loginToken)
        {
            try
            {
                _logger.LogInformation("创建 TXT 记录: 域名ID={DomainId}, 子域名={SubDomain}, 值={RecordValue}",
                    domainId, subDomain, recordValue);

                var parameters = new Dictionary<string, string>
                {
                    ["domain_id"] = domainId,
                    ["sub_domain"] = subDomain,
                    ["record_type"] = "TXT",
                    ["record_line"] = "默认",
                    ["value"] = recordValue,
                    ["ttl"] = "600",
                    ["login_token"] = loginToken
                };

                var response = await SendRequest("Record.Create", parameters);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("DNSPod 创建记录响应: {Content}", responseContent);

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);
                    if (result != null && result.ContainsKey("record"))
                    {
                        var record = JsonSerializer.Deserialize<Dictionary<string, object>>(result["record"].ToString()!);
                        if (record != null && record.ContainsKey("id"))
                        {
                            var recordId = record["id"].ToString();
                            _logger.LogInformation("记录创建成功，ID: {RecordId}", recordId);
                            return recordId;
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

        private async Task<string?> GetRecordId(string domainId, string subDomain, string recordValue, string loginToken)
        {
            try
            {
                var parameters = new Dictionary<string, string>
                {
                    ["domain_id"] = domainId,
                    ["sub_domain"] = subDomain,
                    ["record_type"] = "TXT",
                    ["login_token"] = loginToken
                };

                var response = await SendRequest("Record.List", parameters);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);
                    if (result != null && result.ContainsKey("records"))
                    {
                        var records = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(result["records"].ToString()!);
                        if (records != null)
                        {
                            var targetRecord = records.FirstOrDefault(r =>
                                r.ContainsKey("value") && r["value"].ToString()!.Equals(recordValue, StringComparison.OrdinalIgnoreCase));

                            if (targetRecord != null && targetRecord.ContainsKey("id"))
                            {
                                return targetRecord["id"].ToString();
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

        private async Task<bool> DeleteRecord(string domainId, string recordId, string loginToken)
        {
            try
            {
                var parameters = new Dictionary<string, string>
                {
                    ["domain_id"] = domainId,
                    ["record_id"] = recordId,
                    ["login_token"] = loginToken
                };

                var response = await SendRequest("Record.Remove", parameters);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("DNSPod 删除记录响应: {Content}", responseContent);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除记录时发生错误");
                return false;
            }
        }

        /// <summary>
        /// 发送传统 DNSPod API 请求
        /// 使用 FormUrlEncodedContent 格式
        /// </summary>
        private async Task<HttpResponseMessage> SendRequest(string action, Dictionary<string, string> parameters)
        {
            // 添加格式参数
            if (!parameters.ContainsKey("format"))
            {
                parameters["format"] = "json";
            }

            var content = new FormUrlEncodedContent(parameters);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            var request = new HttpRequestMessage(HttpMethod.Post, action)
            {
                Content = content
            };
            request.Headers.UserAgent.ParseAdd("DockerPanel-ACME/1.0");

            _logger.LogInformation("DNSPod API 请求: Action={Action}", action);

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("DNSPod API 响应状态: {StatusCode}, 内容: {Content}",
                response.StatusCode, responseContent);

            return response;
        }

        private string ExtractRootDomain(string domain)
        {
            // 移除通配符前缀
            if (domain.StartsWith("*."))
            {
                domain = domain[2..];
            }

            var parts = domain.Split('.');
            if (parts.Length >= 2)
            {
                return string.Join('.', parts[^2], parts[^1]);
            }
            return domain;
        }

        private string ExtractSubDomain(string recordName, string domainName)
        {
            // 从完整记录名中提取子域名部分
            // 例如：_acme-challenge.test.example.com -> _acme-challenge.test
            // 例如：_acme-challenge.example.com -> _acme-challenge

            // 如果记录名以域名结尾，移除域名部分
            if (recordName.EndsWith("." + domainName, StringComparison.OrdinalIgnoreCase))
            {
                return recordName[..^("." + domainName).Length];
            }

            // 如果记录名等于域名，返回 @
            if (recordName.Equals(domainName, StringComparison.OrdinalIgnoreCase))
            {
                return "@";
            }

            // 对于 ACME challenge，如果格式是 _acme-challenge.xxx.yyy.zzz
            // 需要移除最后两个部分（根域名）
            var parts = recordName.Split('.');
            if (parts.Length > 2)
            {
                return string.Join('.', parts[..^2]);
            }

            return recordName;
        }

        #endregion
    }
}