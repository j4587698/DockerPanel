using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Formats.Tar;
using DockerPanel.API.Hubs;
using DockerPanel.API.Models;
using DockerPanel.API.Serialization;
using DockerPanel.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;
using IOFile = System.IO.File;

namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// 镜像管理 Minimal API 端点（原 ImageController）。
    /// </summary>
    public static class ImageEndpoints
    {
        /// <summary>
        /// 日志类别标记（静态类不能作泛型参数）。
        /// </summary>
        public sealed class LoggingTag
        {
        }

        /// <summary>
        /// 映射镜像管理相关路由。
        /// </summary>
        public static IEndpointRouteBuilder MapImageEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/images");

            group.MapGet("", GetImages);
            group.MapGet("{imageId}", GetImage);
            group.MapPost("pull", PullImage);
            group.MapDelete("{imageId}", RemoveImage);
            group.MapPost("{sourceImageId}/tag", TagImage);
            group.MapPost("{imageName}/push", PushImage);
            group.MapGet("search", SearchImages);
            group.MapGet("{imageId}/history", GetImageHistory);
            group.MapPost("build-test", BuildImageTest);
            group.MapPost("build", BuildImage);
            group.MapGet("{imageId}/layers", GetImageLayers);
            group.MapGet("{imageId}/inspect", InspectImage);
            group.MapGet("{imageId}/export", ExportImage);
            group.MapPost("import", ImportImage);
            group.MapDelete("batch", BatchRemoveImages);
            group.MapGet("statistics", GetImageStatistics);
            group.MapPost("prune", PruneImages);

            return app;
        }

        private static async Task<IResult> GetImages(IImageService imageService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                var images = await imageService.GetImagesAsync(nodeId);
                return TypedResults.Ok(images);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取镜像列表失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("image.listFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetImage(string imageId, IImageService imageService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                var image = await imageService.GetImageAsync(imageId, nodeId);
                if (image == null)
                {
                    return TypedResults.NotFound(new ImageNotFoundResponse
                    {
                        Error = localization.GetMessage("image.notFound"),
                        ImageId = imageId
                    });
                }
                return TypedResults.Ok(image);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取镜像详情失败: {ImageId}", imageId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("image.detailFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> PullImage(PullImageRequest request, IImageService imageService, IHubContext<DockerPanelHub> hubContext, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var pullId = $"pull-{Guid.NewGuid():N}";
                var fullImageName = $"{request.ImageName}:{request.Tag ?? "latest"}";

                // 后台执行拉取任务
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await DockerPanelHub.BroadcastImagePullProgress(hubContext, pullId, fullImageName, "准备中", 5, "正在连接仓库...");

                        var progress = DockerPanelHub.CreatePullProgressBroadcaster(hubContext, pullId, fullImageName);

                        await imageService.PullImageAsync(request.ImageName, request.Tag, request.NodeId, progress, request.Registry);

                        await DockerPanelHub.BroadcastImagePullProgress(hubContext, pullId, fullImageName, "完成", 100, "拉取完成");
                    }
                    catch (Docker.DotNet.DockerApiException ex)
                    {
                        logger.LogError(ex, "拉取镜像失败: {ImageName}:{Tag}", request.ImageName, request.Tag);

                        var userMessage = "拉取镜像失败";
                        if (ex.Message.Contains("pull access denied", StringComparison.OrdinalIgnoreCase))
                        {
                            userMessage = "拉取被拒绝，可能原因：用户名或密码错误、没有访问权限或仓库不存在";
                        }
                        else if (ex.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
                        {
                            userMessage = "认证失败，请检查仓库凭据是否正确";
                        }
                        else if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                        {
                            userMessage = "镜像不存在，请检查镜像名称和标签";
                        }
                        else if (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                        {
                            userMessage = "连接超时，请检查网络";
                        }

                        await DockerPanelHub.BroadcastImagePullProgress(hubContext, pullId, fullImageName, "失败", 100, userMessage);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "拉取镜像失败: {ImageName}:{Tag}", request.ImageName, request.Tag);
                        await DockerPanelHub.BroadcastImagePullProgress(hubContext, pullId, fullImageName, "失败", 100, ex.Message);
                    }
                });

                // 立即返回
                return TypedResults.Ok(new ImagePullStartedResponse
                {
                    Message = localization.GetMessage("image.pullStarted"),
                    PullId = pullId,
                    ImageName = request.ImageName,
                    Tag = request.Tag
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "启动拉取任务失败: {ImageName}:{Tag}", request.ImageName, request.Tag);
                return TypedResults.Json(new ApiErrorResponse { Error = "启动拉取任务失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> RemoveImage(string imageId, IImageService imageService, ILocalizationService localization, ILogger<LoggingTag> logger, bool force = false, string? nodeId = null)
        {
            try
            {
                await imageService.RemoveImageAsync(imageId, force, nodeId);
                return TypedResults.Ok(new ImageDeleteResponse
                {
                    Message = localization.GetMessage("image.deleteSuccess"),
                    ImageId = imageId,
                    Force = force
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "删除镜像失败: {ImageId}, NodeId={NodeId}", imageId, nodeId ?? "<default>");
                return TypedResults.Json(new ApiErrorResponse { Error = "删除镜像失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> TagImage(string sourceImageId, TagImageRequest request, IImageService imageService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                await imageService.TagImageAsync(sourceImageId, request.TargetRepository + (string.IsNullOrEmpty(request.TargetTag) ? "" : ":" + request.TargetTag));
                return TypedResults.Ok(new ImageTagResponse
                {
                    Message = "镜像标记成功",
                    SourceImageId = sourceImageId,
                    TargetRepository = request.TargetRepository,
                    TargetTag = request.TargetTag
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "标记镜像失败: {SourceImageId} -> {TargetRepository}:{TargetTag}",
                    sourceImageId, request.TargetRepository, request.TargetTag);
                return TypedResults.Json(new ApiErrorResponse { Error = "标记镜像失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static IResult PushImage(string imageName, PushImageRequest request, IImageService imageService, IHubContext<DockerPanelHub> hubContext, BackgroundTaskService taskService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var pushId = $"push-{Guid.NewGuid():N}"[..12];
                var tag = request.Tag ?? "latest";
                var fullImageName = $"{imageName}:{tag}";

                // 注册后台任务
                var taskTitle = $"推送镜像: {fullImageName}";
                taskService.AddTask(pushId, "image-push", taskTitle, new Dictionary<string, object>
                {
                    ["imageName"] = fullImageName
                });

                // 后台执行推送
                _ = Task.Run(async () =>
                {
                    try
                    {
                        taskService.UpdateTask(pushId, "running", 0, "启动推送...");
                        await DockerPanelHub.BroadcastImagePushProgress(hubContext, pushId, fullImageName, "启动中", 0);

                        var progress = new Progress<ImagePushProgress>(p =>
                        {
                            var progressPercent = p.Total > 0 ? (int)((double)p.Current / p.Total * 100) : 0;
                            var step = string.IsNullOrEmpty(p.Id) ? p.Status : $"{p.Id}: {p.Status}";
                            taskService.UpdateTask(pushId, "running", progressPercent, step);
                            _ = DockerPanelHub.BroadcastImagePushProgress(hubContext, pushId, fullImageName, step, progressPercent, p.Status);
                        });

                        await imageService.PushImageAsync(imageName, tag, progress);

                        taskService.CompleteTask(pushId, "推送成功");
                        await DockerPanelHub.BroadcastImagePushProgress(hubContext, pushId, fullImageName, "完成", 100, "推送成功");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "推送镜像失败: {ImageName}:{Tag}", imageName, tag);
                        taskService.FailTask(pushId, ex.Message);
                        await DockerPanelHub.BroadcastImagePushProgress(hubContext, pushId, fullImageName, "失败", 0, ex.Message);
                    }
                });

                return TypedResults.Ok(new ImagePushStartedResponse
                {
                    PushId = pushId,
                    ImageName = imageName,
                    Tag = tag,
                    Message = localization.GetMessage("image.pushStarted")
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "启动推送镜像任务失败: {ImageName}:{Tag}", imageName, request.Tag);
                return TypedResults.Json(new ApiErrorResponse { Error = "启动推送任务失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> SearchImages(IImageService imageService, ILocalizationService localization, ILogger<LoggingTag> logger, string term)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(term))
                {
                    return TypedResults.BadRequest(new ApiErrorResponse { Error = "搜索关键词不能为空" });
                }

                var results = await imageService.SearchImagesAsync(term);
                return TypedResults.Ok(results);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "搜索镜像失败: {Term}", term);
                return TypedResults.Json(new ApiErrorResponse { Error = "搜索镜像失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetImageHistory(string imageId, IImageService imageService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                var history = await imageService.GetImageHistoryAsync(imageId);
                return TypedResults.Ok(history);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取镜像历史失败: {ImageId}", imageId);
                return TypedResults.Json(new ApiErrorResponse { Error = "获取镜像历史失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static IResult BuildImageTest(ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            logger.LogInformation("构建镜像测试端点被调用");
            return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("image.testEndpointOk") });
        }

        private static async Task<IResult> BuildImage(IImageService imageService, IHubContext<DockerPanelHub> hubContext, BackgroundTaskService taskService, ILocalizationService localization, ILogger<LoggingTag> logger, HttpRequest request)
        {
            try
            {
                var form = await request.ReadFormAsync();

                var mode = form["mode"].FirstOrDefault();
                var tag = form["tag"].FirstOrDefault();
                var buildArgs = form["buildArgs"].FirstOrDefault();
                var dockerfileContent = form["dockerfileContent"].FirstOrDefault();
                var dockerfilePath = form["dockerfilePath"].FirstOrDefault();
                var noCacheStr = form["noCache"].FirstOrDefault();
                var file = form.Files.FirstOrDefault();

                logger.LogInformation("构建镜像请求: mode={Mode}, tag={Tag}, dockerfileContent长度={DockerfileLen}, file={HasFile}",
                    mode, tag, dockerfileContent?.Length ?? 0, file != null);

                if (string.IsNullOrEmpty(mode))
                {
                    return TypedResults.BadRequest(new ApiErrorResponse { Error = "缺少 mode 参数" });
                }

                if (string.IsNullOrEmpty(tag))
                {
                    return TypedResults.BadRequest(new ApiErrorResponse { Error = "请指定镜像标签" });
                }

                // 解析构建参数
                var buildArgsDict = new Dictionary<string, string>();
                if (!string.IsNullOrEmpty(buildArgs))
                {
                    foreach (var line in buildArgs.Split('\n'))
                    {
                        var trimmed = line.Trim();
                        if (!string.IsNullOrEmpty(trimmed) && trimmed.Contains('='))
                        {
                            var idx = trimmed.IndexOf('=');
                            buildArgsDict[trimmed.Substring(0, idx)] = trimmed.Substring(idx + 1);
                        }
                    }
                }

                var noCacheValue = noCacheStr?.ToLower() == "true";

                // 生成构建 ID
                var buildId = Guid.NewGuid().ToString("N")[..8];

                // 注册后台任务
                var taskTitle = $"构建镜像: {tag}";
                taskService.AddTask(buildId, "image-build", taskTitle, new Dictionary<string, object>
                {
                    ["tag"] = tag,
                    ["mode"] = mode
                });

                // 准备构建上下文
                string? tempFilePath = null;
                string? dockerfileContentCopy = dockerfileContent;
                string buildMode = mode;
                bool needsZipConversion = false;

                if (mode == "dockerfile")
                {
                    // Dockerfile 模式：只发送 Dockerfile 内容
                    if (string.IsNullOrWhiteSpace(dockerfileContent))
                    {
                        taskService.FailTask(buildId, "请提供 Dockerfile 内容");
                        return TypedResults.BadRequest(new ApiErrorResponse { Error = "请提供 Dockerfile 内容" });
                    }
                    dockerfileContentCopy = dockerfileContent;
                }
                else
                {
                    // 压缩包模式：保存到临时文件
                    if (file == null || file.Length == 0)
                    {
                        taskService.FailTask(buildId, "请上传压缩包文件");
                        return TypedResults.BadRequest(new ApiErrorResponse { Error = "请上传压缩包文件" });
                    }

                    var fileName = file.FileName.ToLower();

                    if (fileName.EndsWith(".zip"))
                    {
                        // ZIP 文件：先保存原始文件，后台再转换
                        needsZipConversion = true;
                        tempFilePath = Path.Combine(Path.GetTempPath(), $"build_{buildId}.zip");
                        using var zipStream = file.OpenReadStream();
                        using var fileStream = IOFile.Create(tempFilePath);
                        await zipStream.CopyToAsync(fileStream);
                        logger.LogInformation("ZIP 文件已保存，将在后台转换为 TAR: {FileName}", file.FileName);
                    }
                    else if (fileName.EndsWith(".tar") || fileName.EndsWith(".tar.gz") || fileName.EndsWith(".tgz"))
                    {
                        // TAR 格式直接保存到临时文件
                        tempFilePath = Path.Combine(Path.GetTempPath(), $"build_{buildId}{Path.GetExtension(fileName)}");
                        using var tarStream = file.OpenReadStream();
                        using var fileStream = IOFile.Create(tempFilePath);
                        await tarStream.CopyToAsync(fileStream);
                    }
                    else
                    {
                        taskService.FailTask(buildId, "不支持的文件格式");
                        return TypedResults.BadRequest(new ApiErrorResponse { Error = "不支持的文件格式，请上传 .tar, .tar.gz, .tgz 或 .zip 文件" });
                    }
                }

                // 启动后台构建任务
                _ = Task.Run(async () =>
                {
                    string? actualTempFilePath = tempFilePath;

                    try
                    {
                        // 如果需要转换 ZIP 为 TAR
                        if (needsZipConversion && !string.IsNullOrEmpty(tempFilePath))
                        {
                            taskService.UpdateTask(buildId, "running", 5, "Converting file format...");
                            await DockerPanelHub.BroadcastImageBuildProgress(hubContext, buildId, "build.preparing", 5, "Converting ZIP to TAR format...");

                            var tarFilePath = Path.Combine(Path.GetTempPath(), $"build_{buildId}.tar");
                            using (var zipStream = IOFile.OpenRead(tempFilePath))
                            {
                                await ConvertZipToTarFileAsync(zipStream, tarFilePath);
                            }

                            // 删除原始 ZIP 文件
                            IOFile.Delete(tempFilePath);
                            actualTempFilePath = tarFilePath;

                            logger.LogInformation("ZIP 转换为 TAR 完成: {TarFile}", tarFilePath);
                        }

                        taskService.UpdateTask(buildId, "running", 10, "Initializing build environment...");
                        await DockerPanelHub.BroadcastImageBuildProgress(hubContext, buildId, "build.preparing", 10, "Initializing build environment...");

                        var parameters = new BuildImageParams
                        {
                            Tag = tag,
                            Dockerfile = buildMode == "dockerfile" ? "Dockerfile" : (dockerfilePath ?? "./Dockerfile"),
                            BuildArgs = buildArgsDict,
                            NoCache = noCacheValue,
                            Remove = true
                        };

                        string? imageId = null;

                        if (buildMode == "dockerfile")
                        {
                            taskService.UpdateTask(buildId, "running", 20, "Building from Dockerfile...");
                            await DockerPanelHub.BroadcastImageBuildProgress(hubContext, buildId, "build.building", 20, "Building from Dockerfile...");
                            imageId = await imageService.BuildImageFromDockerfileAsync(dockerfileContentCopy!, parameters, new Progress<ImageBuildProgress>(p =>
                            {
                                taskService.UpdateTask(buildId, "running", 50, null, p.Stream);
                                DockerPanelHub.BroadcastImageBuildProgress(hubContext, buildId, "build.building", 50, null, p.Stream).Wait();
                            }));
                        }
                        else if (!string.IsNullOrEmpty(actualTempFilePath) && IOFile.Exists(actualTempFilePath))
                        {
                            taskService.UpdateTask(buildId, "running", 20, "Building from context...");
                            await DockerPanelHub.BroadcastImageBuildProgress(hubContext, buildId, "build.building", 20, "Building from context...");
                            using var contextStream = IOFile.OpenRead(actualTempFilePath);
                            imageId = await imageService.BuildImageFromContextAsync(contextStream, parameters, new Progress<ImageBuildProgress>(p =>
                            {
                                taskService.UpdateTask(buildId, "running", 50, null, p.Stream);
                                DockerPanelHub.BroadcastImageBuildProgress(hubContext, buildId, "build.building", 50, null, p.Stream).Wait();
                            }));
                        }

                        if (!string.IsNullOrEmpty(imageId))
                        {
                            taskService.CompleteTask(buildId, $"Image build succeeded: {tag}");
                            await DockerPanelHub.BroadcastImageBuildProgress(hubContext, buildId, "build.completed", 100, $"Image build succeeded: {tag}");
                            logger.LogInformation("镜像构建成功: {Tag}, ID: {ImageId}", tag, imageId);
                        }
                        else
                        {
                            taskService.FailTask(buildId, "Build completed but no image ID returned");
                            await DockerPanelHub.BroadcastImageBuildProgress(hubContext, buildId, "build.failed", 100, "Build completed but no image ID returned", null, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "构建镜像失败: {Tag}", tag);
                        taskService.FailTask(buildId, ex.Message);
                        await DockerPanelHub.BroadcastImageBuildProgress(hubContext, buildId, "build.failed", 100, ex.Message, null, true);
                    }
                    finally
                    {
                        // 清理临时文件（使用实际文件路径，可能是转换后的 tar 文件）
                        if (!string.IsNullOrEmpty(actualTempFilePath) && IOFile.Exists(actualTempFilePath))
                        {
                            try
                            {
                                IOFile.Delete(actualTempFilePath);
                                logger.LogInformation("已清理临时文件: {TempFile}", actualTempFilePath);
                            }
                            catch (Exception ex)
                            {
                                logger.LogWarning(ex, "清理临时文件失败: {TempFile}", actualTempFilePath);
                            }
                        }
                    }
                });

                // 立即返回构建 ID
                return TypedResults.Ok(new ImageBuildSubmittedResponse
                {
                    Message = "构建任务已提交",
                    BuildId = buildId,
                    Tag = tag,
                    Mode = buildMode
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "构建镜像失败");
                return TypedResults.Json(new ApiErrorResponse { Error = "构建镜像失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetImageLayers(string imageId, IImageService imageService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                var layers = await imageService.GetImageLayersAsync(imageId, nodeId);
                return TypedResults.Ok(layers);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取镜像层级信息失败: {ImageId}", imageId);
                return TypedResults.Json(new ApiErrorResponse { Error = "获取镜像层级信息失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> InspectImage(string imageId, IImageService imageService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                var inspect = await imageService.InspectImageAsync(imageId);
                if (inspect == null)
                {
                    return TypedResults.NotFound(new ImageNotFoundResponse
                    {
                        Error = "镜像不存在或获取详细信息失败",
                        ImageId = imageId
                    });
                }
                return TypedResults.Ok(inspect);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取镜像详细信息失败: {ImageId}", imageId);
                return TypedResults.Json(new ApiErrorResponse { Error = "获取镜像详细信息失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ExportImage(string imageId, IImageService imageService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                var imageData = await imageService.SaveImageAsync(imageId);
                if (imageData == null || imageData.Length == 0)
                {
                    return TypedResults.NotFound(new ImageNotFoundResponse
                    {
                        Error = "镜像不存在或导出失败",
                        ImageId = imageId
                    });
                }

                var fileName = $"{imageId.Replace(":", "_").Replace("/", "_")}.tar";
                return TypedResults.File(imageData, "application/x-tar", fileName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "导出镜像失败: {ImageId}", imageId);
                return TypedResults.Json(new ApiErrorResponse { Error = "导出镜像失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ImportImage(IFormFile file, IImageService imageService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return TypedResults.BadRequest(new ApiErrorResponse { Error = "未选择镜像文件" });
                }

                using var stream = file.OpenReadStream();
                var loadedImages = await imageService.LoadImageAsync(stream);

                logger.LogInformation("导入镜像成功: {Images}", string.Join(", ", loadedImages));
                return TypedResults.Ok(new ImageImportResponse
                {
                    Message = localization.GetMessage("image.importSuccess"),
                    Images = loadedImages
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "导入镜像失败");
                return TypedResults.Json(new ApiErrorResponse { Error = "导入镜像失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> BatchRemoveImages(BatchRemoveImagesRequest request, IImageService imageService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var result = await imageService.BatchRemoveImagesAsync(request);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "批量删除镜像失败");
                return TypedResults.Json(new ApiErrorResponse { Error = "批量删除镜像失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetImageStatistics(IImageService imageService, ILocalizationService localization, ILogger<LoggingTag> logger, string? nodeId = null)
        {
            try
            {
                var statistics = await imageService.GetImageStatisticsAsync(nodeId);
                return TypedResults.Ok(statistics);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取镜像统计信息失败");
                return TypedResults.Json(new ApiErrorResponse { Error = "获取镜像统计信息失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> PruneImages(PruneImagesRequest request, IImageService imageService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var options = new PruneOptions
                {
                    Dangling = request.Dangling,
                    All = request.All,
                    Filter = request.Filter,
                    KeepUntil = request.KeepUntil,
                    KeepUntilDuration = request.KeepUntilDuration
                };

                var result = await imageService.PruneImagesAsync(options, request.NodeId);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "清理镜像失败");
                return TypedResults.Json(new ApiErrorResponse { Error = "清理镜像失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        /// <summary>
        /// 将 ZIP 文件转换为 TAR 格式，直接写入临时文件
        /// </summary>
        private static async Task<string> ConvertZipToTarFileAsync(Stream zipStream, string outputPath)
        {
            await using var fileStream = IOFile.Create(outputPath);

            using (var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true))
            await using (var tarWriter = new TarWriter(fileStream, TarEntryFormat.Pax, leaveOpen: true))
            {
                foreach (var entry in zipArchive.Entries)
                {
                    // 跳过目录条目
                    if (string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith("/"))
                    {
                        continue;
                    }

                    // 创建 TAR 条目
                    var tarEntry = new PaxTarEntry(TarEntryType.RegularFile, entry.FullName)
                    {
                        ModificationTime = entry.LastWriteTime.UtcDateTime,
                    };

                    // 创建临时文件存储 ZIP 条目内容（避免内存问题）
                    var tempEntryPath = Path.Combine(Path.GetTempPath(), $"zip_entry_{Guid.NewGuid():N}");
                    try
                    {
                        await using (var entryStream = entry.Open())
                        await using (var tempFileStream = IOFile.Create(tempEntryPath))
                        {
                            await entryStream.CopyToAsync(tempFileStream);
                        }

                        await using var dataStream = IOFile.OpenRead(tempEntryPath);
                        tarEntry.DataStream = dataStream;
                        await tarWriter.WriteEntryAsync(tarEntry);
                    }
                    finally
                    {
                        // 清理临时文件
                        if (IOFile.Exists(tempEntryPath))
                        {
                            IOFile.Delete(tempEntryPath);
                        }
                    }
                }
            }

            return outputPath;
        }
    }
}
