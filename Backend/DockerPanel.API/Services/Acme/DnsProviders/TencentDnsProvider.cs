using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace DockerPanel.API.Services.Acme.DnsProviders
{
    /// <summary>
    /// 腾讯云 DNS API 提供商实现
    /// </summary>
    public class TencentDnsProvider : IDnsProvider
    {
        private readonly ILogger<TencentDnsProvider> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public TencentDnsProvider(
            ILogger<TencentDnsProvider> logger,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _httpClient = _httpClientFactory.CreateClient("Tencent");
            _httpClient.BaseAddress = new Uri("https://cns.tencentcloudapi.com/");
        }

        public string Name => "tencent";
        public string DisplayName => "腾讯云DNS";
        public bool RequiresCredentials => true;

        /// <summary>
        /// 创建 DNS TXT 记录
        /// </summary>
        public async Task<DnsOperationResult> CreateTxtRecordAsync(string domain, string recordName, string recordValue, Dictionary<string, object>? credentials)
        {
            try
            {
                _logger.LogInformation("创建腾讯云 DNS TXT 记录: {RecordName} = {RecordValue}", recordName, recordValue);

                var secretId = GetSecretId(credentials);
                var secretKey = GetSecretKey(credentials);

                if (string.IsNullOrEmpty(secretId) || string.IsNullOrEmpty(secretKey))
                {
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = "腾讯云 Secret 未提供",
                        ErrorCode = "MISSING_CREDENTIALS"
                    };
                }

                // 获取域名信息
                var domainName = ExtractRootDomain(domain);
                var domainId = await GetDomainId(domainName, secretId, secretKey);
                if (string.IsNullOrEmpty(domainId))
                {
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = $"无法获取域名 {domainName} 的 ID",
                        ErrorCode = "DOMAIN_NOT_FOUND"
                    };
                }

                // 检查记录是否已存在
                var existingRecord = await GetExistingTxtRecord(domainId, recordName, secretId, secretKey);
                if (existingRecord != null && !string.IsNullOrEmpty(existingRecord.RecordId))
                {
                    // 更新现有记录
                    return await UpdateTxtRecord(domainId, existingRecord.RecordId!, recordName, recordValue, secretId, secretKey);
                }

                // 创建新记录
                var response = await CreateRecordRequest(domainId, recordName, recordValue, secretId, secretKey);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("创建腾讯云 DNS 记录失败: {StatusCode} - {Content}", response.StatusCode, responseContent);
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = $"创建 DNS 记录失败: {response.StatusCode}",
                        ErrorCode = "CREATE_FAILED",
                        Details = responseContent
                    };
                }

                var result = JsonSerializer.Deserialize<TencentDnsResponse>(responseContent);
                if (result?.Response?.Error == null && result?.Response != null)
                {
                    var recordId = result.Response!.RecordId ?? string.Empty;
                    _logger.LogInformation("腾讯云 DNS TXT 记录创建成功: {RecordName}", recordName);
                    return new DnsOperationResult
                    {
                        Success = true,
                        Message = "DNS 记录创建成功",
                        RecordId = recordId,
                        Details = $"记录 ID: {recordId}"
                    };
                }

                return new DnsOperationResult
                {
                    Success = false,
                    Message = $"创建 DNS 记录失败: {result?.Response?.Error?.Message}",
                    ErrorCode = result?.Response?.Error?.Code ?? "API_ERROR",
                    Details = responseContent
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建腾讯云 DNS 记录异常: {RecordName}", recordName);
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
                _logger.LogInformation("删除腾讯云 DNS TXT 记录: {RecordName}", recordName);

                var secretId = GetSecretId(credentials);
                var secretKey = GetSecretKey(credentials);

                if (string.IsNullOrEmpty(secretId) || string.IsNullOrEmpty(secretKey))
                {
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = "腾讯云 Secret 未提供",
                        ErrorCode = "MISSING_CREDENTIALS"
                    };
                }

                // 获取域名信息
                var domainName = ExtractRootDomain(domain);
                var domainId = await GetDomainId(domainName, secretId, secretKey);
                if (string.IsNullOrEmpty(domainId))
                {
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = $"无法获取域名 {domainName} 的 ID",
                        ErrorCode = "DOMAIN_NOT_FOUND"
                    };
                }

                // 查找现有记录
                var existingRecord = await GetExistingTxtRecord(domainId, recordName, secretId, secretKey);
                if (existingRecord == null)
                {
                    _logger.LogWarning("未找到要删除的 DNS 记录: {RecordName}", recordName);
                    return new DnsOperationResult
                    {
                        Success = true,
                        Message = "记录不存在，无需删除"
                    };
                }

                if (string.IsNullOrEmpty(existingRecord.RecordId))
                {
                    _logger.LogWarning("DNS 记录 ID 为空，无法删除: {RecordName}", recordName);
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = "记录 ID 为空，无法删除"
                    };
                }

                // 删除记录
                var response = await DeleteRecordRequest(domainId, existingRecord.RecordId!, secretId, secretKey);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("删除腾讯云 DNS 记录失败: {StatusCode} - {Content}", response.StatusCode, responseContent);
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = $"删除 DNS 记录失败: {response.StatusCode}",
                        ErrorCode = "DELETE_FAILED",
                        Details = responseContent
                    };
                }

                var result = JsonSerializer.Deserialize<TencentDnsResponse>(responseContent);
                if (result?.Response?.Error == null)
                {
                    _logger.LogInformation("腾讯云 DNS TXT 记录删除成功: {RecordName}", recordName);
                    return new DnsOperationResult
                    {
                        Success = true,
                        Message = "DNS 记录删除成功",
                        RecordId = existingRecord.RecordId
                    };
                }

                return new DnsOperationResult
                {
                    Success = false,
                    Message = $"删除 DNS 记录失败: {result.Response?.Error?.Message}",
                    ErrorCode = result.Response?.Error?.Code ?? "API_ERROR",
                    Details = responseContent
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除腾讯云 DNS 记录异常: {RecordName}", recordName);
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
                _logger.LogInformation("测试腾讯云 DNS 连接");

                var secretId = GetSecretId(credentials);
                var secretKey = GetSecretKey(credentials);

                if (string.IsNullOrEmpty(secretId) || string.IsNullOrEmpty(secretKey))
                {
                    return new DnsTestResult
                    {
                        Success = false,
                        Message = "腾讯云 Secret 未提供",
                        ErrorCode = "MISSING_CREDENTIALS"
                    };
                }

                // 测试获取域名列表
                var response = await DescribeDomainsRequest(secretId, secretKey);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("腾讯云 API 认证失败: {StatusCode} - {Content}", response.StatusCode, responseContent);
                    return new DnsTestResult
                    {
                        Success = false,
                        Message = $"API 认证失败: {response.StatusCode}",
                        ErrorCode = "AUTH_FAILED",
                        Details = responseContent
                    };
                }

                var result = JsonSerializer.Deserialize<TencentDomainsResponse>(responseContent);
                if (result?.Response?.Error == null && result?.Response != null)
                {
                    var domainCount = result.Response!.DomainCount ?? 0;
                    return new DnsTestResult
                    {
                        Success = true,
                        Message = "腾讯云 API 连接测试成功",
                        Details = $"找到 {domainCount} 个域名"
                    };
                }

                return new DnsTestResult
                {
                    Success = false,
                    Message = $"API 连接测试失败: {result?.Response?.Error?.Message}",
                    ErrorCode = result?.Response?.Error?.Code ?? "API_ERROR",
                    Details = responseContent
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "测试腾讯云 DNS 连接异常");
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

        private string? GetSecretId(Dictionary<string, object>? credentials)
        {
            return credentials?.GetValueOrDefault("secret_id")?.ToString()
                   ?? _configuration["DnsProviders:Tencent:SecretId"];
        }

        private string? GetSecretKey(Dictionary<string, object>? credentials)
        {
            return credentials?.GetValueOrDefault("secret_key")?.ToString()
                   ?? _configuration["DnsProviders:Tencent:SecretKey"];
        }

        private async Task<string?> GetDomainId(string domainName, string secretId, string secretKey)
        {
            try
            {
                var response = await DescribeDomainsRequest(secretId, secretKey);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("获取域名列表失败: {StatusCode} - {Content}", response.StatusCode, responseContent);
                    return null;
                }

                var result = JsonSerializer.Deserialize<TencentDomainsResponse>(responseContent);
                var domain = result?.Response?.DomainList?.FirstOrDefault(d => d.DomainName == domainName);

                return domain?.DomainId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取域名ID异常: {Domain}", domainName);
                return null;
            }
        }

        private async Task<TencentDnsRecord?> GetExistingTxtRecord(string domainId, string recordName, string secretId, string secretKey)
        {
            try
            {
                var response = await DescribeRecordListRequest(domainId, recordName, secretId, secretKey);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("查询现有 DNS 记录失败: {StatusCode} - {Content}", response.StatusCode, responseContent);
                    return null;
                }

                var result = JsonSerializer.Deserialize<TencentRecordsResponse>(responseContent);
                return result?.Response?.RecordList?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查询现有 DNS 记录异常: {RecordName}", recordName);
                return null;
            }
        }

        private async Task<DnsOperationResult> UpdateTxtRecord(string domainId, string recordId, string recordName, string recordValue, string secretId, string secretKey)
        {
            try
            {
                var response = await ModifyRecordRequest(domainId, recordId, recordName, recordValue, secretId, secretKey);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("更新腾讯云 DNS 记录失败: {StatusCode} - {Content}", response.StatusCode, responseContent);
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = $"更新 DNS 记录失败: {response.StatusCode}",
                        ErrorCode = "UPDATE_FAILED",
                        Details = responseContent
                    };
                }

                var result = JsonSerializer.Deserialize<TencentDnsResponse>(responseContent);
                if (result?.Response?.Error == null)
                {
                    _logger.LogInformation("腾讯云 DNS TXT 记录更新成功: {RecordName}", recordName);
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
                    Message = $"更新 DNS 记录失败: {result.Response?.Error?.Message}",
                    ErrorCode = result.Response?.Error?.Code ?? "API_ERROR",
                    Details = responseContent
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新腾讯云 DNS 记录异常: {RecordName}", recordName);
                return new DnsOperationResult
                {
                    Success = false,
                    Message = $"更新 DNS 记录异常: {ex.Message}",
                    ErrorCode = "EXCEPTION",
                    Details = ex.ToString()
                };
            }
        }

        private async Task<HttpResponseMessage> CreateRecordRequest(string domainId, string recordName, string recordValue, string secretId, string secretKey)
        {
            var requestBody = new
            {
                DomainId = domainId,
                SubDomain = ExtractSubDomain(recordName, domainId),
                RecordType = "TXT",
                RecordValue = recordValue,
                TTL = 120
            };

            return await SendRequestAsync("CreateRecord", requestBody, secretId, secretKey);
        }

        private async Task<HttpResponseMessage> DeleteRecordRequest(string domainId, string recordId, string secretId, string secretKey)
        {
            var requestBody = new
            {
                DomainId = domainId,
                RecordId = recordId
            };

            return await SendRequestAsync("DeleteRecord", requestBody, secretId, secretKey);
        }

        private async Task<HttpResponseMessage> ModifyRecordRequest(string domainId, string recordId, string recordName, string recordValue, string secretId, string secretKey)
        {
            var requestBody = new
            {
                DomainId = domainId,
                RecordId = recordId,
                SubDomain = ExtractSubDomain(recordName, domainId),
                RecordType = "TXT",
                RecordValue = recordValue,
                TTL = 120
            };

            return await SendRequestAsync("ModifyRecord", requestBody, secretId, secretKey);
        }

        private async Task<HttpResponseMessage> DescribeDomainsRequest(string secretId, string secretKey)
        {
            var requestBody = new { };
            return await SendRequestAsync("DescribeDomainList", requestBody, secretId, secretKey);
        }

        private async Task<HttpResponseMessage> DescribeRecordListRequest(string domainId, string recordName, string secretId, string secretKey)
        {
            var requestBody = new
            {
                DomainId = domainId,
                Subdomain = ExtractSubDomain(recordName, domainId),
                RecordType = "TXT"
            };

            return await SendRequestAsync("DescribeRecordList", requestBody, secretId, secretKey);
        }

        private async Task<HttpResponseMessage> SendRequestAsync(string action, object requestBody, string secretId, string secretKey)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var host = _httpClient.BaseAddress!.Host;

            // 🔧 修复：直接序列化请求参数，而不是包装在复杂结构中
            var jsonBody = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            // 生成签名 - 使用请求体内容
            var signature = GenerateTencentSignature(action, "2021-03-23", timestamp, jsonBody, secretKey, secretId, host);

            // 🔧 使用新的 HttpRequestMessage 避免共享 DefaultRequestHeaders 问题
            using var request = new HttpRequestMessage(HttpMethod.Post, "");
            request.Content = content;
            request.Headers.TryAddWithoutValidation("Host", host);
            request.Headers.TryAddWithoutValidation("X-TC-Action", action);
            request.Headers.TryAddWithoutValidation("X-TC-Version", "2021-03-23");
            request.Headers.TryAddWithoutValidation("X-TC-Timestamp", timestamp.ToString());
            request.Headers.TryAddWithoutValidation("X-TC-Region", "");
            request.Headers.TryAddWithoutValidation("Authorization", signature);

            _logger.LogInformation("腾讯云 DNS API 请求: Action={Action}, Timestamp={Timestamp}", action, timestamp);
            _logger.LogDebug("腾讯云 请求体: {Body}", jsonBody);

            return await _httpClient.SendAsync(request);
        }

        private string GenerateTencentSignature(string action, string version, long timestamp, string body, string secretKey, string secretId, string host)
        {
            // 完全按照腾讯云官方示例实现 TC3-HMAC-SHA256 签名
            var contentType = "application/json; charset=utf-8";
            var canonicalURI = "/";
            var canonicalHeaders = "content-type:" + contentType + "\nhost:" + host + "\n";
            var signedHeaders = "content-type;host";

            // 计算请求体哈希
            var hashedRequestPayload = Sha256Hex(body);
            var canonicalRequest = "POST" + "\n"
                                          + canonicalURI + "\n"
                                          + "\n"
                                          + canonicalHeaders + "\n"
                                          + signedHeaders + "\n"
                                          + hashedRequestPayload;

            var algorithm = "TC3-HMAC-SHA256";
            var date = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime.ToString("yyyy-MM-dd");
            var service = "cns"; // 腾讯云DNS服务名
            var credentialScope = date + "/" + service + "/" + "tc3_request";
            var hashedCanonicalRequest = Sha256Hex(canonicalRequest);
            var stringToSign = algorithm + "\n"
                                         + timestamp.ToString() + "\n"
                                         + credentialScope + "\n"
                                         + hashedCanonicalRequest;

            var tc3SecretKey = Encoding.UTF8.GetBytes("TC3" + secretKey);
            var secretDate = HMACSHA256(tc3SecretKey, Encoding.UTF8.GetBytes(date));
            var secretService = HMACSHA256(secretDate, Encoding.UTF8.GetBytes(service));
            var secretSigning = HMACSHA256(secretService, Encoding.UTF8.GetBytes("tc3_request"));
            var signatureBytes = HMACSHA256(secretSigning, Encoding.UTF8.GetBytes(stringToSign));
            var signature = BitConverter.ToString(signatureBytes).Replace("-", "").ToLower();

            return algorithm + " "
                             + "Credential=" + secretId + "/" + credentialScope + ", "
                             + "SignedHeaders=" + signedHeaders + ", "
                             + "Signature=" + signature;
        }

        private static string Sha256Hex(string s)
        {
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
            var builder = new StringBuilder();
            foreach (var b in hashBytes)
            {
                builder.Append(b.ToString("x2"));
            }
            return builder.ToString();
        }

        private byte[] HMACSHA256(byte[] key, byte[] message)
        {
            using var hmac = new HMACSHA256(key);
            return hmac.ComputeHash(message);
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
            // 🔧 修复：ACME challenge 记录的格式是 _acme-challenge.subdomain.domain.com
            // 对于根域名证书：_acme-challenge.example.com -> 子域名应为 _acme-challenge
            // 对于子域名证书：_acme-challenge.www.example.com -> 子域名应为 _acme-challenge.www
            if (recordName.StartsWith("_acme-challenge", StringComparison.OrdinalIgnoreCase))
            {
                // 提取 _acme-challenge 和可能的子域名部分
                var parts = recordName.Split('.').ToList();
                if (parts.Count >= 3)
                {
                    // 移除根域名部分，保留腾讯云需要的主机记录。
                    parts.RemoveRange(parts.Count - 2, 2);
                    return string.Join(".", parts);
                }
                else
                {
                    return "_acme-challenge";
                }
            }
            return recordName;
        }

        /// <summary>
        /// 删除所有匹配名称的 DNS TXT 记录（默认实现：逐个删除）
        /// </summary>
        public async Task<DnsOperationResult> DeleteAllTxtRecordsByNameAsync(string domain, string recordName, Dictionary<string, object>? credentials)
        {
            try
            {
                _logger.LogInformation("腾讯云: 删除所有匹配的 DNS TXT 记录: {RecordName}", recordName);

                var secretId = GetSecretId(credentials);
                var secretKey = GetSecretKey(credentials);

                if (string.IsNullOrEmpty(secretId) || string.IsNullOrEmpty(secretKey))
                {
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = "缺少腾讯云 API 凭证",
                        ErrorCode = "MISSING_CREDENTIALS"
                    };
                }

                var domainName = ExtractRootDomain(domain);
                var domainId = await GetDomainId(domainName, secretId, secretKey);

                if (string.IsNullOrEmpty(domainId))
                {
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = $"无法获取域名 {domainName} 的 ID",
                        ErrorCode = "DOMAIN_NOT_FOUND"
                    };
                }

                // 获取所有匹配的 TXT 记录
                var records = await ListTxtRecords(domainId, domainName, recordName, secretId, secretKey);
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
                    if (!string.IsNullOrEmpty(record.RecordId))
                    {
                        var deleteResult = await DeleteRecord(domainId, record.RecordId, secretId, secretKey);
                        if (deleteResult)
                        {
                            deletedCount++;
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "腾讯云删除所有 DNS TXT 记录时发生错误: {RecordName}", recordName);
                return new DnsOperationResult
                {
                    Success = false,
                    Message = $"删除 DNS TXT 记录时发生错误: {ex.Message}",
                    ErrorCode = "EXCEPTION"
                };
            }
        }

        /// <summary>
        /// 列出所有 TXT 记录
        /// </summary>
        private async Task<List<TencentDnsRecord>> ListTxtRecords(string domainId, string domainName, string recordName, string secretId, string secretKey)
        {
            try
            {
                var subDomain = ExtractSubDomain(recordName, domainId);
                var response = await DescribeRecordListRequest(domainId, subDomain, secretId, secretKey);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("查询现有 DNS 记录失败: {StatusCode} - {Content}", response.StatusCode, responseContent);
                    return new List<TencentDnsRecord>();
                }

                var result = JsonSerializer.Deserialize<TencentRecordsResponse>(responseContent);
                return result?.Response?.RecordList?.Where(r => r.RecordType == "TXT").ToList() ?? new List<TencentDnsRecord>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "列出 TXT 记录时发生错误: {RecordName}", recordName);
                return new List<TencentDnsRecord>();
            }
        }

        /// <summary>
        /// 删除 DNS 记录
        /// </summary>
        private async Task<bool> DeleteRecord(string domainId, string recordId, string secretId, string secretKey)
        {
            try
            {
                var response = await DeleteRecordRequest(domainId, recordId, secretId, secretKey);
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

    #region 腾讯云 API 响应模型

    public class TencentDnsResponse
    {
        public TencentResponse? Response { get; set; }
    }

    public class TencentResponse
    {
        public string? RequestId { get; set; }
        public string? RecordId { get; set; }
        public TencentError? Error { get; set; }
    }

    public class TencentDomainsResponse
    {
        public TencentDomainsResponseData? Response { get; set; }
    }

    public class TencentDomainsResponseData
    {
        public string? RequestId { get; set; }
        public int? DomainCount { get; set; }
        public List<TencentDomain>? DomainList { get; set; }
        public TencentError? Error { get; set; }
    }

    public class TencentRecordsResponse
    {
        public TencentRecordsResponseData? Response { get; set; }
    }

    public class TencentRecordsResponseData
    {
        public string? RequestId { get; set; }
        public int? TotalCount { get; set; }
        public List<TencentDnsRecord>? RecordList { get; set; }
        public TencentError? Error { get; set; }
    }

    public class TencentDomain
    {
        public string? DomainId { get; set; }
        public string? DomainName { get; set; }
        public string? Status { get; set; }
        public bool IsDefault { get; set; }
    }

    public class TencentDnsRecord
    {
        public string? RecordId { get; set; }
        public string? SubDomain { get; set; }
        public string? RecordType { get; set; }
        public string? RecordValue { get; set; }
        public int? TTL { get; set; }
        public int? Weight { get; set; }
        public string? Status { get; set; }
        public bool? Mx { get; set; }
    }

    public class TencentError
    {
        public string? Code { get; set; }
        public string? Message { get; set; }
    }

    #endregion
}