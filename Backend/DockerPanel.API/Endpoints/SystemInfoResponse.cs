namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// GET /api/info 响应（具名类型，替代原内联匿名对象）。
    /// </summary>
    public sealed class SystemInfoResponse
    {
        public string Application { get; set; } = "DockerPanel API";

        public string Version { get; set; } = string.Empty;

        public string Environment { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; }

        public SystemInfoConfiguration Configuration { get; set; } = new();
    }

    public sealed class SystemInfoConfiguration
    {
        public string? Logging { get; set; }

        public string? AllowedHosts { get; set; }
    }
}