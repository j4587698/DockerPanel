using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DockerPanel.API.Models;
using DockerPanel.API.Serialization;
using DockerPanel.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Authorization;

namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// SSH 连接管理 Minimal API 端点（原 SshController，全部端点要求 Admin 角色）。
    /// </summary>
    public static class SshEndpoints
    {
        /// <summary>
        /// 日志类别标记（静态类不能作泛型参数）。
        /// </summary>
        public sealed class LoggingTag
        {
        }

        /// <summary>
        /// 映射 SSH 连接管理相关路由。
        /// </summary>
        public static IEndpointRouteBuilder MapSshEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/ssh")
                .RequireAuthorization(new AuthorizeAttribute { Roles = AuthRoles.Admin });

            group.MapPost("test-connection", TestSshConnection);
            group.MapPost("generate-keypair", GenerateKeyPair);
            group.MapPost("validate-privatekey", ValidatePrivateKey);
            group.MapPost("execute-command", ExecuteCommand);
            group.MapPost("upload-file", UploadFile);
            group.MapPost("download-file", DownloadFile);
            group.MapPost("batch-test-connection", BatchTestConnection);
            group.MapGet("connections", GetConnectionConfigs);
            group.MapGet("connections/{id}", GetConnectionConfig);
            group.MapPost("connections", CreateConnectionConfig);
            group.MapPut("connections/{id}", UpdateConnectionConfig);
            group.MapDelete("connections/{id}", DeleteConnectionConfig);
            group.MapGet("keypairs", GetKeyPairs);
            group.MapPost("keypairs/import", ImportKeyPair);
            group.MapDelete("keypairs/{id}", DeleteKeyPair);
            group.MapGet("sessions", GetSessions);
            group.MapDelete("sessions/{id}", TerminateSession);
            group.MapPost("sessions/{id}/reconnect", ReconnectSession);
            group.MapPost("terminal-sessions", CreateTerminalSession);
            group.MapGet("host-keys", GetHostKeys);
            group.MapPost("host-keys", UpsertHostKey);
            group.MapDelete("host-keys/{id}", DeleteHostKey);
            group.MapPost("list-directory", ListDirectory);
            group.MapPost("delete-remote-file", DeleteRemoteFile);
            group.MapGet("statistics", GetStatistics);
            group.MapGet("logs", GetOperationLogs);
            group.MapGet("settings", GetSettings);
            group.MapPut("settings", UpdateSettings);

            return app;
        }

        private static async Task<IResult> TestSshConnection(SshConnectionTestRequest request, ISshService sshService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var result = await sshService.TestSshConnectionAsync(
                    request.Host,
                    request.Port,
                    request.Username,
                    request.Password,
                    request.PrivateKeyPath);

                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "测试SSH连接失败: {Host}:{Port}", request.Host, request.Port);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("ssh.testConnectionFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GenerateKeyPair(GenerateKeyPairRequest request, ISshService sshService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var keyPair = await sshService.GenerateKeyPairAsync(
                    request.KeyName,
                    request.KeyType ?? "RSA",
                    request.KeySize ?? 2048,
                    request.Passphrase);

                return TypedResults.Ok(keyPair);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "生成SSH密钥对失败: {KeyName}", request.KeyName);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("ssh.generateKeyPairFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ValidatePrivateKey(ValidatePrivateKeyRequest request, ISshService sshService, ILogger<LoggingTag> logger, ILocalizationService localization)
        {
            try
            {
                var result = await sshService.ValidatePrivateKeyAsync(request.PrivateKeyPath, request.Passphrase);

                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "验证SSH私钥失败: {PrivateKeyPath}", request.PrivateKeyPath);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("ssh.validateKeyFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ExecuteCommand(ExecuteCommandRequest request, ILocalizationService localization, ISshService sshService, ILogger<LoggingTag> logger)
        {
            try
            {
                var result = await sshService.ExecuteCommandAsync(
                    request.Host,
                    request.Port,
                    request.Username,
                    request.Command,
                    request.Password,
                    request.PrivateKeyPath,
                    request.WorkingDirectory,
                    request.Timeout ?? 60000);

                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SSH命令执行失败: {Host}:{Port} 命令: {Command}", request.Host, request.Port, request.Command);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("ssh.commandFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> UploadFile(UploadFileRequest request, ILogger<LoggingTag> logger, ISshService sshService, ILocalizationService localization)
        {
            try
            {
                var result = await sshService.UploadFileAsync(
                    request.Host,
                    request.Port,
                    request.Username,
                    request.LocalPath,
                    request.RemotePath,
                    request.Password,
                    request.PrivateKeyPath);

                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SSH文件上传失败: {LocalPath} -> {RemotePath} @ {Host}:{Port}",
                    request.LocalPath, request.RemotePath, request.Host, request.Port);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("ssh.uploadFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> DownloadFile(DownloadFileRequest request, ILocalizationService localization, ILogger<LoggingTag> logger, ISshService sshService)
        {
            try
            {
                var result = await sshService.DownloadFileAsync(
                    request.Host,
                    request.Port,
                    request.Username,
                    request.RemotePath,
                    request.LocalPath,
                    request.Password,
                    request.PrivateKeyPath);

                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SSH文件下载失败: {RemotePath} -> {LocalPath} @ {Host}:{Port}",
                    request.RemotePath, request.LocalPath, request.Host, request.Port);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("ssh.downloadFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> BatchTestConnection(BatchSshTestRequest request, ILogger<LoggingTag> logger, ILocalizationService localization, ISshService sshService)
        {
            try
            {
                var results = new List<SshBatchTestResult>();

                foreach (var connection in request.Connections)
                {
                    try
                    {
                        var result = await sshService.TestSshConnectionAsync(
                            connection.Host,
                            connection.Port,
                            connection.Username,
                            connection.Password,
                            connection.PrivateKeyPath);

                        results.Add(new SshBatchTestResult
                        {
                            Host = connection.Host,
                            Port = connection.Port,
                            Username = connection.Username,
                            Success = result
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new SshBatchTestResult
                        {
                            Host = connection.Host,
                            Port = connection.Port,
                            Username = connection.Username,
                            Success = false,
                            Error = ex.Message
                        });
                    }
                }

                return TypedResults.Ok(new SshBatchTestResponse { Results = results });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "批量SSH连接测试失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("ssh.batchTestFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetConnectionConfigs(int page, int pageSize, string? search, ISshService sshService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var result = await sshService.GetConnectionConfigsAsync(page, pageSize, search);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取SSH连接配置列表失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("ssh.getConfigListFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetConnectionConfig(string id, ISshService sshService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var config = await sshService.GetConnectionConfigAsync(id);
                if (config == null)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("ssh.configNotFound") });
                }
                return TypedResults.Ok(config);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取SSH连接配置失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("ssh.getConfigFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> CreateConnectionConfig(SshConnectionConfigEntity config, ISshService sshService, ILogger<LoggingTag> logger, ILocalizationService localization)
        {
            try
            {
                var result = await sshService.CreateConnectionConfigAsync(config);
                return TypedResults.Created($"/api/ssh/connections/{result.Id}", result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "创建SSH连接配置失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("ssh.createConfigFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> UpdateConnectionConfig(string id, SshConnectionConfigEntity config, ILocalizationService localization, ISshService sshService, ILogger<LoggingTag> logger)
        {
            try
            {
                var result = await sshService.UpdateConnectionConfigAsync(id, config);
                if (result == null)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("ssh.configNotFound") });
                }
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "更新SSH连接配置失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("ssh.updateConfigFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> DeleteConnectionConfig(string id, ILogger<LoggingTag> logger, ISshService sshService, ILocalizationService localization)
        {
            try
            {
                var result = await sshService.DeleteConnectionConfigAsync(id);
                if (!result)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("ssh.configNotFound") });
                }
                return TypedResults.NoContent();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "删除SSH连接配置失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("ssh.deleteConfigFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetKeyPairs(int page, int pageSize, ILocalizationService localization, ISshService sshService, ILogger<LoggingTag> logger)
        {
            try
            {
                var result = await sshService.GetKeyPairsAsync(page, pageSize);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取SSH密钥对列表失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("ssh.getKeyPairListFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ImportKeyPair(ImportKeyPairRequest request, ISshService sshService, ILogger<LoggingTag> logger, ILocalizationService localization)
        {
            try
            {
                var result = await sshService.ImportKeyPairAsync(
                    request.Name,
                    request.PublicKey,
                    request.PrivateKey,
                    request.Passphrase);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "导入SSH密钥对失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("ssh.importKeyPairFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> DeleteKeyPair(string id, ILocalizationService localization, ILogger<LoggingTag> logger, ISshService sshService)
        {
            try
            {
                var result = await sshService.DeleteKeyPairAsync(id);
                if (!result)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("ssh.keyPairNotFound") });
                }
                return TypedResults.NoContent();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "删除SSH密钥对失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("ssh.deleteKeyPairFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetSessions(int page, int pageSize, ILogger<LoggingTag> logger, ISshService sshService, ILocalizationService localization)
        {
            try
            {
                var result = await sshService.GetSessionsAsync(page, pageSize);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取SSH会话列表失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("ssh.getSessionListFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> TerminateSession(string id, ILogger<LoggingTag> logger, ILocalizationService localization, ISshService sshService)
        {
            try
            {
                var result = await sshService.TerminateSessionAsync(id);
                if (!result)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("ssh.sessionNotFound") });
                }
                return TypedResults.NoContent();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "终止SSH会话失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("ssh.terminateSessionFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ReconnectSession(string id, ISshService sshService, ILogger<LoggingTag> logger, ILocalizationService localization)
        {
            try
            {
                var result = await sshService.ReconnectSessionAsync(id);
                if (result == null)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("ssh.sessionNotFound") });
                }
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "重连SSH会话失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("ssh.reconnectSessionFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> CreateTerminalSession(CreateSshTerminalSessionRequest request, ILogger<LoggingTag> logger, ILocalizationService localization, ISshService sshService)
        {
            try
            {
                var result = await sshService.CreateTerminalSessionAsync(request);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "创建SSH终端会话描述失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("ssh.createTerminalSessionFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetHostKeys(int page, int pageSize, string? search, bool? trusted, ISshService sshService, ILogger<LoggingTag> logger, ILocalizationService localization)
        {
            try
            {
                var result = await sshService.GetHostKeysAsync(page, pageSize, search, trusted);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取SSH主机密钥列表失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("ssh.getHostKeyListFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> UpsertHostKey(SshHostKey hostKey, ISshService sshService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var result = await sshService.UpsertHostKeyAsync(hostKey);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "保存SSH主机密钥失败: {Host}:{Port}", hostKey.Host, hostKey.Port);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("ssh.saveHostKeyFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> DeleteHostKey(string id, ILocalizationService localization, ISshService sshService, ILogger<LoggingTag> logger)
        {
            try
            {
                var result = await sshService.DeleteHostKeyAsync(id);
                if (!result)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("ssh.hostKeyNotFound") });
                }
                return TypedResults.NoContent();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "删除SSH主机密钥失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("ssh.deleteHostKeyFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ListDirectory(ListDirectoryRequest request, ILocalizationService localization, ILogger<LoggingTag> logger, ISshService sshService)
        {
            try
            {
                var result = await sshService.ListDirectoryAsync(request.ConnectionId, request.Path);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "列出远程目录失败: {ConnectionId} {Path}", request.ConnectionId, request.Path);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("ssh.listDirectoryFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> DeleteRemoteFile(DeleteRemoteFileRequest request, ILogger<LoggingTag> logger, ISshService sshService, ILocalizationService localization)
        {
            try
            {
                var result = await sshService.DeleteRemoteFileAsync(request.ConnectionId, request.Path);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "删除远程文件失败: {ConnectionId} {Path}", request.ConnectionId, request.Path);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("ssh.deleteFileFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetStatistics(ISshService sshService, ILocalizationService localization, ILogger<LoggingTag> logger)
        {
            try
            {
                var result = await sshService.GetStatisticsAsync();
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取SSH统计信息失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("ssh.getStatsFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetOperationLogs(OperationLogFilter? filter, ILocalizationService localization, ILogger<LoggingTag> logger, ISshService sshService)
        {
            try
            {
                var result = await sshService.GetOperationLogsAsync(filter);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取SSH操作日志失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("ssh.getLogsFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetSettings(ILocalizationService localization, ISshService sshService, ILogger<LoggingTag> logger)
        {
            try
            {
                var result = await sshService.GetSettingsAsync();
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取SSH设置失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("ssh.getSettingsFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> UpdateSettings(SshSettings settings, ISshService sshService, ILogger<LoggingTag> logger, ILocalizationService localization)
        {
            try
            {
                var result = await sshService.UpdateSettingsAsync(settings);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "更新SSH设置失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("ssh.updateSettingsFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
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

    /// <summary>
    /// 批量SSH连接测试单项结果
    /// </summary>
    public sealed class SshBatchTestResult
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 22;
        public string Username { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// 批量SSH连接测试响应
    /// </summary>
    public sealed class SshBatchTestResponse
    {
        public List<SshBatchTestResult> Results { get; set; } = new();
    }
}
