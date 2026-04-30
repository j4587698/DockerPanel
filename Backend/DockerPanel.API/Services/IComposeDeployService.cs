using Compose.NET.Types;
using DockerPanel.API.Models;

namespace DockerPanel.API.Services;

/// <summary>
/// Compose 部署服务接口 - 使用 Compose.NET 解析 compose 文件，Docker.DotNet 创建资源
/// </summary>
public interface IComposeDeployService
{
    /// <summary>
    /// 解析 compose 文件内容
    /// </summary>
    /// <param name="content">YAML 内容</param>
    /// <param name="projectName">项目名称（可选）</param>
    /// <param name="workingDir">工作目录（可选）</param>
    /// <returns>解析后的项目</returns>
    Task<ComposeParseResult> ParseAsync(string content, string? projectName = null, string? workingDir = null);

    /// <summary>
    /// 解析 compose 文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="projectName">项目名称（可选）</param>
    /// <returns>解析后的项目</returns>
    Task<ComposeParseResult> ParseFileAsync(string filePath, string? projectName = null);

    /// <summary>
    /// 部署 compose 项目
    /// </summary>
    /// <param name="project">已解析的项目</param>
    /// <param name="options">部署选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>部署结果</returns>
    Task<ComposeDeployResult> DeployAsync(Project project, ComposeDeployOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从内容直接部署
    /// </summary>
    /// <param name="content">YAML 内容</param>
    /// <param name="projectName">项目名称</param>
    /// <param name="options">部署选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>部署结果</returns>
    Task<ComposeDeployResult> DeployFromContentAsync(string content, string projectName, ComposeDeployOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止 compose 项目
    /// </summary>
    /// <param name="projectName">项目名称</param>
    /// <param name="services">要停止的服务列表（可选，不指定则停止所有）</param>
    /// <param name="timeout">超时秒数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<Models.ComposeOperationResult> StopAsync(string projectName, List<string>? services = null, int timeout = 30, CancellationToken cancellationToken = default);

    /// <summary>
    /// 启动 compose 项目
    /// </summary>
    /// <param name="projectName">项目名称</param>
    /// <param name="services">要启动的服务列表（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<Models.ComposeOperationResult> StartAsync(string projectName, List<string>? services = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除 compose 项目
    /// </summary>
    /// <param name="projectName">项目名称</param>
    /// <param name="removeVolumes">是否删除卷</param>
    /// <param name="removeImages">是否删除镜像</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<Models.ComposeOperationResult> RemoveAsync(string projectName, bool removeVolumes = false, bool removeImages = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取 compose 项目状态
    /// </summary>
    /// <param name="projectName">项目名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>项目状态</returns>
    Task<ComposeProjectStatus> GetStatusAsync(string projectName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取 compose 项目日志
    /// </summary>
    /// <param name="projectName">项目名称</param>
    /// <param name="services">服务列表（可选）</param>
    /// <param name="tail">行数</param>
    /// <param name="since">开始时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>日志内容</returns>
    Task<string> GetLogsAsync(string projectName, List<string>? services = null, int tail = 100, DateTime? since = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 重启 compose 项目
    /// </summary>
    /// <param name="projectName">项目名称</param>
    /// <param name="services">要重启的服务列表（可选）</param>
    /// <param name="timeout">超时秒数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<Models.ComposeOperationResult> RestartAsync(string projectName, List<string>? services = null, int timeout = 30, CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出所有 compose 项目
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>项目列表</returns>
    Task<List<ComposeProjectSummary>> ListProjectsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Compose 解析结果
/// </summary>
public class ComposeParseResult
{
    /// <summary>
    /// 解析后的项目
    /// </summary>
    public Project? Project { get; set; }

    /// <summary>
    /// 警告信息
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// 错误信息
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success => Errors.Count == 0;
}

/// <summary>
/// Compose 部署选项
/// </summary>
public class ComposeDeployOptions
{
    /// <summary>
    /// 是否在后台运行
    /// </summary>
    public bool Detach { get; set; } = true;

    /// <summary>
    /// 是否构建镜像
    /// </summary>
    public bool Build { get; set; }

    /// <summary>
    /// 是否拉取镜像
    /// </summary>
    public bool Pull { get; set; } = true;

    /// <summary>
    /// 是否删除孤立容器
    /// </summary>
    public bool RemoveOrphans { get; set; }

    /// <summary>
    /// 强制重新创建容器
    /// </summary>
    public bool ForceRecreate { get; set; }

    /// <summary>
    /// 不启动依赖服务
    /// </summary>
    public bool NoDeps { get; set; }

    /// <summary>
    /// 要启动的服务列表（可选）
    /// </summary>
    public List<string>? Services { get; set; }

    /// <summary>
    /// 环境变量覆盖
    /// </summary>
    public Dictionary<string, string>? Environment { get; set; }

    /// <summary>
    /// 激活的 profiles
    /// </summary>
    public List<string>? Profiles { get; set; }

    /// <summary>
    /// 拉取进度回调
    /// </summary>
    public IProgress<ImagePullProgress>? PullProgress { get; set; }
}

/// <summary>
/// Compose 部署结果
/// </summary>
public class ComposeDeployResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 部署步骤
    /// </summary>
    public List<ComposeDeployStep> Steps { get; set; } = new();

    /// <summary>
    /// 创建的网络
    /// </summary>
    public List<string> CreatedNetworks { get; set; } = new();

    /// <summary>
    /// 创建的卷
    /// </summary>
    public List<string> CreatedVolumes { get; set; } = new();

    /// <summary>
    /// 创建的容器
    /// </summary>
    public List<ComposeServiceStatus> CreatedContainers { get; set; } = new();

    /// <summary>
    /// 错误信息
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// 警告信息
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Compose 服务状态
/// </summary>
public class ComposeServiceStatus
{
    /// <summary>
    /// 服务名称
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// 容器 ID
    /// </summary>
    public string? ContainerId { get; set; }

    /// <summary>
    /// 容器名称
    /// </summary>
    public string? ContainerName { get; set; }

    /// <summary>
    /// 镜像
    /// </summary>
    public string? Image { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 是否运行中
    /// </summary>
    public bool IsRunning { get; set; }

    /// <summary>
    /// 健康状态
    /// </summary>
    public string? Health { get; set; }

    /// <summary>
    /// 端口映射列表
    /// </summary>
    public List<string> Ports { get; set; } = new();
}

/// <summary>
/// Compose 项目状态
/// </summary>
public class ComposeProjectStatus
{
    /// <summary>
    /// 项目名称
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// 状态
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 服务列表
    /// </summary>
    public List<ComposeServiceStatus> Services { get; set; } = new();

    /// <summary>
    /// 网络列表
    /// </summary>
    public List<string> Networks { get; set; } = new();

    /// <summary>
    /// 卷列表
    /// </summary>
    public List<string> Volumes { get; set; } = new();
}

/// <summary>
/// Compose 项目摘要
/// </summary>
public class ComposeProjectSummary
{
    /// <summary>
    /// 项目名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 状态
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 配置文件列表
    /// </summary>
    public List<string> ConfigFiles { get; set; } = new();

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 容器数量
    /// </summary>
    public int ContainerCount { get; set; }

    /// <summary>
    /// 运行中数量
    /// </summary>
    public int RunningCount { get; set; }
}

/// <summary>
/// 部署步骤
/// </summary>
public class ComposeDeployStep
{
    /// <summary>
    /// 步骤名称
    /// </summary>
    public string Step { get; set; } = string.Empty;

    /// <summary>
    /// 详细信息
    /// </summary>
    public string? Detail { get; set; }

    /// <summary>
    /// 进度百分比
    /// </summary>
    public int Progress { get; set; }

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}