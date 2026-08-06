using System.Text.Json.Serialization;

namespace DockerPanel.API.Serialization;

/// <summary>
/// SignalR 载荷源生成上下文（AOT 兼容）。
/// 覆盖所有 Hub 发送/接收的类型。
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Hubs.TerminalErrorMessage))]
[JsonSerializable(typeof(Hubs.SshConnectedMessage))]
[JsonSerializable(typeof(Hubs.ContainerTerminalConnectedMessage))]
[JsonSerializable(typeof(Hubs.WelcomeMessage))]
[JsonSerializable(typeof(Hubs.HubErrorMessage))]
[JsonSerializable(typeof(Hubs.LogsSubscribedMessage))]
[JsonSerializable(typeof(Hubs.PongMessage))]
[JsonSerializable(typeof(Hubs.ConnectionStatusMessage))]
[JsonSerializable(typeof(Hubs.ComposeDeployProgressMessage))]
[JsonSerializable(typeof(Hubs.ComposeOperationProgressMessage))]
[JsonSerializable(typeof(Hubs.VolumeArchiveProgressMessage))]
[JsonSerializable(typeof(Hubs.ImagePullProgressMessage))]
[JsonSerializable(typeof(Hubs.ImagePushProgressMessage))]
[JsonSerializable(typeof(Hubs.ImageBuildProgressMessage))]
[JsonSerializable(typeof(Hubs.SshTerminalHub.SshConnectRequest))]
[JsonSerializable(typeof(Hubs.ContainerTerminalHub.TerminalConnectRequest))]
[JsonSerializable(typeof(Models.ContainerInfo))]
[JsonSerializable(typeof(Models.ImageInfo))]
[JsonSerializable(typeof(Services.ClusterResourceStats))]
[JsonSerializable(typeof(IEnumerable<Models.ContainerInfo>))]
[JsonSerializable(typeof(List<Models.ContainerInfo>))]
[JsonSerializable(typeof(IEnumerable<Models.ImageInfo>))]
[JsonSerializable(typeof(List<Models.ImageInfo>))]
[JsonSerializable(typeof(LogStreamMessage))]
internal partial class DockerPanelSignalRJsonContext : JsonSerializerContext
{
}
