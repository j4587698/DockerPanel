using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DockerPanel.API.Models;
using DockerPanel.API.Serialization;
using DockerPanel.API.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// Docker Compose 管理 Minimal API 端点（原 ComposeController）。
    /// </summary>
    public static class ComposeEndpoints
    {
        /// <summary>
        /// 日志类别标记（静态类不能作泛型参数）。
        /// </summary>
        public sealed class LoggingTag
        {
        }

        /// <summary>
        /// 映射 Compose 管理相关路由。
        /// </summary>
        public static IEndpointRouteBuilder MapComposeEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/compose");

            group.MapGet("", GetComposeFiles);
            group.MapGet("{id}", GetComposeFile);
            group.MapPost("", CreateComposeFile);
            group.MapPut("{id}", UpdateComposeFile);
            group.MapDelete("{id}", DeleteComposeFile);
            group.MapPost("{id}/validate", ValidateComposeFile);
            group.MapPost("parse", ParseComposeContent);
            group.MapPost("validate", ValidateComposeContent);
            group.MapPost("deploy", DeployCompose);
            group.MapPost("stop", StopCompose);
            group.MapPost("start", StartCompose);
            group.MapPost("restart", RestartCompose);
            group.MapPost("remove", RemoveCompose);
            group.MapGet("projects/{composeFileId}/status", GetComposeProjectStatus);
            group.MapGet("projects", GetComposeProjects);
            group.MapPost("logs", GetComposeLogs);
            group.MapGet("projects/{composeFileId}/stats", GetComposeProjectStats);
            group.MapGet("{id}/export", ExportComposeFile);
            group.MapPost("import", ImportComposeFile);
            group.MapGet("templates", GetComposeTemplates);
            group.MapPost("create-from-template", CreateFromTemplate);
            group.MapPost("batch-operation", BatchOperation);
            group.MapGet("{id}/history", GetComposeFileHistory);
            group.MapPost("{id}/restore", RestoreComposeFileVersion);
            group.MapPost("{id}/check-dependencies", CheckComposeDependencies);

            return app;
        }

        private static async Task<IResult> GetComposeFiles(IComposeService composeService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null, bool includeContent = false)
        {
            try
            {
                var files = await composeService.GetComposeFilesAsync(nodeId, includeContent);
                return TypedResults.Ok(files);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取Compose文件列表失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("compose.listFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetComposeFile(string id, IComposeService composeService, ILocalizationService localization, ILogger<LoggingTag> logger, bool includeContent = true)
        {
            try
            {
                var file = await composeService.GetComposeFileAsync(id, includeContent);
                if (file == null)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("compose.notFound"), Message = $"文件ID {id} 不存在" });
                }
                return TypedResults.Ok(file);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取Compose文件详情失败: {FileId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("compose.detailFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> CreateComposeFile(CreateComposeFileRequest request, IComposeService composeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var file = await composeService.CreateComposeFileAsync(request);
                return TypedResults.Created($"/api/compose/{file.Id}", file);
            }
            catch (ArgumentException ex)
            {
                return TypedResults.BadRequest(new ApiErrorResponse { Error = localization.GetMessage("error.invalidParams"), Message = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "创建Compose文件失败: {Name}", request.Name);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("compose.createFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> UpdateComposeFile(string id, UpdateComposeFileRequest request, IComposeService composeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var file = await composeService.UpdateComposeFileAsync(id, request);
                return TypedResults.Ok(file);
            }
            catch (ArgumentException ex)
            {
                return TypedResults.BadRequest(new ApiErrorResponse { Error = localization.GetMessage("error.invalidParams"), Message = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "更新Compose文件失败: {FileId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("compose.updateFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> DeleteComposeFile(string id, IComposeService composeService, ILocalizationService localization, ILogger<LoggingTag> logger, bool force = false)
        {
            try
            {
                var result = await composeService.DeleteComposeFileAsync(id, force);
                if (!result)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("compose.notFound"), Message = $"文件ID {id} 不存在" });
                }
                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("compose.deleteSuccess") });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new ApiErrorResponse { Error = localization.GetMessage("error.invalidOperation"), Message = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "删除Compose文件失败: {FileId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("compose.deleteFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ValidateComposeFile(string id, string? content, IComposeService composeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var result = await composeService.ValidateComposeFileAsync(id, content);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "验证Compose文件失败: {FileId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("compose.validateFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ParseComposeContent(ParseComposeContentRequest request, IComposeService composeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.Content))
                {
                    return TypedResults.BadRequest(new ApiErrorResponse { Error = localization.GetMessage("compose.contentEmpty") });
                }

                var result = await composeService.ParseComposeContentAsync(request.Content);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "解析Compose内容失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("compose.parseFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ValidateComposeContent(ValidateComposeContentRequest request, IComposeService composeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.Content))
                {
                    return TypedResults.BadRequest(new ApiErrorResponse { Error = localization.GetMessage("compose.contentEmpty") });
                }

                // 使用解析来验证内容
                var parsed = await composeService.ParseComposeContentAsync(request.Content);
                var result = new ComposeValidationResult
                {
                    IsValid = true,
                    ValidatedAt = DateTime.UtcNow,
                    Version = parsed.Version,
                    ServiceCount = parsed.Services?.Count ?? 0,
                    NetworkCount = parsed.Networks?.Count ?? 0,
                    VolumeCount = parsed.Volumes?.Count ?? 0
                };
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "验证Compose内容失败");
                return TypedResults.Ok(new ComposeValidationResult
                {
                    IsValid = false,
                    Errors = new List<ValidationError>
                    {
                        new ValidationError { Message = ex.Message }
                    },
                    ValidatedAt = DateTime.UtcNow
                });
            }
        }

        private static async Task<IResult> DeployCompose(DeployComposeRequest request, IComposeService composeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var result = await composeService.DeployComposeAsync(request);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "部署Compose项目失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("compose.deployFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> StopCompose(ComposeOperationRequest request, IComposeService composeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var result = await composeService.StopComposeAsync(request);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "停止Compose项目失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("compose.stopFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> StartCompose(ComposeOperationRequest request, IComposeService composeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var result = await composeService.StartComposeAsync(request);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "启动Compose项目失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("compose.startFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> RestartCompose(ComposeOperationRequest request, IComposeService composeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var result = await composeService.RestartComposeAsync(request);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "重启Compose项目失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("compose.restartFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> RemoveCompose(ComposeOperationRequest request, IComposeService composeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var result = await composeService.RemoveComposeAsync(request);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "删除Compose项目失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("compose.removeFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetComposeProjectStatus(string composeFileId, IComposeService composeService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                var project = await composeService.GetComposeProjectStatusAsync(composeFileId, nodeId);
                if (project == null)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("compose.projectNotFound"), Message = $"项目ID {composeFileId} 不存在" });
                }
                return TypedResults.Ok(project);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取Compose项目状态失败: {ComposeFileId}", composeFileId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("compose.projectStatusFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetComposeProjects(IComposeService composeService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                var projects = await composeService.GetComposeProjectsAsync(nodeId);
                return TypedResults.Ok(projects);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取Compose项目列表失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("compose.projectListFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetComposeLogs(ComposeLogsRequest request, IComposeService composeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var logs = await composeService.GetComposeLogsAsync(request);
                return TypedResults.Ok(logs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取Compose日志失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("compose.logsFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetComposeProjectStats(string composeFileId, IComposeService composeService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                var stats = await composeService.GetComposeProjectStatsAsync(composeFileId, nodeId);
                if (stats == null)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("compose.projectStatsNotFound"), Message = $"项目ID {composeFileId} 不存在" });
                }
                return TypedResults.Ok(stats);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取Compose项目统计信息失败: {ComposeFileId}", composeFileId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("compose.projectStatsFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ExportComposeFile(string id, IComposeService composeService, ILocalizationService localization, ILogger<LoggingTag> logger, string format = "yaml")
        {
            try
            {
                var content = await composeService.ExportComposeFileAsync(id, format);
                return TypedResults.Ok(content);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "导出Compose文件失败: {FileId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("compose.exportFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ImportComposeFile(ImportComposeFileRequest request, IComposeService composeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var file = await composeService.ImportComposeFileAsync(
                    request.Content,
                    request.Name,
                    request.Description,
                    request.NodeId
                );
                return TypedResults.Ok(file);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "导入Compose文件失败: {Name}", request.Name);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("compose.importFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetComposeTemplates(IComposeService composeService, ILocalizationService localization, ILogger<LoggingTag> logger, string? category = null, List<string>? tags = null)
        {
            try
            {
                var templates = await composeService.GetComposeTemplatesAsync(category, tags);
                return TypedResults.Ok(templates);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取Compose模板列表失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("compose.templateListFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> CreateFromTemplate(CreateFromTemplateRequest request, IComposeService composeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var file = await composeService.CreateFromTemplateAsync(
                    request.TemplateId,
                    request.Variables,
                    request.Name,
                    request.Description
                );
                return TypedResults.Ok(file);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "根据模板创建Compose文件失败: {TemplateId}", request.TemplateId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("compose.createFromTemplateFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> BatchOperation(BatchComposeOperationRequest request, IComposeService composeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var results = await composeService.BatchOperationAsync(
                    request.FileIds,
                    request.Operation,
                    request.Parameters
                );
                return TypedResults.Ok(results);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "批量操作Compose文件失败: {Operation}", request.Operation);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("compose.batchOperationFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetComposeFileHistory(string id, IComposeService composeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var history = await composeService.GetComposeFileHistoryAsync(id);
                return TypedResults.Ok(history);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取Compose文件历史版本失败: {FileId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("compose.historyFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> RestoreComposeFileVersion(string id, RestoreFileVersionRequest request, IComposeService composeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var file = await composeService.RestoreComposeFileVersionAsync(id, request.VersionId);
                return TypedResults.Ok(file);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "恢复Compose文件版本失败: {FileId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("compose.restoreFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> CheckComposeDependencies(string id, IComposeService composeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var check = await composeService.CheckComposeDependenciesAsync(id);
                return TypedResults.Ok(check);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "检查Compose文件依赖失败: {FileId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("compose.checkDependenciesFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }
    }
}
