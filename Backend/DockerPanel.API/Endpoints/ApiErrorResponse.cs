namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// Minimal API 统一错误响应体（替代 MVC/全局中间件匿名对象）。
    /// </summary>
    public sealed class ApiErrorResponse
    {
        public string? Code { get; set; }

        public string? Error { get; set; }

        public string? Message { get; set; }

        public DateTime? Timestamp { get; set; }

        public string? Path { get; set; }
    }
}