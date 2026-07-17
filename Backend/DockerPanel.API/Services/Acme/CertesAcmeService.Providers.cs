using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using DockerPanel.API.Models.Acme;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http;
using Microsoft.AspNetCore.SignalR;
using DockerPanel.API.Hubs;
using DockerPanel.API.Data;
using DockerPanel.API.Services.Acme.DnsProviders;
using TinyDb;
using TinyDb.Bson;
using TinyDb.Core;
using TinyDb.Collections;
using DnsClient;
using DockerPanel.API.Services;

namespace DockerPanel.API.Services.Acme
{
    public partial class CertesAcmeService
    {

        public async Task<IEnumerable<AcmeProvider>> GetProvidersAsync()
        {
            return await Task.FromResult(new List<AcmeProvider>
            {
                new AcmeProvider
                {
                    Name = "letsencrypt",
                    DisplayName = "Let's Encrypt",
                    DirectoryUrl = "https://acme-v02.api.letsencrypt.org/directory",
                    IsProduction = false,
                    IsStaging = false,
                    SupportedChallengeTypes = new List<string> { "http-01", "dns-01" },
                    Description = "免费、自动化、开放的证书颁发机构"
                },

                new AcmeProvider
                {
                    Name = "zerossl",
                    DisplayName = "ZeroSSL",
                    DirectoryUrl = "https://acme.zerossl.com/v2/DV90",
                    IsProduction = false,
                    IsStaging = false,
                    SupportedChallengeTypes = new List<string> { "http-01", "dns-01" },
                    Description = "提供免费 SSL 证书的服务商"
                }
            });
        }

        public async Task<AcmeConnectionTestResult> TestProviderConnectionAsync(string provider)
        {
            try
            {
                var directoryUrl = GetDirectoryUrl(provider);
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);
                using var response = await httpClient.GetAsync(directoryUrl);
                stopwatch.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    return new AcmeConnectionTestResult
                    {
                        Success = false,
                        Message = $"连接失败: {(int)response.StatusCode} {response.ReasonPhrase}",
                        Provider = provider,
                        DirectoryUrl = directoryUrl,
                        ResponseTime = stopwatch.Elapsed,
                        Version = "ACMEv2"
                    };
                }

                return new AcmeConnectionTestResult
                {
                    Success = true,
                    Message = "连接成功",
                    Provider = provider,
                    DirectoryUrl = directoryUrl,
                    ResponseTime = stopwatch.Elapsed,
                    Version = "ACMEv2",
                    SupportedChallengeTypes = new List<string> { "http-01", "dns-01" }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "测试 ACME 提供商连接失败: {Provider}", provider);
                return new AcmeConnectionTestResult
                {
                    Success = false,
                    Message = "连接失败: " + ex.Message,
                    Provider = provider,
                    DirectoryUrl = GetDirectoryUrl(provider),
                    ResponseTime = TimeSpan.Zero,
                    Version = "ACMEv2"
                };
            }
        }


    }
}
