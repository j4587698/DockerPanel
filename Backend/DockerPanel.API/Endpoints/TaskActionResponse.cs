namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// 仅含操作结果的通用响应。
    /// </summary>
    public sealed class TaskActionResponse
    {
        public string? Id { get; set; }

        public string Message { get; set; } = string.Empty;
    }
}