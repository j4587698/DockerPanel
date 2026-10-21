using System.Globalization;
using System.Text;

namespace DockerPanel.API.Utils;

/// <summary>
/// 访问日志行格式化：nginx combined 风格的单行文本，供 fail2ban / awk / 日志采集器直接解析。
/// 输出示例（末尾两个字段为本项目扩展的耗时与命中域名）：
/// <code>
/// 203.0.113.7 - - [21/Oct/2026:14:10:54 +0800] "GET /api/foo?x=1 HTTP/1.1" 200 1234 "-" "curl/8.5.0" 12.4ms school.example.com
/// </code>
/// 字段顺序与 nginx 的 <c>$remote_addr - $remote_user [$time_local] "$request" $status $body_bytes_sent "$http_referer" "$http_user_agent"</c>
/// 完全一致，因此现成的 nginx 类 fail2ban filter 稍作调整即可复用。
/// </summary>
public static class AccessLogFormatter
{
    private const int MaxPathLength = 1024;
    private const int MaxHeaderValueLength = 512;

    /// <summary>
    /// 生成一行访问日志。
    /// </summary>
    /// <param name="timestamp">本地时间（含时区偏移），格式化为 <c>dd/MMM/yyyy:HH:mm:ss +0800</c>。</param>
    /// <param name="clientIp">客户端 IP，无法获取时传 <c>-</c>。</param>
    /// <param name="method">HTTP 方法。</param>
    /// <param name="pathAndQuery">PathBase + Path + QueryString。</param>
    /// <param name="protocol">如 <c>HTTP/1.1</c>。</param>
    /// <param name="statusCode">响应状态码。</param>
    /// <param name="responseBytes">响应体字节数；流式响应拿不到 Content-Length 时为 0。</param>
    /// <param name="referer">Referer，可空。</param>
    /// <param name="userAgent">User-Agent，可空。</param>
    /// <param name="elapsedMilliseconds">请求耗时（毫秒）。</param>
    /// <param name="host">请求 Host，用于区分同一入口上的多个代理域名。</param>
    /// <param name="forwardedFor">
    /// X-Forwarded-For 原始值，仅在有值时作为行尾扩展字段 <c>xff=</c> 输出，用于排查面板前面是否还挂着
    /// CDN / nginx。该头客户端可任意伪造，不可作为封禁依据，故不参与前面的标准字段。
    /// </param>
    public static string Format(
        DateTimeOffset timestamp,
        string clientIp,
        string method,
        string pathAndQuery,
        string protocol,
        int statusCode,
        long responseBytes,
        string? referer,
        string? userAgent,
        double elapsedMilliseconds,
        string? host,
        string? forwardedFor = null)
    {
        var sb = new StringBuilder(256);

        sb.Append(clientIp).Append(" - - [");
        sb.Append(timestamp.ToString("dd/MMM/yyyy:HH:mm:ss ", CultureInfo.InvariantCulture));
        // nginx 的时区写成 +0800，.NET 的 zzz 会给出 +08:00，去掉冒号保持一致
        sb.Append(timestamp.ToString("zzz", CultureInfo.InvariantCulture).Replace(":", string.Empty));
        sb.Append("] \"");

        sb.Append(method).Append(' ');
        sb.Append(Sanitize(pathAndQuery, MaxPathLength));
        sb.Append(' ').Append(protocol);
        sb.Append("\" ");

        sb.Append(statusCode).Append(' ').Append(responseBytes);

        sb.Append(" \"").Append(Sanitize(referer, MaxHeaderValueLength)).Append('"');
        sb.Append(" \"").Append(Sanitize(userAgent, MaxHeaderValueLength)).Append('"');

        sb.Append(' ').Append(elapsedMilliseconds.ToString("F1", CultureInfo.InvariantCulture)).Append("ms ");
        sb.Append(Sanitize(host, 255));

        if (!string.IsNullOrEmpty(forwardedFor))
        {
            sb.Append(" xff=").Append(Sanitize(forwardedFor, MaxHeaderValueLength));
        }

        return sb.ToString();
    }

    /// <summary>
    /// 清洗单行字段：换行、制表符与其它控制字符统一替换为空格，双引号替换为单引号。
    /// 目的是防止 UA / Referer / 路径里夹带换行伪造出额外的日志行，进而欺骗按行解析的工具
    /// （例如 fail2ban 的过滤器），也避免日志文件被写入不可见控制字符。
    /// </summary>
    private static string Sanitize(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "-";
        }

        var sb = new StringBuilder(Math.Min(value.Length, maxLength));
        foreach (var ch in value)
        {
            if (sb.Length >= maxLength)
            {
                sb.Append("...");
                break;
            }

            switch (ch)
            {
                case '\r':
                case '\n':
                case '\t':
                    sb.Append(' ');
                    break;
                case '"':
                    sb.Append('\'');
                    break;
                default:
                    sb.Append(char.IsControl(ch) ? ' ' : ch);
                    break;
            }
        }

        return sb.Length == 0 ? "-" : sb.ToString();
    }
}
