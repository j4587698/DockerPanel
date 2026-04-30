using Microsoft.AspNetCore.Mvc;
using DockerPanel.API.Services;
using DockerPanel.API.Models;

namespace DockerPanel.API.Controllers;

/// <summary>
/// 卷管理控制器
/// </summary>
[ApiController]
[Route("api/volumes")]
public class VolumeController : ControllerBase
{
    private readonly IVolumeService _volumeService;
    private readonly ILogger<VolumeController> _logger;
    private readonly ILocalizationService _localization;

    public VolumeController(IVolumeService volumeService, ILogger<VolumeController> logger, ILocalizationService localization)
    {
        _volumeService = volumeService;
        _logger = logger;
        _localization = localization;
    }

    /// <summary>
    /// 获取卷列表
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DockerPanel.API.Models.VolumeInfo>>> GetVolumes([FromQuery] string? nodeId = null, [FromQuery] int? page = null, [FromQuery] int? pageSize = null)
    {
        try
        {
            var volumes = await _volumeService.GetVolumesAsync(nodeId);
            return Ok(volumes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取卷列表失败");
            return StatusCode(500, new { error = _localization.GetMessage("volume.listFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 根据ID获取卷详情
    /// </summary>
    [HttpGet("{volumeId}")]
    public async Task<ActionResult<VolumeDetailInfo>> GetVolume(string volumeId, [FromQuery] string? nodeId = null)
    {
        try
        {
            var volume = await _volumeService.GetVolumeByIdAsync(volumeId, nodeId);
            if (volume == null)
            {
                return NotFound(new { error = _localization.GetMessage("volume.notFound"), volumeId });
            }
            return Ok(volume);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取卷详情失败: {VolumeId}", volumeId);
            return StatusCode(500, new { error = _localization.GetMessage("volume.detailFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 创建卷
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<DockerPanel.API.Models.VolumeInfo>> CreateVolume([FromBody] CreateVolumeRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var volumeName = await _volumeService.CreateVolumeAsync(request);
            // 返回包含名称的 VolumeInfo 对象
            var volumeInfo = new DockerPanel.API.Models.VolumeInfo
            {
                Name = volumeName,
                Id = volumeName,
                Driver = request.Driver ?? "local"
            };
            return CreatedAtAction(nameof(GetVolume), new { volumeId = volumeName, nodeId = request.NodeId }, volumeInfo);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "创建卷参数错误: {Name}", request.Name);
            return BadRequest(new { error = ex.Message, name = request.Name });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "创建卷操作失败: {Name}", request.Name);
            return Conflict(new { error = ex.Message, name = request.Name });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建卷失败: {Name}", request.Name);
            return StatusCode(500, new { error = _localization.GetMessage("volume.createFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 从归档文件恢复创建新卷
    /// </summary>
    [HttpPost("restore-from-archive")]
    public async Task<ActionResult<DockerPanel.API.Models.VolumeInfo>> RestoreVolumeFromArchive(
        [FromForm] string? volumeName,
        [FromForm] IFormFile archive,
        [FromQuery] string? nodeId = null)
    {
        try
        {
            if (archive == null || archive.Length == 0)
            {
                return BadRequest(new { error = _localization.GetMessage("volume.noArchiveSelected") });
            }

            using var stream = archive.OpenReadStream();
            var volume = await _volumeService.RestoreVolumeFromArchiveAsync(volumeName, stream, nodeId);
            
            _logger.LogInformation("从归档恢复卷成功: {VolumeName}", volume.Name);
            return Ok(volume);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从归档恢复卷失败: {VolumeName}", volumeName);
            return StatusCode(500, new { error = _localization.GetMessage("volume.restoreFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 删除卷
    /// </summary>
    [HttpDelete("{volumeId}")]
    public async Task<ActionResult> DeleteVolume(string volumeId, [FromQuery] bool force = false, [FromQuery] string? nodeId = null)
    {
        try
        {
            var success = await _volumeService.DeleteVolumeAsync(volumeId, force, nodeId);
            if (success)
            {
                return Ok(new { message = _localization.GetMessage("volume.deleteSuccess"), volumeId, force });
            }
            else
            {
                return NotFound(new { error = _localization.GetMessage("volume.notFound"), volumeId });
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "删除卷操作失败: {VolumeId}", volumeId);
            return Conflict(new { error = ex.Message, volumeId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除卷失败: {VolumeId}", volumeId);
            return StatusCode(500, new { error = _localization.GetMessage("volume.deleteFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 更新卷配置
    /// </summary>
    [HttpPut("{volumeId}")]
    public async Task<ActionResult<DockerPanel.API.Models.VolumeInfo>> UpdateVolume(string volumeId, [FromBody] UpdateVolumeRequest request, [FromQuery] string? nodeId = null)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var volume = await _volumeService.UpdateVolumeAsync(volumeId, request, nodeId);
            return Ok(volume);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "更新卷配置参数错误: {VolumeId}", volumeId);
            return BadRequest(new { error = ex.Message, volumeId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新卷配置失败: {VolumeId}", volumeId);
            return StatusCode(500, new { error = _localization.GetMessage("volume.updateFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 清理未使用的卷
    /// </summary>
    [HttpPost("prune")]
    public async Task<ActionResult<VolumePruneResult>> PruneVolumes([FromBody] PruneVolumesRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var options = new VolumePruneOptions
            {
                Filters = request.Filters,
                LabelFilter = request.LabelFilter,
                All = request.All
            };

            var result = await _volumeService.PruneVolumesAsync(options, request.NodeId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理卷失败");
            return StatusCode(500, new { error = _localization.GetMessage("volume.pruneFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 获取卷统计信息
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult<VolumeStatistics>> GetVolumeStatistics([FromQuery] string? nodeId = null)
    {
        try
        {
            var statistics = await _volumeService.GetVolumeStatisticsAsync(nodeId);
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取卷统计信息失败");
            return StatusCode(500, new { error = _localization.GetMessage("volume.statsFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 检查卷是否存在
    /// </summary>
    [HttpGet("{volumeId}/exists")]
    public async Task<ActionResult<bool>> VolumeExists(string volumeId, [FromQuery] string? nodeId = null)
    {
        try
        {
            var exists = await _volumeService.VolumeExistsAsync(volumeId, nodeId);
            return Ok(exists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查卷是否存在失败: {VolumeId}", volumeId);
            return StatusCode(500, new { error = _localization.GetMessage("volume.existsCheckFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 获取卷使用情况
    /// </summary>
    [HttpGet("{volumeId}/usage")]
    public async Task<ActionResult<VolumeUsageInfo>> GetVolumeUsage(string volumeId, [FromQuery] string? nodeId = null)
    {
        try
        {
            var usage = await _volumeService.GetVolumeUsageAsync(volumeId, nodeId);
            return Ok(usage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取卷使用情况失败: {VolumeId}", volumeId);
            return StatusCode(500, new { error = _localization.GetMessage("volume.usageFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 备份卷
    /// </summary>
    [HttpPost("{volumeId}/backup")]
    public async Task<ActionResult<VolumeBackupResult>> BackupVolume(string volumeId, [FromBody] VolumeBackupRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            request.VolumeId = volumeId;
            var result = await _volumeService.BackupVolumeAsync(volumeId, request);

            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "备份卷失败: {VolumeId}", volumeId);
            return StatusCode(500, new { error = _localization.GetMessage("volume.backupFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 恢复卷
    /// </summary>
    [HttpPost("restore")]
    public async Task<ActionResult<VolumeRestoreResult>> RestoreVolume([FromBody] VolumeRestoreRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _volumeService.RestoreVolumeAsync(request);

            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "恢复卷失败: {VolumeId}", request.VolumeId);
            return StatusCode(500, new { error = _localization.GetMessage("volume.restoreFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 获取卷备份列表
    /// </summary>
    [HttpGet("{volumeId}/backups")]
    public async Task<ActionResult<IEnumerable<VolumeBackupInfo>>> GetVolumeBackups(string volumeId)
    {
        try
        {
            var backups = await _volumeService.GetVolumeBackupsAsync(volumeId);
            return Ok(backups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取卷备份列表失败: {VolumeId}", volumeId);
            return StatusCode(500, new { error = _localization.GetMessage("volume.backupListFailed"), message = ex.Message });
        }
    }

    /// <summary>
    /// 删除卷备份
    /// </summary>
    [HttpDelete("{volumeId}/backups/{backupId}")]
    public async Task<ActionResult> DeleteVolumeBackup(string volumeId, string backupId)
    {
        try
        {
            var success = await _volumeService.DeleteVolumeBackupAsync(volumeId, backupId);
            if (success)
            {
                return Ok(new { message = _localization.GetMessage("volume.backupDeleteSuccess"), volumeId, backupId });
            }
            else
            {
                return NotFound(new { error = _localization.GetMessage("volume.backupNotFound"), volumeId, backupId });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除卷备份失败: {VolumeId}, {BackupId}", volumeId, backupId);
            return StatusCode(500, new { error = _localization.GetMessage("volume.backupDeleteFailed"), message = ex.Message });
        }
    }

    #region 文件操作

    /// <summary>
    /// 获取卷文件列表
    /// </summary>
    [HttpGet("{volumeId}/files")]
    public async Task<ActionResult<ContainerFileListResponse>> GetVolumeFiles(string volumeId, [FromQuery] string path = "/", [FromQuery] string? nodeId = null)
    {
        try
        {
            var files = await _volumeService.GetVolumeFilesAsync(volumeId, path, nodeId);
            return Ok(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取卷文件列表失败: {VolumeId}", volumeId);
            return BadRequest(new { message = _localization.GetMessage("volume.fileListFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 下载卷文件
    /// </summary>
    [HttpGet("{volumeId}/files/download")]
    public async Task<ActionResult> DownloadVolumeFile(string volumeId, [FromQuery] string path, [FromQuery] bool archive = false, [FromQuery] string? nodeId = null)
    {
        try
        {
            if (archive)
            {
                // 打包下载整个目录或多个文件
                var (content, fileName) = await _volumeService.ArchiveVolumeFilesAsync(volumeId, path, nodeId);
                return File(content, "application/gzip", fileName);
            }
            else
            {
                var content = await _volumeService.DownloadVolumeFileAsync(volumeId, path, nodeId);
                var fileName = System.IO.Path.GetFileName(path);
                return File(content, "application/octet-stream", fileName);
            }
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载卷文件失败: {VolumeId}, {Path}", volumeId, path);
            return StatusCode(500, new { message = _localization.GetMessage("volume.downloadFileFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 上传文件到卷
    /// </summary>
    [HttpPost("{volumeId}/files/upload")]
    public async Task<ActionResult> UploadVolumeFile(string volumeId, [FromQuery] string path, IFormFile file, [FromQuery] string? nodeId = null)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = _localization.GetMessage("volume.noFileSelected") });
            }

            using var stream = file.OpenReadStream();
            await _volumeService.UploadVolumeFileAsync(volumeId, path, file.FileName, stream, nodeId);
            return Ok(new { message = _localization.GetMessage("volume.uploadSuccess"), fileName = file.FileName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "上传卷文件失败: {VolumeId}", volumeId);
            return StatusCode(500, new { message = _localization.GetMessage("volume.uploadFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 在卷中创建文件夹
    /// </summary>
    [HttpPost("{volumeId}/files/folder")]
    public async Task<ActionResult> CreateVolumeFolder(string volumeId, [FromBody] CreateFolderRequest request, [FromQuery] string? nodeId = null)
    {
        try
        {
            await _volumeService.CreateVolumeFolderAsync(volumeId, request.Path, request.Name, nodeId);
            return Ok(new { message = _localization.GetMessage("volume.createFolderSuccess") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建文件夹失败: {VolumeId}", volumeId);
            return StatusCode(500, new { message = _localization.GetMessage("volume.createFolderFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 重命名卷文件
    /// </summary>
    [HttpPut("{volumeId}/files/rename")]
    public async Task<ActionResult> RenameVolumeFile(string volumeId, [FromBody] RenameFileRequest request, [FromQuery] string? nodeId = null)
    {
        try
        {
            await _volumeService.RenameVolumeFileAsync(volumeId, request.Path, request.OldName, request.NewName, nodeId);
            return Ok(new { message = _localization.GetMessage("volume.renameSuccess") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重命名文件失败: {VolumeId}", volumeId);
            return StatusCode(500, new { message = _localization.GetMessage("volume.renameFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 删除卷文件
    /// </summary>
    [HttpDelete("{volumeId}/files")]
    public async Task<ActionResult> DeleteVolumeFile(string volumeId, [FromQuery] string path, [FromQuery] bool recursive = false, [FromQuery] string? nodeId = null)
    {
        try
        {
            await _volumeService.DeleteVolumeFileAsync(volumeId, path, recursive, nodeId);
            return Ok(new { message = _localization.GetMessage("volume.deleteFileSuccess") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除卷文件失败: {VolumeId}", volumeId);
            return StatusCode(500, new { message = _localization.GetMessage("volume.deleteFileFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 获取卷文件内容
    /// </summary>
    [HttpGet("{volumeId}/files/content")]
    public async Task<ActionResult> GetVolumeFileContent(string volumeId, [FromQuery] string path, [FromQuery] string? nodeId = null)
    {
        try
        {
            var content = await _volumeService.GetVolumeFileContentAsync(volumeId, path, nodeId);
            return Ok(new { content, path });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取文件内容失败: {VolumeId}, {Path}", volumeId, path);
            return StatusCode(500, new { message = _localization.GetMessage("volume.getFileContentFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 保存卷文件内容
    /// </summary>
    [HttpPut("{volumeId}/files/content")]
    public async Task<ActionResult> SaveVolumeFileContent(string volumeId, [FromBody] SaveFileContentRequest request, [FromQuery] string? nodeId = null)
    {
        try
        {
            await _volumeService.SaveVolumeFileContentAsync(volumeId, request.Path, request.Content, nodeId);
            return Ok(new { message = _localization.GetMessage("volume.saveSuccess") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存文件内容失败: {VolumeId}", volumeId);
            return StatusCode(500, new { message = _localization.GetMessage("volume.saveFileFailed"), error = ex.Message });
        }
    }

    #endregion
}

/// <summary>
/// 清理卷请求
/// </summary>
public class PruneVolumesRequest
{
    public bool Filters { get; set; } = false;
    public string? LabelFilter { get; set; }
    public bool All { get; set; } = false;
    public string? NodeId { get; set; }
}

/// <summary>
/// 保存文件内容请求
/// </summary>
public class SaveFileContentRequest
{
    public string Path { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
