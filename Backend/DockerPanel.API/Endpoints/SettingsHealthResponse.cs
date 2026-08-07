namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// 系统健康检查响应（具名类型，替代原控制器的匿名对象，保证源生成上下文可解析）。
    /// </summary>
    public sealed class SettingsHealthResponse
    {
        public string Status { get; set; } = "Healthy";

        public DateTime Timestamp { get; set; }

        public string? Version { get; set; }

        public TimeSpan Uptime { get; set; }

        public string? Message { get; set; }

        public SettingsHealthMemory Memory { get; set; } = new();

        public SettingsHealthCpu Cpu { get; set; } = new();

        public IReadOnlyList<SettingsHealthService> Services { get; set; } = Array.Empty<SettingsHealthService>();
    }

    public sealed class SettingsHealthMemory
    {
        public long UsedBytes { get; set; }

        public long HeapSizeBytes { get; set; }

        public long HighMemoryLoadThresholdBytes { get; set; }

        public long MemoryLoadBytes { get; set; }
    }

    public sealed class SettingsHealthCpu
    {
        public int Cores { get; set; }

        public TimeSpan TotalProcessorTime { get; set; }
    }

    public sealed class SettingsHealthService
    {
        public string Name { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;
    }
}