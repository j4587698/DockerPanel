using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using DockerPanel.API.Models;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;
using SshConnectionInfo = Renci.SshNet.ConnectionInfo;

namespace DockerPanel.API.Services;

/// <summary>
/// SSH服务实现 - 使用 SSH.NET 库
/// </summary>
public class SshService : ISshService
{
    private readonly ILogger<SshService> _logger;
    private readonly DataBaseService _dbService;

    // 仅会话信息保留在内存中（因为是运行时状态）
    private static readonly ConcurrentDictionary<string, SshSessionInfo> _sessions = new();
    private static SshSettings _settings = new();
    private static int _totalCommands = 0;
    private static int _totalFileTransfers = 0;

    public SshService(ILogger<SshService> logger, DataBaseService dbService)
    {
        _logger = logger;
        _dbService = dbService;
    }

    // ==================== SSH.NET 连接辅助方法 ====================

    private SshConnectionInfo CreateConnectionInfo(string host, int port, string username, string? password = null, string? privateKeyPath = null, string? passphrase = null)
    {
        var authMethods = new List<AuthenticationMethod>();

        // 私钥认证
        var keyAuthMethod = (PrivateKeyAuthenticationMethod?)null;
        if (!string.IsNullOrEmpty(privateKeyPath) && File.Exists(privateKeyPath))
        {
            try
            {
                PrivateKeyFile keyFile;
                if (!string.IsNullOrEmpty(passphrase))
                {
                    keyFile = new PrivateKeyFile(privateKeyPath, passphrase);
                }
                else
                {
                    keyFile = new PrivateKeyFile(privateKeyPath);
                }
                keyAuthMethod = new PrivateKeyAuthenticationMethod(username, keyFile);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "无法加载私钥文件: {PrivateKeyPath}", privateKeyPath);
            }
        }

        // 密码认证
        var passwordAuthMethod = !string.IsNullOrEmpty(password) 
            ? new PasswordAuthenticationMethod(username, password) 
            : null;

        // 根据设置决定认证方法顺序
        if (_settings.PreferKeyAuth)
        {
            if (keyAuthMethod != null) authMethods.Add(keyAuthMethod);
            if (passwordAuthMethod != null) authMethods.Add(passwordAuthMethod);
        }
        else
        {
            if (passwordAuthMethod != null) authMethods.Add(passwordAuthMethod);
            if (keyAuthMethod != null) authMethods.Add(keyAuthMethod);
        }

        if (authMethods.Count == 0)
        {
            throw new ArgumentException("必须提供密码或私钥进行认证");
        }

        var connectionInfo = new SshConnectionInfo(host, port, username, authMethods.ToArray());
        
        // 应用全局连接超时设置
        connectionInfo.Timeout = TimeSpan.FromMilliseconds(_settings.DefaultConnectionTimeout);

        return connectionInfo;
    }

    // ==================== 基础SSH操作 ====================

    public async Task<bool> TestSshConnectionAsync(string host, int port, string username, string? password = null, string? privateKeyPath = null)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            _logger.LogInformation("测试SSH连接: {Host}:{Port} 用户: {Username}", host, port, username);

            var connectionInfo = CreateConnectionInfo(host, port, username, password, privateKeyPath);
            // 测试连接使用较短的超时（10秒），不受全局设置影响

            using var client = new SshClient(connectionInfo);

            await Task.Run(() => client.Connect());

            var connected = client.IsConnected;

            if (connected)
            {
                client.Disconnect();
            }

            var duration = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            LogOperation("connect", host, port, username, connected ? "success" : "failed", null, duration);

            _logger.LogInformation("SSH连接测试结果: {Success}", connected);
            return connected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSH连接测试失败: {Host}:{Port}", host, port);
            LogOperation("connect", host, port, username, "failed", null, null, ex.Message);
            return false;
        }
    }

    public async Task<SshKeyPair> GenerateKeyPairAsync(string keyName, string keyType = "RSA", int keySize = 2048, string? passphrase = null)
    {
        try
        {
            _logger.LogInformation("生成SSH密钥对: {KeyName} 类型: {KeyType} 大小: {KeySize}", keyName, keyType, keySize);

            string publicKey;
            string privateKey;
            string fingerprint;

            if (string.Equals(keyType, "ED25519", StringComparison.OrdinalIgnoreCase))
            {
                // ED25519 密钥生成
                using var algorithm = System.Security.Cryptography.ECDsa.Create(ECCurve.NamedCurves.nistP256);
                var publicKeyBytes = algorithm.ExportSubjectPublicKeyInfo();
                var privateKeyBytes = algorithm.ExportPkcs8PrivateKey();

                publicKey = $"ecdsa-sha2-nistp256 {Convert.ToBase64String(publicKeyBytes)} {keyName}";
                privateKey = FormatPemKey(privateKeyBytes, "EC PRIVATE KEY");
                fingerprint = GenerateFingerprint(publicKey);
            }
            else
            {
                // RSA 密钥生成
                using var rsa = RSA.Create(keySize);

                var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
                var privateKeyBytes = rsa.ExportPkcs8PrivateKey();

                publicKey = FormatSshRsaPublicKey(rsa, keyName);
                privateKey = FormatPemKey(privateKeyBytes, "PRIVATE KEY");
                fingerprint = GenerateFingerprint(publicKey);
            }

            var keyPair = new SshKeyPair
            {
                Id = Guid.NewGuid().ToString(),
                KeyName = keyName,
                KeyType = keyType,
                KeySize = keySize,
                PublicKey = publicKey,
                PrivateKey = privateKey,
                Fingerprint = fingerprint,
                CreatedAt = DateTime.UtcNow,
                HasPassphrase = !string.IsNullOrEmpty(passphrase)
            };

            // 保存到数据库
            _dbService.SshKeyPairs.Insert(keyPair);

            _logger.LogInformation("SSH密钥对生成成功: {KeyName} 指纹: {Fingerprint}", keyName, fingerprint);

            return await Task.FromResult(keyPair);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成SSH密钥对失败: {KeyName}", keyName);
            throw;
        }
    }

    public async Task<bool> ValidatePrivateKeyAsync(string privateKeyPath, string? passphrase = null)
    {
        try
        {
            _logger.LogInformation("验证SSH私钥: {PrivateKeyPath}", privateKeyPath);

            if (!File.Exists(privateKeyPath))
            {
                _logger.LogWarning("私钥文件不存在: {PrivateKeyPath}", privateKeyPath);
                return false;
            }

            // 使用 SSH.NET 验证私钥
            await Task.Run(() =>
            {
                PrivateKeyFile keyFile;
                if (!string.IsNullOrEmpty(passphrase))
                {
                    keyFile = new PrivateKeyFile(privateKeyPath, passphrase);
                }
                else
                {
                    keyFile = new PrivateKeyFile(privateKeyPath);
                }
                // 如果能成功创建 PrivateKeyFile，说明私钥有效
                _ = keyFile.HostKeyAlgorithms;
            });

            _logger.LogInformation("SSH私钥验证成功: {PrivateKeyPath}", privateKeyPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证SSH私钥失败: {PrivateKeyPath}", privateKeyPath);
            return false;
        }
    }

    public async Task<SshCommandResult> ExecuteCommandAsync(string host, int port, string username, string command, string? password = null, string? privateKeyPath = null, string? workingDirectory = null, int timeout = 60000)
    {
        var startTime = DateTime.UtcNow;
        var result = new SshCommandResult();

        // 使用传入的超时或全局默认值
        var actualTimeout = timeout > 0 ? timeout : _settings.DefaultCommandTimeout;

        try
        {
            _logger.LogInformation("通过SSH执行命令: {Host}:{Port} 用户: {Username} 命令: {Command}", host, port, username, command);

            var connectionInfo = CreateConnectionInfo(host, port, username, password, privateKeyPath);

            using var client = new SshClient(connectionInfo);

            await Task.Run(() => client.Connect());

            // 应用保活间隔
            if (_settings.KeepAliveInterval > 0)
            {
                client.KeepAliveInterval = TimeSpan.FromSeconds(_settings.KeepAliveInterval);
            }

            var actualCommand = string.IsNullOrEmpty(workingDirectory)
                ? command
                : $"cd {workingDirectory} && {command}";

            using var cmd = client.CreateCommand(actualCommand);
            cmd.CommandTimeout = TimeSpan.FromMilliseconds(actualTimeout);

            var output = await Task.Run(() => cmd.Execute());

            result.Success = (cmd.ExitStatus ?? -1) == 0;
            result.Output = output;
            result.Error = cmd.Error;
            result.ExitCode = cmd.ExitStatus ?? -1;
            result.ExecutedAt = startTime;
            result.ExecutionDuration = DateTime.UtcNow - startTime;

            client.Disconnect();

            Interlocked.Increment(ref _totalCommands);
            LogOperation("command", host, port, username, result.Success ? "success" : "failed", command, (long)result.ExecutionDuration.TotalMilliseconds);

            _logger.LogInformation("SSH命令执行完成: {Success} 退出码: {ExitCode} 耗时: {Duration}ms",
                result.Success, result.ExitCode, result.ExecutionDuration.TotalMilliseconds);

            return result;
        }
        catch (SshConnectionException ex)
        {
            _logger.LogError(ex, "SSH连接失败: {Host}:{Port}", host, port);
            result.Success = false;
            result.Error = $"连接失败: {ex.Message}";
            result.ExitCode = -1;
            result.ExecutedAt = startTime;
            result.ExecutionDuration = DateTime.UtcNow - startTime;
            LogOperation("command", host, port, username, "failed", command, null, ex.Message);
            return result;
        }
        catch (SshAuthenticationException ex)
        {
            _logger.LogError(ex, "SSH认证失败: {Host}:{Port}", host, port);
            result.Success = false;
            result.Error = $"认证失败: {ex.Message}";
            result.ExitCode = -1;
            result.ExecutedAt = startTime;
            result.ExecutionDuration = DateTime.UtcNow - startTime;
            LogOperation("command", host, port, username, "failed", command, null, ex.Message);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSH命令执行失败: {Host}:{Port} 命令: {Command}", host, port, command);
            result.Success = false;
            result.Error = ex.Message;
            result.ExitCode = -1;
            result.ExecutedAt = startTime;
            result.ExecutionDuration = DateTime.UtcNow - startTime;
            LogOperation("command", host, port, username, "failed", command, null, ex.Message);
            return result;
        }
    }

    public async Task<bool> UploadFileAsync(string host, int port, string username, string localPath, string remotePath, string? password = null, string? privateKeyPath = null)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            _logger.LogInformation("通过SFTP上传文件: {LocalPath} -> {RemotePath} @ {Host}:{Port}", localPath, remotePath, host, port);

            if (!File.Exists(localPath))
            {
                _logger.LogWarning("本地文件不存在: {LocalPath}", localPath);
                return false;
            }

            var connectionInfo = CreateConnectionInfo(host, port, username, password, privateKeyPath);

            using var client = new SftpClient(connectionInfo);

            await Task.Run(() => client.Connect());

            using var fileStream = File.OpenRead(localPath);
            await Task.Run(() => client.UploadFile(fileStream, remotePath));

            client.Disconnect();

            var duration = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            Interlocked.Increment(ref _totalFileTransfers);
            LogOperation("upload", host, port, username, "success", $"{localPath} -> {remotePath}", duration);

            _logger.LogInformation("SFTP文件上传成功: {LocalPath}", localPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SFTP文件上传失败: {LocalPath} -> {RemotePath} @ {Host}:{Port}", localPath, remotePath, host, port);
            LogOperation("upload", host, port, username, "failed", $"{localPath} -> {remotePath}", null, ex.Message);
            return false;
        }
    }

    public async Task<bool> DownloadFileAsync(string host, int port, string username, string remotePath, string localPath, string? password = null, string? privateKeyPath = null)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            _logger.LogInformation("通过SFTP下载文件: {RemotePath} -> {LocalPath} @ {Host}:{Port}", remotePath, localPath, host, port);

            // 确保本地目录存在
            var localDir = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(localDir) && !Directory.Exists(localDir))
            {
                Directory.CreateDirectory(localDir);
            }

            var connectionInfo = CreateConnectionInfo(host, port, username, password, privateKeyPath);

            using var client = new SftpClient(connectionInfo);

            await Task.Run(() => client.Connect());

            using var fileStream = File.Create(localPath);
            await Task.Run(() => client.DownloadFile(remotePath, fileStream));

            client.Disconnect();

            var duration = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            Interlocked.Increment(ref _totalFileTransfers);
            LogOperation("download", host, port, username, "success", $"{remotePath} -> {localPath}", duration);

            _logger.LogInformation("SFTP文件下载成功: {LocalPath}", localPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SFTP文件下载失败: {RemotePath} -> {LocalPath} @ {Host}:{Port}", remotePath, localPath, host, port);
            LogOperation("download", host, port, username, "failed", $"{remotePath} -> {localPath}", null, ex.Message);
            return false;
        }
    }

    // ==================== 连接配置管理 ====================

    public Task<PagedResponse<SshConnectionConfigEntity>> GetConnectionConfigsAsync(int page = 1, int pageSize = 20, string? search = null)
    {
        var collection = _dbService.SshConnections;
        var query = collection.Query();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(c =>
                c.Host.Contains(search) ||
                c.Name.Contains(search) ||
                c.Username.Contains(search));
        }

        var total = query.Count();
        var items = query.OrderByDescending(c => c.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Task.FromResult(new PagedResponse<SshConnectionConfigEntity>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public Task<SshConnectionConfigEntity?> GetConnectionConfigAsync(string id)
    {
        SshConnectionConfigEntity? config = _dbService.SshConnections.FindById(id);
        return Task.FromResult<SshConnectionConfigEntity?>(config);
    }

    public Task<SshConnectionConfigEntity> CreateConnectionConfigAsync(SshConnectionConfigEntity config)
    {
        config.Id = Guid.NewGuid().ToString();
        config.CreatedAt = DateTime.UtcNow;
        _dbService.SshConnections.Insert(config);
        _logger.LogInformation("创建SSH连接配置: {Id} {Name} {Host}", config.Id, config.Name, config.Host);
        return Task.FromResult(config);
    }

    public Task<SshConnectionConfigEntity?> UpdateConnectionConfigAsync(string id, SshConnectionConfigEntity config)
    {
        var existing = _dbService.SshConnections.FindById(id);
        if (existing != null)
        {
            config.Id = id;
            config.CreatedAt = existing.CreatedAt;
            _dbService.SshConnections.Update(config);
            _logger.LogInformation("更新SSH连接配置: {Id}", id);
            return Task.FromResult<SshConnectionConfigEntity?>(config);
        }
        return Task.FromResult<SshConnectionConfigEntity?>(null);
    }

    public Task<bool> DeleteConnectionConfigAsync(string id)
    {
        var removed = _dbService.SshConnections.Delete(id);
        if (removed > 0)
        {
            _logger.LogInformation("删除SSH连接配置: {Id}", id);
        }
        return Task.FromResult(removed > 0);
    }

    // ==================== 密钥对管理 ====================

    public Task<PagedResponse<SshKeyPair>> GetKeyPairsAsync(int page = 1, int pageSize = 20)
    {
        var collection = _dbService.SshKeyPairs;
        var total = (int)collection.Count();
        var items = collection.Query().OrderByDescending(k => k.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Task.FromResult(new PagedResponse<SshKeyPair>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public Task<SshKeyPair> ImportKeyPairAsync(string name, string publicKey, string? privateKey = null, string? passphrase = null)
    {
        var keyPair = new SshKeyPair
        {
            Id = Guid.NewGuid().ToString(),
            KeyName = name,
            PublicKey = publicKey,
            PrivateKey = privateKey ?? string.Empty,
            Fingerprint = GenerateFingerprint(publicKey),
            CreatedAt = DateTime.UtcNow,
            HasPassphrase = !string.IsNullOrEmpty(passphrase)
        };

        _dbService.SshKeyPairs.Insert(keyPair);
        _logger.LogInformation("导入SSH密钥对: {Id} {Name}", keyPair.Id, name);
        return Task.FromResult(keyPair);
    }

    public Task<bool> DeleteKeyPairAsync(string id)
    {
        var removed = _dbService.SshKeyPairs.Delete(id);
        if (removed > 0)
        {
            _logger.LogInformation("删除SSH密钥对: {Id}", id);
        }
        return Task.FromResult(removed > 0);
    }

    // ==================== 会话管理 ====================

    public Task<PagedResponse<SshSessionInfo>> GetSessionsAsync(int page = 1, int pageSize = 20)
    {
        var total = _sessions.Count;
        var items = _sessions.Values.OrderByDescending(s => s.ConnectedAt).Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Task.FromResult(new PagedResponse<SshSessionInfo>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public Task<bool> TerminateSessionAsync(string sessionId)
    {
        var removed = _sessions.TryRemove(sessionId, out var session);
        if (removed && session != null)
        {
            LogOperation("disconnect", session.Host, session.Port, session.Username, "success", null);
            _logger.LogInformation("终止SSH会话: {SessionId}", sessionId);
        }
        return Task.FromResult(removed);
    }

    public Task<SshSessionInfo?> ReconnectSessionAsync(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.Status = "connected";
            session.LastActivityAt = DateTime.UtcNow;
            _logger.LogInformation("重连SSH会话: {SessionId}", sessionId);
            return Task.FromResult<SshSessionInfo?>(session);
        }
        return Task.FromResult<SshSessionInfo?>(null);
    }

    public async Task<SshTerminalSessionDescriptor> CreateTerminalSessionAsync(CreateSshTerminalSessionRequest request)
    {
        SshConnectionConfigEntity? config = null;
        if (!string.IsNullOrWhiteSpace(request.ConnectionId))
        {
            config = await GetConnectionConfigAsync(request.ConnectionId);
        }

        var host = config?.Host ?? request.Host;
        var username = config?.Username ?? request.Username;
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("必须提供 SSH 主机和用户名");
        }

        var terminalType = string.IsNullOrWhiteSpace(request.TerminalType)
            ? _settings.DefaultTerminalType
            : request.TerminalType;

        var descriptor = new SshTerminalSessionDescriptor
        {
            Id = Guid.NewGuid().ToString(),
            TerminalType = terminalType,
            Rows = request.Rows > 0 ? request.Rows : _settings.TerminalRows,
            Cols = request.Cols > 0 ? request.Cols : _settings.TerminalCols,
            CreatedAt = DateTime.UtcNow,
            IsActive = false,
            ConnectionInfo = new SshTerminalConnectionInfo
            {
                Host = host,
                Port = config?.Port ?? request.Port,
                Username = username,
                Password = config?.Password ?? request.Password,
                PrivateKeyPath = config?.PrivateKeyPath ?? request.PrivateKeyPath,
                PrivateKeyPassphrase = config?.PrivateKeyPassphrase ?? request.PrivateKeyPassphrase
            }
        };

        _logger.LogInformation("创建 SSH 终端会话描述: {SessionId} {Host}:{Port}", descriptor.Id, host, descriptor.ConnectionInfo.Port);
        return descriptor;
    }

    public Task<PagedResponse<SshHostKey>> GetHostKeysAsync(int page = 1, int pageSize = 20, string? search = null, bool? trusted = null)
    {
        var query = _dbService.SshHostKeys.Query();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(k => k.Host.Contains(search) ||
                                     k.KeyFingerprint.Contains(search) ||
                                     k.Algorithm.Contains(search));
        }

        if (trusted.HasValue)
        {
            var trustedValue = trusted.Value;
            query = query.Where(k => k.Trusted == trustedValue);
        }

        var total = query.Count();
        var items = query.OrderByDescending(k => k.LastSeen)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult(new PagedResponse<SshHostKey>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public Task<SshHostKey> UpsertHostKeyAsync(SshHostKey hostKey)
    {
        if (string.IsNullOrWhiteSpace(hostKey.Host))
        {
            throw new ArgumentException("主机地址不能为空", nameof(hostKey));
        }

        hostKey.Port = hostKey.Port > 0 ? hostKey.Port : 22;
        hostKey.LastSeen = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(hostKey.KeyFingerprint) && !string.IsNullOrWhiteSpace(hostKey.PublicKey))
        {
            hostKey.KeyFingerprint = GenerateFingerprint(hostKey.PublicKey);
        }

        if (string.IsNullOrWhiteSpace(hostKey.Id))
        {
            var identity = $"{hostKey.Host}:{hostKey.Port}:{hostKey.KeyFingerprint}";
            hostKey.Id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
        }

        var existing = _dbService.SshHostKeys.FindById(hostKey.Id);
        if (existing != null)
        {
            hostKey.FirstSeen = existing.FirstSeen;
            _dbService.SshHostKeys.Update(hostKey);
        }
        else
        {
            if (hostKey.FirstSeen == default)
            {
                hostKey.FirstSeen = hostKey.LastSeen;
            }
            _dbService.SshHostKeys.Insert(hostKey);
        }

        _logger.LogInformation("保存 SSH 主机密钥: {Host}:{Port} {Fingerprint}", hostKey.Host, hostKey.Port, hostKey.KeyFingerprint);
        return Task.FromResult(hostKey);
    }

    public Task<bool> DeleteHostKeyAsync(string id)
    {
        var removed = _dbService.SshHostKeys.Delete(id);
        if (removed > 0)
        {
            _logger.LogInformation("删除 SSH 主机密钥: {Id}", id);
        }

        return Task.FromResult(removed > 0);
    }

    // ==================== 目录操作 ====================

    public async Task<List<RemoteFileInfo>> ListDirectoryAsync(string connectionId, string path)
    {
        var config = await GetConnectionConfigAsync(connectionId);
        if (config == null)
        {
            throw new ArgumentException($"Connection config not found: {connectionId}");
        }

        try
        {
            var connectionInfo = CreateConnectionInfo(config.Host, config.Port, config.Username, config.Password, config.PrivateKeyPath, config.PrivateKeyPassphrase);

            using var client = new SftpClient(connectionInfo);

            await Task.Run(() => client.Connect());

            var files = new List<RemoteFileInfo>();
            var entries = await Task.Run(() => client.ListDirectory(path));

            foreach (var entry in entries)
            {
                if (entry.Name == "." || entry.Name == "..") continue;

                files.Add(new RemoteFileInfo
                {
                    Name = entry.Name,
                    Path = $"{path.TrimEnd('/')}/{entry.Name}",
                    Type = entry.IsDirectory ? "directory" : "file",
                    Size = entry.Length,
                    ModifiedAt = entry.LastWriteTime,
                    Permissions = GetPermissionString(entry),
                    Owner = entry.UserId.ToString(),
                    Group = entry.GroupId.ToString()
                });
            }

            client.Disconnect();

            return files;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "列出远程目录失败: {Path}", path);
            throw;
        }
    }

    public async Task<bool> DeleteRemoteFileAsync(string connectionId, string path)
    {
        var config = await GetConnectionConfigAsync(connectionId);
        if (config == null)
        {
            throw new ArgumentException($"Connection config not found: {connectionId}");
        }

        try
        {
            var connectionInfo = CreateConnectionInfo(config.Host, config.Port, config.Username, config.Password, config.PrivateKeyPath, config.PrivateKeyPassphrase);

            using var client = new SftpClient(connectionInfo);

            await Task.Run(() => client.Connect());

            var attributes = await Task.Run(() => client.GetAttributes(path));

            if (attributes.IsDirectory)
            {
                await Task.Run(() => DeleteDirectoryRecursive(client, path));
            }
            else
            {
                await Task.Run(() => client.DeleteFile(path));
            }

            client.Disconnect();

            _logger.LogInformation("成功删除远程文件/目录: {Path}", path);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除远程文件失败: {Path}", path);
            throw;
        }
    }

    private void DeleteDirectoryRecursive(SftpClient client, string path)
    {
        foreach (var entry in client.ListDirectory(path))
        {
            if (entry.Name == "." || entry.Name == "..") continue;

            var fullPath = $"{path.TrimEnd('/')}/{entry.Name}";
            if (entry.IsDirectory)
            {
                DeleteDirectoryRecursive(client, fullPath);
            }
            else
            {
                client.DeleteFile(fullPath);
            }
        }
        client.DeleteDirectory(path);
    }

    // ==================== 统计和日志 ====================

    public Task<SshStatistics> GetStatisticsAsync()
    {
        return Task.FromResult(new SshStatistics
        {
            TotalConnections = (int)_dbService.SshConnections.Count(),
            ActiveConnections = _sessions.Count(s => s.Value.Status == "connected"),
            TotalKeyPairs = (int)_dbService.SshKeyPairs.Count(),
            TotalCommands = _totalCommands,
            TotalFileTransfers = _totalFileTransfers
        });
    }

    public Task<PagedResponse<SshOperationLog>> GetOperationLogsAsync(OperationLogFilter? filter = null)
    {
        var collection = _dbService.SshOperationLogs;
        var query = collection.Query();

        if (filter != null)
        {
            if (!string.IsNullOrEmpty(filter.Search))
            {
                query = query.Where(l => l.Host.Contains(filter.Search) ||
                                         (l.Details != null && l.Details.Contains(filter.Search)));
            }
            if (!string.IsNullOrEmpty(filter.Operation))
            {
                query = query.Where(l => l.Operation == filter.Operation);
            }
            if (!string.IsNullOrEmpty(filter.Host))
            {
                query = query.Where(l => l.Host == filter.Host);
            }
            if (!string.IsNullOrEmpty(filter.Status))
            {
                query = query.Where(l => l.Status == filter.Status);
            }
            if (filter.StartDate.HasValue)
            {
                var startDate = filter.StartDate.Value;
                query = query.Where(l => l.Timestamp >= startDate);
            }
            if (filter.EndDate.HasValue)
            {
                var endDate = filter.EndDate.Value;
                query = query.Where(l => l.Timestamp <= endDate);
            }
        }

        var page = filter?.Page ?? 1;
        var pageSize = filter?.PageSize ?? 20;
        var total = query.Count();
        var items = query.OrderByDescending(l => l.Timestamp).Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Task.FromResult(new PagedResponse<SshOperationLog>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        });
    }

    // ==================== 设置管理 ====================

    public Task<SshSettings> GetSettingsAsync()
    {
        return Task.FromResult(_settings);
    }

    public Task<SshSettings> UpdateSettingsAsync(SshSettings settings)
    {
        _settings = settings;
        _logger.LogInformation("更新SSH设置");
        return Task.FromResult(_settings);
    }

    // ==================== 辅助方法 ====================

    private void LogOperation(string operation, string host, int port, string username, string status, string? details, long? duration = null, string? error = null)
    {
        if (!_settings.LogOperations) return;

        var log = new SshOperationLog
        {
            Id = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            Operation = operation,
            Host = host,
            Port = port,
            Username = username,
            Status = status,
            Details = details,
            Duration = duration,
            ErrorMessage = error
        };

        _dbService.SshOperationLogs.Insert(log);
    }

    private string GetPermissionString(ISftpFile entry)
    {
        var permissions = new StringBuilder();

        permissions.Append(entry.IsDirectory ? 'd' : '-');
        permissions.Append(entry.OwnerCanRead ? 'r' : '-');
        permissions.Append(entry.OwnerCanWrite ? 'w' : '-');
        permissions.Append(entry.OwnerCanExecute ? 'x' : '-');
        permissions.Append(entry.GroupCanRead ? 'r' : '-');
        permissions.Append(entry.GroupCanWrite ? 'w' : '-');
        permissions.Append(entry.GroupCanExecute ? 'x' : '-');
        permissions.Append(entry.OthersCanRead ? 'r' : '-');
        permissions.Append(entry.OthersCanWrite ? 'w' : '-');
        permissions.Append(entry.OthersCanExecute ? 'x' : '-');

        return permissions.ToString();
    }

    private string FormatPemKey(byte[] keyBytes, string keyType)
    {
        var base64 = Convert.ToBase64String(keyBytes);
        var sb = new StringBuilder();
        sb.AppendLine($"-----BEGIN {keyType}-----");

        for (int i = 0; i < base64.Length; i += 64)
        {
            sb.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));
        }

        sb.AppendLine($"-----END {keyType}-----");
        return sb.ToString();
    }

    private string FormatSshRsaPublicKey(RSA rsa, string comment)
    {
        var parameters = rsa.ExportParameters(false);

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // 写入 "ssh-rsa" 标识
        var keyType = Encoding.ASCII.GetBytes("ssh-rsa");
        writer.Write(BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(keyType.Length) : keyType.Length);
        writer.Write(keyType);

        // 写入指数 (e)
        var e = parameters.Exponent!;
        if ((e[0] & 0x80) != 0)
        {
            var paddedE = new byte[e.Length + 1];
            Array.Copy(e, 0, paddedE, 1, e.Length);
            e = paddedE;
        }
        writer.Write(BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(e.Length) : e.Length);
        writer.Write(e);

        // 写入模数 (n)
        var n = parameters.Modulus!;
        if ((n[0] & 0x80) != 0)
        {
            var paddedN = new byte[n.Length + 1];
            Array.Copy(n, 0, paddedN, 1, n.Length);
            n = paddedN;
        }
        writer.Write(BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(n.Length) : n.Length);
        writer.Write(n);

        return $"ssh-rsa {Convert.ToBase64String(ms.ToArray())} {comment}";
    }

    private string GenerateFingerprint(string publicKey)
    {
        try
        {
            using var sha256 = SHA256.Create();
            var keyBytes = Encoding.UTF8.GetBytes(publicKey);
            var hash = sha256.ComputeHash(keyBytes);

            // 返回 SHA256 格式的指纹
            return "SHA256:" + Convert.ToBase64String(hash).TrimEnd('=');
        }
        catch
        {
            return Guid.NewGuid().ToString("N").Substring(0, 16);
        }
    }
}