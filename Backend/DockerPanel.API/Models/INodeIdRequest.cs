namespace DockerPanel.API.Models;

/// <summary>
/// 标记带节点 ID 的请求模型，避免 AOT 下通过反射读取 NodeId。
/// 供 OperationAuditFilter 在审计时提取目标节点。
/// </summary>
public interface INodeIdRequest
{
    string? NodeId { get; }
}