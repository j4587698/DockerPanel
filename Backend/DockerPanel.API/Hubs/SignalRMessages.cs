namespace DockerPanel.API.Hubs;

/// <summary>
/// SignalR 错误消息（Ssh/Container 终端 Hub 载荷）
/// </summary>
public class TerminalErrorMessage
{
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
}

/// <summary>
/// SSH 终端连接成功消息
/// </summary>
public class SshConnectedMessage
{
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public string Username { get; set; } = "";
    public DateTime ConnectedAt { get; set; }
}

/// <summary>
/// 容器终端连接成功消息
/// </summary>
public class ContainerTerminalConnectedMessage
{
    public string ContainerId { get; set; } = "";
    public string Shell { get; set; } = "";
    public DateTime ConnectedAt { get; set; }
}

/// <summary>
/// 欢迎消息
/// </summary>
public class WelcomeMessage
{
    public string Message { get; set; } = "";
    public string ConnectionId { get; set; } = "";
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// DockerPanelHub 通用错误消息
/// </summary>
public class HubErrorMessage
{
    public string Message { get; set; } = "";
}

/// <summary>
/// 日志订阅确认消息
/// </summary>
public class LogsSubscribedMessage
{
    public string ContainerId { get; set; } = "";
    public int TailLines { get; set; }
}

/// <summary>
/// 心跳响应消息
/// </summary>
public class PongMessage
{
    public DateTime Timestamp { get; set; }
    public string ServerTime { get; set; } = "";
}

/// <summary>
/// 连接状态消息
/// </summary>
public class ConnectionStatusMessage
{
    public string ConnectionId { get; set; } = "";
    public bool IsConnected { get; set; }
    public DateTime ConnectedAt { get; set; }
    public List<string> Subscriptions { get; set; } = new();
}

/// <summary>
/// 更新后的日志（LogUpdated 载荷）
/// </summary>
public class LogUpdatedMessage
{
    public string ContainerId { get; set; } = "";
    public object Log { get; set; } = "";
}

/// <summary>
/// Compose 部署进度载荷
/// </summary>
public class ComposeDeployProgressMessage
{
    public string ProjectId { get; set; } = "";
    public string Step { get; set; } = "";
    public string StepKey { get; set; } = "";
    public string Status { get; set; } = "";
    public int Progress { get; set; }
    public string? Detail { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Compose 操作进度载荷（启动/停止等）
/// </summary>
public class ComposeOperationProgressMessage
{
    public string ProjectName { get; set; } = "";
    public string Step { get; set; } = "";
    public string StepKey { get; set; } = "";
    public string Status { get; set; } = "";
    public int Progress { get; set; }
    public string? Detail { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// 卷打包进度载荷
/// </summary>
public class VolumeArchiveProgressMessage
{
    public string VolumeId { get; set; } = "";
    public string Step { get; set; } = "";
    public string StepKey { get; set; } = "";
    public string Status { get; set; } = "";
    public int Progress { get; set; }
    public string? Detail { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// 镜像拉取进度载荷
/// </summary>
public class ImagePullProgressMessage
{
    public string PullId { get; set; } = "";
    public string ImageName { get; set; } = "";
    public string Step { get; set; } = "";
    public string StepKey { get; set; } = "";
    public string Status { get; set; } = "";
    public int Progress { get; set; }
    public string? Detail { get; set; }
    public ImagePullLayerMessage? Layer { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// 镜像拉取进度-层信息
/// </summary>
public class ImagePullLayerMessage
{
    public string LayerId { get; set; } = "";
    public string Status { get; set; } = "";
    public long Current { get; set; }
    public long Total { get; set; }
    public int Progress { get; set; }
}

/// <summary>
/// 镜像推送进度载荷
/// </summary>
public class ImagePushProgressMessage
{
    public string PushId { get; set; } = "";
    public string ImageName { get; set; } = "";
    public string Step { get; set; } = "";
    public string StepKey { get; set; } = "";
    public string Status { get; set; } = "";
    public int Progress { get; set; }
    public string? Detail { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// 镜像构建进度载荷
/// </summary>
public class ImageBuildProgressMessage
{
    public string BuildId { get; set; } = "";
    public string Step { get; set; } = "";
    public string StepKey { get; set; } = "";
    public string Status { get; set; } = "";
    public int Progress { get; set; }
    public string? Detail { get; set; }
    public string? Stream { get; set; }
    public bool IsError { get; set; }
    public DateTime Timestamp { get; set; }
}