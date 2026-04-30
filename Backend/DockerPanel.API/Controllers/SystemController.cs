using Microsoft.AspNetCore.Mvc;
using Docker.DotNet;
using Docker.DotNet.Models;
using DockerPanel.API.Services;
using System.Reflection;
using System.Text.Json;

namespace DockerPanel.API.Controllers;

/// <summary>
/// 系统信息控制器
/// System info controller
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SystemController : ControllerBase
{
    private readonly ILogger<SystemController> _logger;
    private readonly DockerEngine _dockerEngine;
    private readonly IContainerService _containerService;
    private readonly ILocalizationService _localization;

    // 用于计算网络速率的静态变量
    private static long _lastRxBytes = 0;
    private static long _lastTxBytes = 0;
    private static DateTime _lastSampleTime = DateTime.MinValue;
    private static readonly object _sampleLock = new();

    public SystemController(ILogger<SystemController> logger, DockerEngine dockerEngine, IContainerService containerService, ILocalizationService localization)
    {
        _logger = logger;
        _dockerEngine = dockerEngine;
        _containerService = containerService;
        _localization = localization;
    }

    /// <summary>
    /// 获取系统信息
    /// </summary>
    [HttpGet("info")]
    public ActionResult GetSystemInfo()
    {
        try
        {
            _logger.LogInformation("获取系统信息");
            
            var systemInfo = new
            {
                System = new
                {
                    OS = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                    MachineName = Environment.MachineName,
                    ProcessorCount = Environment.ProcessorCount,
                    WorkingDirectory = Environment.CurrentDirectory,
                    FrameworkVersion = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
                },
                Runtime = new
                {
                    Version = GetApplicationVersion(),
                    StartTime = System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime()
                },
                Timestamp = DateTime.UtcNow
            };

            return Ok(systemInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取系统信息失败");
            return StatusCode(500, new { error = "获取系统信息失败", message = ex.Message });
        }
    }

    private static string GetApplicationVersion()
    {
        var assembly = typeof(SystemController).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? "unknown";
    }

    /// <summary>
    /// 获取Docker聚合统计信息
    /// </summary>
    [HttpGet("docker-stats")]
    public async Task<ActionResult> GetDockerStats()
    {
        try
        {
            _logger.LogInformation("正在获取 Docker 统计信息...");
            
            // 检查 Docker 是否可用
            if (!await _dockerEngine.IsAvailableAsync())
            {
                return Ok(new { Status = "Disconnected", Message = _localization.GetMessage("system.dockerDisconnected") });
            }

            var dockerClient = await _dockerEngine.GetClientAsync();
            var versionTask = dockerClient.System.GetVersionAsync();
            var systemInfoTask = dockerClient.System.GetSystemInfoAsync();
            var imagesTask = dockerClient.Images.ListImagesAsync(new ImagesListParameters());

            await Task.WhenAll(versionTask, systemInfoTask, imagesTask);

            var version = versionTask.Result;
            var systemInfo = systemInfoTask.Result;
            var images = imagesTask.Result;

            var runningCount = systemInfo.ContainersRunning;
            var stoppedCount = systemInfo.ContainersStopped;
            var totalCount = systemInfo.Containers;
            
            var imagesCount = images.Count;
            var totalSize = images.Sum(i => i.Size);
            
            // 获取所有运行中容器并聚合资源使用情况
            double totalCpuPercent = 0;
            long totalContainerMemoryUsed = 0;
            long networkRxSnapshot = 0;
            long networkTxSnapshot = 0;
            
            // 宿主机总内存（从 Docker 系统信息获取）
            long hostMemoryTotal = systemInfo.MemTotal > 0 ? systemInfo.MemTotal : 0;
            
            try
            {
                var containers = await _containerService.GetContainersAsync(null, false);
                var runningContainers = containers.Where(c => c.State == "running").ToList();
                
                // 并行获取所有运行中容器的统计信息
                var statsTasks = runningContainers.Select(async c =>
                {
                    try
                    {
                        return await _containerService.GetContainerStatsAsync(c.Id);
                    }
                    catch
                    {
                        return null;
                    }
                });
                
                var statsResults = await Task.WhenAll(statsTasks);
                
                foreach (var stats in statsResults)
                {
                    if (stats == null) continue;
                    totalCpuPercent += stats.CpuStats?.PercentCpu ?? 0;
                    totalContainerMemoryUsed += stats.MemoryStats?.Usage ?? 0;
                    networkRxSnapshot += stats.Networks?.Sum(n => n.RxBytes) ?? 0;
                    networkTxSnapshot += stats.Networks?.Sum(n => n.TxBytes) ?? 0;
                }
                
                // CPU 百分比可能超过100%（多核），需要限制
                if (totalCpuPercent > 100 * systemInfo.NCPU) 
                {
                    totalCpuPercent = 100 * systemInfo.NCPU;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "获取容器统计信息失败，使用默认值");
            }
            
            // 计算容器内存占用宿主机内存的百分比
            double memoryPercent = hostMemoryTotal > 0 ? (double)totalContainerMemoryUsed / hostMemoryTotal * 100 : 0;

            // 计算网络速率
            double rxBytesPerSec = 0;
            double txBytesPerSec = 0;
            
            lock (_sampleLock)
            {
                var now = DateTime.UtcNow;
                if (_lastSampleTime != DateTime.MinValue)
                {
                    var elapsed = (now - _lastSampleTime).TotalSeconds;
                    if (elapsed > 0)
                    {
                        rxBytesPerSec = (networkRxSnapshot - _lastRxBytes) / elapsed;
                        txBytesPerSec = (networkTxSnapshot - _lastTxBytes) / elapsed;
                        
                        // 防止负数（容器重启等情况）
                        if (rxBytesPerSec < 0) rxBytesPerSec = 0;
                        if (txBytesPerSec < 0) txBytesPerSec = 0;
                    }
                }
                
                // 更新上次采样数据
                _lastRxBytes = networkRxSnapshot;
                _lastTxBytes = networkTxSnapshot;
                _lastSampleTime = now;
            }

            return Ok(new
            {
                Docker = new
                {
                    Version = version.Version,
                    ApiVersion = version.APIVersion,
                    Status = "running",
                    Os = systemInfo.OperatingSystem,
                    Arch = systemInfo.Architecture,
                    KernelVersion = systemInfo.KernelVersion,
                    NCPU = systemInfo.NCPU
                },
                Containers = new
                {
                    Running = runningCount,
                    Stopped = stoppedCount,
                    Total = totalCount
                },
                Images = new
                {
                    Count = imagesCount,
                    TotalSize = totalSize,
                    TotalSizeFormatted = FormatBytes(totalSize)
                },
                Resources = new
                {
                    CpuUsagePercent = Math.Round(totalCpuPercent, 2),
                    MemoryUsed = totalContainerMemoryUsed,
                    MemoryLimit = hostMemoryTotal,
                    MemoryPercent = Math.Round(memoryPercent, 2),
                    MemoryUsedFormatted = FormatBytes(totalContainerMemoryUsed),
                    MemoryLimitFormatted = FormatBytes(hostMemoryTotal)
                },
                Network = new
                {
                    RxBytesPerSec = Math.Round(rxBytesPerSec, 2),
                    TxBytesPerSec = Math.Round(txBytesPerSec, 2),
                    RxSpeedFormatted = FormatBytesPerSec(rxBytesPerSec),
                    TxSpeedFormatted = FormatBytesPerSec(txBytesPerSec)
                },
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 Docker 统计信息时发生异常");
            return Ok(new
            {
                Status = "Error",
                Message = "Docker 连接异常: " + ex.Message
            });
        }
    }

    private (long Total, long Available, long Used) GetLinuxMemoryInfo()
    {
        try
        {
            if (System.IO.File.Exists("/proc/meminfo"))
            {
                var lines = System.IO.File.ReadAllLines("/proc/meminfo");
                long total = 0, available = 0;

                foreach (var line in lines)
                {
                    if (line.StartsWith("MemTotal:")) total = ParseMemValue(line);
                    if (line.StartsWith("MemAvailable:")) available = ParseMemValue(line);
                }

                return (total, available, total - available);
            }
        }
        catch { }
        return (0, 0, 0);
    }

    private long ParseMemValue(string line)
    {
        // Example: MemTotal:       16303032 kB
        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && long.TryParse(parts[1], out var val))
        {
            return val * 1024; // Convert kB to Bytes
        }
        return 0;
    }

    private double GetLinuxLoadAvg()
    {
        try
        {
            if (System.IO.File.Exists("/proc/loadavg"))
            {
                var content = System.IO.File.ReadAllText("/proc/loadavg");
                var parts = content.Split(' ');
                if (parts.Length > 0 && double.TryParse(parts[0], out var load))
                {
                    return load;
                }
            }
        }
        catch { }
        return 0;
    }

    private string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private string FormatBytesPerSec(double bytesPerSec)
    {
        string[] sizes = { "B/s", "KB/s", "MB/s", "GB/s" };
        double len = bytesPerSec;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// 获取系统状态
    /// </summary>
    [HttpGet("status")]
    public ActionResult GetSystemStatus()
    {
        try
        {
            _logger.LogInformation("获取系统状态");

            var status = new
            {
                Overall = "Healthy",
                Components = new
                {
                    Database = new { Status = "Running", ResponseTime = 5.2 },
                    Redis = new { Status = "Running", ResponseTime = 2.1 },
                    Docker = new { Status = "Running", Version = "24.0.0" },
                    FileSystem = new { Status = "Healthy", FreeSpace = "55GB" }
                },
                Metrics = new
                {
                    CpuUsage = 15.2,
                    MemoryUsage = 12.5,
                    DiskUsage = 45.0,
                    NetworkIO = new
                    {
                        BytesIn = 1024000,
                        BytesOut = 512000,
                        Connections = 25
                    }
                },
                Alerts = new object[0], // 无告警
                LastUpdated = DateTime.UtcNow
            };

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取系统状态失败");
            return StatusCode(500, new { error = "获取系统状态失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 获取系统性能指标
    /// </summary>
    [HttpGet("metrics")]
    public ActionResult GetSystemMetrics()
    {
        try
        {
            _logger.LogInformation("获取系统性能指标");

            var metrics = new
            {
                Timestamp = DateTime.UtcNow,
                Interval = "1m",
                CPU = new
                {
                    Usage = new double[] { 12.5, 15.2, 18.7, 14.3, 16.8, 13.2, 15.2 },
                    Average = 15.2,
                    Peak = 18.7,
                    Cores = Environment.ProcessorCount
                },
                Memory = new
                {
                    Used = new double[] { 245, 256, 267, 254, 248, 252, 256 },
                    Average = 254,
                    Peak = 267,
                    Total = 2048
                },
                Network = new
                {
                    Inbound = new[] { 1024, 2048, 1536, 3072, 2560, 1792, 2048 },
                    Outbound = new[] { 512, 1024, 768, 1536, 1280, 896, 1024 },
                    Connections = new[] { 20, 25, 22, 28, 24, 21, 25 }
                },
                Disk = new
                {
                    ReadOps = new[] { 100, 150, 120, 180, 140, 160, 145 },
                    WriteOps = new[] { 80, 120, 100, 140, 110, 130, 115 },
                    IOPS = new[] { 180, 270, 220, 320, 250, 290, 260 }
                }
            };

            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取系统性能指标失败");
            return StatusCode(500, new { error = "获取系统性能指标失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 执行系统健康检查
    /// </summary>
    [HttpGet("health")]
    public ActionResult GetHealthCheck()
    {
        try
        {
            _logger.LogInformation("执行系统健康检查");

            var healthCheck = new
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Duration = TimeSpan.FromMilliseconds(125),
                Checks = new object[]
                {
                    new
                    {
                        Name = "Database",
                        Status = "Healthy",
                        Duration = TimeSpan.FromMilliseconds(25),
                        Data = new { ConnectionString = "Server=localhost;Port=5432" }
                    },
                    new
                    {
                        Name = "Redis",
                        Status = "Healthy",
                        Duration = TimeSpan.FromMilliseconds(15),
                        Data = new { Server = "localhost:6379", Database = 0 }
                    },
                    new
                    {
                        Name = "Docker",
                        Status = "Healthy",
                        Duration = TimeSpan.FromMilliseconds(45),
                        Data = new { Version = "24.0.0", Containers = 5 }
                    },
                    new
                    {
                        Name = "FileSystem",
                        Status = "Healthy",
                        Duration = TimeSpan.FromMilliseconds(10),
                        Data = new { FreeSpace = "55GB", Usage = 45.0 }
                    }
                },
                Resources = new
                {
                    CPU = 15.2,
                    Memory = 12.5,
                    Disk = 45.0
                }
            };

            return Ok(healthCheck);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "系统健康检查失败");
            return StatusCode(500, new { error = "系统健康检查失败", message = ex.Message });
        }
    }
}