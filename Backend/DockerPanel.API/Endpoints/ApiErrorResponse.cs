namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// Minimal API 统一错误响应体（替代 MVC 匿名对象 { error, message }）。
    /// </summary>
    public sealed class ApiErrorResponse
    {
        public string? Error { get; set; }

        public string? Message { get; set; }
    }
}