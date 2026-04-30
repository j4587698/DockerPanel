using Microsoft.AspNetCore.Mvc;
using DockerPanel.API.Services;
using DockerPanel.API.Models;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;

namespace DockerPanel.API.Controllers;

/// <summary>
/// SSH连接管理控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AuthRoles.Admin)]
public class SshController : ControllerBase
{
    private readonly ISshService _sshService;
    private readonly ILogger<SshController> _logger;
    private readonly ILocalizationService _localization;

    public SshController(ISshService sshService, ILogger<SshController> logger, ILocalizationService localization)
    {
        _sshService = sshService;
        _logger = logger;
        _localization = localization;
    }

    // ==================== 基础SSH操作 ====================

    /// <summary>
    /// 测试SSH连接
    /// </summary>
    [HttpPost("test-connection")]
    public async Task<ActionResult<bool>> TestSshConnection([FromBody] SshConnectionTestRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _sshService.TestSshConnectionAsync(
                request.Host,
                request.Port,
                request.Username,
                request.Password,
                request.PrivateKeyPath);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试SSH连接失败: {Host}:{Port}", request.Host, request.Port);
            return StatusCode(500, new { message = _localization.GetMessage("ssh.testConnectionFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 生成SSH密钥对
    /// </summary>
    [HttpPost("generate-keypair")]
    public async Task<ActionResult<SshKeyPair>> GenerateKeyPair([FromBody] GenerateKeyPairRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var keyPair = await _sshService.GenerateKeyPairAsync(
                request.KeyName,
                request.KeyType ?? "RSA",
                request.KeySize ?? 2048,
                request.Passphrase);

            return Ok(keyPair);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成SSH密钥对失败: {KeyName}", request.KeyName);
            return StatusCode(500, new { message = _localization.GetMessage("ssh.generateKeyPairFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 验证SSH私钥
    /// </summary>
    [HttpPost("validate-privatekey")]
    public async Task<ActionResult<bool>> ValidatePrivateKey([FromBody] ValidatePrivateKeyRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _sshService.ValidatePrivateKeyAsync(request.PrivateKeyPath, request.Passphrase);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证SSH私钥失败: {PrivateKeyPath}", request.PrivateKeyPath);
            return StatusCode(500, new { message = _localization.GetMessage("ssh.validateKeyFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 通过SSH执行命令
    /// </summary>
    [HttpPost("execute-command")]
    public async Task<ActionResult<SshCommandResult>> ExecuteCommand([FromBody] ExecuteCommandRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _sshService.ExecuteCommandAsync(
                request.Host,
                request.Port,
                request.Username,
                request.Command,
                request.Password,
                request.PrivateKeyPath,
                request.WorkingDirectory,
                request.Timeout ?? 60000);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSH命令执行失败: {Host}:{Port} 命令: {Command}", request.Host, request.Port, request.Command);
            return StatusCode(500, new { message = _localization.GetMessage("ssh.commandFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 上传文件到远程服务器
    /// </summary>
    [HttpPost("upload-file")]
    public async Task<ActionResult<bool>> UploadFile([FromBody] UploadFileRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _sshService.UploadFileAsync(
                request.Host,
                request.Port,
                request.Username,
                request.LocalPath,
                request.RemotePath,
                request.Password,
                request.PrivateKeyPath);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSH文件上传失败: {LocalPath} -> {RemotePath} @ {Host}:{Port}",
                request.LocalPath, request.RemotePath, request.Host, request.Port);
            return StatusCode(500, new { message = _localization.GetMessage("ssh.uploadFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 从远程服务器下载文件
    /// </summary>
    [HttpPost("download-file")]
    public async Task<ActionResult<bool>> DownloadFile([FromBody] DownloadFileRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _sshService.DownloadFileAsync(
                request.Host,
                request.Port,
                request.Username,
                request.RemotePath,
                request.LocalPath,
                request.Password,
                request.PrivateKeyPath);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSH文件下载失败: {RemotePath} -> {LocalPath} @ {Host}:{Port}",
                request.RemotePath, request.LocalPath, request.Host, request.Port);
            return StatusCode(500, new { message = _localization.GetMessage("ssh.downloadFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 批量测试SSH连接
    /// </summary>
    [HttpPost("batch-test-connection")]
    public async Task<ActionResult> BatchTestConnection([FromBody] BatchSshTestRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var results = new List<object>();

            foreach (var connection in request.Connections)
            {
                try
                {
                    var result = await _sshService.TestSshConnectionAsync(
                        connection.Host,
                        connection.Port,
                        connection.Username,
                        connection.Password,
                        connection.PrivateKeyPath);

                    results.Add(new
                    {
                        Host = connection.Host,
                        Port = connection.Port,
                        Username = connection.Username,
                        Success = result
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new
                    {
                        Host = connection.Host,
                        Port = connection.Port,
                        Username = connection.Username,
                        Success = false,
                        Error = ex.Message
                    });
                }
            }

            return Ok(new { Results = results });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量SSH连接测试失败");
            return StatusCode(500, new { message = _localization.GetMessage("ssh.batchTestFailed"), error = ex.Message });
        }
    }

    // ==================== 连接配置管理 ====================

    /// <summary>
    /// 获取连接配置列表
    /// </summary>
    [HttpGet("connections")]
    public async Task<ActionResult<PagedResponse<SshConnectionConfigEntity>>> GetConnectionConfigs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        try
        {
            var result = await _sshService.GetConnectionConfigsAsync(page, pageSize, search);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取SSH连接配置列表失败");
            return StatusCode(500, new { message = _localization.GetMessage("ssh.getConfigListFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 获取单个连接配置
    /// </summary>
    [HttpGet("connections/{id}")]
    public async Task<ActionResult<SshConnectionConfigEntity>> GetConnectionConfig(string id)
    {
        try
        {
            var config = await _sshService.GetConnectionConfigAsync(id);
            if (config == null)
            {
                return NotFound(new { message = _localization.GetMessage("ssh.configNotFound") });
            }
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取SSH连接配置失败: {Id}", id);
            return StatusCode(500, new { message = _localization.GetMessage("ssh.getConfigFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 创建连接配置
    /// </summary>
    [HttpPost("connections")]
    public async Task<ActionResult<SshConnectionConfigEntity>> CreateConnectionConfig([FromBody] SshConnectionConfigEntity config)
    {
        try
        {
            var result = await _sshService.CreateConnectionConfigAsync(config);
            return CreatedAtAction(nameof(GetConnectionConfig), new { id = result.Id }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建SSH连接配置失败");
            return StatusCode(500, new { message = _localization.GetMessage("ssh.createConfigFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 更新连接配置
    /// </summary>
    [HttpPut("connections/{id}")]
    public async Task<ActionResult<SshConnectionConfigEntity>> UpdateConnectionConfig(string id, [FromBody] SshConnectionConfigEntity config)
    {
        try
        {
            var result = await _sshService.UpdateConnectionConfigAsync(id, config);
            if (result == null)
            {
                return NotFound(new { message = _localization.GetMessage("ssh.configNotFound") });
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新SSH连接配置失败: {Id}", id);
            return StatusCode(500, new { message = _localization.GetMessage("ssh.updateConfigFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 删除连接配置
    /// </summary>
    [HttpDelete("connections/{id}")]
    public async Task<ActionResult> DeleteConnectionConfig(string id)
    {
        try
        {
            var result = await _sshService.DeleteConnectionConfigAsync(id);
            if (!result)
            {
                return NotFound(new { message = _localization.GetMessage("ssh.configNotFound") });
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除SSH连接配置失败: {Id}", id);
            return StatusCode(500, new { message = _localization.GetMessage("ssh.deleteConfigFailed"), error = ex.Message });
        }
    }

    // ==================== 密钥对管理 ====================

    /// <summary>
    /// 获取密钥对列表
    /// </summary>
    [HttpGet("keypairs")]
    public async Task<ActionResult<PagedResponse<SshKeyPair>>> GetKeyPairs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var result = await _sshService.GetKeyPairsAsync(page, pageSize);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取SSH密钥对列表失败");
            return StatusCode(500, new { message = _localization.GetMessage("ssh.getKeyPairListFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 导入密钥对
    /// </summary>
    [HttpPost("keypairs/import")]
    public async Task<ActionResult<SshKeyPair>> ImportKeyPair([FromBody] ImportKeyPairRequest request)
    {
        try
        {
            var result = await _sshService.ImportKeyPairAsync(
                request.Name,
                request.PublicKey,
                request.PrivateKey,
                request.Passphrase);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入SSH密钥对失败");
            return StatusCode(500, new { message = _localization.GetMessage("ssh.importKeyPairFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 删除密钥对
    /// </summary>
    [HttpDelete("keypairs/{id}")]
    public async Task<ActionResult> DeleteKeyPair(string id)
    {
        try
        {
            var result = await _sshService.DeleteKeyPairAsync(id);
            if (!result)
            {
                return NotFound(new { message = _localization.GetMessage("ssh.keyPairNotFound") });
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除SSH密钥对失败: {Id}", id);
            return StatusCode(500, new { message = _localization.GetMessage("ssh.deleteKeyPairFailed"), error = ex.Message });
        }
    }

    // ==================== 会话管理 ====================

    /// <summary>
    /// 获取活跃会话列表
    /// </summary>
    [HttpGet("sessions")]
    public async Task<ActionResult<PagedResponse<SshSessionInfo>>> GetSessions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var result = await _sshService.GetSessionsAsync(page, pageSize);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取SSH会话列表失败");
            return StatusCode(500, new { message = _localization.GetMessage("ssh.getSessionListFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 终止会话
    /// </summary>
    [HttpDelete("sessions/{id}")]
    public async Task<ActionResult> TerminateSession(string id)
    {
        try
        {
            var result = await _sshService.TerminateSessionAsync(id);
            if (!result)
            {
                return NotFound(new { message = _localization.GetMessage("ssh.sessionNotFound") });
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "终止SSH会话失败: {Id}", id);
            return StatusCode(500, new { message = _localization.GetMessage("ssh.terminateSessionFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 重连会话
    /// </summary>
    [HttpPost("sessions/{id}/reconnect")]
    public async Task<ActionResult<SshSessionInfo>> ReconnectSession(string id)
    {
        try
        {
            var result = await _sshService.ReconnectSessionAsync(id);
            if (result == null)
            {
                return NotFound(new { message = _localization.GetMessage("ssh.sessionNotFound") });
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重连SSH会话失败: {Id}", id);
            return StatusCode(500, new { message = _localization.GetMessage("ssh.reconnectSessionFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 创建终端会话描述。真实 SSH 连接由 SshTerminalHub 建立。
    /// </summary>
    [HttpPost("terminal-sessions")]
    public async Task<ActionResult<SshTerminalSessionDescriptor>> CreateTerminalSession([FromBody] CreateSshTerminalSessionRequest request)
    {
        try
        {
            var result = await _sshService.CreateTerminalSessionAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建SSH终端会话描述失败");
            return StatusCode(500, new { message = _localization.GetMessage("ssh.createTerminalSessionFailed"), error = ex.Message });
        }
    }

    // ==================== 主机密钥管理 ====================

    /// <summary>
    /// 获取 SSH 主机密钥列表
    /// </summary>
    [HttpGet("host-keys")]
    public async Task<ActionResult<PagedResponse<SshHostKey>>> GetHostKeys(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? trusted = null)
    {
        try
        {
            var result = await _sshService.GetHostKeysAsync(page, pageSize, search, trusted);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取SSH主机密钥列表失败");
            return StatusCode(500, new { message = _localization.GetMessage("ssh.getHostKeyListFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 新增或更新 SSH 主机密钥
    /// </summary>
    [HttpPost("host-keys")]
    public async Task<ActionResult<SshHostKey>> UpsertHostKey([FromBody] SshHostKey hostKey)
    {
        try
        {
            var result = await _sshService.UpsertHostKeyAsync(hostKey);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存SSH主机密钥失败: {Host}:{Port}", hostKey.Host, hostKey.Port);
            return StatusCode(500, new { message = _localization.GetMessage("ssh.saveHostKeyFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 删除 SSH 主机密钥
    /// </summary>
    [HttpDelete("host-keys/{id}")]
    public async Task<ActionResult> DeleteHostKey(string id)
    {
        try
        {
            var result = await _sshService.DeleteHostKeyAsync(id);
            if (!result)
            {
                return NotFound(new { message = _localization.GetMessage("ssh.hostKeyNotFound") });
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除SSH主机密钥失败: {Id}", id);
            return StatusCode(500, new { message = _localization.GetMessage("ssh.deleteHostKeyFailed"), error = ex.Message });
        }
    }

    // ==================== 目录操作 ====================

    /// <summary>
    /// 列出远程目录
    /// </summary>
    [HttpPost("list-directory")]
    public async Task<ActionResult<List<RemoteFileInfo>>> ListDirectory([FromBody] ListDirectoryRequest request)
    {
        try
        {
            var result = await _sshService.ListDirectoryAsync(request.ConnectionId, request.Path);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "列出远程目录失败: {ConnectionId} {Path}", request.ConnectionId, request.Path);
            return StatusCode(500, new { message = _localization.GetMessage("ssh.listDirectoryFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 删除远程文件
    /// </summary>
    [HttpPost("delete-remote-file")]
    public async Task<ActionResult<bool>> DeleteRemoteFile([FromBody] DeleteRemoteFileRequest request)
    {
        try
        {
            var result = await _sshService.DeleteRemoteFileAsync(request.ConnectionId, request.Path);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除远程文件失败: {ConnectionId} {Path}", request.ConnectionId, request.Path);
            return StatusCode(500, new { message = _localization.GetMessage("ssh.deleteFileFailed"), error = ex.Message });
        }
    }

    // ==================== 统计和日志 ====================

    /// <summary>
    /// 获取统计信息
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult<SshStatistics>> GetStatistics()
    {
        try
        {
            var result = await _sshService.GetStatisticsAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取SSH统计信息失败");
            return StatusCode(500, new { message = _localization.GetMessage("ssh.getStatsFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 获取操作日志
    /// </summary>
    [HttpGet("logs")]
    public async Task<ActionResult<PagedResponse<SshOperationLog>>> GetOperationLogs([FromQuery] OperationLogFilter? filter = null)
    {
        try
        {
            var result = await _sshService.GetOperationLogsAsync(filter);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取SSH操作日志失败");
            return StatusCode(500, new { message = _localization.GetMessage("ssh.getLogsFailed"), error = ex.Message });
        }
    }

    // ==================== 设置管理 ====================

    /// <summary>
    /// 获取SSH设置
    /// </summary>
    [HttpGet("settings")]
    public async Task<ActionResult<SshSettings>> GetSettings()
    {
        try
        {
            var result = await _sshService.GetSettingsAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取SSH设置失败");
            return StatusCode(500, new { message = _localization.GetMessage("ssh.getSettingsFailed"), error = ex.Message });
        }
    }

    /// <summary>
    /// 更新SSH设置
    /// </summary>
    [HttpPut("settings")]
    public async Task<ActionResult<SshSettings>> UpdateSettings([FromBody] SshSettings settings)
    {
        try
        {
            var result = await _sshService.UpdateSettingsAsync(settings);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新SSH设置失败");
            return StatusCode(500, new { message = _localization.GetMessage("ssh.updateSettingsFailed"), error = ex.Message });
        }
    }
}

// ==================== 请求/响应模型 ====================

/// <summary>
/// SSH连接测试请求
/// </summary>
public class SshConnectionTestRequest
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string? PrivateKeyPath { get; set; }
}

/// <summary>
/// 生成密钥对请求
/// </summary>
public class GenerateKeyPairRequest
{
    public string KeyName { get; set; } = string.Empty;
    public string? KeyType { get; set; } = "RSA";
    public int? KeySize { get; set; } = 2048;
    public string? Passphrase { get; set; }
}

/// <summary>
/// 验证私钥请求
/// </summary>
public class ValidatePrivateKeyRequest
{
    public string PrivateKeyPath { get; set; } = string.Empty;
    public string? Passphrase { get; set; }
}

/// <summary>
/// 执行命令请求
/// </summary>
public class ExecuteCommandRequest
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string? PrivateKeyPath { get; set; }
    public string? WorkingDirectory { get; set; }
    public int? Timeout { get; set; }
}

/// <summary>
/// 上传文件请求
/// </summary>
public class UploadFileRequest
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    public string RemotePath { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string? PrivateKeyPath { get; set; }
}

/// <summary>
/// 下载文件请求
/// </summary>
public class DownloadFileRequest
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string RemotePath { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string? PrivateKeyPath { get; set; }
}

/// <summary>
/// 批量SSH测试请求
/// </summary>
public class BatchSshTestRequest
{
    public List<SshConnectionInfo> Connections { get; set; } = new();
}

/// <summary>
/// SSH连接信息
/// </summary>
public class SshConnectionInfo
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string? PrivateKeyPath { get; set; }
}