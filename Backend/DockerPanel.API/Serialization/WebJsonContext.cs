using System.Text.Json.Serialization;

namespace DockerPanel.API.Serialization
{
    /// <summary>
    /// Web(HTTP) 边界的源生成 JSON 上下文：camelCase + 字符串枚举，
    /// 与 MVC/Miniial 全局 HttpJsonOptions 对齐，供 AOT 裁剪后使用。
    /// 已注册类型之外的负载在非 AOT 模式下由 CombineToReflection 解析，逐步收敛注册类型。
    /// </summary>
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
    [JsonSerializable(typeof(DockerPanel.API.Models.SystemSettingsDto))]
    [JsonSerializable(typeof(DockerPanel.API.Models.PublicSystemSettingsDto))]
    [JsonSerializable(typeof(DockerPanel.API.Endpoints.ApiErrorResponse))]
    [JsonSerializable(typeof(DockerPanel.API.Endpoints.SettingsHealthResponse))]
    [JsonSerializable(typeof(DockerPanel.API.Endpoints.SystemInfoResponse))]
    public sealed partial class WebJsonContext : JsonSerializerContext
    {
    }
}