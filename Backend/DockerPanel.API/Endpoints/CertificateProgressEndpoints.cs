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
    /// 证书申请进度跟踪 Minimal API 端点（原 CertificateProgressController）。
    /// </summary>
    public static class CertificateProgressEndpoints
    {
        /// <summary>
        /// 日志类别标记（静态类不能作泛型参数）。
        /// </summary>
        public sealed class LoggingTag
        {
        }

        /// <summary>
        /// 映射证书申请进度跟踪相关路由。
        /// </summary>
        public static IEndpointRouteBuilder MapCertificateProgressEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/certificateprogress");

            group.MapPost("create", CreateProgress);
            group.MapGet("by-certificate/{certificateId}", GetProgressByCertificateId);
            group.MapPost("cleanup", CleanupExpiredProgress);
            group.MapGet("{progressId}", GetProgress);
            group.MapGet("", GetAllProgress);
            group.MapPut("{progressId}/step", UpdateProgressStep);
            group.MapPut("{progressId}/complete-current", CompleteCurrentStep);
            group.MapPost("{progressId}/error", AddError);
            group.MapPost("{progressId}/warning", AddWarning);
            group.MapPut("{progressId}/complete", MarkAsCompleted);
            group.MapPut("{progressId}/fail", MarkAsFailed);
            group.MapDelete("{progressId}", DeleteProgress);

            return app;
        }

        private static async Task<IResult> CreateProgress(
            ProgressTrackRequest request,
            ICertificateProgressService progressService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                var progressId = await progressService.CreateProgressAsync(request);
                return TypedResults.Ok(new ProgressIdResponse { ProgressId = progressId });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "创建进度跟踪失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("progress.createFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetProgress(
            string progressId,
            ICertificateProgressService progressService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                var progress = await progressService.GetProgressAsync(progressId);
                return TypedResults.Ok(progress);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取进度信息失败: {ProgressId}", progressId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("progress.getFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetProgressByCertificateId(
            string certificateId,
            ICertificateProgressService progressService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                var progress = await progressService.GetProgressByCertificateIdAsync(certificateId);
                return TypedResults.Ok(progress);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取证书进度信息失败: {CertificateId}", certificateId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("progress.getByCertificateFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetAllProgress(
            ICertificateProgressService progressService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                var progressList = await progressService.GetAllProgressAsync();
                return TypedResults.Ok(progressList);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取所有进度列表失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("progress.listFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> UpdateProgressStep(
            string progressId,
            UpdateProgressStepRequest request,
            ICertificateProgressService progressService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                await progressService.UpdateProgressStepAsync(progressId, request.Step, request.Message, request.IsCompleted);
                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("progress.stepUpdateSuccess") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "更新进度步骤失败: {ProgressId}", progressId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("progress.stepUpdateFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> CompleteCurrentStep(
            string progressId,
            CompleteCurrentStepRequest request,
            ICertificateProgressService progressService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                await progressService.CompleteCurrentStepAsync(progressId, request.Message);
                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("progress.completeCurrentSuccess") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "完成当前步骤失败: {ProgressId}", progressId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("progress.completeCurrentFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> AddError(
            string progressId,
            AddErrorRequest request,
            ICertificateProgressService progressService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                await progressService.AddErrorAsync(progressId, request.Error);
                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("progress.addErrorSuccess") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "添加错误信息失败: {ProgressId}", progressId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("progress.addErrorFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> AddWarning(
            string progressId,
            AddWarningRequest request,
            ICertificateProgressService progressService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                await progressService.AddWarningAsync(progressId, request.Warning);
                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("progress.addWarningSuccess") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "添加警告信息失败: {ProgressId}", progressId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("progress.addWarningFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> MarkAsCompleted(
            string progressId,
            ICertificateProgressService progressService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                await progressService.MarkAsCompletedAsync(progressId);
                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("progress.markCompleteSuccess") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "标记进度完成失败: {ProgressId}", progressId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("progress.markCompleteFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> MarkAsFailed(
            string progressId,
            MarkAsFailedRequest request,
            ICertificateProgressService progressService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                await progressService.MarkAsFailedAsync(progressId, request.ErrorMessage);
                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("progress.markFailSuccess") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "标记进度失败失败: {ProgressId}", progressId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("progress.markFailFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> DeleteProgress(
            string progressId,
            ICertificateProgressService progressService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                await progressService.DeleteProgressAsync(progressId);
                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("progress.deleteSuccess") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "删除进度记录失败: {ProgressId}", progressId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("progress.deleteFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> CleanupExpiredProgress(
            ICertificateProgressService progressService,
            ILocalizationService localization,
            ILogger<LoggingTag> logger)
        {
            try
            {
                await progressService.CleanupExpiredProgressAsync();
                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("progress.cleanupSuccess") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "清理过期进度记录失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("progress.cleanupFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }
    }
}
