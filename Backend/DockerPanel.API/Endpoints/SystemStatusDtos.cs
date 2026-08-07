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

    /// <summary>
    /// GET /api/system/status 响应（替代原内联匿名对象）。
    /// </summary>
    public sealed class SystemStatusResponse
    {
        public string Overall { get; set; } = "Healthy";
        public SystemComponentStatuses Components { get; set; } = new();
        public SystemStatusMetrics Metrics { get; set; } = new();
        public object[] Alerts { get; set; } = Array.Empty<object>();
        public DateTime LastUpdated { get; set; }
    }

    public sealed class SystemComponentStatuses
    {
        public SystemComponentStatus Database { get; set; } = new();
        public SystemComponentStatus Redis { get; set; } = new();
        public SystemComponentStatus Docker { get; set; } = new();
        public SystemComponentStatus FileSystem { get; set; } = new();
    }

    public sealed class SystemComponentStatus
    {
        public string Status { get; set; } = string.Empty;
        public double? ResponseTime { get; set; }
        public string? Version { get; set; }
        public string? FreeSpace { get; set; }
    }

    public sealed class SystemStatusMetrics
    {
        public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
        public double DiskUsage { get; set; }
        public SystemNetworkIoInfo NetworkIO { get; set; } = new();
    }

    public sealed class SystemNetworkIoInfo
    {
        public long BytesIn { get; set; }
        public long BytesOut { get; set; }
        public int Connections { get; set; }
    }

    /// <summary>
    /// GET /api/system/metrics 响应（替代原内联匿名对象）。
    /// </summary>
    public sealed class SystemMetricsResponse
    {
        public DateTime Timestamp { get; set; }
        public string Interval { get; set; } = "1m";
        public SystemMetricsCpu CPU { get; set; } = new();
        public SystemMetricsMemory Memory { get; set; } = new();
        public SystemMetricsNetwork Network { get; set; } = new();
        public SystemMetricsDisk Disk { get; set; } = new();
    }

    public sealed class SystemMetricsCpu
    {
        public double[] Usage { get; set; } = Array.Empty<double>();
        public double Average { get; set; }
        public double Peak { get; set; }
        public int Cores { get; set; }
    }

    public sealed class SystemMetricsMemory
    {
        public double[] Used { get; set; } = Array.Empty<double>();
        public double Average { get; set; }
        public double Peak { get; set; }
        public double Total { get; set; }
    }

    public sealed class SystemMetricsNetwork
    {
        public long[] Inbound { get; set; } = Array.Empty<long>();
        public long[] Outbound { get; set; } = Array.Empty<long>();
        public int[] Connections { get; set; } = Array.Empty<int>();
    }

    public sealed class SystemMetricsDisk
    {
        public long[] ReadOps { get; set; } = Array.Empty<long>();
        public long[] WriteOps { get; set; } = Array.Empty<long>();
        public long[] IOPS { get; set; } = Array.Empty<long>();
    }

    /// <summary>
    /// GET /api/system/health 响应（替代原内联匿名对象）。
    /// </summary>
    public sealed class SystemHealthCheckResponse
    {
        public string Status { get; set; } = "Healthy";
        public DateTime Timestamp { get; set; }
        public TimeSpan Duration { get; set; }
        public SystemHealthCheckItem[] Checks { get; set; } = Array.Empty<SystemHealthCheckItem>();
        public SystemHealthResources Resources { get; set; } = new();
    }

    public sealed class SystemHealthCheckItem
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public Dictionary<string, object>? Data { get; set; }
    }

    public sealed class SystemHealthResources
    {
        public double CPU { get; set; }
        public double Memory { get; set; }
        public double Disk { get; set; }
    }
}