using Microsoft.AspNetCore.Mvc;
using DockerPanel.API.Services;
using DockerPanel.API.Models;
using Microsoft.AspNetCore.SignalR;
using DockerPanel.API.Hubs;
using System.IO.Compression;
using System.Formats.Tar;
using IOFile = System.IO.File;

namespace DockerPanel.API.Controllers;

/// <summary>
/// 镜像管理控制器
/// </summary>
[ApiController]
[Route("api/images")]
public class ImageController : ControllerBase
{
    private readonly IImageService _imageService;
    private readonly ILogger<ImageController> _logger;
    private readonly IHubContext<DockerPanelHub> _hubContext;
    private readonly BackgroundTaskService _taskService;
    private readonly ILocalizationService _localization;

    public ImageController(IImageService imageService, ILogger<ImageController> logger, IHubContext<DockerPanelHub> hubContext, BackgroundTaskService taskService, ILocalizationService localization)
    {
        _imageService = imageService;
        _logger = logger;
        _hubContext = hubContext;
        _taskService = taskService;
        _localization = localization;
    }

    /// <summary>
    /// 获取镜像列表
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ImageInfo>>> GetImages([FromQuery] string? nodeId = null)
    {
        try
        {
            var images = await _imageService.GetImagesAsync(nodeId);
            return Ok(images);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取镜像列表失败");
            return StatusCode(500, new { error = _localization.GetMessage("image.listFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 根据ID获取镜像详情
    /// </summary>
    [HttpGet("{imageId}")]
    public async Task<ActionResult<ImageDetailInfo>> GetImage(string imageId, [FromQuery] string? nodeId = null)
    {
        try
        {
            var image = await _imageService.GetImageAsync(imageId);
            if (image == null)
            {
                return NotFound(new { error = _localization.GetMessage("image.notFound"), imageId });
            }
            return Ok(image);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取镜像详情失败: {ImageId}", imageId);
            return StatusCode(500, new { error = _localization.GetMessage("image.detailFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 拉取镜像（后台异步执行）
    /// </summary>
    [HttpPost("pull")]
    public async Task<ActionResult> PullImage([FromBody] PullImageRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var pullId = $"pull-{Guid.NewGuid():N}";
            var fullImageName = $"{request.ImageName}:{request.Tag ?? "latest"}";

            // 后台执行拉取任务
            _ = Task.Run(async () =>
            {
                try
                {
                    await DockerPanelHub.BroadcastImagePullProgress(_hubContext, pullId, fullImageName, "准备中", 5, "正在连接仓库...");

                    var progress = new Progress<ImagePullProgress>(p =>
                    {
                        var status = p.Status ?? "";
                        var detail = p.Id != null ? $"{p.Id}: {status}" : status;
                        var progressValue = p.Current > 0 && p.Total > 0 
                            ? (int)((double)p.Current / p.Total * 80) + 10 
                            : 20;
                        
                        DockerPanelHub.BroadcastImagePullProgress(_hubContext, pullId, fullImageName, "拉取中", progressValue, detail).Wait();
                    });

                    await _imageService.PullImageAsync(request.ImageName, request.Tag, request.NodeId, progress, request.Registry);
                    
                    await DockerPanelHub.BroadcastImagePullProgress(_hubContext, pullId, fullImageName, "完成", 100, "拉取完成");
                }
                catch (Docker.DotNet.DockerApiException ex)
                {
                    _logger.LogError(ex, "拉取镜像失败: {ImageName}:{Tag}", request.ImageName, request.Tag);
                    
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
                    
                    await DockerPanelHub.BroadcastImagePullProgress(_hubContext, pullId, fullImageName, "失败", 100, userMessage);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "拉取镜像失败: {ImageName}:{Tag}", request.ImageName, request.Tag);
                    await DockerPanelHub.BroadcastImagePullProgress(_hubContext, pullId, fullImageName, "失败", 100, ex.Message);
                }
            });

            // 立即返回
            return Ok(new { message = _localization.GetMessage("image.pullStarted"), pullId, imageName = request.ImageName, tag = request.Tag });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动拉取任务失败: {ImageName}:{Tag}", request.ImageName, request.Tag);
            return StatusCode(500, new { error = "启动拉取任务失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 删除镜像
    /// </summary>
    [HttpDelete("{imageId}")]
    public async Task<ActionResult> RemoveImage(string imageId, [FromQuery] bool force = false, [FromQuery] string? nodeId = null)
    {
        try
        {
            await _imageService.RemoveImageAsync(imageId, force);
            return Ok(new { message = _localization.GetMessage("image.deleteSuccess"), imageId, force });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除镜像失败: {ImageId}", imageId);
            return StatusCode(500, new { error = "删除镜像失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 标记镜像
    /// </summary>
    [HttpPost("{sourceImageId}/tag")]
    public async Task<ActionResult> TagImage(string sourceImageId, [FromBody] TagImageRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            await _imageService.TagImageAsync(sourceImageId, request.TargetRepository + (string.IsNullOrEmpty(request.TargetTag) ? "" : ":" + request.TargetTag));
            return Ok(new {
                message = "镜像标记成功",
                sourceImageId,
                targetRepository = request.TargetRepository,
                targetTag = request.TargetTag
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "标记镜像失败: {SourceImageId} -> {TargetRepository}:{TargetTag}",
                sourceImageId, request.TargetRepository, request.TargetTag);
            return StatusCode(500, new { error = "标记镜像失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 推送镜像
    /// </summary>
    [HttpPost("{imageName}/push")]
    public ActionResult PushImage(string imageName, [FromBody] PushImageRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var pushId = $"push-{Guid.NewGuid():N}"[..12];
            var tag = request.Tag ?? "latest";
            var fullImageName = $"{imageName}:{tag}";

            // 注册后台任务
            var taskTitle = $"推送镜像: {fullImageName}";
            _taskService.AddTask(pushId, "image-push", taskTitle, new Dictionary<string, object>
            {
                ["imageName"] = fullImageName
            });

            // 后台执行推送
            _ = Task.Run(async () =>
            {
                try
                {
                    _taskService.UpdateTask(pushId, "running", 0, "启动推送...");
                    await DockerPanelHub.BroadcastImagePushProgress(_hubContext, pushId, fullImageName, "启动中", 0);
                    
                    var progress = new Progress<ImagePushProgress>(p =>
                    {
                        var progressPercent = p.Total > 0 ? (int)((double)p.Current / p.Total * 100) : 0;
                        var step = string.IsNullOrEmpty(p.Id) ? p.Status : $"{p.Id}: {p.Status}";
                        _taskService.UpdateTask(pushId, "running", progressPercent, step);
                        _ = DockerPanelHub.BroadcastImagePushProgress(_hubContext, pushId, fullImageName, step, progressPercent, p.Status);
                    });

                    await _imageService.PushImageAsync(imageName, tag, progress);
                    
                    _taskService.CompleteTask(pushId, "推送成功");
                    await DockerPanelHub.BroadcastImagePushProgress(_hubContext, pushId, fullImageName, "完成", 100, "推送成功");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "推送镜像失败: {ImageName}:{Tag}", imageName, tag);
                    _taskService.FailTask(pushId, ex.Message);
                    await DockerPanelHub.BroadcastImagePushProgress(_hubContext, pushId, fullImageName, "失败", 0, ex.Message);
                }
            });

            return Ok(new { pushId, imageName, tag, message = _localization.GetMessage("image.pushStarted") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动推送镜像任务失败: {ImageName}:{Tag}", imageName, request.Tag);
            return StatusCode(500, new { error = "启动推送任务失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 搜索镜像
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<ImageSearchResult>>> SearchImages([FromQuery] string term)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return BadRequest(new { error = "搜索关键词不能为空" });
            }

            var results = await _imageService.SearchImagesAsync(term);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "搜索镜像失败: {Term}", term);
            return StatusCode(500, new { error = "搜索镜像失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 获取镜像构建历史
    /// </summary>
    [HttpGet("{imageId}/history")]
    public async Task<ActionResult<IEnumerable<ImageHistoryEntry>>> GetImageHistory(string imageId, [FromQuery] string? nodeId = null)
    {
        try
        {
            var history = await _imageService.GetImageHistoryAsync(imageId);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取镜像历史失败: {ImageId}", imageId);
            return StatusCode(500, new { error = "获取镜像历史失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 测试构建镜像端点
    /// </summary>
    [HttpPost("build-test")]
    public ActionResult BuildImageTest()
    {
        _logger.LogInformation("构建镜像测试端点被调用");
        return Ok(new { message = _localization.GetMessage("image.testEndpointOk") });
    }

    /// <summary>
    /// 构建镜像（支持两种模式，后台执行并推送进度）
    /// </summary>
    [HttpPost("build")]
    [RequestSizeLimit(1_073_741_824)]
    [RequestFormLimits(MultipartBodyLengthLimit = 1_073_741_824)]
    public async Task<ActionResult> BuildImage()
    {
        try
        {
            var form = await Request.ReadFormAsync();
            
            var mode = form["mode"].FirstOrDefault();
            var tag = form["tag"].FirstOrDefault();
            var buildArgs = form["buildArgs"].FirstOrDefault();
            var dockerfileContent = form["dockerfileContent"].FirstOrDefault();
            var dockerfilePath = form["dockerfilePath"].FirstOrDefault();
            var noCacheStr = form["noCache"].FirstOrDefault();
            var file = form.Files.FirstOrDefault();
            
            _logger.LogInformation("构建镜像请求: mode={Mode}, tag={Tag}, dockerfileContent长度={DockerfileLen}, file={HasFile}", 
                mode, tag, dockerfileContent?.Length ?? 0, file != null);

            if (string.IsNullOrEmpty(mode))
            {
                return BadRequest(new { error = "缺少 mode 参数" });
            }

            if (string.IsNullOrEmpty(tag))
            {
                return BadRequest(new { error = "请指定镜像标签" });
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
            _taskService.AddTask(buildId, "image-build", taskTitle, new Dictionary<string, object>
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
                    _taskService.FailTask(buildId, "请提供 Dockerfile 内容");
                    return BadRequest(new { error = "请提供 Dockerfile 内容" });
                }
                dockerfileContentCopy = dockerfileContent;
            }
            else
            {
                // 压缩包模式：保存到临时文件
                if (file == null || file.Length == 0)
                {
                    _taskService.FailTask(buildId, "请上传压缩包文件");
                    return BadRequest(new { error = "请上传压缩包文件" });
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
                    _logger.LogInformation("ZIP 文件已保存，将在后台转换为 TAR: {FileName}", file.FileName);
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
                    _taskService.FailTask(buildId, "不支持的文件格式");
                    return BadRequest(new { error = "不支持的文件格式，请上传 .tar, .tar.gz, .tgz 或 .zip 文件" });
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
                        _taskService.UpdateTask(buildId, "running", 5, "Converting file format...");
                        await DockerPanelHub.BroadcastImageBuildProgress(_hubContext, buildId, "build.preparing", 5, "Converting ZIP to TAR format...");
                        
                        var tarFilePath = Path.Combine(Path.GetTempPath(), $"build_{buildId}.tar");
                        using (var zipStream = IOFile.OpenRead(tempFilePath))
                        {
                            await ConvertZipToTarFileAsync(zipStream, tarFilePath);
                        }
                        
                        // 删除原始 ZIP 文件
                        IOFile.Delete(tempFilePath);
                        actualTempFilePath = tarFilePath;
                        
                        _logger.LogInformation("ZIP 转换为 TAR 完成: {TarFile}", tarFilePath);
                    }
                    
                    _taskService.UpdateTask(buildId, "running", 10, "Initializing build environment...");
                    await DockerPanelHub.BroadcastImageBuildProgress(_hubContext, buildId, "build.preparing", 10, "Initializing build environment...");

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
                        _taskService.UpdateTask(buildId, "running", 20, "Building from Dockerfile...");
                        await DockerPanelHub.BroadcastImageBuildProgress(_hubContext, buildId, "build.building", 20, "Building from Dockerfile...");
                        imageId = await _imageService.BuildImageFromDockerfileAsync(dockerfileContentCopy!, parameters, new Progress<ImageBuildProgress>(p =>
                        {
                            _taskService.UpdateTask(buildId, "running", 50, null, p.Stream);
                            DockerPanelHub.BroadcastImageBuildProgress(_hubContext, buildId, "build.building", 50, null, p.Stream).Wait();
                        }));
                    }
                    else if (!string.IsNullOrEmpty(actualTempFilePath) && IOFile.Exists(actualTempFilePath))
                    {
                        _taskService.UpdateTask(buildId, "running", 20, "Building from context...");
                        await DockerPanelHub.BroadcastImageBuildProgress(_hubContext, buildId, "build.building", 20, "Building from context...");
                        using var contextStream = IOFile.OpenRead(actualTempFilePath);
                        imageId = await _imageService.BuildImageFromContextAsync(contextStream, parameters, new Progress<ImageBuildProgress>(p =>
                        {
                            _taskService.UpdateTask(buildId, "running", 50, null, p.Stream);
                            DockerPanelHub.BroadcastImageBuildProgress(_hubContext, buildId, "build.building", 50, null, p.Stream).Wait();
                        }));
                    }

                    if (!string.IsNullOrEmpty(imageId))
                    {
                        _taskService.CompleteTask(buildId, $"Image build succeeded: {tag}");
                        await DockerPanelHub.BroadcastImageBuildProgress(_hubContext, buildId, "build.completed", 100, $"Image build succeeded: {tag}");
                        _logger.LogInformation("镜像构建成功: {Tag}, ID: {ImageId}", tag, imageId);
                    }
                    else
                    {
                        _taskService.FailTask(buildId, "Build completed but no image ID returned");
                        await DockerPanelHub.BroadcastImageBuildProgress(_hubContext, buildId, "build.failed", 100, "Build completed but no image ID returned", null, true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "构建镜像失败: {Tag}", tag);
                    _taskService.FailTask(buildId, ex.Message);
                    await DockerPanelHub.BroadcastImageBuildProgress(_hubContext, buildId, "build.failed", 100, ex.Message, null, true);
                }
                finally
                {
                    // 清理临时文件（使用实际文件路径，可能是转换后的 tar 文件）
                    if (!string.IsNullOrEmpty(actualTempFilePath) && IOFile.Exists(actualTempFilePath))
                    {
                        try
                        {
                            IOFile.Delete(actualTempFilePath);
                            _logger.LogInformation("已清理临时文件: {TempFile}", actualTempFilePath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "清理临时文件失败: {TempFile}", actualTempFilePath);
                        }
                    }
                }
            });

            // 立即返回构建 ID
            return Ok(new { 
                message = "构建任务已提交", 
                buildId, 
                tag,
                mode = buildMode 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "构建镜像失败");
            return StatusCode(500, new { error = "构建镜像失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 获取镜像层级信息
    /// </summary>
    [HttpGet("{imageId}/layers")]
    public async Task<ActionResult<ImageLayersInfo>> GetImageLayers(string imageId, [FromQuery] string? nodeId = null)
    {
        try
        {
            var layers = await _imageService.GetImageLayersAsync(imageId, nodeId);
            return Ok(layers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取镜像层级信息失败: {ImageId}", imageId);
            return StatusCode(500, new { error = "获取镜像层级信息失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 获取镜像详细信息（Docker inspect）
    /// </summary>
    [HttpGet("{imageId}/inspect")]
    public async Task<ActionResult<ImageInspect>> InspectImage(string imageId, [FromQuery] string? nodeId = null)
    {
        try
        {
            var inspect = await _imageService.InspectImageAsync(imageId);
            if (inspect == null)
            {
                return NotFound(new { error = "镜像不存在或获取详细信息失败", imageId });
            }
            return Ok(inspect);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取镜像详细信息失败: {ImageId}", imageId);
            return StatusCode(500, new { error = "获取镜像详细信息失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 导出镜像
    /// </summary>
    [HttpGet("{imageId}/export")]
    public async Task<ActionResult> ExportImage(string imageId, [FromQuery] string? nodeId = null)
    {
        try
        {
            var imageData = await _imageService.SaveImageAsync(imageId);
            if (imageData == null || imageData.Length == 0)
            {
                return NotFound(new { error = "镜像不存在或导出失败", imageId });
            }

            var fileName = $"{imageId.Replace(":", "_").Replace("/", "_")}.tar";
            return File(imageData, "application/x-tar", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出镜像失败: {ImageId}", imageId);
            return StatusCode(500, new { error = "导出镜像失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 导入镜像
    /// </summary>
    [HttpPost("import")]
    public async Task<ActionResult> ImportImage([FromForm] IFormFile file, [FromQuery] string? nodeId = null)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "未选择镜像文件" });
            }

            using var stream = file.OpenReadStream();
            var loadedImages = await _imageService.LoadImageAsync(stream);
            
            _logger.LogInformation("导入镜像成功: {Images}", string.Join(", ", loadedImages));
            return Ok(new { message = _localization.GetMessage("image.importSuccess"), images = loadedImages });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入镜像失败");
            return StatusCode(500, new { error = "导入镜像失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 批量删除镜像
    /// </summary>
    [HttpDelete("batch")]
    public async Task<ActionResult<ImageBatchOperationResult>> BatchRemoveImages([FromBody] BatchRemoveImagesRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _imageService.BatchRemoveImagesAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量删除镜像失败");
            return StatusCode(500, new { error = "批量删除镜像失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 获取镜像统计信息
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult<ImageStatistics>> GetImageStatistics([FromQuery] string? nodeId = null)
    {
        try
        {
            var statistics = await _imageService.GetImageStatisticsAsync(nodeId);
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取镜像统计信息失败");
            return StatusCode(500, new { error = "获取镜像统计信息失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 清理未使用的镜像
    /// </summary>
    [HttpPost("prune")]
    public async Task<ActionResult<PruneResult>> PruneImages([FromBody] PruneImagesRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var options = new PruneOptions
            {
                Dangling = request.Dangling,
                All = request.All,
                Filter = request.Filter,
                KeepUntil = request.KeepUntil,
                KeepUntilDuration = request.KeepUntilDuration
            };

            var result = await _imageService.PruneImagesAsync(options, request.NodeId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理镜像失败");
            return StatusCode(500, new { error = "清理镜像失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 将 ZIP 文件转换为 TAR 格式
    /// </summary>
    /// <summary>
    /// 将 ZIP 文件转换为 TAR 格式，直接写入临时文件
    /// </summary>
    private async Task<string> ConvertZipToTarFileAsync(Stream zipStream, string outputPath)
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

    /// <summary>
    /// 将 ZIP 文件转换为 TAR 格式（内存模式，仅用于小文件）
    /// </summary>
    private async Task<MemoryStream> ConvertZipToTarAsync(Stream zipStream)
    {
        var tarMemoryStream = new MemoryStream();
        
        using (var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true))
        await using (var tarWriter = new TarWriter(tarMemoryStream, TarEntryFormat.Pax, leaveOpen: true))
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

        tarMemoryStream.Position = 0;
        return tarMemoryStream;
    }
}

/// <summary>
/// 拉取镜像请求
/// </summary>
public class PullImageRequest
{
    public string ImageName { get; set; } = string.Empty;
    public string? Tag { get; set; }
    public string? NodeId { get; set; }
    public string? ConnectionId { get; set; }
    /// <summary>
    /// 镜像加速器ID（可选），指定后使用加速器拉取镜像
    /// </summary>
    public string? Registry { get; set; }
}

/// <summary>
/// 标记镜像请求
/// </summary>
public class TagImageRequest
{
    public string TargetRepository { get; set; } = string.Empty;
    public string? TargetTag { get; set; }
    public string? NodeId { get; set; }
}

/// <summary>
/// 推送镜像请求
/// </summary>
public class PushImageRequest
{
    public string? Tag { get; set; }
    public string? NodeId { get; set; }
}


/// <summary>
/// 镜像历史条目
/// </summary>
public class ImageHistoryEntry
{
    public string Id { get; set; } = string.Empty;
    public DateTime Created { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string[] Tags { get; set; } = Array.Empty<string>();
    public long Size { get; set; }
    public string Comment { get; set; } = string.Empty;
}
