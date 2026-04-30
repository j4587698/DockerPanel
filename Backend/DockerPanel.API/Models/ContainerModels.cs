using System.ComponentModel.DataAnnotations;

namespace DockerPanel.API.Models;

/// <summary>
/// 容器日志信息
/// </summary>
public class ContainerLogs
{
    public string ContainerId { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public List<ContainerLogEntry> Logs { get; set; } = new();
    public DateTime? Since { get; set; }
    public DateTime? Until { get; set; }
    public bool ShowTimestamps { get; set; } = true;
    public bool Follow { get; set; } = false;
    public int? Tail { get; set; }
    public string? StdType { get; set; } // "stdout", "stderr", "all"
    public bool HasMore { get; set; }
}

/// <summary>
/// 容器日志条目
/// </summary>
public class ContainerLogEntry
{
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Stream { get; set; } = string.Empty; // "stdout" or "stderr"
}

/// <summary>
/// 容器统计信息
/// </summary>
public class ContainerStats
{
    public string ContainerId { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }

    // CPU使用情况
    public ContainerCpuStats CpuStats { get; set; } = new();

    // 内存使用情况
    public ContainerMemoryStats MemoryStats { get; set; } = new();

    // 网络使用情况
    public List<ContainerNetworkStats> Networks { get; set; } = new();

    // 块设备使用情况
    public List<ContainerBlockIoStats> BlockIo { get; set; } = new();

    // PIDs使用情况
    public ContainerPidsStats PidsStats { get; set; } = new();
}

/// <summary>
/// 容器CPU统计
/// </summary>
public class ContainerCpuStats
{
    public long CpuUsage { get; set; }
    public long SystemUsage { get; set; }
    public long OnlineCpus { get; set; }
    public long ThrottledPeriods { get; set; }
    public long ThrottledTime { get; set; }
    public double PercentCpu { get; set; }
}

/// <summary>
/// 容器内存统计
/// </summary>
public class ContainerMemoryStats
{
    public long Usage { get; set; }
    public long Limit { get; set; }
    public long MaxUsage { get; set; }
    public long Cache { get; set; }
    public long SwapUsage { get; set; }
    public long SwapLimit { get; set; }
    public double PercentMemory { get; set; }
    public double UsagePercent { get; set; }
}

/// <summary>
/// 容器网络统计
/// </summary>
public class ContainerNetworkStats
{
    public string Name { get; set; } = string.Empty;
    public string Interface { get; set; } = string.Empty;
    public long RxBytes { get; set; }
    public long RxPackets { get; set; }
    public long RxErrors { get; set; }
    public long RxDropped { get; set; }
    public long TxBytes { get; set; }
    public long TxPackets { get; set; }
    public long TxErrors { get; set; }
    public long TxDropped { get; set; }
}

/// <summary>
/// 容器块设备IO统计
/// </summary>
public class ContainerBlockIoStats
{
    public string Device { get; set; } = string.Empty;
    public long ReadBytes { get; set; }
    public long ReadOperations { get; set; }
    public long WriteBytes { get; set; }
    public long WriteOperations { get; set; }
}

/// <summary>
/// 容器PIDs统计
/// </summary>
public class ContainerPidsStats
{
    public long Current { get; set; }
    public long Limit { get; set; }
}

/// <summary>
/// 容器执行结果
/// </summary>
public class ContainerExecResult
{
    public string ContainerId { get; set; } = string.Empty;
    public string ExecId { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public string Output { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}

/// <summary>
/// 命令执行结果（与IContainerService中的ExecResult保持一致）
/// </summary>
public class ExecResult
{
    /// <summary>
    /// 退出代码
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>
    /// 标准输出
    /// </summary>
    public string Stdout { get; set; } = string.Empty;

    /// <summary>
    /// 标准错误
    /// </summary>
    public string Stderr { get; set; } = string.Empty;

    /// <summary>
    /// 执行开始时间
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 执行结束时间
    /// </summary>
    public DateTime EndTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 执行耗时（毫秒）
    /// </summary>
    public long DurationMs => (long)(EndTime - StartTime).TotalMilliseconds;
}

/// <summary>
/// 容器进程信息
/// </summary>
public class ContainerProcess
{
    public string Pid { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public string? CpuTime { get; set; }
    public string? Memory { get; set; }
}

/// <summary>
/// 容器进程列表
/// </summary>
public class ContainerProcessList
{
    public string ContainerId { get; set; } = string.Empty;
    public List<ContainerProcess> Processes { get; set; } = new();
    public List<string> Titles { get; set; } = new(); // 列标题
}

/// <summary>
/// 容器文件系统变更
/// </summary>
public class ContainerFileSystemChange
{
    public string Path { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty; // 0=修改, 1=添加, 2=删除
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// 文件系统变更（通用别名）
/// </summary>
public class FileSystemChange : ContainerFileSystemChange
{
}

/// <summary>
/// 容器文件系统变更列表
/// </summary>
public class ContainerChanges
{
    public string ContainerId { get; set; } = string.Empty;
    public List<ContainerFileSystemChange> Changes { get; set; } = new();
}

/// <summary>
/// 容器检查点
/// </summary>
public class ContainerCheckpoint
{
    public string CheckpointId { get; set; } = string.Empty;
    public string ContainerId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? CheckpointDir { get; set; }
    public bool Exit { get; set; }
    public bool TcpEstablished { get; set; }
    public bool Shell { get; set; }
    public bool LinuxPrivileged { get; set; }
}

/// <summary>
/// 创建容器检查点请求
/// </summary>
public class CreateContainerCheckpointRequest
{
    [Required]
    public string CheckpointId { get; set; } = string.Empty;
    public bool Exit { get; set; } = false;
    public bool TcpEstablished { get; set; } = false;
    public bool Shell { get; set; } = false;
    public bool LinuxPrivileged { get; set; } = false;
    public string? CheckpointDir { get; set; }
}

/// <summary>
/// 日志条目
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string Stream { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// CPU统计信息（简化版，用于PodmanEngine）
/// </summary>
public class CpuStats
{
    public long Usage { get; set; }
    public double Percent { get; set; }
}

/// <summary>
/// 内存统计信息（简化版，用于PodmanEngine）
/// </summary>
public class MemoryStats
{
    public long Usage { get; set; }
    public long Limit { get; set; }
}

/// <summary>
/// 网络统计信息（简化版，用于PodmanEngine）
/// </summary>
public class NetworkStats
{
    public long RxBytes { get; set; }
    public long TxBytes { get; set; }
}

/// <summary>
/// 块设备IO统计信息（简化版，用于PodmanEngine）
/// </summary>
public class BlockIoStats
{
    public long ReadBytes { get; set; }
    public long WriteBytes { get; set; }
}

/// <summary>
/// 容器Top进程信息
/// </summary>
public class ContainerTop
{
    public string ContainerId { get; set; } = string.Empty;
    public List<string> Titles { get; set; } = new();
    public List<List<string>> Processes { get; set; } = new();
}

/// <summary>
/// 容器检查信息
/// </summary>
public class ContainerInspect
{
    public string Id { get; set; } = string.Empty;
    public DateTime Created { get; set; }
    public string Path { get; set; } = string.Empty;
    public List<string> Args { get; set; } = new();
    public string State { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, object> Config { get; set; } = new();
    public Dictionary<string, object> HostConfig { get; set; } = new();
    public Dictionary<string, object> NetworkSettings { get; set; } = new();
}

/// <summary>
/// 容器统计摘要
/// </summary>
public class ContainerStatsSummary
{
    public string ContainerId { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public double MemoryLimit { get; set; }
    public long NetworkRx { get; set; }
    public long NetworkTx { get; set; }
    public long BlockRead { get; set; }
    public long BlockWrite { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// 容器事件
/// </summary>
public class ContainerEvent
{
    public string Type { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = new();
}

/// <summary>
/// 容器统计历史
/// </summary>
public class ContainerStatsHistory
{
    public string ContainerId { get; set; } = string.Empty;
    public List<ContainerStats> Stats { get; set; } = new();
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

/// <summary>
/// 容器端口绑定
/// </summary>
public class ContainerPortBinding
{
    public string ContainerPort { get; set; } = string.Empty;
    public string HostPort { get; set; } = string.Empty;
    public string HostIp { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
}

/// <summary>
/// 容器文件系统差异
/// </summary>
public class ContainerDiff
{
    public string ContainerId { get; set; } = string.Empty;
    public List<string> Additions { get; set; } = new();
    public List<string> Deletions { get; set; } = new();
    public List<string> Modifications { get; set; } = new();
}

/// <summary>
/// 容器提交请求
/// </summary>
public class ContainerCommitRequest
{
    public string ContainerId { get; set; } = string.Empty;
    public string? Repository { get; set; }
    public string? Tag { get; set; }
    public string? Message { get; set; }
    public string? Author { get; set; }
    public bool Pause { get; set; } = false;
    public Dictionary<string, string>? Labels { get; set; }
}

/// <summary>
/// 容器提交结果
/// </summary>
public class ContainerCommitResult
{
    public string ImageId { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? Digest { get; set; }
    public long Size { get; set; }
}

/// <summary>
/// 容器文件信息
/// </summary>
public class ContainerFileInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "file" or "directory"
    public long Size { get; set; }
    public DateTime? Modified { get; set; }
    public string Permissions { get; set; } = string.Empty;
    public string? Owner { get; set; }
    public string? Group { get; set; }
    /// <summary>
    /// 是否为挂载点（Volume）
    /// </summary>
    public bool IsMount { get; set; }
    /// <summary>
    /// 挂载点来源（主机路径或卷名）
    /// </summary>
    public string? MountSource { get; set; }
    /// <summary>
    /// 挂载类型（bind, volume, tmpfs）
    /// </summary>
    public string? MountType { get; set; }
    /// <summary>
    /// 文件变更状态：null=原始文件, "A"=新增, "C"=修改, "D"=已删除
    /// </summary>
    public string? ChangeStatus { get; set; }
}

/// <summary>
/// 容器文件列表响应
/// </summary>
public class ContainerFileListResponse
{
    public string ContainerId { get; set; } = string.Empty;
    public string CurrentPath { get; set; } = string.Empty;
    public List<ContainerFileInfo> Files { get; set; } = new();
    public List<ContainerMountInfo> Mounts { get; set; } = new();
}

/// <summary>
/// 容器挂载点信息
/// </summary>
public class ContainerMountInfo
{
    public string Destination { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string Type { get; set; } = string.Empty; // "bind", "volume", "tmpfs"
    public string? Name { get; set; }
    public bool Rw { get; set; }
    public string? Driver { get; set; }
    /// <summary>
    /// 是否为命名卷（持久化存储）
    /// </summary>
    public bool IsNamedVolume => Type == "volume" && !string.IsNullOrEmpty(Name);
    /// <summary>
    /// 是否为绑定挂载（主机目录）
    /// </summary>
    public bool IsBindMount => Type == "bind";
}

/// <summary>
/// 创建文件夹请求
/// </summary>
public class CreateFolderRequest
{
    [Required]
    public string Path { get; set; } = string.Empty;
    [Required]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// 重命名文件请求
/// </summary>
public class RenameFileRequest
{
    [Required]
    public string Path { get; set; } = string.Empty;
    [Required]
    public string OldName { get; set; } = string.Empty;
    [Required]
    public string NewName { get; set; } = string.Empty;
}

/// <summary>
/// 删除文件请求
/// </summary>
public class DeleteFileRequest
{
    [Required]
    public string Path { get; set; } = string.Empty;
    public bool Recursive { get; set; } = false;
}


