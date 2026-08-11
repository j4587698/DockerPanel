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
    /// 证书管理 Minimal API 端点（原 CertificateManagementController）。
    /// </summary>
    public static class CertificateManagementEndpoints
    {
        /// <summary>
        /// 日志类别标记（静态类不能作泛型参数）。
        /// </summary>
        public sealed class LoggingTag
        {
        }

        /// <summary>
        /// 映射证书管理相关路由。
        /// </summary>
        public static IEndpointRouteBuilder MapCertificateManagementEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/certificatemanagement");

            group.MapGet("", GetCertificates);
            group.MapGet("expiring", GetExpiringCertificates);
            group.MapPost("import", ImportCertificate);
            group.MapPost("batch", BatchOperateCertificates);
            group.MapGet("statistics", GetCertificateListStatistics);
            group.MapGet("search", SearchCertificates);
            group.MapGet("summary", GetCertificateSummary);
            group.MapGet("{id}", GetCertificateDetails);
            group.MapPost("{id}/renew", RenewCertificate);
            group.MapPost("{id}/auto-renewal/enable", EnableAutoRenewal);
            group.MapPost("{id}/auto-renewal/disable", DisableAutoRenewal);
            group.MapDelete("{id}", DeleteCertificate);
            group.MapGet("{id}/export", ExportCertificate);
            group.MapPost("{id}/validate", ValidateCertificate);
            group.MapGet("{id}/statistics", GetCertificateUsageStatistics);
            group.MapGet("{id}/history", GetCertificateOperationHistory);
            group.MapGet("{id}/download", DownloadCertificate);

            return app;
        }

        private static async Task<IResult> GetCertificates(
            bool includeExpired,
            string? certificateType,
            string? statusFilter,
            string? domainFilter,
            int pageIndex,
            int pageSize,
            ICertificateManagementService certificateManagementService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("获取证书列表请求: IncludeExpired={IncludeExpired}, Type={Type}, Status={Status}, Domain={Domain}",
                    includeExpired, certificateType, statusFilter, domainFilter);

                var result = await certificateManagementService.GetCertificatesAsync(
                    includeExpired, certificateType, statusFilter, domainFilter,
                    pageIndex, pageSize);

                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取证书列表时发生异常");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetCertificateDetails(
            string id,
            ICertificateManagementService certificateManagementService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("获取证书详情请求: {CertificateId}", id);

                var details = await certificateManagementService.GetCertificateDetailsAsync(id);

                if (details != null)
                {
                    return TypedResults.Ok(details);
                }

                logger.LogWarning("未找到证书: {CertificateId}", id);
                return TypedResults.NotFound(new ApiErrorResponse { Message = localization.GetMessage("certificate.notFound") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取证书详情时发生异常: {CertificateId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetExpiringCertificates(
            int daysBeforeExpiry,
            ICertificateManagementService certificateManagementService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("获取即将到期证书请求: 天数={Days}", daysBeforeExpiry);

                var expiringCertificates = await certificateManagementService.GetExpiringCertificatesAsync(
                    daysBeforeExpiry);

                return TypedResults.Ok(expiringCertificates);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取即将到期证书时发生异常");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> RenewCertificate(
            string id,
            ICertificateManagementService certificateManagementService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("手动续期证书请求: {CertificateId}", id);

                var result = await certificateManagementService.RenewCertificateAsync(id);

                if (result.Success)
                {
                    logger.LogInformation("证书续期成功: {CertificateId}", id);
                    return TypedResults.Ok(result);
                }

                logger.LogWarning("证书续期失败: {CertificateId} - {Message}", id, result.Message);
                return TypedResults.BadRequest(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "续期证书时发生异常: {CertificateId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> EnableAutoRenewal(
            string id,
            AutoRenewalConfiguration configuration,
            ICertificateManagementService certificateManagementService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("启用证书自动续期请求: {CertificateId}", id);

                var result = await certificateManagementService.EnableAutoRenewalAsync(id, configuration);

                if (result.Success)
                {
                    logger.LogInformation("启用自动续期成功: {CertificateId}", id);
                    return TypedResults.Ok(result);
                }

                logger.LogWarning("启用自动续期失败: {CertificateId} - {Message}", id, result.Message);
                return TypedResults.BadRequest(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "启用证书自动续期时发生异常: {CertificateId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> DisableAutoRenewal(
            string id,
            ICertificateManagementService certificateManagementService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("禁用证书自动续期请求: {CertificateId}", id);

                var result = await certificateManagementService.DisableAutoRenewalAsync(id);

                if (result)
                {
                    logger.LogInformation("禁用自动续期成功: {CertificateId}", id);
                    return TypedResults.Ok(new ActionBooleanResponse { Success = true, Message = localization.GetMessage("certificate.autoRenewDisabled") });
                }

                logger.LogWarning("禁用自动续期失败: {CertificateId}", id);
                return TypedResults.BadRequest(new ActionBooleanResponse { Success = false, Message = localization.GetMessage("acme.autoRenewDisableFailed") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "禁用证书自动续期时发生异常: {CertificateId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> DeleteCertificate(
            string id,
            bool force,
            ICertificateManagementService certificateManagementService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("删除证书请求: {CertificateId}, Force: {Force}", id, force);

                CertificateDetails? certificate = await certificateManagementService.GetCertificateDetailsAsync(id);
                if (certificate == null && !force)
                {
                    logger.LogWarning("证书不存在: {CertificateId}", id);
                    return TypedResults.NotFound(new CertificateDeletionResult
                    {
                        Success = false,
                        Message = localization.GetMessage("certificate.notFound"),
                        CertificateId = id,
                        DeletedAt = DateTime.UtcNow
                    });
                }

                CertificateDeletionResult result;
                if (force)
                {
                    result = await certificateManagementService.ForceDeleteCertificateAsync(id);
                }
                else
                {
                    result = await certificateManagementService.DeleteCertificateAsync(id);
                }

                if (result.Success)
                {
                    logger.LogInformation("证书删除成功: {CertificateId}, Force: {Force}", id, force);
                    return TypedResults.Ok(result);
                }

                logger.LogWarning("证书删除失败: {CertificateId} - {Message}", id, result.Message);
                return TypedResults.BadRequest(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "删除证书时发生异常: {CertificateId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ImportCertificate(
            CertificateImportRequest request,
            ICertificateManagementService certificateManagementService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("导入证书请求: {Name}", request.Name);

                var result = await certificateManagementService.ImportCertificateAsync(request);

                if (result.Success)
                {
                    logger.LogInformation("证书导入成功: {Name} - {CertificateId}", request.Name, result.CertificateId);
                    return TypedResults.Ok(result);
                }

                logger.LogWarning("证书导入失败: {Name} - {Message}", request.Name, result.Message);
                return TypedResults.BadRequest(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "导入证书时发生异常: {Name}", request.Name);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ExportCertificate(
            string id,
            string format,
            bool includePrivateKey,
            ICertificateManagementService certificateManagementService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("导出证书请求: {CertificateId}, Format={Format}, IncludeKey={IncludeKey}",
                    id, format, includePrivateKey);

                var result = await certificateManagementService.ExportCertificateAsync(
                    id, format, includePrivateKey);

                if (result.Success)
                {
                    logger.LogInformation("证书导出成功: {CertificateId}", id);
                    return TypedResults.Ok(result);
                }

                logger.LogWarning("证书导出失败: {CertificateId} - {Message}", id, result.Message);
                return TypedResults.BadRequest(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "导出证书时发生异常: {CertificateId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ValidateCertificate(
            string id,
            ICertificateManagementService certificateManagementService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("验证证书请求: {CertificateId}", id);

                var result = await certificateManagementService.ValidateCertificateAsync(id);

                logger.LogInformation("证书验证完成: {CertificateId} - {Valid}", id, result.IsValid);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "验证证书时发生异常: {CertificateId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetCertificateUsageStatistics(
            string id,
            ICertificateManagementService certificateManagementService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("获取证书使用统计请求: {CertificateId}", id);

                var statistics = await certificateManagementService.GetCertificateUsageStatisticsAsync(id);

                return TypedResults.Ok(statistics);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取证书使用统计时发生异常: {CertificateId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetCertificateOperationHistory(
            string id,
            string? operationType,
            int limit,
            int offset,
            ICertificateManagementService certificateManagementService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("获取证书操作历史请求: {CertificateId}, Type={Type}", id, operationType);

                var history = await certificateManagementService.GetCertificateOperationHistoryAsync(
                    id, operationType, limit, offset);

                return TypedResults.Ok(history);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取证书操作历史时发生异常: {CertificateId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> BatchOperateCertificates(
            CertificateBatchOperationRequest request,
            ICertificateManagementService certificateManagementService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("批量操作证书请求: 操作={Operation}, 数量={Count}",
                    request.Operation, request.CertificateIds.Count);

                var result = await certificateManagementService.BatchOperateCertificatesAsync(request);

                logger.LogInformation("批量操作完成: {Operation} - 成功={Success}, 失败={Failed}",
                    request.Operation, result.SuccessfulOperations, result.FailedOperations);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "批量操作证书时发生异常");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetCertificateListStatistics(
            ICertificateManagementService certificateManagementService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("获取证书列表统计信息请求");

                var statistics = await certificateManagementService.GetCertificateListStatisticsAsync();

                return TypedResults.Ok(statistics);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取证书列表统计信息时发生异常");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> SearchCertificates(
            string searchTerm,
            IEnumerable<string>? searchFields,
            int pageIndex,
            int pageSize,
            ICertificateManagementService certificateManagementService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("搜索证书请求: 搜索词={SearchTerm}", searchTerm);

                var result = await certificateManagementService.SearchCertificatesAsync(
                    searchTerm, searchFields, pageIndex, pageSize);

                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "搜索证书时发生异常: {SearchTerm}", searchTerm);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> DownloadCertificate(
            string id,
            ICertificateManagementService certificateManagementService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("下载证书请求: {CertificateId}", id);

                var certificate = await certificateManagementService.GetCertificateDetailsAsync(id);
                if (certificate == null)
                {
                    logger.LogWarning("证书不存在: {CertificateId}", id);
                    return TypedResults.NotFound(new ApiErrorResponse { Message = localization.GetMessage("certificate.notFound") });
                }

                var domainName = certificate.Domains?.FirstOrDefault()?.Replace("*.", "wildcard_") ?? "certificate";
                var fileNamePrefix = $"{domainName}_{DateTime.UtcNow:yyyyMMdd}";

                using var memoryStream = new MemoryStream();
                using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
                {
                    if (!string.IsNullOrEmpty(certificate.CertificateData))
                    {
                        var certEntry = archive.CreateEntry("cert.pem");
                        using var entryStream = certEntry.Open();
                        using var writer = new StreamWriter(entryStream);
                        await writer.WriteAsync(certificate.CertificateData);
                    }

                    if (!string.IsNullOrEmpty(certificate.PrivateKeyData))
                    {
                        var keyEntry = archive.CreateEntry("privkey.pem");
                        using var entryStream = keyEntry.Open();
                        using var writer = new StreamWriter(entryStream);
                        await writer.WriteAsync(certificate.PrivateKeyData);
                    }

                    if (!string.IsNullOrEmpty(certificate.CertificateChain))
                    {
                        var chainEntry = archive.CreateEntry("fullchain.pem");
                        using var entryStream = chainEntry.Open();
                        using var writer = new StreamWriter(entryStream);
                        await writer.WriteAsync(certificate.CertificateChain);
                    }
                    else if (!string.IsNullOrEmpty(certificate.CertificateData))
                    {
                        var chainEntry = archive.CreateEntry("fullchain.pem");
                        using var entryStream = chainEntry.Open();
                        using var writer = new StreamWriter(entryStream);
                        await writer.WriteAsync(certificate.CertificateData);
                    }
                }

                memoryStream.Position = 0;
                var zipBytes = memoryStream.ToArray();
                var zipFileName = $"{fileNamePrefix}.zip";

                logger.LogInformation("证书下载成功: {CertificateId}, 文件大小: {Size} bytes", id, zipBytes.Length);
                return TypedResults.File(zipBytes, "application/zip", zipFileName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "下载证书时发生异常: {CertificateId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetCertificateSummary(
            ICertificateManagementService certificateManagementService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                logger.LogInformation("获取证书状态摘要请求");

                var statistics = await certificateManagementService.GetCertificateListStatisticsAsync();
                var expiringSoon = await certificateManagementService.GetExpiringCertificatesAsync(7);

                var summary = new CertificateSummaryResponse
                {
                    TotalCertificates = statistics.TotalCertificates,
                    ActiveCertificates = statistics.ActiveCertificates,
                    ExpiredCertificates = statistics.ExpiredCertificates,
                    ExpiringIn7Days = expiringSoon.Count(),
                    ExpiringIn30Days = statistics.ExpiringNext30Days,
                    CertificatesWithAutoRenewal = statistics.CertificatesWithAutoRenewal,
                    WildcardCertificates = statistics.WildcardCertificates,
                    LastUpdated = DateTime.UtcNow,
                    Status = "healthy",
                    UpcomingRenewals = expiringSoon.Take(5).Select(x => new CertificateSummaryUpcomingRenewal
                    {
                        CertificateId = x.CertificateId,
                        Domains = x.Domains,
                        ExpiresAt = x.ExpiresAt,
                        DaysUntilExpiry = x.DaysUntilExpiry,
                        AutoRenewalEnabled = x.AutoRenewalEnabled
                    }).ToList()
                };

                return TypedResults.Ok(summary);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取证书状态摘要时发生异常");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("error.serverError"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }
    }
}
