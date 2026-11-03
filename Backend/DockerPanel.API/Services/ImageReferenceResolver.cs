using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;

namespace DockerPanel.API.Services;

/// <summary>
/// 镜像引用解析。
///
/// 存在的理由：Docker 的 <c>GET /containers/json</c> 里 <c>Image</c> 字段不保证是 <c>repo:tag</c>。
/// 容器若以镜像 ID 创建（<c>docker run &lt;image-id&gt;</c>、某些 compose / 脚本写法），
/// 该字段就是 <c>sha256:...</c>；以 digest 创建时是 <c>repo@sha256:...</c>。
///
/// 直接把这些值按 <c>repo:tag</c> 切分，会得到 <c>name=sha256</c>、<c>tag=&lt;摘要&gt;</c>，
/// 随后 <c>docker pull sha256</c> 必然返回
/// <c>pull access denied for sha256, repository does not exist</c>。
/// </summary>
public static class ImageReferenceResolver
{
    private const string DigestSeparator = "@sha256:";

    /// <summary>
    /// 是否是纯镜像 ID（<c>sha256:&lt;64 hex&gt;</c> 或 12 位以上短 ID）。
    /// 这类值不能直接用于 pull，必须先反查本地镜像的 RepoTags。
    /// </summary>
    public static bool IsBareImageId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        var trimmed = value.Trim();

        if (trimmed.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            // 仓库名不可能含冒号前缀，走到这里必然是摘要形式
            return !trimmed.Contains('/');
        }

        // 短 ID：12~64 位纯十六进制，且不含路径分隔符
        if (trimmed.Length is < 12 or > 64 || trimmed.Contains('/')) return false;
        return trimmed.All(Uri.IsHexDigit);
    }

    /// <summary>
    /// 是否是 digest 引用（<c>repo@sha256:...</c>）。
    /// </summary>
    public static bool IsDigestReference(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && value.Contains(DigestSeparator, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 拆出仓库名与标签。正确处理 <c>registry:5000/foo</c> 这类带端口的地址。
    /// </summary>
    public static (string Repository, string Tag) Split(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return (string.Empty, "latest");

        var value = reference.Trim();

        // digest 引用无法用 tag 表达，由调用方先行转换
        if (IsDigestReference(value))
        {
            var at = value.IndexOf('@');
            return (value[..at], string.Empty);
        }

        var slashIndex = value.LastIndexOf('/');
        var colonIndex = value.LastIndexOf(':');
        if (colonIndex > slashIndex && colonIndex > 0)
        {
            return (value[..colonIndex], value[(colonIndex + 1)..]);
        }

        return (value, "latest");
    }

    /// <summary>
    /// 把容器上的镜像引用解析成可以安全 pull 的 <c>repo:tag</c>。
    /// 无法解析时返回 null，由调用方给出明确错误而不是拼出一个非法仓库名。
    /// </summary>
    /// <param name="reference">容器上的镜像引用，可能是 repo:tag、repo@sha256:... 或 sha256:...</param>
    /// <param name="repoTagsLookup">用镜像 ID 查本地镜像 RepoTags 的委托</param>
    public static async Task<string?> ResolvePullableAsync(
        string? reference,
        Func<string, Task<string[]?>> repoTagsLookup)
    {
        if (string.IsNullOrWhiteSpace(reference)) return null;

        var value = reference.Trim();

        // 1. digest 引用：repo@sha256:xxx → 取 repo 的 latest。
        //    自升级/自动更新的语义是「拉最新」，保留 pinned digest 反而拉不到更新。
        if (IsDigestReference(value))
        {
            var repo = value[..value.IndexOf('@')];
            return string.IsNullOrWhiteSpace(repo) ? null : $"{repo}:latest";
        }

        // 2. 纯镜像 ID：反查本地镜像的 RepoTags
        if (IsBareImageId(value))
        {
            try
            {
                var repoTags = await repoTagsLookup(value);
                return repoTags?
                    .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t) && !t.Contains(":<none>", StringComparison.Ordinal));
            }
            catch
            {
                // 镜像已不存在或 Docker 拒绝访问，交给调用方报错
                return null;
            }
        }

        // 3. 正常 repo:tag 或裸仓库名
        return value;
    }

    /// <summary>
    /// 基于 Docker 客户端的重载，直接查本地镜像信息。
    /// </summary>
    public static Task<string?> ResolvePullableAsync(
        DockerClient client,
        string? reference,
        CancellationToken ct = default)
        => ResolvePullableAsync(reference, async imageId =>
        {
            var inspect = await client.Images.InspectImageAsync(imageId, ct);
            return inspect?.RepoTags?.ToArray();
        });
}
