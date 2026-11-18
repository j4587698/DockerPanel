using System.Globalization;
using System.Text;
using DockerPanel.API.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DockerPanel.API.Services;

/// <summary>
/// 宿主机指标采集器。
///
/// DockerPanel 运行在容器中时，容器自己的 /proc 并不等于宿主机：
/// 网络、磁盘容量、挂载点、进程视图都受命名空间限制。因此只有检测到
/// /host/proc（或显式 HOST_PROC）时才认为“宿主机监控已启用”。
/// 未挂载时返回 Available=false，由前端提示用户按需开启，而不是展示错误数据。
///
/// 支持的挂载（均为只读）：
///   /proc:/host/proc:ro
///   /sys:/host/sys:ro
///   /etc/os-release:/host/etc/os-release:ro
///   /:/host/root:ro        （可选，用于磁盘容量）
/// </summary>
public sealed class HostMetricsCollector
{
    private static readonly HashSet<string> PseudoFileSystems = new(StringComparer.OrdinalIgnoreCase)
    {
        "proc", "sysfs", "tmpfs", "devtmpfs", "devpts", "cgroup", "cgroup2", "overlay",
        "squashfs", "ramfs", "mqueue", "securityfs", "debugfs", "tracefs", "pstore", "bpf",
        "autofs", "fusectl", "configfs", "hugetlbfs", "nfsd", "rpc_pipefs", "nsfs", "efivarfs",
        "binfmt_misc", "fuse.gvfsd-fuse", "fuse.portal"
    };

    private readonly ILogger<HostMetricsCollector> _logger;
    private readonly object _sync = new();

    private readonly string? _procPath;
    private readonly string? _sysPath;
    private readonly string? _rootPath;
    private readonly string? _osReleasePath;
    private readonly string? _networkDevPath;
    private readonly string? _diskStatsPath;
    private readonly bool _containerized;
    private readonly bool _available;
    private readonly string? _unavailableReason;

    private CpuSample? _lastCpu;
    private NetworkSample? _lastNetwork;
    private DiskIoSample? _lastDiskIo;
    private FileSystemCache? _fileSystemCache;

    public HostMetricsCollector(ILogger<HostMetricsCollector> logger, IConfiguration configuration)
    {
        _logger = logger;
        _containerized = DetectContainerized();

        var explicitProc = FirstNonEmpty(
            configuration["HOST_PROC"],
            Environment.GetEnvironmentVariable("HOST_PROC"));
        var explicitSys = FirstNonEmpty(
            configuration["HOST_SYS"],
            Environment.GetEnvironmentVariable("HOST_SYS"));
        var explicitRoot = FirstNonEmpty(
            configuration["HOST_ROOT"],
            Environment.GetEnvironmentVariable("HOST_ROOT"));
        var explicitOsRelease = FirstNonEmpty(
            configuration["HOST_OS_RELEASE"],
            Environment.GetEnvironmentVariable("HOST_OS_RELEASE"));

        _procPath = ResolveProcPath(explicitProc, _containerized, out _unavailableReason);
        _available = _procPath != null;

        // 从 proc 的父目录推导 sys：/host/proc -> /host/sys，/proc -> /sys
        var procParent = _procPath == null ? null : Path.GetDirectoryName(_procPath);
        _sysPath = ResolveDirectory(explicitSys)
            ?? (procParent == null ? null : ResolveDirectory(Path.Combine(procParent, "sys")));

        _rootPath = ResolveDirectory(explicitRoot)
            ?? (procParent == null ? null : ResolveDirectory(Path.Combine(procParent, "root")));

        _osReleasePath = ResolveFile(explicitOsRelease)
            ?? (_rootPath == null ? null : ResolveFile(Path.Combine(_rootPath, "etc", "os-release")))
            ?? (procParent == null ? null : ResolveFile(Path.Combine(procParent, "etc", "os-release")));

        if (_procPath != null)
        {
            // /proc/net/dev 在容器里指向读取进程自己的网络命名空间（self/net），
            // 只有 /proc/1/net/dev 在 bind mount 宿主机 /proc 时才代表宿主机网络。
            var pid1NetDev = Path.Combine(_procPath, "1", "net", "dev");
            if (File.Exists(pid1NetDev))
            {
                _networkDevPath = pid1NetDev;
            }
            else if (!_containerized)
            {
                var localNetDev = Path.Combine(_procPath, "net", "dev");
                if (File.Exists(localNetDev)) _networkDevPath = localNetDev;
            }

            var diskStats = Path.Combine(_procPath, "diskstats");
            if (File.Exists(diskStats)) _diskStatsPath = diskStats;
        }

        if (_available)
        {
            _logger.LogInformation(
                "宿主机监控已启用: proc={ProcPath}, sys={SysPath}, root={RootPath}, network={Network}",
                _procPath,
                _sysPath ?? "(none)",
                _rootPath ?? "(none)",
                _networkDevPath ?? "(unavailable)");
        }
        else
        {
            _logger.LogInformation("宿主机监控未启用: {Reason}", _unavailableReason);
        }
    }

    public bool IsAvailable => _available;

    public string? UnavailableReason => _unavailableReason;

    /// <summary>
    /// 采集一次宿主机指标。CPU/网络/磁盘 I/O 速率依赖两次采样，首次调用时速率值为 0。
    /// </summary>
    public HostMetricsSnapshot Collect()
    {
        if (!_available || _procPath == null)
        {
            return new HostMetricsSnapshot
            {
                Available = false,
                UnavailableReason = _unavailableReason,
                Containerized = _containerized,
                Timestamp = DateTime.UtcNow
            };
        }

        var now = DateTime.UtcNow;
        var snapshot = new HostMetricsSnapshot
        {
            Available = true,
            Containerized = _containerized,
            Timestamp = now
        };

        try
        {
            CollectHostInfo(snapshot);
            CollectCpu(snapshot);
            CollectMemory(snapshot);
            CollectLoad(snapshot);
            CollectUptime(snapshot);
            CollectNetwork(snapshot, now);
            CollectDiskIo(snapshot, now);
            CollectFileSystems(snapshot, now);

            snapshot.Cpu.UsagePercent = Round(snapshot.Cpu.UsagePercent);
            snapshot.Cpu.UserPercent = Round(snapshot.Cpu.UserPercent);
            snapshot.Cpu.SystemPercent = Round(snapshot.Cpu.SystemPercent);
            snapshot.Cpu.IowaitPercent = Round(snapshot.Cpu.IowaitPercent);
            snapshot.Memory.UsagePercent = Round(snapshot.Memory.UsagePercent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取宿主机指标失败");
            snapshot.Available = false;
            snapshot.UnavailableReason = $"读取宿主机指标失败: {ex.Message}";
        }

        return snapshot;
    }

    private void CollectHostInfo(HostMetricsSnapshot snapshot)
    {
        snapshot.HostName = ReadFirstLine(Path.Combine(_procPath!, "sys", "kernel", "hostname"))
            ?? Environment.MachineName;
        snapshot.KernelVersion = ReadFirstLine(Path.Combine(_procPath!, "sys", "kernel", "osrelease"));

        if (_osReleasePath == null) return;

        try
        {
            var name = default(string);
            var prettyName = default(string);
            var versionId = default(string);
            var version = default(string);

            foreach (var raw in File.ReadLines(_osReleasePath))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;

                var index = line.IndexOf('=');
                if (index <= 0) continue;

                var key = line[..index];
                var value = Unquote(line[(index + 1)..]);
                switch (key)
                {
                    case "NAME": name = value; break;
                    case "PRETTY_NAME": prettyName = value; break;
                    case "VERSION_ID": versionId = value; break;
                    case "VERSION": version = value; break;
                }
            }

            snapshot.OsName = FirstNonEmpty(prettyName, name);
            snapshot.OsVersion = FirstNonEmpty(versionId, version);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "读取 {Path} 失败", _osReleasePath);
        }
    }

    private void CollectCpu(HostMetricsSnapshot snapshot)
    {
        var statPath = Path.Combine(_procPath!, "stat");
        var lines = File.ReadAllLines(statPath);

        var coreCount = 0;
        string? cpuLine = null;
        foreach (var line in lines)
        {
            if (line.StartsWith("cpu", StringComparison.Ordinal))
            {
                if (line.Length > 3 && char.IsDigit(line[3]))
                {
                    coreCount++;
                }
                else if (cpuLine == null && line.Length > 3 && line[3] == ' ')
                {
                    cpuLine = line;
                }
            }
        }

        snapshot.Cpu.CoreCount = Math.Max(coreCount, 1);
        if (cpuLine == null) return;

        var parts = cpuLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5) return;

        var user = ParseULong(parts, 1);
        var nice = ParseULong(parts, 2);
        var system = ParseULong(parts, 3);
        var idle = ParseULong(parts, 4);
        var iowait = ParseULong(parts, 5);
        var irq = ParseULong(parts, 6);
        var softirq = ParseULong(parts, 7);
        var steal = ParseULong(parts, 8);

        var total = user + nice + system + idle + iowait + irq + softirq + steal;
        var idleTotal = idle + iowait;

        lock (_sync)
        {
            if (_lastCpu != null)
            {
                var deltaTotal = total >= _lastCpu.Total ? total - _lastCpu.Total : 0;
                if (deltaTotal > 0)
                {
                    var deltaIdle = idleTotal >= _lastCpu.IdleTotal ? idleTotal - _lastCpu.IdleTotal : 0;
                    snapshot.Cpu.UsagePercent = ClampPercent((deltaTotal - deltaIdle) * 100.0 / deltaTotal);
                    snapshot.Cpu.UserPercent = DeltaPercent(user + nice, _lastCpu.UserNice, deltaTotal);
                    snapshot.Cpu.SystemPercent = DeltaPercent(system + irq + softirq, _lastCpu.SystemTotal, deltaTotal);
                    snapshot.Cpu.IowaitPercent = DeltaPercent(iowait, _lastCpu.Iowait, deltaTotal);
                }
            }

            _lastCpu = new CpuSample
            {
                Total = total,
                IdleTotal = idleTotal,
                UserNice = user + nice,
                SystemTotal = system + irq + softirq,
                Iowait = iowait
            };
        }
    }

    private void CollectMemory(HostMetricsSnapshot snapshot)
    {
        var memInfoPath = Path.Combine(_procPath!, "meminfo");
        if (!File.Exists(memInfoPath)) return;

        long memTotal = 0, memAvailable = 0, memFree = 0, buffers = 0, cached = 0;
        long swapTotal = 0, swapFree = 0;

        foreach (var line in File.ReadLines(memInfoPath))
        {
            var index = line.IndexOf(':');
            if (index <= 0) continue;

            var key = line[..index];
            var valuePart = line[(index + 1)..].Trim();
            var spaceIndex = valuePart.IndexOf(' ');
            if (spaceIndex > 0) valuePart = valuePart[..spaceIndex];

            if (!long.TryParse(valuePart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                continue;

            switch (key)
            {
                case "MemTotal": memTotal = value * 1024; break;
                case "MemAvailable": memAvailable = value * 1024; break;
                case "MemFree": memFree = value * 1024; break;
                case "Buffers": buffers = value * 1024; break;
                case "Cached": cached = value * 1024; break;
                case "SwapTotal": swapTotal = value * 1024; break;
                case "SwapFree": swapFree = value * 1024; break;
            }
        }

        if (memAvailable <= 0) memAvailable = memFree + buffers + cached;

        var used = Math.Max(0, memTotal - memAvailable);
        snapshot.Memory.TotalBytes = memTotal;
        snapshot.Memory.UsedBytes = used;
        snapshot.Memory.AvailableBytes = memAvailable;
        snapshot.Memory.SwapTotalBytes = swapTotal;
        snapshot.Memory.SwapUsedBytes = Math.Max(0, swapTotal - swapFree);
        snapshot.Memory.UsagePercent = memTotal > 0 ? ClampPercent(used * 100.0 / memTotal) : 0;
        snapshot.Memory.UsedFormatted = FormatBytes(used);
        snapshot.Memory.TotalFormatted = FormatBytes(memTotal);
        snapshot.Memory.AvailableFormatted = FormatBytes(memAvailable);
    }

    private void CollectLoad(HostMetricsSnapshot snapshot)
    {
        var loadPath = Path.Combine(_procPath!, "loadavg");
        if (!File.Exists(loadPath)) return;

        var parts = File.ReadAllText(loadPath).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3)
        {
            snapshot.Load.Load1 = ParseDouble(parts[0]);
            snapshot.Load.Load5 = ParseDouble(parts[1]);
            snapshot.Load.Load15 = ParseDouble(parts[2]);
        }
    }

    private void CollectUptime(HostMetricsSnapshot snapshot)
    {
        var uptimePath = Path.Combine(_procPath!, "uptime");
        if (!File.Exists(uptimePath)) return;

        var text = File.ReadAllText(uptimePath);
        var index = text.IndexOf(' ');
        if (index > 0) text = text[..index];
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            snapshot.UptimeSeconds = (long)seconds;
        }
    }

    private void CollectNetwork(HostMetricsSnapshot snapshot, DateTime now)
    {
        if (_networkDevPath == null)
        {
            snapshot.Network.Available = false;
            snapshot.Network.UnavailableReason = _containerized
                ? "无法读取宿主机网络命名空间（需要只读挂载 /proc 到 /host/proc，并允许读取 /host/proc/1/net/dev）"
                : "无法读取 /proc/net/dev";
            return;
        }

        try
        {
            long rxBytes = 0, txBytes = 0, rxPackets = 0, txPackets = 0;

            foreach (var line in File.ReadLines(_networkDevPath))
            {
                var colon = line.IndexOf(':');
                if (colon <= 0) continue;

                var name = line[..colon].Trim();
                if (name.Equals("lo", StringComparison.Ordinal)) continue;

                var parts = line[(colon + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 16) continue;

                rxBytes += ParseLong(parts[0]);
                rxPackets += ParseLong(parts[1]);
                txBytes += ParseLong(parts[8]);
                txPackets += ParseLong(parts[9]);
            }

            snapshot.Network.Available = true;
            snapshot.Network.TotalRxBytes = rxBytes;
            snapshot.Network.TotalTxBytes = txBytes;
            snapshot.Network.TotalRxPackets = rxPackets;
            snapshot.Network.TotalTxPackets = txPackets;

            lock (_sync)
            {
                if (_lastNetwork != null)
                {
                    var elapsed = (now - _lastNetwork.Timestamp).TotalSeconds;
                    if (elapsed > 0)
                    {
                        snapshot.Network.RxBytesPerSec = (long)Math.Max(0, (rxBytes - _lastNetwork.RxBytes) / elapsed);
                        snapshot.Network.TxBytesPerSec = (long)Math.Max(0, (txBytes - _lastNetwork.TxBytes) / elapsed);
                    }
                }

                _lastNetwork = new NetworkSample
                {
                    Timestamp = now,
                    RxBytes = rxBytes,
                    TxBytes = txBytes
                };
            }

            snapshot.Network.RxSpeedFormatted = FormatBytesPerSec(snapshot.Network.RxBytesPerSec);
            snapshot.Network.TxSpeedFormatted = FormatBytesPerSec(snapshot.Network.TxBytesPerSec);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "读取宿主机网络指标失败");
            snapshot.Network.Available = false;
            snapshot.Network.UnavailableReason = $"读取宿主机网络失败: {ex.Message}";
        }
    }

    private void CollectDiskIo(HostMetricsSnapshot snapshot, DateTime now)
    {
        if (_diskStatsPath == null)
        {
            snapshot.Disk.Available = false;
            snapshot.Disk.UnavailableReason = "无法读取宿主机 /proc/diskstats";
            return;
        }

        try
        {
            var wholeDevices = GetWholeDeviceNames();
            long readSectors = 0, writeSectors = 0;

            foreach (var line in File.ReadLines(_diskStatsPath))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 10) continue;

                var device = parts[2];
                if (wholeDevices != null)
                {
                    if (!wholeDevices.Contains(device)) continue;
                }
                else if (!IsLikelyWholeDevice(device))
                {
                    continue;
                }

                readSectors += ParseLong(parts[5]);
                writeSectors += ParseLong(parts[9]);
            }

            lock (_sync)
            {
                if (_lastDiskIo != null)
                {
                    var elapsed = (now - _lastDiskIo.Timestamp).TotalSeconds;
                    if (elapsed > 0)
                    {
                        // /proc/diskstats 的 sectors 固定按 512 字节计
                        snapshot.Disk.ReadBytesPerSec = (long)Math.Max(0, (readSectors - _lastDiskIo.ReadSectors) * 512.0 / elapsed);
                        snapshot.Disk.WriteBytesPerSec = (long)Math.Max(0, (writeSectors - _lastDiskIo.WriteSectors) * 512.0 / elapsed);
                    }
                }

                _lastDiskIo = new DiskIoSample
                {
                    Timestamp = now,
                    ReadSectors = readSectors,
                    WriteSectors = writeSectors
                };
            }

            snapshot.Disk.Available = true;
            snapshot.Disk.ReadSpeedFormatted = FormatBytesPerSec(snapshot.Disk.ReadBytesPerSec);
            snapshot.Disk.WriteSpeedFormatted = FormatBytesPerSec(snapshot.Disk.WriteBytesPerSec);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "读取宿主机磁盘 I/O 失败");
            snapshot.Disk.Available = false;
            snapshot.Disk.UnavailableReason = $"读取宿主机磁盘 I/O 失败: {ex.Message}";
        }
    }

    private void CollectFileSystems(HostMetricsSnapshot snapshot, DateTime now)
    {
        if (_rootPath == null)
        {
            snapshot.FileSystemAvailable = false;
            snapshot.FileSystemUnavailableReason = "未挂载宿主机根目录（可选：-v /:/host/root:ro）";
            return;
        }

        var mountsPath = Path.Combine(_procPath!, "mounts");
        if (!File.Exists(mountsPath))
        {
            snapshot.FileSystemAvailable = false;
            snapshot.FileSystemUnavailableReason = "无法读取宿主机 /proc/mounts";
            return;
        }

        try
        {
            if (_fileSystemCache == null || now - _fileSystemCache.Timestamp > TimeSpan.FromSeconds(30))
            {
                _fileSystemCache = new FileSystemCache
                {
                    Timestamp = now,
                    Items = ReadFileSystems(mountsPath)
                };
            }

            snapshot.FileSystems = _fileSystemCache.Items
                .Select(item => new HostFileSystemMetrics
                {
                    Device = item.Device,
                    MountPoint = item.MountPoint,
                    FileSystem = item.FileSystem,
                    TotalBytes = item.TotalBytes,
                    UsedBytes = item.UsedBytes,
                    AvailableBytes = item.AvailableBytes,
                    UsagePercent = item.UsagePercent,
                    TotalFormatted = item.TotalFormatted,
                    UsedFormatted = item.UsedFormatted
                })
                .ToList();

            snapshot.FileSystemAvailable = snapshot.FileSystems.Count > 0;
            if (!snapshot.FileSystemAvailable)
            {
                snapshot.FileSystemUnavailableReason = "未在 /host/root 下找到可读取的宿主机文件系统";
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "读取宿主机文件系统容量失败");
            snapshot.FileSystemAvailable = false;
            snapshot.FileSystemUnavailableReason = $"读取宿主机磁盘容量失败: {ex.Message}";
        }
    }

    private List<HostFileSystemMetrics> ReadFileSystems(string mountsPath)
    {
        var result = new List<HostFileSystemMetrics>();
        var seenDevices = new HashSet<string>(StringComparer.Ordinal);

        foreach (var raw in File.ReadLines(mountsPath))
        {
            var parts = raw.Split(' ', 6, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) continue;

            var device = UnescapeMount(parts[0]);
            var mountPoint = UnescapeMount(parts[1]);
            var fileSystem = parts[2];

            if (!device.StartsWith("/dev/", StringComparison.Ordinal)) continue;
            if (device.StartsWith("/dev/loop", StringComparison.Ordinal)) continue;
            if (PseudoFileSystems.Contains(fileSystem)) continue;
            if (mountPoint.StartsWith("/snap", StringComparison.Ordinal)) continue;
            if (!seenDevices.Add(device)) continue;

            var mappedPath = Path.Combine(_rootPath!, mountPoint.TrimStart('/'));
            if (!Directory.Exists(mappedPath)) continue;

            try
            {
                var drive = new DriveInfo(mappedPath);
                if (!drive.IsReady || drive.TotalSize <= 0) continue;

                var total = drive.TotalSize;
                var available = drive.AvailableFreeSpace;
                var used = Math.Max(0, total - available);

                result.Add(new HostFileSystemMetrics
                {
                    Device = device,
                    MountPoint = mountPoint,
                    FileSystem = fileSystem,
                    TotalBytes = total,
                    UsedBytes = used,
                    AvailableBytes = available,
                    UsagePercent = Round(ClampPercent(used * 100.0 / total)),
                    TotalFormatted = FormatBytes(total),
                    UsedFormatted = FormatBytes(used)
                });
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "读取挂载点 {MountPoint} 容量失败", mountPoint);
            }
        }

        return result
            .OrderByDescending(item => item.MountPoint == "/")
            .ThenByDescending(item => item.TotalBytes)
            .Take(12)
            .ToList();
    }

    private HashSet<string>? GetWholeDeviceNames()
    {
        if (_sysPath == null) return null;

        var blockPath = Path.Combine(_sysPath, "block");
        if (!Directory.Exists(blockPath)) return null;

        try
        {
            return Directory.EnumerateDirectories(blockPath)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .Select(name => name!)
                .ToHashSet(StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "读取 /sys/block 失败，回退到设备名过滤");
            return null;
        }
    }

    private static bool IsLikelyWholeDevice(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        if (name.StartsWith("loop", StringComparison.Ordinal) ||
            name.StartsWith("ram", StringComparison.Ordinal) ||
            name.StartsWith("fd", StringComparison.Ordinal) ||
            name.StartsWith("sr", StringComparison.Ordinal))
        {
            return false;
        }

        if (name.StartsWith("dm-", StringComparison.Ordinal) ||
            name.StartsWith("md", StringComparison.Ordinal))
        {
            return true;
        }

        // nvme0n1 是整盘，nvme0n1p1 是分区；mmcblk0 / mmcblk0p1 同理
        if (name.StartsWith("nvme", StringComparison.Ordinal) ||
            name.StartsWith("mmcblk", StringComparison.Ordinal))
        {
            return !name.Contains('p');
        }

        return !char.IsDigit(name[^1]);
    }

    private static string? ResolveProcPath(string? explicitPath, bool containerized, out string? reason)
    {
        reason = null;

        foreach (var candidate in EnumerateCandidates(explicitPath, "/host/proc", containerized ? null : "/proc"))
        {
            if (File.Exists(Path.Combine(candidate, "stat")))
            {
                return TrimTrailingSeparator(candidate);
            }
        }

        reason = containerized
            ? "宿主机监控未启用：容器内未挂载 /host/proc。"
            : "宿主机监控不可用：无法读取 /proc/stat。";
        return null;
    }

    private static IEnumerable<string> EnumerateCandidates(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            yield return candidate.Trim();
        }
    }

    private static string? ResolveDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            return Directory.Exists(path) ? TrimTrailingSeparator(path) : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        return File.Exists(path) ? path : null;
    }

    private static bool DetectContainerized()
    {
        if (File.Exists("/.dockerenv") || File.Exists("/run/.containerenv")) return true;

        var runningInContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
        return string.Equals(runningInContainer, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static string TrimTrailingSeparator(string path)
    {
        return path.Length > 1 ? path.TrimEnd('/', '\\') : path;
    }

    private static string? ReadFirstLine(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var reader = new StreamReader(path);
            return reader.ReadLine()?.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static string UnescapeMount(string value)
    {
        if (!value.Contains('\\')) return value;

        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 3 < value.Length)
            {
                var octal = value.Substring(i + 1, 3);
                if (octal.All(c => c is >= '0' and <= '7'))
                {
                    builder.Append((char)Convert.ToInt32(octal, 8));
                    i += 3;
                    continue;
                }
            }

            builder.Append(value[i]);
        }

        return builder.ToString();
    }

    private static string Unquote(string value)
    {
        value = value.Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1];
        }

        return value;
    }

    private static ulong ParseULong(string[] parts, int index)
    {
        if (index >= parts.Length) return 0;
        return ulong.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static long ParseLong(string value)
    {
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? Math.Max(0, result)
            : 0;
    }

    private static double ParseDouble(string value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;
    }

    private static double DeltaPercent(ulong current, ulong previous, ulong deltaTotal)
    {
        if (deltaTotal == 0 || current < previous) return 0;
        return ClampPercent((current - previous) * 100.0 / deltaTotal);
    }

    private static double ClampPercent(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return 0;
        return Math.Clamp(value, 0, 100);
    }

    private static double Round(double value) => Math.Round(value, 2);

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] sizes = { "B", "KB", "MB", "GB", "TB", "PB" };
        double value = bytes;
        var order = 0;
        while (value >= 1024 && order < sizes.Length - 1)
        {
            order++;
            value /= 1024;
        }

        return $"{value:0.##} {sizes[order]}";
    }

    public static string FormatBytesPerSec(long bytesPerSec)
    {
        if (bytesPerSec <= 0) return "0 B/s";
        string[] sizes = { "B/s", "KB/s", "MB/s", "GB/s", "TB/s" };
        double value = bytesPerSec;
        var order = 0;
        while (value >= 1024 && order < sizes.Length - 1)
        {
            order++;
            value /= 1024;
        }

        return $"{value:0.##} {sizes[order]}";
    }

    private sealed class CpuSample
    {
        public ulong Total { get; init; }
        public ulong IdleTotal { get; init; }
        public ulong UserNice { get; init; }
        public ulong SystemTotal { get; init; }
        public ulong Iowait { get; init; }
    }

    private sealed class NetworkSample
    {
        public DateTime Timestamp { get; init; }
        public long RxBytes { get; init; }
        public long TxBytes { get; init; }
    }

    private sealed class DiskIoSample
    {
        public DateTime Timestamp { get; init; }
        public long ReadSectors { get; init; }
        public long WriteSectors { get; init; }
    }

    private sealed class FileSystemCache
    {
        public DateTime Timestamp { get; init; }
        public List<HostFileSystemMetrics> Items { get; init; } = new();
    }
}
