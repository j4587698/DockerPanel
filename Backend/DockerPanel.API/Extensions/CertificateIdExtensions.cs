using System;

namespace DockerPanel.API.Extensions
{
    /// <summary>
    /// 证书ID格式转换扩展方法
    /// </summary>
    public static class CertificateIdExtensions
    {
        /// <summary>
        /// 检查字符串是否为有效的证书ID（long格式）
        /// </summary>
        /// <param name="id">要检查的字符串</param>
        /// <returns>是否为有效的证书ID</returns>
        public static bool IsValidCertificateId(this string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;

            return long.TryParse(id, out _);
        }

        /// <summary>
        /// 将字符串转换为证书ID
        /// </summary>
        /// <param name="id">ID字符串</param>
        /// <returns>证书ID（long）</returns>
        public static long ToCertificateId(this string id)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("ID不能为空", nameof(id));

            if (!long.TryParse(id, out var certificateId))
                throw new ArgumentException("无效的证书ID格式", nameof(id));

            return certificateId;
        }

        /// <summary>
        /// 尝试将字符串转换为证书ID
        /// </summary>
        /// <param name="id">ID字符串</param>
        /// <param name="certificateId">输出的证书ID</param>
        /// <returns>是否转换成功</returns>
        public static bool TryParseCertificateId(this string id, out long certificateId)
        {
            certificateId = 0;
            if (string.IsNullOrEmpty(id))
                return false;

            return long.TryParse(id, out certificateId);
        }

        /// <summary>
        /// 获取证书ID的显示格式（用于返回给前端）
        /// </summary>
        /// <param name="id">证书ID</param>
        /// <returns>用于显示的ID字符串</returns>
        public static string ToDisplayId(this long id)
        {
            return id.ToString();
        }

        /// <summary>
        /// 获取证书ID的显示格式（用于返回给前端）
        /// </summary>
        /// <param name="id">ID字符串</param>
        /// <returns>用于显示的ID字符串</returns>
        public static string ToDisplayId(this string id)
        {
            if (string.IsNullOrEmpty(id))
                return string.Empty;

            return id;
        }
    }
}
