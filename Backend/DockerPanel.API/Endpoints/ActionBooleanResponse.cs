namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// 布尔操作结果响应（替代 { success, message } 匿名对象）。
    /// </summary>
    public sealed class ActionBooleanResponse
    {
        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;
    }
}