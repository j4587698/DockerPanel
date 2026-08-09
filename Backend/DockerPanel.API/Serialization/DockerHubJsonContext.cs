using System.Text.Json.Serialization;
using DockerPanel.API.Services;

namespace DockerPanel.API.Serialization
{
    /// <summary>
    /// Docker Hub / Registry API 响应的源生成上下文（AOT 兼容）。
    /// Docker Hub 返回小写字段名（如 "name"/"tags"/"token"），需与
    /// 原反射方案一致的 PropertyNameCaseInsensitive 匹配行为，
    /// 不能复用 DockerPanelJsonContext（默认大小写敏感，会导致字段绑定失败）。
    /// </summary>
    [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
    [JsonSerializable(typeof(DockerTagsResponse))]
    [JsonSerializable(typeof(DockerAuthToken))]
    internal partial class DockerHubJsonContext : JsonSerializerContext
    {
    }
}
