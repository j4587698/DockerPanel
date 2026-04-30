using DockerPanel.API.Models;
using Microsoft.Extensions.Logging;

namespace DockerPanel.API.Services;

/// <summary>
/// 容器引擎工厂，用于创建和管理不同的容器引擎实现
/// </summary>
public class ContainerEngineFactory
{
    private readonly ILogger<ContainerEngineFactory> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, IContainerEngine> _engines;

    public ContainerEngineFactory(ILogger<ContainerEngineFactory> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _engines = new Dictionary<string, IContainerEngine>();
        InitializeEngines();
    }

    /// <summary>
    /// 初始化所有可用的容器引擎
    /// </summary>
    private void InitializeEngines()
    {
        try
        {
            // 注册真实 Docker 引擎
            var dockerEngine = _serviceProvider.GetRequiredService<DockerEngine>();
                    _engines["docker"] = dockerEngine;
            _logger.LogInformation("已初始化 {Count} 个容器引擎", _engines.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化容器引擎失败");
        }
    }

    /// <summary>
    /// 根据引擎类型获取容器引擎实例
    /// </summary>
    /// <param name="engineType">引擎类型（docker、podman）</param>
    /// <returns>容器引擎实例</returns>
    public IContainerEngine GetEngine(string engineType)
    {
        if (_engines.TryGetValue(engineType.ToLowerInvariant(), out var engine))
        {
            return engine;
        }

        throw new ArgumentException($"不支持的容器引擎类型: {engineType}");
    }

    /// <summary>
    /// 获取所有可用的容器引擎
    /// </summary>
    /// <returns>容器引擎列表</returns>
    public IEnumerable<IContainerEngine> GetAllEngines()
    {
        return _engines.Values;
    }

    /// <summary>
    /// 获取可用的容器引擎类型列表
    /// </summary>
    /// <returns>引擎类型列表</returns>
    public IEnumerable<string> GetAvailableEngineTypes()
    {
        return _engines.Keys;
    }

    /// <summary>
    /// 检查指定引擎类型是否可用
    /// </summary>
    /// <param name="engineType">引擎类型</param>
    /// <returns>是否可用</returns>
    public async Task<bool> IsEngineAvailableAsync(string engineType)
    {
        try
        {
            var engine = GetEngine(engineType);
            return await engine.IsAvailableAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查引擎 {EngineType} 可用性失败", engineType);
            return false;
        }
    }

    /// <summary>
    /// 获取第一个可用的容器引擎
    /// </summary>
    /// <returns>可用的容器引擎实例，如果没有可用的则返回null</returns>
    public async Task<IContainerEngine?> GetFirstAvailableEngineAsync()
    {
        foreach (var engine in _engines.Values)
        {
            try
            {
                if (await engine.IsAvailableAsync())
                {
                    _logger.LogInformation("找到可用的容器引擎: {EngineName}", engine.EngineName);
                    return engine;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "检查引擎 {EngineName} 可用性时发生错误", engine.EngineName);
            }
        }

        _logger.LogWarning("未找到可用的容器引擎");
        return null;
    }

    /// <summary>
    /// 获取所有可用容器引擎的状态
    /// </summary>
    /// <returns>引擎状态字典</returns>
    public async Task<Dictionary<string, bool>> GetEnginesStatusAsync()
    {
        var status = new Dictionary<string, bool>();

        foreach (var kvp in _engines)
        {
            try
            {
                status[kvp.Key] = await kvp.Value.IsAvailableAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取引擎 {EngineType} 状态失败", kvp.Key);
                status[kvp.Key] = false;
            }
        }

        return status;
    }
}

/// <summary>
/// 容器引擎管理器，提供高级的容器引擎管理功能
/// </summary>
public class ContainerEngineManager
{
    private readonly ContainerEngineFactory _factory;
    private readonly ILogger<ContainerEngineManager> _logger;
    private string? _defaultEngineType;

    public ContainerEngineManager(ContainerEngineFactory factory, ILogger<ContainerEngineManager> logger)
    {
        _factory = factory;
        _logger = logger;
        _defaultEngineType = "docker"; // 默认使用 Docker
    }

    /// <summary>
    /// 设置默认容器引擎类型
    /// </summary>
    /// <param name="engineType">引擎类型</param>
    public void SetDefaultEngine(string engineType)
    {
        if (_factory.GetAvailableEngineTypes().Contains(engineType.ToLowerInvariant()))
        {
            _defaultEngineType = engineType.ToLowerInvariant();
            _logger.LogInformation("已设置默认容器引擎为: {EngineType}", _defaultEngineType);
        }
        else
        {
            throw new ArgumentException($"不支持的容器引擎类型: {engineType}");
        }
    }

    /// <summary>
    /// 获取默认容器引擎
    /// </summary>
    /// <returns>默认容器引擎实例</returns>
    public IContainerEngine GetDefaultEngine()
    {
        if (!string.IsNullOrEmpty(_defaultEngineType))
        {
            return _factory.GetEngine(_defaultEngineType);
        }

        throw new InvalidOperationException("未设置默认容器引擎");
    }

    /// <summary>
    /// 获取或创建可用的默认容器引擎
    /// </summary>
    /// <returns>可用的默认容器引擎实例</returns>
    public async Task<IContainerEngine> GetOrCreateDefaultEngineAsync()
    {
        // 首先尝试使用默认引擎
        if (!string.IsNullOrEmpty(_defaultEngineType))
        {
            var defaultEngine = _factory.GetEngine(_defaultEngineType);
            if (await defaultEngine.IsAvailableAsync())
            {
                return defaultEngine;
            }
        }

        // 如果默认引擎不可用，尝试找到第一个可用的引擎
        var availableEngine = await _factory.GetFirstAvailableEngineAsync();
        if (availableEngine != null)
        {
            _logger.LogInformation("默认引擎不可用，使用备用引擎: {EngineName}", availableEngine.EngineName);
            return availableEngine;
        }

        throw new InvalidOperationException("没有可用的容器引擎");
    }

    /// <summary>
    /// 检查容器引擎的健康状态
    /// </summary>
    /// <returns>健康检查结果</returns>
    public async Task<ContainerEngineHealthStatus> GetHealthStatusAsync()
    {
        var enginesStatus = await _factory.GetEnginesStatusAsync();
        var availableCount = enginesStatus.Values.Count(available => available);

        return new ContainerEngineHealthStatus
        {
            IsHealthy = availableCount > 0,
            TotalEngines = enginesStatus.Count,
            AvailableEngines = availableCount,
            EngineStatuses = enginesStatus,
            DefaultEngine = _defaultEngineType,
            DefaultEngineAvailable = !string.IsNullOrEmpty(_defaultEngineType) &&
                                   enginesStatus.GetValueOrDefault(_defaultEngineType, false)
        };
    }
}

/// <summary>
/// 容器引擎健康状态
/// </summary>
public class ContainerEngineHealthStatus
{
    /// <summary>
    /// 是否健康（至少有一个引擎可用）
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// 总引擎数量
    /// </summary>
    public int TotalEngines { get; set; }

    /// <summary>
    /// 可用引擎数量
    /// </summary>
    public int AvailableEngines { get; set; }

    /// <summary>
    /// 各引擎状态
    /// </summary>
    public Dictionary<string, bool> EngineStatuses { get; set; } = new();

    /// <summary>
    /// 默认引擎类型
    /// </summary>
    public string? DefaultEngine { get; set; }

    /// <summary>
    /// 默认引擎是否可用
    /// </summary>
    public bool DefaultEngineAvailable { get; set; }
}