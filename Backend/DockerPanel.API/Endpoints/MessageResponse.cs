namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// 简单操作消息响应（替代 { message } 匿名对象）。
    /// </summary>
    public sealed class MessageResponse
    {
        public string Message { get; set; } = string.Empty;
    }
}