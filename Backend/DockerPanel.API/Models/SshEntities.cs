namespace DockerPanel.API.Models;

/// <summary>
/// SSH连接配置实体 - 带ID用于持久化
/// </summary>
public class SshConnectionConfigEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string? PrivateKeyPath { get; set; }
    public string? PrivateKeyPassphrase { get; set; }
    public int ConnectionTimeout { get; set; } = 30000;
    public int CommandTimeout { get; set; } = 60000;
    public bool StrictHostKeyChecking { get; set; } = false;
    public string? Tags { get; set; }
    public string Status { get; set; } = "disconnected";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastConnectedAt { get; set; }
}

/// <summary>
/// SSH会话信息
/// </summary>
public class SshSessionInfo
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public string ConnectionId { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string SessionType { get; set; } = "terminal"; // terminal, sftp
    public string Status { get; set; } = "connected";
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public long BytesSent { get; set; }
    public long BytesReceived { get; set; }
}

/// <summary>
/// 远程文件信息
/// </summary>
public class RemoteFileInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Type { get; set; } = "file"; // file, directory
    public long Size { get; set; }
    public DateTime ModifiedAt { get; set; }
    public string Permissions { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
}

/// <summary>
/// SSH统计信息
/// </summary>
public class SshStatistics
{
    public int TotalConnections { get; set; }
    public int ActiveConnections { get; set; }
    public int TotalKeyPairs { get; set; }
    public int TotalCommands { get; set; }
    public int TotalFileTransfers { get; set; }
    public long TotalBytesTransferred { get; set; }
}

/// <summary>
/// SSH操作日志
/// </summary>
public class SshOperationLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Operation { get; set; } = string.Empty; // connect, disconnect, command, upload, download
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string Status { get; set; } = "success"; // success, failed
    public string? Details { get; set; }
    public long? Duration { get; set; } // milliseconds
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 操作日志过滤器
/// </summary>
public class OperationLogFilter
{
    public string? Search { get; set; }
    public string? Operation { get; set; }
    public string? Host { get; set; }
    public string? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// SSH 全局设置
/// </summary>
public class SshSettings
{
    // 连接设置
    public int DefaultConnectionTimeout { get; set; } = 30000; // 毫秒
    public int DefaultCommandTimeout { get; set; } = 60000; // 毫秒
    public int KeepAliveInterval { get; set; } = 30; // 秒，0表示禁用

    // 安全设置
    public bool StrictHostKeyChecking { get; set; } = false;
    public bool PreferKeyAuth { get; set; } = true;
    public string DefaultKeyPath { get; set; } = "~/.ssh/id_rsa";

    // 终端设置
    public string DefaultTerminalType { get; set; } = "xterm-256color";
    public int TerminalCols { get; set; } = 120;
    public int TerminalRows { get; set; } = 30;

    // 日志设置
    public bool LogOperations { get; set; } = true;
    public int LogRetentionDays { get; set; } = 30;
    public bool LogCommandContent { get; set; } = true;
}

/// <summary>
/// SSH 主机密钥记录
/// </summary>
public class SshHostKey
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string KeyType { get; set; } = string.Empty;
    public string KeyFingerprint { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string Algorithm { get; set; } = string.Empty;
    public int KeySize { get; set; }
    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public bool Trusted { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// 创建 SSH 终端会话描述请求
/// </summary>
public class CreateSshTerminalSessionRequest
{
    public string? ConnectionId { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string? PrivateKeyPath { get; set; }
    public string? PrivateKeyPassphrase { get; set; }
    public string TerminalType { get; set; } = "xterm-256color";
    public int Rows { get; set; } = 30;
    public int Cols { get; set; } = 120;
}

/// <summary>
/// SSH 终端连接信息
/// </summary>
public class SshTerminalConnectionInfo
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string? PrivateKeyPath { get; set; }
    public string? PrivateKeyPassphrase { get; set; }
}

/// <summary>
/// SSH 终端会话描述。真实交互由 SshTerminalHub 负责。
/// </summary>
public class SshTerminalSessionDescriptor
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public SshTerminalConnectionInfo ConnectionInfo { get; set; } = new();
    public string TerminalType { get; set; } = "xterm-256color";
    public int Rows { get; set; } = 30;
    public int Cols { get; set; } = 120;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; }
    public List<string> Buffer { get; set; } = new();
    public object CursorPosition { get; set; } = new { row = 0, col = 0 };
}

/// <summary>
/// 分页响应
/// </summary>
public class PagedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)Total / PageSize);
}

/// <summary>
/// 导入密钥对请求
/// </summary>
public class ImportKeyPairRequest
{
    public string Name { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string? PrivateKey { get; set; }
    public string? Passphrase { get; set; }
}

/// <summary>
/// 列出目录请求
/// </summary>
public class ListDirectoryRequest
{
    public string ConnectionId { get; set; } = string.Empty;
    public string Path { get; set; } = "/";
}

/// <summary>
/// 删除远程文件请求
/// </summary>
public class DeleteRemoteFileRequest
{
    public string ConnectionId { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}
