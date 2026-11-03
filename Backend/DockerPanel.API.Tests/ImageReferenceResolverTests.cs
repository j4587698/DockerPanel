using DockerPanel.API.Services;
using Xunit;

namespace DockerPanel.API.Tests;

/// <summary>
/// 镜像引用解析测试。
///
/// 背景：Docker 的 GET /containers/json 里 Image 字段不保证是 repo:tag。
/// 容器以镜像 ID 创建时是 sha256:&lt;摘要&gt;，以 digest 创建时是 repo@sha256:...。
/// 原先各处都用 LastIndexOf(':') 或 Split(':') 切分，会把 sha256:... 切成
/// name=sha256、tag=&lt;摘要&gt;，随后 docker pull sha256 必然返回
/// 「pull access denied for sha256, repository does not exist」。
/// </summary>
public class ImageReferenceResolverTests
{
    private const string FullDigestId =
        "sha256:1dbc5d2a6efb9a43eddb70d51438c16250c15d86ffa3b606db958066ab264bff";

    [Fact]
    public void IsBareImageId_WithSha256Digest_ReturnsTrue()
    {
        Assert.True(ImageReferenceResolver.IsBareImageId(FullDigestId));
    }

    [Fact]
    public void IsBareImageId_WithShortHexId_ReturnsTrue()
    {
        Assert.True(ImageReferenceResolver.IsBareImageId("1dbc5d2a6efb"));
    }

    [Fact]
    public void IsBareImageId_WithRepoTag_ReturnsFalse()
    {
        Assert.False(ImageReferenceResolver.IsBareImageId("j4587698/dockerpanel:latest"));
    }

    [Fact]
    public void IsBareImageId_WithRepoAtDigest_ReturnsFalse()
    {
        // repo@sha256:... 是 digest 引用，不是裸 ID，两者要走不同分支
        Assert.False(ImageReferenceResolver.IsBareImageId($"j4587698/dockerpanel@{FullDigestId}"));
    }

    [Fact]
    public void IsBareImageId_WithNullOrEmpty_ReturnsFalse()
    {
        Assert.False(ImageReferenceResolver.IsBareImageId(null));
        Assert.False(ImageReferenceResolver.IsBareImageId("   "));
    }

    [Fact]
    public void IsDigestReference_WithRepoAtDigest_ReturnsTrue()
    {
        Assert.True(ImageReferenceResolver.IsDigestReference($"j4587698/dockerpanel@{FullDigestId}"));
    }

    [Fact]
    public void IsDigestReference_WithNormalTag_ReturnsFalse()
    {
        Assert.False(ImageReferenceResolver.IsDigestReference("j4587698/dockerpanel:latest"));
    }

    [Fact]
    public void Split_WithRepoTag_ReturnsRepositoryAndTag()
    {
        var (repository, tag) = ImageReferenceResolver.Split("j4587698/dockerpanel:0.10.5");

        Assert.Equal("j4587698/dockerpanel", repository);
        Assert.Equal("0.10.5", tag);
    }

    [Fact]
    public void Split_WithoutTag_DefaultsToLatest()
    {
        var (repository, tag) = ImageReferenceResolver.Split("j4587698/dockerpanel");

        Assert.Equal("j4587698/dockerpanel", repository);
        Assert.Equal("latest", tag);
    }

    [Fact]
    public void Split_WithRegistryPort_KeepsPortInRepository()
    {
        // registry:5000 里的冒号是端口，不是 tag 分隔符
        var (repository, tag) = ImageReferenceResolver.Split("registry.example.com:5000/team/app:1.2.3");

        Assert.Equal("registry.example.com:5000/team/app", repository);
        Assert.Equal("1.2.3", tag);
    }

    [Fact]
    public async Task ResolvePullableAsync_WithBareImageId_ResolvesRepoTagFromLookup()
    {
        var resolved = await ImageReferenceResolver.ResolvePullableAsync(
            FullDigestId,
            _ => Task.FromResult<string[]?>(["j4587698/dockerpanel:0.10.5"]));

        Assert.Equal("j4587698/dockerpanel:0.10.5", resolved);
    }

    [Fact]
    public async Task ResolvePullableAsync_WithDanglingImage_ReturnsNull()
    {
        // RepoTags 为空或全是 <none> 时没有任何名字可 pull，必须返回 null 让调用方报错，
        // 而不是把 sha256:... 原样交给 docker pull
        var resolved = await ImageReferenceResolver.ResolvePullableAsync(
            FullDigestId,
            _ => Task.FromResult<string[]?>(["j4587698/dockerpanel:<none>"]));

        Assert.Null(resolved);
    }

    [Fact]
    public async Task ResolvePullableAsync_WithDigestReference_ConvertsToLatest()
    {
        var resolved = await ImageReferenceResolver.ResolvePullableAsync(
            $"j4587698/dockerpanel@{FullDigestId}",
            _ => Task.FromResult<string[]?>(null));

        Assert.Equal("j4587698/dockerpanel:latest", resolved);
    }

    [Fact]
    public async Task ResolvePullableAsync_WithNormalTag_PassesThroughWithoutLookup()
    {
        var lookupCalled = false;

        var resolved = await ImageReferenceResolver.ResolvePullableAsync(
            "j4587698/dockerpanel:latest",
            _ =>
            {
                lookupCalled = true;
                return Task.FromResult<string[]?>(null);
            });

        Assert.Equal("j4587698/dockerpanel:latest", resolved);
        Assert.False(lookupCalled);
    }

    [Fact]
    public async Task ResolvePullableAsync_WhenLookupThrows_ReturnsNull()
    {
        var resolved = await ImageReferenceResolver.ResolvePullableAsync(
            FullDigestId,
            _ => throw new InvalidOperationException("镜像不存在"));

        Assert.Null(resolved);
    }
}
