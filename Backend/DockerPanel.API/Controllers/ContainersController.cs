using Microsoft.AspNetCore.Mvc;
using DockerPanel.API.Models;
using DockerPanel.API.Services;
using Microsoft.AspNetCore.SignalR;
using DockerPanel.API.Hubs;

namespace DockerPanel.API.Controllers;

/// <summary>
/// 容器管理控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ContainersController : ControllerBase
{
    private readonly IContainerService _containerService;
    private readonly DomainMappingService _domainMappingService;
    private readonly ILogger<ContainersController> _logger;
    private readonly IHubContext<DockerPanelHub> _hubContext;
    private readonly ILocalizationService _localization;

    public ContainersController(
        IContainerService containerService,
        DomainMappingService domainMappingService,
        ILogger<ContainersController> logger,
        IHubContext<DockerPanelHub> hubContext,
        ILocalizationService localization)
    {
        _containerService = containerService;
        _domainMappingService = domainMappingService;
        _logger = logger;
        _hubContext = hubContext;
        _localization = localization;
    }

    /// <summary>
    /// 获取容器列表
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ContainerInfo>>> GetContainers([FromQuery] string? nodeId = null, [FromQuery] bool all = false)
    {
        try
        {
            var containers = await _containerService.GetContainersAsync(nodeId, all);
            return Ok(containers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取容器列表失败");
            return BadRequest(new { message = _localization.GetMessage("container.listFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 获取容器详情
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ContainerInfo>> GetContainer(string id, [FromQuery] string? nodeId = null)
    {
        try
        {
            var container = await _containerService.GetContainerAsync(id, nodeId);
            if (container == null) return NotFound(new { message = _localization.GetMessage("container.notFound") });
            
            // 获取容器的域名映射信息
            var mappings = await _domainMappingService.GetContainerDomainMappingsAsync(id);
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
            
            return Ok(container);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取容器详情失败: {Id}", id);
            return BadRequest(new { message = _localization.GetMessage("container.detailFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 获取容器日志
    /// </summary>
    [HttpGet("{id}/logs")]
    public async Task<ActionResult<ContainerLogs>> GetContainerLogs(string id, [FromQuery] int tail = 100, [FromQuery] string? nodeId = null)
    {
        try
        {
            var logs = await _containerService.GetContainerLogsAsync(id, tail: tail, nodeId: nodeId);
            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取容器日志失败: {Id}", id);
            return BadRequest(new { message = _localization.GetMessage("container.logsFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 获取容器统计信息
    /// </summary>
    [HttpGet("{id}/stats")]
    public async Task<ActionResult<ContainerStats>> GetContainerStats(string id, [FromQuery] string? nodeId = null)
    {
        try
        {
            var stats = await _containerService.GetContainerStatsAsync(id, nodeId);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取容器统计信息失败: {Id}", id);
            return BadRequest(new { message = _localization.GetMessage("container.statsFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 在容器中执行命令
    /// </summary>
    [HttpPost("{id}/exec")]
    public async Task<ActionResult<ExecResult>> ExecuteCommand(string id, [FromBody] ExecCommandRequest request, [FromQuery] string? nodeId = null)
    {
        try
        {
            var result = await _containerService.ExecuteCommandAsync(id, request, nodeId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "容器命令执行失败: {Id}", id);
            return BadRequest(new { message = _localization.GetMessage("container.execFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 批量操作容器
    /// </summary>
    [HttpPost("batch")]
    public async Task<ActionResult<ContainerBatchOperationResult>> BatchOperation([FromBody] BatchContainerOperationRequest request)
    {
        try
        {
            var result = await _containerService.BatchOperationAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量容器操作失败: {Operation}", request.Operation);
            return BadRequest(new { message = _localization.GetMessage("container.batchFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 创建容器
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<string>> CreateContainer([FromBody] CreateContainerRequest request)
    {
        _logger.LogInformation("创建容器请求: Name={Name}, Image={Image}, NetworkMode={NetworkMode}, Network={NetworkId}",
            request?.Name, request?.Image, request?.NetworkMode, request?.Network?.NetworkId);

        // 调试：检查 ModelState
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            _logger.LogWarning("创建容器参数验证失败: {Errors}", string.Join(", ", errors));
            return BadRequest(new { message = _localization.GetMessage("container.paramValidationFailed"), errors });
        }

        if (request == null)
        {
            _logger.LogWarning("创建容器请求为空");
            return BadRequest(new { message = _localization.GetMessage("container.requestBodyEmpty") });
        }

        try
        {
            var progress = new Progress<ImagePullProgress>(async p =>
            {
                if (!string.IsNullOrEmpty(request.ConnectionId))
                {
                    await _hubContext.Clients.Client(request.ConnectionId).SendAsync("ImagePullUpdate", p);
                }
            });

            var container = await _containerService.CreateContainerAsync(request, progress);
            if (!string.IsNullOrEmpty(container.Id))
            {
                await _domainMappingService.ProcessContainerDomainMappingAsync(container.Id, request);
            }

            return Ok(container.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建容器失败");
            return BadRequest(new { message = _localization.GetMessage("container.createFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 启动容器
    /// </summary>
    [HttpPost("{id}/start")]
    public async Task<ActionResult> StartContainer(string id, [FromQuery] string? nodeId = null)
    {
        try
        {
            await _containerService.StartContainerAsync(id, nodeId);
            return Ok(new { message = string.Format(_localization.GetMessage("container.startSuccess"), id) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动容器失败: {Id}", id);
            return BadRequest(new { message = _localization.GetMessage("container.startFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 停止容器
    /// </summary>
    [HttpPost("{id}/stop")]
    public async Task<ActionResult> StopContainer(string id, [FromQuery] int timeout = 30, [FromQuery] string? nodeId = null)
    {
        try
        {
            await _containerService.StopContainerAsync(id, timeout, nodeId);
            return Ok(new { message = string.Format(_localization.GetMessage("container.stopSuccess"), id) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止容器失败: {Id}", id);
            return BadRequest(new { message = _localization.GetMessage("container.stopFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 重启容器
    /// </summary>
    [HttpPost("{id}/restart")]
    public async Task<ActionResult> RestartContainer(string id, [FromQuery] int timeout = 30, [FromQuery] string? nodeId = null)
    {
        try
        {
            await _containerService.RestartContainerAsync(id, timeout, nodeId);
            return Ok(new { message = string.Format(_localization.GetMessage("container.restartSuccess"), id) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重启容器失败: {Id}", id);
            return BadRequest(new { message = _localization.GetMessage("container.restartFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 删除容器
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> RemoveContainer(string id, [FromQuery] bool force = false, [FromQuery] string? nodeId = null)
    {
        try
        {
            await _containerService.RemoveContainerAsync(id, force, nodeId: nodeId);
            return Ok(new { message = string.Format(_localization.GetMessage("container.deleteSuccess"), id) });
        }
        catch (Docker.DotNet.DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            // 容器正在运行，需要强制删除
            _logger.LogWarning("删除容器失败，容器正在运行: {Id}", id);
            return BadRequest(new { message = _localization.GetMessage("container.runningCannotDelete"), error = _localization.GetMessage("container.pleaseStopFirst"), needForce = true });
        }
        catch (Docker.DotNet.DockerApiException ex)
        {
            _logger.LogError(ex, "删除容器失败: {Id}, StatusCode: {StatusCode}", id, ex.StatusCode);
            return BadRequest(new { message = _localization.GetMessage("container.deleteFailed"), error = ex.ResponseBody ?? ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除容器失败: {Id}", id);
            return BadRequest(new { message = _localization.GetMessage("container.deleteFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 重命名容器
    /// </summary>
    [HttpPost("{id}/rename")]
    public async Task<ActionResult> RenameContainer(string id, [FromBody] RenameContainerRequest request)
    {
        try
        {
            await _containerService.RenameContainerAsync(id, request.NewName);
            return Ok(new { message = string.Format(_localization.GetMessage("container.renameSuccess"), id) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重命名容器失败: {Id}", id);
            return BadRequest(new { message = _localization.GetMessage("container.renameFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 更新容器配置（可热更新的配置：重启策略、资源限制）
    /// </summary>
    [HttpPatch("{id}")]
    public async Task<ActionResult> UpdateContainer(string id, [FromBody] UpdateContainerResourcesRequest request)
    {
        try
        {
            await _containerService.UpdateContainerResourcesAsync(id, request);
            return Ok(new { message = string.Format(_localization.GetMessage("container.configUpdateSuccess"), id) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新容器配置失败: {Id}", id);
            return BadRequest(new { message = _localization.GetMessage("container.configUpdateFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 导出容器为tar文件
    /// </summary>
    [HttpGet("{id}/export")]
    public async Task<ActionResult> ExportContainer(string id)
    {
        try
        {
            var data = await _containerService.ExportContainerAsync(id);
            var container = await _containerService.GetContainerAsync(id);
            var filename = $"{container?.Name?.TrimStart('/') ?? id.Substring(0, 12)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.tar";
            
            return File(data, "application/x-tar", filename);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出容器失败: {Id}", id);
            return BadRequest(new { message = _localization.GetMessage("container.exportFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 重建容器（删除并使用相同配置重新创建）
    /// </summary>
    [HttpPost("{id}/recreate")]
    public async Task<ActionResult> RecreateContainer(string id, [FromBody] RecreateContainerRequest? request = null)
    {
        try
        {
            // 获取原容器信息
            var container = await _containerService.GetContainerAsync(id);
            if (container == null)
            {
                return NotFound(new { message = "容器未找到" });
            }

            // 构建创建请求
            var createRequest = new CreateContainerRequest
            {
                Name = container.Name?.TrimStart('/'),
                Image = container.Image,
                Entrypoint = container.Entrypoint,
                Command = container.Command,
                WorkingDir = container.WorkingDir,
                Hostname = container.HostName,
                NetworkMode = container.HostConfig?.NetworkMode ?? "bridge",
                Labels = container.Labels
            };

            // 转换环境变量
            if (container.Environment != null && container.Environment.Count > 0)
            {
                createRequest.Environment = new Dictionary<string, string>();
                foreach (var env in container.Environment)
                {
                    var idx = env.IndexOf('=');
                    if (idx > 0)
                    {
                        createRequest.Environment[env.Substring(0, idx)] = env.Substring(idx + 1);
                    }
                }
            }

            // 转换端口映射
            if (container.Ports != null && container.Ports.Count > 0)
            {
                createRequest.Ports = container.Ports
                    .Where(p => p.PublicPort > 0)
                    .Select(p => new PortMapping
                    {
                        ContainerPort = p.PrivatePort.ToString(),
                        HostPort = p.PublicPort.ToString(),
                        Protocol = p.Type ?? "tcp"
                    }).ToList();
            }

            // 转换卷映射
            if (container.Mounts != null && container.Mounts.Count > 0)
            {
                createRequest.Volumes = container.Mounts
                    .Select(m => new VolumeMapping
                    {
                        HostPath = m.Source ?? "",
                        ContainerPath = m.Destination ?? "",
                        ReadOnly = !m.Rw
                    }).ToList();
            }

            // 设置重启策略
            if (container.RestartPolicy != null)
            {
                createRequest.RestartPolicy = new RestartPolicy
                {
                    Name = container.RestartPolicy.Name ?? "no",
                    MaximumRetryCount = container.RestartPolicy.MaximumRetryCount
                };
            }

            // 如果需要拉取最新镜像
            if (request?.PullLatest == true)
            {
                _logger.LogInformation("重建容器 {Id} 时拉取最新镜像", id);
                // 拉取镜像的逻辑可以在这里添加
            }

            // 删除旧容器
            await _containerService.RemoveContainerAsync(id, force: true);
            _logger.LogInformation("已删除旧容器 {Id}", id);

            // 创建新容器
            var newContainer = await _containerService.CreateContainerAsync(createRequest);
            _logger.LogInformation("已创建新容器 {NewId}", newContainer.Id);

            // 如果原容器是运行状态或请求自动启动，则启动新容器
            if (container.State == "running" || request?.AutoStart == true)
            {
                await _containerService.StartContainerAsync(newContainer.Id);
                _logger.LogInformation("已启动新容器 {NewId}", newContainer.Id);
            }

            return Ok(new { 
                message = "容器重建成功", 
                oldId = id, 
                newId = newContainer.Id,
                name = container.Name
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重建容器失败: {Id}", id);
            return BadRequest(new { message = _localization.GetMessage("container.rebuildFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 获取容器文件列表
    /// </summary>
    [HttpGet("{id}/files")]
    public async Task<ActionResult<ContainerFileListResponse>> GetContainerFiles(string id, [FromQuery] string path = "/", [FromQuery] string? nodeId = null)
    {
        try
        {
            var files = await _containerService.GetContainerFilesAsync(id, path, nodeId);
            return Ok(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取容器文件列表失败: {Id}", id);
            return BadRequest(new { message = _localization.GetMessage("container.fileListFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 获取容器挂载点信息
    /// </summary>
    [HttpGet("{id}/mounts")]
    public async Task<ActionResult<List<ContainerMountInfo>>> GetContainerMounts(string id, [FromQuery] string? nodeId = null)
    {
        try
        {
            var mounts = await _containerService.GetContainerMountsAsync(id, nodeId);
            return Ok(mounts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取容器挂载点信息失败: {Id}", id);
            return BadRequest(new { message = _localization.GetMessage("container.mountInfoFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 下载容器文件
    /// </summary>
    [HttpGet("{id}/files/download")]
    public async Task<ActionResult> DownloadContainerFile(string id, [FromQuery] string path, [FromQuery] string? nodeId = null)
    {
        try
        {
            var content = await _containerService.DownloadContainerFileAsync(id, path, nodeId);
            var fileName = System.IO.Path.GetFileName(path);
            return File(content, "application/octet-stream", fileName);
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载容器文件失败: {Id}, {Path}", id, path);
            return BadRequest(new { message = _localization.GetMessage("container.downloadFileFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 上传文件到容器
    /// </summary>
    [HttpPost("{id}/files/upload")]
    public async Task<ActionResult> UploadContainerFile(string id, [FromForm] IFormFile file, [FromForm] string path, [FromQuery] string? nodeId = null)
    {
        try
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            var content = memoryStream.ToArray();
            
            await _containerService.UploadContainerFileAsync(id, path, file.FileName, content, nodeId);
            return Ok(new { message = _localization.GetMessage("container.uploadSuccess") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "上传文件到容器失败: {Id}", id);
            return BadRequest(new { message = _localization.GetMessage("container.uploadFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 创建文件夹
    /// </summary>
    [HttpPost("{id}/files/folder")]
    public async Task<ActionResult> CreateContainerFolder(string id, [FromBody] CreateFolderRequest request, [FromQuery] string? nodeId = null)
    {
        try
        {
            await _containerService.CreateContainerFolderAsync(id, request.Path, request.Name, nodeId);
            return Ok(new { message = _localization.GetMessage("container.createFolderSuccess") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建文件夹失败: {Id}", id);
            return BadRequest(new { message = _localization.GetMessage("container.createFolderFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 重命名文件
    /// </summary>
    [HttpPut("{id}/files/rename")]
    public async Task<ActionResult> RenameContainerFile(string id, [FromBody] RenameFileRequest request, [FromQuery] string? nodeId = null)
    {
        try
        {
            await _containerService.RenameContainerFileAsync(id, request.Path, request.OldName, request.NewName, nodeId);
            return Ok(new { message = _localization.GetMessage("container.renameFileSuccess") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重命名文件失败: {Id}", id);
            return BadRequest(new { message = _localization.GetMessage("container.renameFileFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 删除文件
    /// </summary>
    [HttpDelete("{id}/files")]
    public async Task<ActionResult> DeleteContainerFile(string id, [FromQuery] string path, [FromQuery] bool recursive = false, [FromQuery] string? nodeId = null)
    {
        try
        {
            await _containerService.DeleteContainerFileAsync(id, path, recursive, nodeId);
            return Ok(new { message = _localization.GetMessage("container.deleteFileSuccess") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除文件失败: {Id}", id);
            return BadRequest(new { message = _localization.GetMessage("container.deleteFileFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 获取容器文件内容（用于编辑）
    /// </summary>
    [HttpGet("{id}/files/content")]
    public async Task<ActionResult> GetContainerFileContent(string id, [FromQuery] string path, [FromQuery] string? nodeId = null)
    {
        try
        {
            var content = await _containerService.GetContainerFileContentAsync(id, path, nodeId);
            return Ok(new { content, path });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取文件内容失败: {Id}, {Path}", id, path);
            return BadRequest(new { message = _localization.GetMessage("container.getFileContentFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 写入容器文件内容
    /// </summary>
    [HttpPut("{id}/files/content")]
    public async Task<ActionResult> WriteContainerFileContent(string id, [FromBody] WriteFileContentRequest request, [FromQuery] string? nodeId = null)
    {
        try
        {
            await _containerService.WriteContainerFileContentAsync(id, request.Path, request.Content, nodeId);
            return Ok(new { message = _localization.GetMessage("container.saveFileSuccess") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "写入文件内容失败: {Id}", id);
            return BadRequest(new { message = _localization.GetMessage("container.saveFileFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 修改容器文件权限
    /// </summary>
    [HttpPut("{id}/files/permissions")]
    public async Task<ActionResult> ChangeContainerFilePermissions(string id, [FromBody] ChangePermissionsRequest request, [FromQuery] string? nodeId = null)
    {
        try
        {
            await _containerService.ChangeContainerFilePermissionsAsync(id, request.Path, request.Permissions, nodeId);
            return Ok(new { message = _localization.GetMessage("container.chmodSuccess") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "修改文件权限失败: {Id}", id);
            return BadRequest(new { message = _localization.GetMessage("container.chmodFailed"), error = ex.Message });
        }
    }
}

/// <summary>
/// 写入文件内容请求
/// </summary>
public class WriteFileContentRequest
{
    /// <summary>
    /// 文件路径
    /// </summary>
    public string Path { get; set; } = "";
    
    /// <summary>
    /// 文件内容
    /// </summary>
    public string Content { get; set; } = "";
}

/// <summary>
/// 修改权限请求
/// </summary>
public class ChangePermissionsRequest
{
    /// <summary>
    /// 文件路径
    /// </summary>
    public string Path { get; set; } = "";
    
    /// <summary>
    /// 权限（如 755, 644）
    /// </summary>
    public string Permissions { get; set; } = "";
}
