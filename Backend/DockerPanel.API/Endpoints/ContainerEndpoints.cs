using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DockerPanel.API.Hubs;
using DockerPanel.API.Models;
using DockerPanel.API.Serialization;
using DockerPanel.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;

namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// 容器管理 Minimal API 端点（原 ContainersController）。
    /// </summary>
    public static class ContainerEndpoints
    {
        /// <summary>
        /// 日志类别标记（静态类不能作泛型参数）。
        /// </summary>
        public sealed class LoggingTag
        {
        }

        /// <summary>
        /// 映射容器管理相关路由。
        /// </summary>
        public static IEndpointRouteBuilder MapContainerEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/containers");

            group.MapGet("", GetContainers);
            group.MapGet("{id}", GetContainer);
            group.MapGet("{id}/logs", GetContainerLogs);
            group.MapGet("{id}/stats", GetContainerStats);
            group.MapPost("{id}/exec", ExecuteCommand);
            group.MapPost("batch", BatchOperation);
            group.MapPost("", CreateContainer);
            group.MapPost("{id}/start", StartContainer);
            group.MapPost("{id}/stop", StopContainer);
            group.MapPost("{id}/restart", RestartContainer);
            group.MapDelete("{id}", RemoveContainer);
            group.MapPost("{id}/rename", RenameContainer);
            group.MapPatch("{id}", UpdateContainer);
            group.MapGet("{id}/export", ExportContainer);
            group.MapPost("{id}/recreate", RecreateContainer);
            group.MapGet("{id}/files", GetContainerFiles);
            group.MapGet("{id}/mounts", GetContainerMounts);
            group.MapGet("{id}/files/download", DownloadContainerFile);
            group.MapPost("{id}/files/upload", UploadContainerFile);
            group.MapPost("{id}/files/folder", CreateContainerFolder);
            group.MapPut("{id}/files/rename", RenameContainerFile);
            group.MapDelete("{id}/files", DeleteContainerFile);
            group.MapGet("{id}/files/content", GetContainerFileContent);
            group.MapPut("{id}/files/content", WriteContainerFileContent);
            group.MapPut("{id}/files/permissions", ChangeContainerFilePermissions);

            return app;
        }

        private static async Task<IResult> GetContainers(IContainerService containerService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null, bool all = false)
        {
            try
            {
                var containers = await containerService.GetContainersAsync(nodeId, all);
                return TypedResults.Ok(containers);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取容器列表失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("container.listFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetContainer(string id, IContainerService containerService, DomainMappingService domainMappingService, DockerEngine dockerEngine, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                var container = await containerService.GetContainerAsync(id, nodeId);
                if (container == null)
                {
                    // 决定性诊断：把「请求的 ID」和「Docker 当前真实存在的 ID」一起打出来，
                    // 一眼区分是前端传了失效 ID，还是后端连到了别的 Docker 守护进程。
                    try
                    {
                        var live = (await containerService.GetContainersAsync(nodeId, all: true)).ToList();
                        logger.LogWarning(
                            "容器未找到: 请求 Id={Id}, nodeId={NodeId}, 目标={Target}; Docker 当前共 {Count} 个容器: {Live}",
                            id, nodeId ?? "(默认)", await dockerEngine.DescribeTargetAsync(nodeId), live.Count,
                            string.Join(", ", live.Select(c => $"{c.Name}={c.Id}")));
                    }
                    catch (Exception diagEx)
                    {
                        logger.LogWarning(diagEx, "容器未找到: 请求 Id={Id}，且枚举现有容器也失败", id);
                    }

                    return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("container.notFound") });
                }

                // 获取容器的域名映射信息
                var mappings = await domainMappingService.GetContainerDomainMappingsAsync(id);
                if (mappings.Count > 0)
                {
                    container.DomainMappings = mappings.Select(m => new ContainerDomainMapping
                    {
                        Id = m.Domain,
                        Domain = m.Domain,
                        ContainerPort = m.ContainerPort,
                        PathPrefix = m.PathPrefix,
                        EnableSsl = m.EnableSsl,
                        Enabled = true
                    }).ToList();
                }

                return TypedResults.Ok(container);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取容器详情失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("container.detailFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetContainerLogs(string id, IContainerService containerService, ILocalizationService localization, ILogger<LoggingTag> logger, int tail = 100, string? nodeId = null)
        {
            try
            {
                var logs = await containerService.GetContainerLogsAsync(id, tail: tail, nodeId: nodeId);
                return TypedResults.Ok(logs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取容器日志失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("container.logsFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetContainerStats(string id, IContainerService containerService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                var stats = await containerService.GetContainerStatsAsync(id, nodeId);
                return TypedResults.Ok(stats);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取容器统计信息失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("container.statsFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ExecuteCommand(string id, ExecCommandRequest request, IContainerService containerService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                var result = await containerService.ExecuteCommandAsync(id, request, nodeId);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "容器命令执行失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("container.execFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> BatchOperation(BatchContainerOperationRequest request, IContainerService containerService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var result = await containerService.BatchOperationAsync(request);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "批量容器操作失败: {Operation}", request.Operation);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("container.batchFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> CreateContainer(CreateContainerRequest request, IContainerService containerService, DomainMappingService domainMappingService, IHubContext<DockerPanelHub> hubContext, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            logger.LogInformation("创建容器请求: Name={Name}, Image={Image}, NetworkMode={NetworkMode}, Network={NetworkId}",
                request?.Name, request?.Image, request?.NetworkMode, request?.Network?.NetworkId);

            if (request == null)
            {
                logger.LogWarning("创建容器请求为空");
                return TypedResults.BadRequest(new ApiErrorResponse { Error = localization.GetMessage("container.requestBodyEmpty") });
            }

            try
            {
                var progress = new Progress<ImagePullProgress>(async p =>
                {
                    if (!string.IsNullOrEmpty(request.ConnectionId))
                    {
                        await hubContext.Clients.Client(request.ConnectionId).SendAsync("ImagePullUpdate", p);
                    }
                });

                var container = await containerService.CreateContainerAsync(request, progress);
                if (!string.IsNullOrEmpty(container.Id))
                {
                    await domainMappingService.ProcessContainerDomainMappingAsync(container.Id, request);
                }

                return TypedResults.Ok(container.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "创建容器失败");
                return TypedResults.Json(new ApiErrorResponse { Error = $"{localization.GetMessage("container.createFailed")}: {ex.Message}", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> StartContainer(string id, IContainerService containerService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                await containerService.StartContainerAsync(id, nodeId);
                return TypedResults.Ok(new MessageResponse { Message = string.Format(localization.GetMessage("container.startSuccess"), id) });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "启动容器失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("container.startFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> StopContainer(string id, IContainerService containerService, ILocalizationService localization, ILogger<LoggingTag> logger, int timeout = 30, string? nodeId = null)
        {
            try
            {
                await containerService.StopContainerAsync(id, timeout, nodeId);
                return TypedResults.Ok(new MessageResponse { Message = string.Format(localization.GetMessage("container.stopSuccess"), id) });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "停止容器失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("container.stopFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> RestartContainer(string id, IContainerService containerService, ILocalizationService localization, ILogger<LoggingTag> logger, int timeout = 30, string? nodeId = null)
        {
            try
            {
                await containerService.RestartContainerAsync(id, timeout, nodeId);
                return TypedResults.Ok(new MessageResponse { Message = string.Format(localization.GetMessage("container.restartSuccess"), id) });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "重启容器失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("container.restartFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> RemoveContainer(string id, IContainerService containerService, ILocalizationService localization, ILogger<LoggingTag> logger, bool force = false, string? nodeId = null)
        {
            try
            {
                await containerService.RemoveContainerAsync(id, force, nodeId: nodeId);
                return TypedResults.Ok(new MessageResponse { Message = string.Format(localization.GetMessage("container.deleteSuccess"), id) });
            }
            catch (Docker.DotNet.DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                // 容器正在运行，需要强制删除
                logger.LogWarning("删除容器失败，容器正在运行: {Id}", id);
                return TypedResults.Json(new ContainerDeleteConflictResponse
                {
                    Message = localization.GetMessage("container.runningCannotDelete"),
                    Error = localization.GetMessage("container.pleaseStopFirst"),
                    NeedForce = true
                }, WebJsonContext.Default.ContainerDeleteConflictResponse, statusCode: 400);
            }
            catch (Docker.DotNet.DockerApiException ex)
            {
                logger.LogError(ex, "删除容器失败: {Id}, StatusCode: {StatusCode}", id, ex.StatusCode);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("container.deleteFailed"), Message = ex.ResponseBody ?? ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "删除容器失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("container.deleteFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> RenameContainer(string id, RenameContainerRequest request, IContainerService containerService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                await containerService.RenameContainerAsync(id, request.NewName);
                return TypedResults.Ok(new MessageResponse { Message = string.Format(localization.GetMessage("container.renameSuccess"), id) });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "重命名容器失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("container.renameFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> UpdateContainer(string id, UpdateContainerResourcesRequest request, IContainerService containerService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                await containerService.UpdateContainerResourcesAsync(id, request);
                return TypedResults.Ok(new MessageResponse { Message = string.Format(localization.GetMessage("container.configUpdateSuccess"), id) });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "更新容器配置失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("container.configUpdateFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ExportContainer(string id, IContainerService containerService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var data = await containerService.ExportContainerAsync(id);
                var container = await containerService.GetContainerAsync(id);
                var filename = $"{container?.Name?.TrimStart('/') ?? id.Substring(0, 12)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.tar";

                return TypedResults.File(data, "application/x-tar", filename);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "导出容器失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("container.exportFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> RecreateContainer(string id, RecreateContainerRequest? request, IContainerService containerService, IHubContext<DockerPanelHub> hubContext, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                request ??= new RecreateContainerRequest();

                var recreateId = $"recreate-{id}";
                var imageName = (await containerService.GetContainerAsync(id))?.Image;

                IProgress<ImagePullProgress>? pullProgress = null;
                if (request.PullLatest && !string.IsNullOrEmpty(imageName))
                {
                    await DockerPanelHub.BroadcastImagePullProgress(hubContext, recreateId, imageName, "准备中", 5, "正在拉取最新镜像...");
                    // 使用按层聚合 + 单调递增的广播器，避免多个层并发导致整体进度来回跳动
                    pullProgress = DockerPanelHub.CreatePullProgressBroadcaster(hubContext, recreateId, imageName);
                }

                var newContainer = await containerService.RecreateContainerAsync(
                    id,
                    pullLatest: request.PullLatest,
                    autoStart: request.AutoStart,
                    progress: pullProgress);

                if (request.PullLatest && !string.IsNullOrEmpty(imageName))
                {
                    await DockerPanelHub.BroadcastImagePullProgress(hubContext, recreateId, imageName!, "完成", 100, "镜像拉取完成");
                }

                return TypedResults.Ok(new ContainerRecreateResponse
                {
                    Message = "容器重建成功",
                    OldId = id,
                    NewId = newContainer.Id,
                    Name = newContainer.Name
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "重建容器失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("container.rebuildFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetContainerFiles(string id, IContainerService containerService, ILocalizationService localization, ILogger<LoggingTag> logger, string path = "/", string? nodeId = null)
        {
            try
            {
                var files = await containerService.GetContainerFilesAsync(id, path, nodeId);
                return TypedResults.Ok(files);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取容器文件列表失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("container.fileListFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetContainerMounts(string id, IContainerService containerService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                var mounts = await containerService.GetContainerMountsAsync(id, nodeId);
                return TypedResults.Ok(mounts);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取容器挂载点信息失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("container.mountInfoFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> DownloadContainerFile(string id, IContainerService containerService, ILocalizationService localization, ILogger<LoggingTag> logger, string path, string? nodeId = null)
        {
            try
            {
                var content = await containerService.DownloadContainerFileAsync(id, path, nodeId);
                var fileName = System.IO.Path.GetFileName(path);
                return TypedResults.File(content, "application/octet-stream", fileName);
            }
            catch (FileNotFoundException ex)
            {
                return TypedResults.NotFound(new ApiErrorResponse { Message = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "下载容器文件失败: {Id}, {Path}", id, path);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("container.downloadFileFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> UploadContainerFile(string id, IFormFile file, string path, IContainerService containerService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                var content = memoryStream.ToArray();

                await containerService.UploadContainerFileAsync(id, path, file.FileName, content, nodeId);
                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("container.uploadSuccess") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "上传文件到容器失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("container.uploadFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> CreateContainerFolder(string id, CreateFolderRequest request, IContainerService containerService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                await containerService.CreateContainerFolderAsync(id, request.Path, request.Name, nodeId);
                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("container.createFolderSuccess") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "创建文件夹失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("container.createFolderFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> RenameContainerFile(string id, RenameFileRequest request, IContainerService containerService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                await containerService.RenameContainerFileAsync(id, request.Path, request.OldName, request.NewName, nodeId);
                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("container.renameFileSuccess") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "重命名文件失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("container.renameFileFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> DeleteContainerFile(string id, IContainerService containerService, ILocalizationService localization, ILogger<LoggingTag> logger, string path, bool recursive = false, string? nodeId = null)
        {
            try
            {
                await containerService.DeleteContainerFileAsync(id, path, recursive, nodeId);
                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("container.deleteFileSuccess") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "删除文件失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("container.deleteFileFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetContainerFileContent(string id, IContainerService containerService, ILocalizationService localization, ILogger<LoggingTag> logger, string path, string? nodeId = null)
        {
            try
            {
                var content = await containerService.GetContainerFileContentAsync(id, path, nodeId);
                return TypedResults.Ok(new FileContentResponse { Content = content, Path = path });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取文件内容失败: {Id}, {Path}", id, path);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("container.getFileContentFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> WriteContainerFileContent(string id, WriteFileContentRequest request, IContainerService containerService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                await containerService.WriteContainerFileContentAsync(id, request.Path, request.Content, nodeId);
                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("container.saveFileSuccess") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "写入文件内容失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("container.saveFileFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ChangeContainerFilePermissions(string id, ChangePermissionsRequest request, IContainerService containerService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                await containerService.ChangeContainerFilePermissionsAsync(id, request.Path, request.Permissions, nodeId);
                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("container.chmodSuccess") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "修改文件权限失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("container.chmodFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }
    }
}
