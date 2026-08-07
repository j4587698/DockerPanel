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
    [JsonSerializable(typeof(DockerPanel.API.Endpoints.TaskActionResponse))]
    [JsonSerializable(typeof(DockerPanel.API.Endpoints.MessageResponse))]
    [JsonSerializable(typeof(DockerPanel.API.Models.OperationAuditLog))]
    [JsonSerializable(typeof(DockerPanel.API.Models.OperationAuditLogPage))]
    [JsonSerializable(typeof(DockerPanel.API.Services.BackgroundTask))]
    [JsonSerializable(typeof(List<DockerPanel.API.Services.BackgroundTask>))]
    [JsonSerializable(typeof(DockerPanel.API.Models.AuthStatusResponse))]
    [JsonSerializable(typeof(DockerPanel.API.Models.LoginResponse))]
    [JsonSerializable(typeof(DockerPanel.API.Models.AuthUserDto))]
    [JsonSerializable(typeof(DockerPanel.API.Models.UserAccountDto))]
    [JsonSerializable(typeof(IReadOnlyList<DockerPanel.API.Models.UserAccountDto>))]
    [JsonSerializable(typeof(List<DockerPanel.API.Models.UserAccountDto>))]
    [JsonSerializable(typeof(DockerPanel.API.Models.SetupAdminRequest))]
    [JsonSerializable(typeof(DockerPanel.API.Models.LoginRequest))]
    [JsonSerializable(typeof(DockerPanel.API.Models.ChangePasswordRequest))]
    [JsonSerializable(typeof(DockerPanel.API.Models.CreateUserRequest))]
    [JsonSerializable(typeof(DockerPanel.API.Models.UpdateUserRequest))]
    [JsonSerializable(typeof(DockerPanel.API.Models.ResetUserPasswordRequest))]
    public sealed partial class WebJsonContext : JsonSerializerContext
    {
    }
}