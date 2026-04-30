using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DockerPanel.API.Models.Acme;
using DockerPanel.API.Services;
using DockerPanel.API.Services.Acme;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DockerPanel.API.Controllers
{
    /// <summary>
    /// ACME挑战验证控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChallengeValidationController : ControllerBase
    {
        private readonly IChallengeValidationService _challengeValidationService;
        private readonly ILogger<ChallengeValidationController> _logger;
        private readonly ILocalizationService _localization;

        public ChallengeValidationController(
            IChallengeValidationService challengeValidationService,
            ILogger<ChallengeValidationController> logger,
            ILocalizationService localization)
        {
            _challengeValidationService = challengeValidationService;
            _logger = logger;
            _localization = localization;
        }

        /// <summary>
        /// 配置HTTP-01挑战验证
        /// </summary>
        [HttpPost("http/configure")]
        public async Task<ActionResult<ChallengeValidationResult>> ConfigureHttpChallengeAsync([FromBody] ConfigureHttpChallengeRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var challenge = new AcmeChallenge
                {
                    Type = "http-01",
                    Token = request.Token,
                    KeyAuthorization = request.KeyAuthorization ?? string.Empty,
                    Url = request.Url ?? string.Empty
                };

                var result = await _challengeValidationService.ConfigureHttpChallengeAsync(challenge, request.Domain);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "配置HTTP-01挑战失败: {Domain}", request?.Domain);
                return StatusCode(500, new { message = _localization.GetMessage("challenge.http01ConfigFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 验证HTTP-01挑战
        /// </summary>
        [HttpPost("http/validate")]
        public async Task<ActionResult<ChallengeValidationResult>> ValidateHttpChallengeAsync([FromBody] ValidateHttpChallengeRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var challenge = new AcmeChallenge
                {
                    Type = "http-01",
                    Token = request.Token,
                    KeyAuthorization = request.KeyAuthorization ?? string.Empty,
                    Url = request.Url ?? string.Empty
                };

                var result = await _challengeValidationService.ValidateHttpChallengeAsync(challenge, request.Domain);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "验证HTTP-01挑战失败: {Domain}", request?.Domain);
                return StatusCode(500, new { message = _localization.GetMessage("challenge.http01ValidateFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 配置DNS-01挑战验证
        /// </summary>
        [HttpPost("dns/configure")]
        public async Task<ActionResult<ChallengeValidationResult>> ConfigureDnsChallengeAsync([FromBody] ConfigureDnsChallengeRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var challenge = new AcmeChallenge
                {
                    Type = "dns-01",
                    Token = request.Token,
                    KeyAuthorization = request.KeyAuthorization ?? string.Empty,
                    Url = request.Url ?? string.Empty
                };

                var result = await _challengeValidationService.ConfigureDnsChallengeAsync(
                    challenge, request.Domain, request.DnsProvider, request.Credentials);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "配置DNS-01挑战失败: {Domain}, Provider: {Provider}",
                    request?.Domain, request?.DnsProvider);
                return StatusCode(500, new { message = _localization.GetMessage("challenge.dns01ConfigFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 验证DNS-01挑战
        /// </summary>
        [HttpPost("dns/validate")]
        public async Task<ActionResult<ChallengeValidationResult>> ValidateDnsChallengeAsync([FromBody] ValidateDnsChallengeRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var challenge = new AcmeChallenge
                {
                    Type = "dns-01",
                    Token = request.Token,
                    KeyAuthorization = request.KeyAuthorization ?? string.Empty,
                    Url = request.Url ?? string.Empty
                };

                var result = await _challengeValidationService.ValidateDnsChallengeAsync(
                    challenge, request.Domain, request.DnsProvider, request.Credentials);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "验证DNS-01挑战失败: {Domain}, Provider: {Provider}",
                    request?.Domain, request?.DnsProvider);
                return StatusCode(500, new { message = _localization.GetMessage("challenge.dns01ValidateFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 配置TLS-ALPN-01挑战验证
        /// </summary>
        [HttpPost("tls-alpn/configure")]
        public async Task<ActionResult<ChallengeValidationResult>> ConfigureTlsAlpnChallengeAsync([FromBody] ConfigureTlsAlpnChallengeRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var challenge = new AcmeChallenge
                {
                    Type = "tls-alpn-01",
                    Token = request.Token,
                    KeyAuthorization = request.KeyAuthorization ?? string.Empty,
                    Url = request.Url ?? string.Empty
                };

                var result = await _challengeValidationService.ConfigureTlsAlpnChallengeAsync(challenge, request.Domain);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "配置TLS-ALPN-01挑战失败: {Domain}", request?.Domain);
                return StatusCode(500, new { message = _localization.GetMessage("challenge.tlsAlpn01ConfigFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 验证TLS-ALPN-01挑战
        /// </summary>
        [HttpPost("tls-alpn/validate")]
        public async Task<ActionResult<ChallengeValidationResult>> ValidateTlsAlpnChallengeAsync([FromBody] ValidateTlsAlpnChallengeRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var challenge = new AcmeChallenge
                {
                    Type = "tls-alpn-01",
                    Token = request.Token,
                    KeyAuthorization = request.KeyAuthorization ?? string.Empty,
                    Url = request.Url ?? string.Empty
                };

                var result = await _challengeValidationService.ValidateTlsAlpnChallengeAsync(challenge, request.Domain);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "验证TLS-ALPN-01挑战失败: {Domain}", request?.Domain);
                return StatusCode(500, new { message = _localization.GetMessage("challenge.tlsAlpn01ValidateFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 清理挑战验证配置
        /// </summary>
        [HttpPost("cleanup")]
        public async Task<ActionResult<ChallengeCleanupResult>> CleanupChallengeAsync([FromBody] CleanupChallengeRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var challenge = new AcmeChallenge
                {
                    Type = request.ChallengeType,
                    Token = request.Token,
                    KeyAuthorization = request.KeyAuthorization ?? string.Empty,
                    Url = request.Url ?? string.Empty
                };

                var result = await _challengeValidationService.CleanupChallengeAsync(challenge, request.Domain, request.ChallengeType);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理挑战失败: {Domain}, Type: {Type}", request?.Domain, request?.ChallengeType);
                return StatusCode(500, new { message = _localization.GetMessage("challenge.cleanupFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 获取挑战配置状态
        /// </summary>
        [HttpGet("status/{challengeId}")]
        public async Task<ActionResult<ChallengeStatus>> GetChallengeStatusAsync(string challengeId)
        {
            try
            {
                var status = await _challengeValidationService.GetChallengeStatusAsync(challengeId);
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取挑战状态失败: {ChallengeId}", challengeId);
                return StatusCode(500, new { message = _localization.GetMessage("challenge.getStatusFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 获取支持的DNS提供商列表
        /// </summary>
        [HttpGet("dns-providers")]
        public async Task<ActionResult<IEnumerable<DnsProvider>>> GetSupportedDnsProvidersAsync()
        {
            try
            {
                var providers = await _challengeValidationService.GetSupportedDnsProvidersAsync();
                return Ok(providers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取DNS提供商列表失败");
                return StatusCode(500, new { message = _localization.GetMessage("challenge.dnsProvidersFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 测试DNS提供商连接
        /// </summary>
        [HttpPost("dns-providers/{provider}/test")]
        public async Task<ActionResult<DnsProviderTestResult>> TestDnsProviderConnectionAsync(
            string provider, [FromBody] TestDnsProviderRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _challengeValidationService.TestDnsProviderConnectionAsync(provider, request.Credentials);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "测试DNS提供商连接失败: {Provider}", provider);
                return StatusCode(500, new { message = _localization.GetMessage("challenge.dnsProviderTestFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 自动配置挑战验证
        /// </summary>
        [HttpPost("auto-configure")]
        public async Task<ActionResult<AutoChallengeResult>> AutoConfigureChallengeAsync([FromBody] AutoConfigureChallengeRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var challenge = new AcmeChallenge
                {
                    Type = request.ChallengeType,
                    Token = request.Token,
                    KeyAuthorization = request.KeyAuthorization ?? string.Empty,
                    Url = request.Url ?? string.Empty
                };

                var result = await _challengeValidationService.AutoConfigureChallengeAsync(
                    challenge, request.Domain, request.PreferredChallengeTypes, request.DnsCredentials);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "自动配置挑战失败: {Domain}", request?.Domain);
                return StatusCode(500, new { message = _localization.GetMessage("challenge.autoConfigFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 监控挑战验证状态（Server-Sent Events）
        /// </summary>
        [HttpGet("monitor/{challengeId}")]
        public async Task MonitorChallengeStatusAsync(string challengeId, CancellationToken cancellationToken)
        {
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Connection"] = "keep-alive";
            Response.Headers["Content-Type"] = "text/event-stream";

            try
            {
                await foreach (var update in _challengeValidationService.MonitorChallengeStatusAsync(challengeId, cancellationToken))
                {
                    var eventData = $"data: {System.Text.Json.JsonSerializer.Serialize(update)}\n\n";
                    await Response.WriteAsync(eventData, cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("挑战监控已取消: {ChallengeId}", challengeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "挑战监控出错: {ChallengeId}", challengeId);
                var errorUpdate = new ChallengeStatusUpdate
                {
                    ChallengeId = challengeId,
                    Status = "error",
                    Timestamp = DateTime.UtcNow,
                    Message = $"监控出错: {ex.Message}"
                };
                var eventData = $"data: {System.Text.Json.JsonSerializer.Serialize(errorUpdate)}\n\n";
                await Response.WriteAsync(eventData);
                await Response.Body.FlushAsync();
            }
        }

        /// <summary>
        /// 批量清理挑战
        /// </summary>
        [HttpPost("batch-cleanup")]
        public async Task<ActionResult<IEnumerable<ChallengeCleanupResult>>> BatchCleanupChallengesAsync([FromBody] BatchCleanupChallengesRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var results = new List<ChallengeCleanupResult>();

                foreach (var challengeInfo in request.Challenges)
                {
                    try
                    {
                        var challenge = new AcmeChallenge
                        {
                            Type = challengeInfo.ChallengeType,
                            Token = challengeInfo.Token,
                            KeyAuthorization = challengeInfo.KeyAuthorization,
                            Url = challengeInfo.Url ?? string.Empty
                        };

                        var result = await _challengeValidationService.CleanupChallengeAsync(
                            challenge, challengeInfo.Domain, challengeInfo.ChallengeType);
                        results.Add(result);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "批量清理挑战失败: {Domain}, Type: {Type}",
                            challengeInfo.Domain, challengeInfo.ChallengeType);

                        results.Add(new ChallengeCleanupResult
                        {
                            Success = false,
                            ChallengeType = challengeInfo.ChallengeType,
                            Domain = challengeInfo.Domain,
                            Message = $"清理失败: {ex.Message}",
                            CleanedAt = DateTime.UtcNow,
                            Errors = new List<string> { ex.Message }
                        });
                    }
                }

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量清理挑战失败");
                return StatusCode(500, new { message = _localization.GetMessage("challenge.batchCleanupFailed"), error = ex.Message });
            }
        }

        /// <summary>
        /// 获取挑战验证统计信息
        /// </summary>
        [HttpGet("stats")]
        public async Task<ActionResult<ChallengeValidationStats>> GetChallengeValidationStatsAsync()
        {
            try
            {
                // 实现挑战验证统计逻辑
                var stats = new ChallengeValidationStats
                {
                    TotalChallenges = 0,
                    SuccessfulChallenges = 0,
                    FailedChallenges = 0,
                    PendingChallenges = 0,
                    ChallengeTypeStats = new Dictionary<string, int>
                    {
                        ["http-01"] = 0,
                        ["dns-01"] = 0,
                        ["tls-alpn-01"] = 0
                    },
                    DnsProviderStats = new Dictionary<string, int>
                    {
                        ["cloudflare"] = 0,
                        ["aliyun"] = 0,
                        ["tencent"] = 0
                    },
                    LastUpdated = DateTime.UtcNow
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取挑战验证统计失败");
                return StatusCode(500, new { message = _localization.GetMessage("challenge.statsFailed"), error = ex.Message });
            }
        }
    }

    // 请求模型
    public class ConfigureHttpChallengeRequest
    {
        public string Domain { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string KeyAuthorization { get; set; } = string.Empty;
        public string? Url { get; set; }
    }

    public class ValidateHttpChallengeRequest
    {
        public string Domain { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string KeyAuthorization { get; set; } = string.Empty;
        public string? Url { get; set; }
    }

    public class ConfigureDnsChallengeRequest
    {
        public string Domain { get; set; } = string.Empty;
        public string DnsProvider { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string KeyAuthorization { get; set; } = string.Empty;
        public Dictionary<string, object>? Credentials { get; set; }
        public string? Url { get; set; }
    }

    public class ValidateDnsChallengeRequest
    {
        public string Domain { get; set; } = string.Empty;
        public string DnsProvider { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string KeyAuthorization { get; set; } = string.Empty;
        public Dictionary<string, object>? Credentials { get; set; }
        public string? Url { get; set; }
    }

    public class ConfigureTlsAlpnChallengeRequest
    {
        public string Domain { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string KeyAuthorization { get; set; } = string.Empty;
        public string? Url { get; set; }
    }

    public class ValidateTlsAlpnChallengeRequest
    {
        public string Domain { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string KeyAuthorization { get; set; } = string.Empty;
        public string? Url { get; set; }
    }

    public class CleanupChallengeRequest
    {
        public string Domain { get; set; } = string.Empty;
        public string ChallengeType { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string KeyAuthorization { get; set; } = string.Empty;
        public string? Url { get; set; }
    }

    public class TestDnsProviderRequest
    {
        public Dictionary<string, object>? Credentials { get; set; }
    }

    public class AutoConfigureChallengeRequest
    {
        public string Domain { get; set; } = string.Empty;
        public string ChallengeType { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string KeyAuthorization { get; set; } = string.Empty;
        public List<string>? PreferredChallengeTypes { get; set; }
        public Dictionary<string, Dictionary<string, object>>? DnsCredentials { get; set; }
        public string? Url { get; set; }
    }

    public class ChallengeInfo
    {
        public string Domain { get; set; } = string.Empty;
        public string ChallengeType { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string KeyAuthorization { get; set; } = string.Empty;
        public string? Url { get; set; }
    }

    public class BatchCleanupChallengesRequest
    {
        public List<ChallengeInfo> Challenges { get; set; } = new();
    }

    public class ChallengeValidationStats
    {
        public int TotalChallenges { get; set; }
        public int SuccessfulChallenges { get; set; }
        public int FailedChallenges { get; set; }
        public int PendingChallenges { get; set; }
        public Dictionary<string, int> ChallengeTypeStats { get; set; } = new();
        public Dictionary<string, int> DnsProviderStats { get; set; } = new();
        public DateTime LastUpdated { get; set; }
    }
}