using System.Security.Cryptography;
using System.Text;

namespace DockerPanel.API.Utils;

/// <summary>
/// 敏感凭据保护：加密落盘 / 读取解密。
/// </summary>
public interface ICredentialProtector
{
    /// <summary>值是否已加密（enc:v1: 前缀）。</summary>
    bool IsProtected(string? value);

    /// <summary>加密（空值/已加密原样返回，幂等）。</summary>
    string? Protect(string? plain);

    /// <summary>解密（非加密格式按原样返回，兼容旧明文数据）。</summary>
    string? Unprotect(string? value);
}

/// <summary>
/// 凭据落盘加密（AES-256-GCM）。
/// 密钥自动生成并保存在数据目录 credential.key（与 jwt-secret.key 同级），
/// 密文格式：enc:v1:{base64(nonce[12] | tag[16] | ciphertext)}。
/// 数据库中已存在的旧明文记录读取时按原样返回，下次更新时自动升级为密文。
/// </summary>
public sealed class CredentialProtector : ICredentialProtector
{
    private const string Prefix = "enc:v1:";
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;

    private readonly byte[] _key;

    public CredentialProtector(ILogger<CredentialProtector> logger)
    {
        var keyPath = Path.Combine(AppPathResolver.GetDataDirectory(), "credential.key");
        if (File.Exists(keyPath))
        {
            var key = Convert.FromHexString(File.ReadAllText(keyPath).Trim());
            if (key.Length != KeySize)
            {
                throw new InvalidOperationException($"凭据加密密钥长度无效（应为 {KeySize} 字节）: {keyPath}");
            }
            _key = key;
        }
        else
        {
            _key = RandomNumberGenerator.GetBytes(KeySize);
            Directory.CreateDirectory(Path.GetDirectoryName(keyPath)!);
            File.WriteAllText(keyPath, Convert.ToHexString(_key));
            logger.LogInformation("已生成新的凭据加密密钥: {KeyPath}", keyPath);
        }
    }

    public bool IsProtected(string? value) =>
        value?.StartsWith(Prefix, StringComparison.Ordinal) == true;

    public string? Protect(string? plain)
    {
        if (string.IsNullOrEmpty(plain) || IsProtected(plain)) return plain;

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plainBytes = Encoding.UTF8.GetBytes(plain);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(_key, TagSize))
        {
            aes.Encrypt(nonce, plainBytes, cipher, tag);
        }

        var payload = new byte[NonceSize + TagSize + cipher.Length];
        nonce.CopyTo(payload, 0);
        tag.CopyTo(payload, NonceSize);
        cipher.CopyTo(payload, NonceSize + TagSize);
        return Prefix + Convert.ToBase64String(payload);
    }

    public string? Unprotect(string? value)
    {
        if (string.IsNullOrEmpty(value) || !IsProtected(value)) return value;

        try
        {
            var payload = Convert.FromBase64String(value![Prefix.Length..]);
            var nonce = payload[..NonceSize];
            var tag = payload[NonceSize..(NonceSize + TagSize)];
            var cipher = payload[(NonceSize + TagSize)..];
            var plain = new byte[cipher.Length];

            using (var aes = new AesGcm(_key, TagSize))
            {
                aes.Decrypt(nonce, cipher, tag, plain);
            }

            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("凭据解密失败（credential.key 是否被更换或删除？）", ex);
        }
    }
}
