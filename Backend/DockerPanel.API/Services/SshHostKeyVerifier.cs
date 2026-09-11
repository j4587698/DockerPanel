using System.Security.Cryptography;
using DockerPanel.API.Models;
using Renci.SshNet;

namespace DockerPanel.API.Services;

/// <summary>
/// SSH 主机密钥校验（TOFU + 指纹比对）。
/// - 已记录且指纹一致：放行并刷新 LastSeen
/// - 已记录但指纹不一致：拒绝（疑似中间人攻击或主机密钥轮换，需管理员在"主机密钥管理"中确认后重新登记）
/// - 未记录：TOFU 模式记录指纹并放行；严格模式（StrictHostKeyChecking）下拒绝，需管理员预先登记
/// </summary>
public class SshHostKeyVerifier
{
    private readonly DataBaseService _dbService;
    private readonly ILogger<SshHostKeyVerifier> _logger;

    public SshHostKeyVerifier(DataBaseService dbService, ILogger<SshHostKeyVerifier> logger)
    {
        _dbService = dbService;
        _logger = logger;
    }

    /// <summary>计算 OpenSSH 风格的 SHA256 指纹（对主机公钥原始字节哈希）。</summary>
    public static string ComputeFingerprint(byte[] hostKey) =>
        "SHA256:" + Convert.ToBase64String(SHA256.HashData(hostKey)).TrimEnd('=');

    /// <summary>
    /// 在 SshClient 上挂载主机密钥校验。必须在 Connect() 之前调用。
    /// </summary>
    public void Attach(SshClient client, string host, int port, bool strictMode)
    {
        var normalizedPort = port > 0 ? port : 22;

        client.HostKeyReceived += (sender, e) =>
        {
            var fingerprint = ComputeFingerprint(e.HostKey);
            var existing = _dbService.SshHostKeys.Query()
                .Where(k => k.Host == host && k.Port == normalizedPort)
                .ToList()
                .FirstOrDefault();

            if (existing != null)
            {
                if (string.Equals(existing.KeyFingerprint, fingerprint, StringComparison.Ordinal))
                {
                    existing.LastSeen = DateTime.UtcNow;
                    _dbService.SshHostKeys.Update(existing);
                    e.CanTrust = true;
                    return;
                }

                _logger.LogError(
                    "SSH 主机密钥指纹不匹配！{Host}:{Port} 已记录 {Expected}，实际 {Actual}。" +
                    "疑似中间人攻击或主机已重装，已拒绝连接。确认无误后请在主机密钥管理中删除旧记录重新连接。",
                    host, normalizedPort, existing.KeyFingerprint, fingerprint);
                e.CanTrust = false;
                return;
            }

            if (strictMode)
            {
                _logger.LogWarning(
                    "严格模式（StrictHostKeyChecking）：未知 SSH 主机 {Host}:{Port}（{Fingerprint}），已拒绝连接。请先在主机密钥管理中登记。",
                    host, normalizedPort, fingerprint);
                e.CanTrust = false;
                return;
            }

            // TOFU：首次连接记录指纹
            var now = DateTime.UtcNow;
            var record = new SshHostKey
            {
                Host = host,
                Port = normalizedPort,
                KeyType = e.HostKeyName ?? string.Empty,
                Algorithm = e.HostKeyName ?? string.Empty,
                KeySize = e.KeyLength,
                PublicKey = Convert.ToBase64String(e.HostKey),
                KeyFingerprint = fingerprint,
                Trusted = false,
                FirstSeen = now,
                LastSeen = now
            };
            record.Id = Convert.ToHexString(
                SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{host}:{normalizedPort}:{fingerprint}")))
                .ToLowerInvariant();

            try
            {
                _dbService.SshHostKeys.Insert(record);
                _logger.LogWarning("已记录新的 SSH 主机指纹（TOFU）: {Host}:{Port} {Algorithm} {Fingerprint}",
                    host, normalizedPort, record.Algorithm, fingerprint);
            }
            catch (Exception ex)
            {
                // 并发首次连接可能重复插入，忽略唯一键冲突，不影响放行
                _logger.LogDebug(ex, "记录 SSH 主机指纹时发生冲突（可忽略）");
            }

            e.CanTrust = true;
        };
    }
}
