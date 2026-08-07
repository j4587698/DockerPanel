using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using AcmeForge;
using AcmeForge.Dns;
using DockerPanel.API.Models.Acme;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http;
using Microsoft.AspNetCore.SignalR;
using DockerPanel.API.Hubs;
using DockerPanel.API.Data;
using TinyDb;
using TinyDb.Bson;
using TinyDb.Core;
using TinyDb.Collections;
using DnsClient;
using DockerPanel.API.Services;

namespace DockerPanel.API.Services.Acme
{
    /// <summary>
    /// 基于 AcmeForge 库的真实 ACME 协议实现（RFC 8555，原生 AOT 兼容）
    /// </summary>
    public partial class AcmeForgeAcmeService : IAcmeService
    {
        private readonly ILogger<AcmeForgeAcmeService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHubContext<DockerPanelHub> _hubContext;
        private readonly ICertificateProgressService _progressService;
        private readonly IAcmeChallengeStore _challengeStore;
        private readonly DataBaseService _dataBaseService;
        private readonly AcmeJobQueueService _jobQueue;
        private readonly Dictionary<string, AcmeClient> _clients;
        private readonly Dictionary<string, AcmeKey> _accountKeys;
        private readonly TinyDbContext _dbContext;
        private readonly Dictionary<string, AcmeForge.Dns.IDnsProvider> _dnsProviders;
        private readonly TlsAlpnChallengeService _tlsAlpnChallengeService;
        private readonly SniCertificateSelector _sniCertificateSelector;

        // 使用静态字典来跨请求保持ACME客户端缓存
        private static readonly ConcurrentDictionary<string, AcmeClient> _staticClients = new();
        private static readonly ConcurrentDictionary<string, AcmeKey> _staticAccountKeys = new();

        public AcmeForgeAcmeService(
            ILogger<AcmeForgeAcmeService> logger,
            IHttpClientFactory httpClientFactory,
            IHubContext<DockerPanelHub> hubContext,
            ICertificateProgressService progressService,
            IAcmeChallengeStore challengeStore,
            DataBaseService dataBaseService,
            AcmeJobQueueService jobQueue,
            TinyDbContext dbContext,
            CloudflareDnsProvider cloudflareProvider,
            AliyunDnsProvider aliyunProvider,
            TencentDnsProvider tencentProvider,
            DnsPodDnsProvider dnspodProvider,
            DnsPodTraditionalDnsProvider dnspodTraditionalProvider,
            AwsRoute53DnsProvider awsProvider,
            AzureDnsProvider azureProvider,
            GoDaddyDnsProvider godaddyProvider,
            TlsAlpnChallengeService tlsAlpnChallengeService,
            SniCertificateSelector sniCertificateSelector)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _hubContext = hubContext;
            _progressService = progressService;
            _challengeStore = challengeStore;
            _dataBaseService = dataBaseService;
            _jobQueue = jobQueue;
            _dbContext = dbContext;
            _tlsAlpnChallengeService = tlsAlpnChallengeService;
            _sniCertificateSelector = sniCertificateSelector;

            // 初始化DNS提供商字典
            _dnsProviders = new Dictionary<string, AcmeForge.Dns.IDnsProvider>(StringComparer.OrdinalIgnoreCase)
            {
                ["cloudflare"] = cloudflareProvider,
                ["aliyun"] = aliyunProvider,
                ["tencent"] = tencentProvider,
                ["dnspod"] = dnspodProvider,
                ["dnspod-traditional"] = dnspodTraditionalProvider,
                ["aws"] = awsProvider,
                ["azure"] = azureProvider,
                ["godaddy"] = godaddyProvider
            };

            _clients = new Dictionary<string, AcmeClient>();
            _accountKeys = new Dictionary<string, AcmeKey>();
        }

        private AcmeClient CreateAcmeClient(string directoryUrl, AcmeKey accountKey)
        {
            var httpClient = _httpClientFactory.CreateClient();
            return new AcmeClient(httpClient, new Uri(directoryUrl), accountKey);
        }

    }
}