namespace DockerPanel.API.Serialization;

/// <summary>
/// 实时日志消息（SignalR "logs" 载荷）
/// </summary>
public class LogStreamMessage
{
    public string ContainerId { get; set; } = "";
    public string Message { get; set; } = "";
    public string Timestamp { get; set; } = "";
    public string Level { get; set; } = "info";
}
