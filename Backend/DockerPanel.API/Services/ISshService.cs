using System.Security.Cryptography;
using System.Text;
using DockerPanel.API.Models;

namespace DockerPanel.API.Services;

/// <summary>
/// SSH连接服务接口
/// </summary>
public interface ISshService
{
    // ==================== 基础SSH操作 ====================

    /// <summary>
    /// 测试SSH连接
    /// </summary>
    Task<bool> TestSshConnectionAsync(string host, int port, string username, string? password = null, string? privateKeyPath = null);

    /// <summary>
    /// 生成SSH密钥对
    /// </summary>
    Task<SshKeyPair> GenerateKeyPairAsync(string keyName, string keyType = "RSA", int keySize = 2048, string? passphrase = null);

    /// <summary>
    /// 验证SSH私钥
    /// </summary>
    Task<bool> ValidatePrivateKeyAsync(string privateKeyPath, string? passphrase = null);

    /// <summary>
    /// 通过SSH执行命令
    /// </summary>
    Task<SshCommandResult> ExecuteCommandAsync(string host, int port, string username, string command, string? password = null, string? privateKeyPath = null, string? workingDirectory = null, int timeout = 60000);

    /// <summary>
    /// 上传文件到远程服务器
    /// </summary>
    Task<bool> UploadFileAsync(string host, int port, string username, string localPath, string remotePath, string? password = null, string? privateKeyPath = null);

    /// <summary>
    /// 从远程服务器下载文件
    /// </summary>
    Task<bool> DownloadFileAsync(string host, int port, string username, string remotePath, string localPath, string? password = null, string? privateKeyPath = null);

    // ==================== 连接配置管理 ====================

    /// <summary>
    /// 获取连接配置列表
    /// </summary>
    Task<PagedResponse<SshConnectionConfigEntity>> GetConnectionConfigsAsync(int page = 1, int pageSize = 20, string? search = null);

    /// <summary>
    /// 获取单个连接配置
    /// </summary>
    Task<SshConnectionConfigEntity?> GetConnectionConfigAsync(string id);

    /// <summary>
    /// 创建连接配置
    /// </summary>
    Task<SshConnectionConfigEntity> CreateConnectionConfigAsync(SshConnectionConfigEntity config);

    /// <summary>
    /// 更新连接配置
    /// </summary>
    Task<SshConnectionConfigEntity?> UpdateConnectionConfigAsync(string id, SshConnectionConfigEntity config);

    /// <summary>
    /// 删除连接配置
    /// </summary>
    Task<bool> DeleteConnectionConfigAsync(string id);

    // ==================== 密钥对管理 ====================

    /// <summary>
    /// 获取密钥对列表
    /// </summary>
    Task<PagedResponse<SshKeyPair>> GetKeyPairsAsync(int page = 1, int pageSize = 20);

    /// <summary>
    /// 导入密钥对
    /// </summary>
    Task<SshKeyPair> ImportKeyPairAsync(string name, string publicKey, string? privateKey = null, string? passphrase = null);

    /// <summary>
    /// 删除密钥对
    /// </summary>
    Task<bool> DeleteKeyPairAsync(string id);

    // ==================== 会话管理 ====================

    /// <summary>
    /// 获取活跃会话列表
    /// </summary>
    Task<PagedResponse<SshSessionInfo>> GetSessionsAsync(int page = 1, int pageSize = 20);

    /// <summary>
    /// 终止会话
    /// </summary>
    Task<bool> TerminateSessionAsync(string sessionId);

    /// <summary>
    /// 重连会话
    /// </summary>
    Task<SshSessionInfo?> ReconnectSessionAsync(string sessionId);

    /// <summary>
    /// 创建 SSH 终端会话描述。真实连接由 SignalR Hub 建立。
    /// </summary>
    Task<SshTerminalSessionDescriptor> CreateTerminalSessionAsync(CreateSshTerminalSessionRequest request);

    /// <summary>
    /// 获取 SSH 主机密钥列表
    /// </summary>
    Task<PagedResponse<SshHostKey>> GetHostKeysAsync(int page = 1, int pageSize = 20, string? search = null, bool? trusted = null);

    /// <summary>
    /// 新增或更新 SSH 主机密钥
    /// </summary>
    Task<SshHostKey> UpsertHostKeyAsync(SshHostKey hostKey);

    /// <summary>
    /// 删除 SSH 主机密钥
    /// </summary>
    Task<bool> DeleteHostKeyAsync(string id);

    // ==================== 目录操作 ====================

    /// <summary>
    /// 列出远程目录
    /// </summary>
    Task<List<RemoteFileInfo>> ListDirectoryAsync(string connectionId, string path);

    /// <summary>
    /// 删除远程文件
    /// </summary>
    Task<bool> DeleteRemoteFileAsync(string connectionId, string path);

    // ==================== 统计和日志 ====================

    /// <summary>
    /// 获取统计信息
    /// </summary>
    Task<SshStatistics> GetStatisticsAsync();

    /// <summary>
    /// 获取操作日志
    /// </summary>
    Task<PagedResponse<SshOperationLog>> GetOperationLogsAsync(OperationLogFilter? filter = null);

    // ==================== 设置管理 ====================

    /// <summary>
    /// 获取SSH设置
    /// </summary>
    Task<SshSettings> GetSettingsAsync();

    /// <summary>
    /// 更新SSH设置
    /// </summary>
    Task<SshSettings> UpdateSettingsAsync(SshSettings settings);
}

/// <summary>
/// SSH密钥对
/// </summary>
public class SshKeyPair
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PublicKey { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string KeyName { get; set; } = string.Empty;
    public string KeyType { get; set; } = "RSA";
    public int KeySize { get; set; } = 2048;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Fingerprint { get; set; } = string.Empty;
    public bool HasPassphrase { get; set; } = false;
}

/// <summary>
/// SSH命令执行结果
/// </summary>
public class SshCommandResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan ExecutionDuration { get; set; }
}

/// <summary>
/// SSH连接配置
/// </summary>
public class SshConnectionConfig
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string? PrivateKeyPath { get; set; }
    public string? PrivateKeyPassphrase { get; set; }
    public int ConnectionTimeout { get; set; } = 30000; // 30秒
    public int CommandTimeout { get; set; } = 60000; // 60秒
    public bool StrictHostKeyChecking { get; set; } = false;
}