using System;
using System.Collections.Generic;
using System.Linq;

namespace DockerPanel.API.Services.Acme
{
    /// <summary>
    /// 证书记录有效性判定与域名匹配。
    /// 与 SniCertificateSelector 的判定保持一致：已过期、已撤销（Revoked）、
    /// 已被覆盖（Superseded）或缺少证书内容的记录都不再承载 TLS。
    /// </summary>
    public static class CertificateValidity
    {
        /// <summary>
        /// 记录是否仍在实际承载 TLS（未过期、未被撤销/覆盖、证书内容完整）
        /// </summary>
        public static bool IsServing(CertificateRecord? record, DateTime? utcNow = null)
        {
            if (record == null)
            {
                return false;
            }

            var now = utcNow ?? DateTime.UtcNow;

            if (record.ExpiresAt <= now)
            {
                return false;
            }

            if (HasStatus(record.Status, "revoked") ||
                HasStatus(record.Status, "superseded") ||
                HasStatus(record.Status, "expired"))
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(record.CertificateData)
                && !string.IsNullOrWhiteSpace(record.PrivateKeyData);
        }

        /// <summary>
        /// 状态字符串大小写无关比较（历史数据存在 Active/active、Revoked/revoked 混用）
        /// </summary>
        public static bool HasStatus(string? status, string expected)
        {
            return status != null &&
                   string.Equals(status.Trim(), expected, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 证书声明的域名是否覆盖目标域名（精确匹配或一级通配符 *.example.com）
        /// </summary>
        public static bool CoversDomain(CertificateRecord record, string? domain)
        {
            if (record.Domains == null || record.Domains.Count == 0 || string.IsNullOrWhiteSpace(domain))
            {
                return false;
            }

            var target = domain.Trim();

            foreach (var declared in record.Domains)
            {
                if (string.IsNullOrWhiteSpace(declared))
                {
                    continue;
                }

                var pattern = declared.Trim();

                if (string.Equals(pattern, target, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!pattern.StartsWith("*."))
                {
                    continue;
                }

                var baseDomain = pattern[2..];
                if (!target.EndsWith(baseDomain, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var prefix = target[..^baseDomain.Length].TrimEnd('.');
                if (!string.IsNullOrEmpty(prefix) && !prefix.Contains('.'))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 在候选记录中挑选最适合承载该域名的证书：仍有效、声明覆盖该域名、剩余有效期最长
        /// </summary>
        public static CertificateRecord? FindBestForDomain(
            IEnumerable<CertificateRecord>? candidates,
            string? domain,
            string? excludeCertificateId = null,
            DateTime? utcNow = null)
        {
            if (candidates == null || string.IsNullOrWhiteSpace(domain))
            {
                return null;
            }

            var now = utcNow ?? DateTime.UtcNow;

            return candidates
                .Where(c => c != null && !string.Equals(c.Id, excludeCertificateId, StringComparison.Ordinal))
                .Where(c => IsServing(c, now) && CoversDomain(c, domain))
                .OrderByDescending(c => c.ExpiresAt)
                .ThenByDescending(c => c.CreatedAt)
                .FirstOrDefault();
        }
    }
}
