using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DockerPanel.API.Models.Acme;
using DockerPanel.API.Serialization;
using DockerPanel.API.Services;
using DockerPanel.API.Services.Acme;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// 通配符证书管理 Minimal API 端点（原 WildcardCertificateController）。
    /// </summary>
    public static class WildcardCertificateEndpoints
    {
        /// <summary>
        /// 日志类别标记（静态类不能作泛型参数）。
        /// </summary>
        public sealed class LoggingTag
        {
        }

        /// <summary>
        /// 映射通配符证书管理相关路由。
        /// </summary>
        public static IEndpointRouteBuilder MapWildcardCertificateEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/wildcardcertificate");

            group.MapPost("request", RequestWildcardCertificate);
            group.MapPost("validate", ValidateWildcardRequest);
            group.MapPost("configure-dns-challenge", ConfigureWildcardDnsChallenge);
            group.MapPost("cleanup-dns-challenge", CleanupWildcardDnsChallenge);
            group.MapPost("import", ImportWildcardCertificate);
            group.MapPost("test", TestWildcardCertificateFlow);
            group.MapGet("dns-providers", GetSupportedDnsProviders);
            group.MapGet("statistics", GetWildcardCertificateStatistics);
            group.MapPost("batch", BatchOperationWildcardCertificates);
            group.MapPost("auto-configure-challenge", AutoConfigureWildcardChallenge);
            group.MapGet("", GetWildcardCertificates);
            group.MapGet("{certificateId}", GetWildcardCertificateDetails);
            group.MapPost("{certificateId}/renew", RenewWildcardCertificate);
            group.MapDelete("{certificateId}", DeleteWildcardCertificate);
            group.MapDelete("{certificateId}/force", ForceDeleteWildcardCertificate);
            group.MapGet("{certificateId}/export", ExportWildcardCertificate);
            group.MapPost("{certificateId}/validate", ValidateWildcardCertificate);
            group.MapGet("{certificateId}/status", CheckWildcardCertificateStatus);

            return app;
        }

        private static async Task<IResult> RequestWildcardCertificate(
            WildcardCertificateRequest request,
            IWildcardCertificateService wildcardCertificateService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("开始申请通配符证书: {Domains}", string.Join(", ", request.Domains));

                var result = await wildcardCertificateService.RequestWildcardCertificateAsync(request);

                if (result.Success)
                {
                    logger.LogInformation("通配符证书申请成功: {CertificateId}", result.CertificateId);
                    return TypedResults.Ok(result);
                }

                logger.LogWarning("通配符证书申请失败: {Message}", result.Message);
                return TypedResults.BadRequest(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "申请通配符证书时发生异常");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> RenewWildcardCertificate(
            string certificateId,
            IWildcardCertificateService wildcardCertificateService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("开始续期通配符证书: {CertificateId}", certificateId);

                var result = await wildcardCertificateService.RenewWildcardCertificateAsync(certificateId);

                if (result.Success)
                {
                    logger.LogInformation("通配符证书续期成功: {CertificateId}", certificateId);
                    return TypedResults.Ok(result);
                }

                logger.LogWarning("通配符证书续期失败: {CertificateId} - {Message}", certificateId, result.Message);
                return TypedResults.BadRequest(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "续期通配符证书时发生异常: {CertificateId}", certificateId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ValidateWildcardRequest(
            WildcardCertificateRequest request,
            IWildcardCertificateService wildcardCertificateService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("开始验证通配符证书请求: {Domains}", string.Join(", ", request.Domains));

                var result = await wildcardCertificateService.ValidateWildcardRequestAsync(request);

                logger.LogInformation("通配符证书请求验证完成: {Passed} - {Message}", result.Passed, result.Message);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "验证通配符证书请求时发生异常");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ConfigureWildcardDnsChallenge(
            WildcardDnsChallengeRequest request,
            IWildcardCertificateService wildcardCertificateService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("开始配置通配符证书DNS挑战: {Domain} - {Provider}", request.Domain, request.DnsProvider);

                var result = await wildcardCertificateService.ConfigureWildcardDnsChallengeAsync(request);

                if (result.Success)
                {
                    logger.LogInformation("通配符证书DNS挑战配置成功: {Domain}", request.Domain);
                    return TypedResults.Ok(result);
                }

                logger.LogWarning("通配符证书DNS挑战配置失败: {Domain} - {Message}", request.Domain, result.Message);
                return TypedResults.BadRequest(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "配置通配符证书DNS挑战时发生异常: {Domain}", request.Domain);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> CleanupWildcardDnsChallenge(
            WildcardDnsChallengeCleanupRequest request,
            IWildcardCertificateService wildcardCertificateService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("开始清理通配符证书DNS挑战: {Domain} - {Provider}", request.Domain, request.DnsProvider);

                var result = await wildcardCertificateService.CleanupWildcardDnsChallengeAsync(request);

                if (result.Success)
                {
                    logger.LogInformation("通配符证书DNS挑战清理成功: {Domain}", request.Domain);
                    return TypedResults.Ok(result);
                }

                logger.LogWarning("通配符证书DNS挑战清理失败: {Domain} - {Message}", request.Domain, result.Message);
                return TypedResults.BadRequest(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "清理通配符证书DNS挑战时发生异常: {Domain}", request.Domain);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetWildcardCertificateDetails(
            string certificateId,
            IWildcardCertificateService wildcardCertificateService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("获取通配符证书详情: {CertificateId}", certificateId);

                var details = await wildcardCertificateService.GetWildcardCertificateDetailsAsync(certificateId);

                if (details != null)
                {
                    return TypedResults.Ok(details);
                }

                logger.LogWarning("未找到通配符证书: {CertificateId}", certificateId);
                return TypedResults.NotFound(new ApiErrorResponse { Message = localization.GetMessage("wildcard.notFound") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取通配符证书详情时发生异常: {CertificateId}", certificateId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetWildcardCertificates(
            IWildcardCertificateService wildcardCertificateService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("获取通配符证书列表");

                var certificates = await wildcardCertificateService.GetWildcardCertificatesAsync(null);

                return TypedResults.Ok(certificates);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取通配符证书列表时发生异常");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> DeleteWildcardCertificate(
            string certificateId,
            IWildcardCertificateService wildcardCertificateService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("删除通配符证书: {CertificateId}", certificateId);

                var result = await wildcardCertificateService.DeleteWildcardCertificateAsync(certificateId);

                if (result.Success)
                {
                    logger.LogInformation("通配符证书删除成功: {CertificateId}", certificateId);
                    return TypedResults.Ok(result);
                }

                logger.LogWarning("通配符证书删除失败: {CertificateId}", certificateId);
                return TypedResults.BadRequest(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "删除通配符证书时发生异常: {CertificateId}", certificateId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ForceDeleteWildcardCertificate(
            string certificateId,
            IWildcardCertificateService wildcardCertificateService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("强制删除通配符证书: {CertificateId}", certificateId);

                var result = await wildcardCertificateService.ForceDeleteWildcardCertificateAsync(certificateId);

                if (result.Success)
                {
                    logger.LogInformation("通配符证书强制删除成功: {CertificateId}", certificateId);
                    return TypedResults.Ok(result);
                }

                logger.LogWarning("通配符证书强制删除失败: {CertificateId}", certificateId);
                return TypedResults.BadRequest(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "强制删除通配符证书时发生异常: {CertificateId}", certificateId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ImportWildcardCertificate(
            WildcardCertificateImportRequest request,
            IWildcardCertificateService wildcardCertificateService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("导入通配符证书: {Domains}", string.Join(", ", request.Domains));

                var result = await wildcardCertificateService.ImportWildcardCertificateAsync(request);

                if (result.Success)
                {
                    logger.LogInformation("通配符证书导入成功: {CertificateId}", result.CertificateId);
                    return TypedResults.Ok(result);
                }

                logger.LogWarning("通配符证书导入失败: {Message}", result.Message);
                return TypedResults.BadRequest(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "导入通配符证书时发生异常");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ExportWildcardCertificate(
            string certificateId,
            string format,
            IWildcardCertificateService wildcardCertificateService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("导出通配符证书: {CertificateId} - {Format}", certificateId, format);

                var result = await wildcardCertificateService.ExportWildcardCertificateAsync(certificateId, format, true);

                if (result.Success)
                {
                    logger.LogInformation("通配符证书导出成功: {CertificateId}", certificateId);
                    return TypedResults.Ok(result);
                }

                logger.LogWarning("通配符证书导出失败: {CertificateId} - {Message}", certificateId, result.Message);
                return TypedResults.BadRequest(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "导出通配符证书时发生异常: {CertificateId}", certificateId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ValidateWildcardCertificate(
            string certificateId,
            IWildcardCertificateService wildcardCertificateService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("验证通配符证书: {CertificateId}", certificateId);

                var result = await wildcardCertificateService.ValidateWildcardCertificateAsync(certificateId);

                logger.LogInformation("通配符证书验证完成: {CertificateId} - {Valid}", certificateId, result.ValidationStatus.IsValid);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "验证通配符证书时发生异常: {CertificateId}", certificateId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> TestWildcardCertificateFlow(
            WildcardCertificateRequest request,
            IWildcardCertificateService wildcardCertificateService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("测试通配符证书申请流程: {Domains}", string.Join(", ", request.Domains));

                var result = await wildcardCertificateService.TestWildcardCertificateFlowAsync(request);

                logger.LogInformation("通配符证书申请流程测试完成: {Success} - {Message}", result.Success, result.Message);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "测试通配符证书申请流程时发生异常");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static IResult GetSupportedDnsProviders(
            IWildcardCertificateService wildcardCertificateService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("获取支持的DNS提供商列表");

                var providers = wildcardCertificateService.GetSupportedDnsProviders();

                return TypedResults.Ok(providers);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取DNS提供商列表时发生异常");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetWildcardCertificateStatistics(
            IWildcardCertificateService wildcardCertificateService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("获取通配符证书统计信息");

                var statistics = await wildcardCertificateService.GetWildcardCertificateStatisticsAsync();

                return TypedResults.Ok(statistics);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取通配符证书统计信息时发生异常");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> BatchOperationWildcardCertificates(
            WildcardCertificateBatchRequest request,
            IWildcardCertificateService wildcardCertificateService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("批量操作通配符证书: {Operation} - {Count}", request.Operation, request.CertificateIds.Count);

                var result = await wildcardCertificateService.BatchOperationWildcardCertificatesAsync(request);

                logger.LogInformation("通配符证书批量操作完成: {Success} - {Successful}/{Total}",
                    result.Success, result.SuccessCount, result.TotalCertificates);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "批量操作通配符证书时发生异常");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> CheckWildcardCertificateStatus(
            string certificateId,
            IWildcardCertificateService wildcardCertificateService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("检查通配符证书状态: {CertificateId}", certificateId);

                var status = await wildcardCertificateService.CheckWildcardCertificateStatusAsync(certificateId);

                if (status != null)
                {
                    return TypedResults.Ok(status);
                }

                logger.LogWarning("未找到通配符证书状态: {CertificateId}", certificateId);
                return TypedResults.NotFound(new ApiErrorResponse { Message = localization.GetMessage("wildcard.notFound") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "检查通配符证书状态时发生异常: {CertificateId}", certificateId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> AutoConfigureWildcardChallenge(
            WildcardAutoChallengeRequest request,
            IWildcardCertificateService wildcardCertificateService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("自动配置通配符证书挑战: {Domain}", request.Domain);

                var result = await wildcardCertificateService.AutoConfigureWildcardChallengeAsync(request);

                if (result.Success)
                {
                    logger.LogInformation("通配符证书挑战自动配置成功: {Domain}", request.Domain);
                    return TypedResults.Ok(result);
                }

                logger.LogWarning("通配符证书挑战自动配置失败: {Domain} - {Message}", request.Domain, result.Message);
                return TypedResults.BadRequest(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "自动配置通配符证书挑战时发生异常: {Domain}", request.Domain);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }
    }
}
