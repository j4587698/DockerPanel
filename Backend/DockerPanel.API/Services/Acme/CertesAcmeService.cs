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
    /// <summary>
    /// 基于 Certes 库的真实 ACME 协议实现
    /// </summary>
    public partial class CertesAcmeService : IAcmeService
    {
        private readonly ILogger<CertesAcmeService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHubContext<DockerPanelHub> _hubContext;
        private readonly ICertificateProgressService _progressService;
        private readonly IAcmeChallengeStore _challengeStore;
        private readonly DataBaseService _dataBaseService;
        private readonly AcmeJobQueueService _jobQueue;
        private readonly Dictionary<string, IAcmeContext> _acmeContexts;
        private readonly Dictionary<string, IKey> _accountKeys;
        private readonly TinyDbContext _dbContext;
        private readonly Dictionary<string, IDnsProvider> _dnsProviders;
        private readonly TlsAlpnChallengeService _tlsAlpnChallengeService;
        private readonly SniCertificateSelector _sniCertificateSelector;

        // 使用静态字典来跨请求保持ACME上下文
        private static readonly ConcurrentDictionary<string, IAcmeContext> _staticAcmeContexts = new();
        private static readonly ConcurrentDictionary<string, IKey> _staticAccountKeys = new();

        public CertesAcmeService(
            ILogger<CertesAcmeService> logger,
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
            _dnsProviders = new Dictionary<string, IDnsProvider>(StringComparer.OrdinalIgnoreCase)
            {
                ["cloudflare"] = cloudflareProvider,
                ["aliyun"] = aliyunProvider,
                ["tencent"] = tencentProvider,
                ["dnspod"] = dnspodProvider,
                ["dnspod-traditional"] = dnspodTraditionalProvider
            };

            _acmeContexts = new Dictionary<string, IAcmeContext>();
            _accountKeys = new Dictionary<string, IKey>();
        }

    }
}
