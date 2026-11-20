namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// GET /api/system/info 响应（替代原内联匿名对象）。
    /// </summary>
    public sealed class SystemOsInfoResponse
    {
        public SystemOsInfo System { get; set; } = new();
        public SystemRuntimeInfo Runtime { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public sealed class SystemOsInfo
    {
        public string OS { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
        public int ProcessorCount { get; set; }
        public string WorkingDirectory { get; set; } = string.Empty;
        public string FrameworkVersion { get; set; } = string.Empty;
    }

    public sealed class SystemRuntimeInfo
    {
        public string Version { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
    }

    /// <summary>
    /// GET /api/system/docker-stats 响应（替代原内联匿名对象，多形态：Disconnected/Error/Running）。
    /// </summary>
    public sealed class DockerStatsResponse
    {
        public string? Status { get; set; }
        public string? Message { get; set; }
        public DockerVersionInfo? Docker { get; set; }
        public DockerContainerCountInfo? Containers { get; set; }
        public DockerImageInfo? Images { get; set; }
        public DockerResourceInfo? Resources { get; set; }
        public DockerNetworkRateInfo? Network { get; set; }
        public DateTime? Timestamp { get; set; }
    }

    public sealed class DockerVersionInfo
    {
        public string? Version { get; set; }
        public string? ApiVersion { get; set; }
        public string? Status { get; set; }
        public string? Os { get; set; }
        public string? Arch { get; set; }
        public string? KernelVersion { get; set; }
        public long? NCPU { get; set; }
    }

    public sealed class DockerContainerCountInfo
    {
        public long? Running { get; set; }
        public long? Stopped { get; set; }
        public long? Total { get; set; }
    }

    public sealed class DockerImageInfo
    {
        public int? Count { get; set; }
        public long? TotalSize { get; set; }
        public string? TotalSizeFormatted { get; set; }
    }

    public sealed class DockerResourceInfo
    {
        public double? CpuUsagePercent { get; set; }
        public long? MemoryUsed { get; set; }
        public long? MemoryLimit { get; set; }
        public double? MemoryPercent { get; set; }
        public string? MemoryUsedFormatted { get; set; }
        public string? MemoryLimitFormatted { get; set; }
    }

    public sealed class DockerNetworkRateInfo
    {
        public double? RxBytesPerSec { get; set; }
        public double? TxBytesPerSec { get; set; }
        public string? RxSpeedFormatted { get; set; }
        public string? TxSpeedFormatted { get; set; }
    }
}