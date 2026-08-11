using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DockerPanel.API.Models.Acme;
using DockerPanel.API.Serialization;
using DockerPanel.API.Services;
using DockerPanel.API.Services.Acme;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// ACME挑战验证 Minimal API 端点（原 ChallengeValidationController）。
    /// </summary>
    public static class ChallengeValidationEndpoints
    {
        /// <summary>
        /// 日志类别标记（静态类不能作泛型参数）。
        /// </summary>
        public sealed class LoggingTag
        {
        }

        /// <summary>
        /// 映射ACME挑战验证相关路由。
        /// </summary>
        public static IEndpointRouteBuilder MapChallengeValidationEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/challengevalidation").RequireAuthorization();

            group.MapPost("http/configure", ConfigureHttpChallenge);
            group.MapPost("http/validate", ValidateHttpChallenge);
            group.MapPost("dns/configure", ConfigureDnsChallenge);
            group.MapPost("dns/validate", ValidateDnsChallenge);
            group.MapPost("tls-alpn/configure", ConfigureTlsAlpnChallenge);
            group.MapPost("tls-alpn/validate", ValidateTlsAlpnChallenge);
            group.MapPost("cleanup", CleanupChallenge);
            group.MapGet("status/{challengeId}", GetChallengeStatus);
            group.MapGet("dns-providers", GetSupportedDnsProviders);
            group.MapPost("dns-providers/{provider}/test", TestDnsProviderConnection);
            group.MapPost("auto-configure", AutoConfigureChallenge);
            group.MapGet("monitor/{challengeId}", MonitorChallengeStatus);
            group.MapPost("batch-cleanup", BatchCleanupChallenges);
            group.MapGet("stats", GetChallengeValidationStats);

            return app;
        }

        private static async Task<IResult> ConfigureHttpChallenge(
            ConfigureHttpChallengeRequest request,
            IChallengeValidationService challengeValidationService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                var challenge = new AcmeChallenge
                {
                    Type = "http-01",
                    Token = request.Token,
                    KeyAuthorization = request.KeyAuthorization ?? string.Empty,
                    Url = request.Url ?? string.Empty
                };

                var result = await challengeValidationService.ConfigureHttpChallengeAsync(challenge, request.Domain);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "配置HTTP-01挑战失败: {Domain}", request?.Domain);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("challenge.http01ConfigFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ValidateHttpChallenge(
            ValidateHttpChallengeRequest request,
            IChallengeValidationService challengeValidationService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                var challenge = new AcmeChallenge
                {
                    Type = "http-01",
                    Token = request.Token,
                    KeyAuthorization = request.KeyAuthorization ?? string.Empty,
                    Url = request.Url ?? string.Empty
                };

                var result = await challengeValidationService.ValidateHttpChallengeAsync(challenge, request.Domain);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "验证HTTP-01挑战失败: {Domain}", request?.Domain);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("challenge.http01ValidateFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ConfigureDnsChallenge(
            ConfigureDnsChallengeRequest request,
            IChallengeValidationService challengeValidationService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                var challenge = new AcmeChallenge
                {
                    Type = "dns-01",
                    Token = request.Token,
                    KeyAuthorization = request.KeyAuthorization ?? string.Empty,
                    Url = request.Url ?? string.Empty
                };

                var result = await challengeValidationService.ConfigureDnsChallengeAsync(
                    challenge, request.Domain, request.DnsProvider, request.Credentials);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "配置DNS-01挑战失败: {Domain}, Provider: {Provider}",
                    request?.Domain, request?.DnsProvider);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("challenge.dns01ConfigFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ValidateDnsChallenge(
            ValidateDnsChallengeRequest request,
            IChallengeValidationService challengeValidationService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                var challenge = new AcmeChallenge
                {
                    Type = "dns-01",
                    Token = request.Token,
                    KeyAuthorization = request.KeyAuthorization ?? string.Empty,
                    Url = request.Url ?? string.Empty
                };

                var result = await challengeValidationService.ValidateDnsChallengeAsync(
                    challenge, request.Domain, request.DnsProvider, request.Credentials);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "验证DNS-01挑战失败: {Domain}, Provider: {Provider}",
                    request?.Domain, request?.DnsProvider);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("challenge.dns01ValidateFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ConfigureTlsAlpnChallenge(
            ConfigureTlsAlpnChallengeRequest request,
            IChallengeValidationService challengeValidationService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                var challenge = new AcmeChallenge
                {
                    Type = "tls-alpn-01",
                    Token = request.Token,
                    KeyAuthorization = request.KeyAuthorization ?? string.Empty,
                    Url = request.Url ?? string.Empty
                };

                var result = await challengeValidationService.ConfigureTlsAlpnChallengeAsync(challenge, request.Domain);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "配置TLS-ALPN-01挑战失败: {Domain}", request?.Domain);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("challenge.tlsAlpn01ConfigFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ValidateTlsAlpnChallenge(
            ValidateTlsAlpnChallengeRequest request,
            IChallengeValidationService challengeValidationService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                var challenge = new AcmeChallenge
                {
                    Type = "tls-alpn-01",
                    Token = request.Token,
                    KeyAuthorization = request.KeyAuthorization ?? string.Empty,
                    Url = request.Url ?? string.Empty
                };

                var result = await challengeValidationService.ValidateTlsAlpnChallengeAsync(challenge, request.Domain);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "验证TLS-ALPN-01挑战失败: {Domain}", request?.Domain);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("challenge.tlsAlpn01ValidateFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> CleanupChallenge(
            CleanupChallengeRequest request,
            IChallengeValidationService challengeValidationService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                var challenge = new AcmeChallenge
                {
                    Type = request.ChallengeType,
                    Token = request.Token,
                    KeyAuthorization = request.KeyAuthorization ?? string.Empty,
                    Url = request.Url ?? string.Empty
                };

                var result = await challengeValidationService.CleanupChallengeAsync(challenge, request.Domain, request.ChallengeType);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "清理挑战失败: {Domain}, Type: {Type}", request?.Domain, request?.ChallengeType);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("challenge.cleanupFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetChallengeStatus(
            string challengeId,
            IChallengeValidationService challengeValidationService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                var status = await challengeValidationService.GetChallengeStatusAsync(challengeId);
                return TypedResults.Ok(status);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取挑战状态失败: {ChallengeId}", challengeId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("challenge.getStatusFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetSupportedDnsProviders(
            IChallengeValidationService challengeValidationService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                var providers = await challengeValidationService.GetSupportedDnsProvidersAsync();
                return TypedResults.Ok(providers);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取DNS提供商列表失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("challenge.dnsProvidersFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> TestDnsProviderConnection(
            string provider,
            TestDnsProviderRequest request,
            IChallengeValidationService challengeValidationService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                var result = await challengeValidationService.TestDnsProviderConnectionAsync(provider, request.Credentials);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "测试DNS提供商连接失败: {Provider}", provider);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("challenge.dnsProviderTestFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> AutoConfigureChallenge(
            AutoConfigureChallengeRequest request,
            IChallengeValidationService challengeValidationService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                var challenge = new AcmeChallenge
                {
                    Type = request.ChallengeType,
                    Token = request.Token,
                    KeyAuthorization = request.KeyAuthorization ?? string.Empty,
                    Url = request.Url ?? string.Empty
                };

                var result = await challengeValidationService.AutoConfigureChallengeAsync(
                    challenge, request.Domain, request.PreferredChallengeTypes, request.DnsCredentials);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "自动配置挑战失败: {Domain}", request?.Domain);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("challenge.autoConfigFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task MonitorChallengeStatus(
            string challengeId,
            HttpContext httpContext,
            CancellationToken cancellationToken,
            IChallengeValidationService challengeValidationService,
            ILogger<LoggingTag> logger)
        {
            httpContext.Response.Headers["Cache-Control"] = "no-cache";
            httpContext.Response.Headers["Connection"] = "keep-alive";
            httpContext.Response.Headers["Content-Type"] = "text/event-stream";

            try
            {
                await foreach (var update in challengeValidationService.MonitorChallengeStatusAsync(challengeId, cancellationToken))
                {
                    var eventData = $"data: {System.Text.Json.JsonSerializer.Serialize(update, DockerPanelJsonContext.Default.ChallengeStatusUpdate)}\n\n";
                    await httpContext.Response.WriteAsync(eventData, cancellationToken);
                    await httpContext.Response.Body.FlushAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("挑战监控已取消: {ChallengeId}", challengeId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "挑战监控出错: {ChallengeId}", challengeId);
                var errorUpdate = new ChallengeStatusUpdate
                {
                    ChallengeId = challengeId,
                    Status = "error",
                    Timestamp = DateTime.UtcNow,
                    Message = $"监控出错: {ex.Message}"
                };
                var eventData = $"data: {System.Text.Json.JsonSerializer.Serialize(errorUpdate, DockerPanelJsonContext.Default.ChallengeStatusUpdate)}\n\n";
                await httpContext.Response.WriteAsync(eventData);
                await httpContext.Response.Body.FlushAsync();
            }
        }

        private static async Task<IResult> BatchCleanupChallenges(
            BatchCleanupChallengesRequest request,
            IChallengeValidationService challengeValidationService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
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

                        var result = await challengeValidationService.CleanupChallengeAsync(
                            challenge, challengeInfo.Domain, challengeInfo.ChallengeType);
                        results.Add(result);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "批量清理挑战失败: {Domain}, Type: {Type}",
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

                return TypedResults.Ok(results);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "批量清理挑战失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("challenge.batchCleanupFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static IResult GetChallengeValidationStats(
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
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

                return TypedResults.Ok(stats);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取挑战验证统计失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("challenge.statsFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }
    }
}
