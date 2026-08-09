using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DockerPanel.API.Models;
using DockerPanel.API.Serialization;
using DockerPanel.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// 卷管理 Minimal API 端点（原 VolumeController）。
    /// </summary>
    public static class VolumeEndpoints
    {
        /// <summary>
        /// 日志类别标记（静态类不能作泛型参数）。
        /// </summary>
        public sealed class LoggingTag
        {
        }

        /// <summary>
        /// 映射卷管理相关路由。
        /// </summary>
        public static IEndpointRouteBuilder MapVolumeEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/volumes");

            group.MapGet("", GetVolumes);
            group.MapGet("{volumeId}", GetVolume);
            group.MapPost("", CreateVolume);
            group.MapPost("restore-from-archive", RestoreVolumeFromArchive);
            group.MapDelete("{volumeId}", DeleteVolume);
            group.MapPut("{volumeId}", UpdateVolume);
            group.MapPost("prune", PruneVolumes);
            group.MapGet("statistics", GetVolumeStatistics);
            group.MapGet("{volumeId}/exists", VolumeExists);
            group.MapGet("{volumeId}/usage", GetVolumeUsage);
            group.MapPost("{volumeId}/backup", BackupVolume);
            group.MapPost("restore", RestoreVolume);
            group.MapGet("{volumeId}/backups", GetVolumeBackups);
            group.MapDelete("{volumeId}/backups/{backupId}", DeleteVolumeBackup);
            group.MapGet("{volumeId}/files", GetVolumeFiles);
            group.MapGet("{volumeId}/files/download", DownloadVolumeFile);
            group.MapPost("{volumeId}/files/upload", UploadVolumeFile);
            group.MapPost("{volumeId}/files/folder", CreateVolumeFolder);
            group.MapPut("{volumeId}/files/rename", RenameVolumeFile);
            group.MapDelete("{volumeId}/files", DeleteVolumeFile);
            group.MapGet("{volumeId}/files/content", GetVolumeFileContent);
            group.MapPut("{volumeId}/files/content", SaveVolumeFileContent);

            return app;
        }

        private static async Task<IResult> GetVolumes(IVolumeService volumeService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null, int? page = null, int? pageSize = null)
        {
            try
            {
                var volumes = await volumeService.GetVolumesAsync(nodeId);
                return TypedResults.Ok(volumes);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取卷列表失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("volume.listFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetVolume(string volumeId, IVolumeService volumeService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                var volume = await volumeService.GetVolumeByIdAsync(volumeId, nodeId);
                if (volume == null)
                {
                    return TypedResults.NotFound(new VolumeNotFoundResponse
                    {
                        Error = localization.GetMessage("volume.notFound"),
                        VolumeId = volumeId
                    });
                }
                return TypedResults.Ok(volume);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取卷详情失败: {VolumeId}", volumeId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("volume.detailFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> CreateVolume(CreateVolumeRequest request, IVolumeService volumeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var volumeName = await volumeService.CreateVolumeAsync(request);
                // 返回包含名称的 VolumeInfo 对象
                var volumeInfo = new DockerPanel.API.Models.VolumeInfo
                {
                    Name = volumeName,
                    Id = volumeName,
                    Driver = request.Driver ?? "local"
                };
                return TypedResults.Created($"/api/volumes/{volumeName}", volumeInfo);
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(ex, "创建卷参数错误: {Name}", request.Name);
                return TypedResults.BadRequest(new VolumeNameErrorResponse { Error = ex.Message, Name = request.Name });
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "创建卷操作失败: {Name}", request.Name);
                return TypedResults.Json(new VolumeNameErrorResponse { Error = ex.Message, Name = request.Name }, WebJsonContext.Default.VolumeNameErrorResponse, statusCode: 409);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "创建卷失败: {Name}", request.Name);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("volume.createFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> RestoreVolumeFromArchive(string? volumeName, IFormFile archive, IVolumeService volumeService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                if (archive == null || archive.Length == 0)
                {
                    return TypedResults.BadRequest(new ApiErrorResponse { Error = localization.GetMessage("volume.noArchiveSelected") });
                }

                using var stream = archive.OpenReadStream();
                var volume = await volumeService.RestoreVolumeFromArchiveAsync(volumeName, stream, nodeId);

                logger.LogInformation("从归档恢复卷成功: {VolumeName}", volume.Name);
                return TypedResults.Ok(volume);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "从归档恢复卷失败: {VolumeName}", volumeName);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("volume.restoreFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> DeleteVolume(string volumeId, IVolumeService volumeService, ILocalizationService localization, ILogger<LoggingTag> logger, bool force = false, string? nodeId = null)
        {
            try
            {
                var success = await volumeService.DeleteVolumeAsync(volumeId, force, nodeId);
                if (success)
                {
                    return TypedResults.Ok(new VolumeDeleteResponse
                    {
                        Message = localization.GetMessage("volume.deleteSuccess"),
                        VolumeId = volumeId,
                        Force = force
                    });
                }
                else
                {
                    return TypedResults.NotFound(new VolumeNotFoundResponse
                    {
                        Error = localization.GetMessage("volume.notFound"),
                        VolumeId = volumeId
                    });
                }
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "删除卷操作失败: {VolumeId}", volumeId);
                return TypedResults.Json(new VolumeIdErrorResponse { Error = ex.Message, VolumeId = volumeId }, WebJsonContext.Default.VolumeIdErrorResponse, statusCode: 409);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "删除卷失败: {VolumeId}", volumeId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("volume.deleteFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> UpdateVolume(string volumeId, UpdateVolumeRequest request, IVolumeService volumeService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                var volume = await volumeService.UpdateVolumeAsync(volumeId, request, nodeId);
                return TypedResults.Ok(volume);
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(ex, "更新卷配置参数错误: {VolumeId}", volumeId);
                return TypedResults.BadRequest(new VolumeIdErrorResponse { Error = ex.Message, VolumeId = volumeId });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "更新卷配置失败: {VolumeId}", volumeId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("volume.updateFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> PruneVolumes(PruneVolumesRequest request, IVolumeService volumeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var options = new VolumePruneOptions
                {
                    Filters = request.Filters,
                    LabelFilter = request.LabelFilter,
                    All = request.All
                };

                var result = await volumeService.PruneVolumesAsync(options, request.NodeId);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "清理卷失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("volume.pruneFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetVolumeStatistics(IVolumeService volumeService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                var statistics = await volumeService.GetVolumeStatisticsAsync(nodeId);
                return TypedResults.Ok(statistics);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取卷统计信息失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("volume.statsFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> VolumeExists(string volumeId, IVolumeService volumeService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                var exists = await volumeService.VolumeExistsAsync(volumeId, nodeId);
                return TypedResults.Ok(exists);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "检查卷是否存在失败: {VolumeId}", volumeId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("volume.existsCheckFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetVolumeUsage(string volumeId, IVolumeService volumeService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                var usage = await volumeService.GetVolumeUsageAsync(volumeId, nodeId);
                return TypedResults.Ok(usage);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取卷使用情况失败: {VolumeId}", volumeId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("volume.usageFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> BackupVolume(string volumeId, VolumeBackupRequest request, IVolumeService volumeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                request.VolumeId = volumeId;
                var result = await volumeService.BackupVolumeAsync(volumeId, request);

                if (result.Success)
                {
                    return TypedResults.Ok(result);
                }
                else
                {
                    return TypedResults.BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "备份卷失败: {VolumeId}", volumeId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("volume.backupFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> RestoreVolume(VolumeRestoreRequest request, IVolumeService volumeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var result = await volumeService.RestoreVolumeAsync(request);

                if (result.Success)
                {
                    return TypedResults.Ok(result);
                }
                else
                {
                    return TypedResults.BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "恢复卷失败: {VolumeId}", request.VolumeId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("volume.restoreFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetVolumeBackups(string volumeId, IVolumeService volumeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var backups = await volumeService.GetVolumeBackupsAsync(volumeId);
                return TypedResults.Ok(backups);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取卷备份列表失败: {VolumeId}", volumeId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("volume.backupListFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> DeleteVolumeBackup(string volumeId, string backupId, IVolumeService volumeService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var success = await volumeService.DeleteVolumeBackupAsync(volumeId, backupId);
                if (success)
                {
                    return TypedResults.Ok(new VolumeBackupDeleteResponse
                    {
                        Message = localization.GetMessage("volume.backupDeleteSuccess"),
                        VolumeId = volumeId,
                        BackupId = backupId
                    });
                }
                else
                {
                    return TypedResults.NotFound(new VolumeBackupNotFoundResponse
                    {
                        Error = localization.GetMessage("volume.backupNotFound"),
                        VolumeId = volumeId,
                        BackupId = backupId
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "删除卷备份失败: {VolumeId}, {BackupId}", volumeId, backupId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("volume.backupDeleteFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetVolumeFiles(string volumeId, IVolumeService volumeService, ILocalizationService localization, ILogger<LoggingTag> logger, string path = "/", string? nodeId = null)
        {
            try
            {
                var files = await volumeService.GetVolumeFilesAsync(volumeId, path, nodeId);
                return TypedResults.Ok(files);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取卷文件列表失败: {VolumeId}", volumeId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("volume.fileListFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> DownloadVolumeFile(string volumeId, IVolumeService volumeService, ILocalizationService localization, ILogger<LoggingTag> logger, string path, bool archive = false, string? nodeId = null)
        {
            try
            {
                if (archive)
                {
                    // 打包下载整个目录或多个文件
                    var (content, fileName) = await volumeService.ArchiveVolumeFilesAsync(volumeId, path, nodeId);
                    return TypedResults.File(content, "application/gzip", fileName);
                }
                else
                {
                    var content = await volumeService.DownloadVolumeFileAsync(volumeId, path, nodeId);
                    var fileName = System.IO.Path.GetFileName(path);
                    return TypedResults.File(content, "application/octet-stream", fileName);
                }
            }
            catch (FileNotFoundException ex)
            {
                return TypedResults.NotFound(new ApiErrorResponse { Message = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "下载卷文件失败: {VolumeId}, {Path}", volumeId, path);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("volume.downloadFileFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> UploadVolumeFile(string volumeId, string path, IFormFile file, IVolumeService volumeService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return TypedResults.BadRequest(new ApiErrorResponse { Error = localization.GetMessage("volume.noFileSelected") });
                }

                using var stream = file.OpenReadStream();
                await volumeService.UploadVolumeFileAsync(volumeId, path, file.FileName, stream, nodeId);
                return TypedResults.Ok(new VolumeUploadResponse
                {
                    Message = localization.GetMessage("volume.uploadSuccess"),
                    FileName = file.FileName
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "上传卷文件失败: {VolumeId}", volumeId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("volume.uploadFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> CreateVolumeFolder(string volumeId, CreateFolderRequest request, IVolumeService volumeService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                await volumeService.CreateVolumeFolderAsync(volumeId, request.Path, request.Name, nodeId);
                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("volume.createFolderSuccess") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "创建文件夹失败: {VolumeId}", volumeId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("volume.createFolderFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> RenameVolumeFile(string volumeId, RenameFileRequest request, IVolumeService volumeService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                await volumeService.RenameVolumeFileAsync(volumeId, request.Path, request.OldName, request.NewName, nodeId);
                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("volume.renameSuccess") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "重命名文件失败: {VolumeId}", volumeId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("volume.renameFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> DeleteVolumeFile(string volumeId, IVolumeService volumeService, ILocalizationService localization, ILogger<LoggingTag> logger, string path, bool recursive = false, string? nodeId = null)
        {
            try
            {
                await volumeService.DeleteVolumeFileAsync(volumeId, path, recursive, nodeId);
                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("volume.deleteFileSuccess") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "删除卷文件失败: {VolumeId}", volumeId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("volume.deleteFileFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetVolumeFileContent(string volumeId, IVolumeService volumeService, ILocalizationService localization, ILogger<LoggingTag> logger, string path, string? nodeId = null)
        {
            try
            {
                var content = await volumeService.GetVolumeFileContentAsync(volumeId, path, nodeId);
                return TypedResults.Ok(new FileContentResponse { Content = content, Path = path });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取文件内容失败: {VolumeId}, {Path}", volumeId, path);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("volume.getFileContentFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> SaveVolumeFileContent(string volumeId, SaveFileContentRequest request, IVolumeService volumeService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                await volumeService.SaveVolumeFileContentAsync(volumeId, request.Path, request.Content, nodeId);
                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("volume.saveSuccess") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "保存文件内容失败: {VolumeId}", volumeId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("volume.saveFileFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }
    }
}
