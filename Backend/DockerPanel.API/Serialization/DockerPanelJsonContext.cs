using System.Text.Json.Serialization;

namespace DockerPanel.API.Serialization
{
    /// <summary>
    /// 应用内自有类型的源生成 JSON 上下文，用于显式 JsonSerializer 调用。
    /// 注意：不配置 camelCase，保持与既有持久化格式（如 ACME 任务队列 Payload）兼容。
    /// Dictionary&lt;string, object&gt; 属性由 DictionaryObjectConverter 在 options 中接管。
    /// </summary>
    [JsonSerializable(typeof(DockerPanel.API.Services.Acme.AutoRenewalJobPayload))]
    [JsonSerializable(typeof(DockerPanel.API.Services.Acme.AutoValidationJobPayload))]
    [JsonSerializable(typeof(DockerPanel.API.Services.Acme.ChallengeValidationService.DohResponse))]
    [JsonSerializable(typeof(DockerPanel.API.Services.Acme.ChallengeValidationService.DohAnswer))]
    [JsonSerializable(typeof(DockerPanel.API.Services.Acme.ChallengeValidationService.DohAuthority))]
    [JsonSerializable(typeof(DockerPanel.API.Services.Acme.ChallengeValidationService.DohQuestion))]
    [JsonSerializable(typeof(DockerPanel.API.Services.DockerTagsResponse))]
    [JsonSerializable(typeof(DockerPanel.API.Services.Acme.ChallengeStatusUpdate))]
    [JsonSerializable(typeof(DockerPanel.API.Services.Acme.WildcardCertificateInfo))]
    [JsonSerializable(typeof(DockerPanel.API.Models.SystemSettingsDto))]
    internal sealed partial class DockerPanelJsonContext : JsonSerializerContext
    {
    }
}