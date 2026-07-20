using DockerPanel.API.Models;
using System;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Docker.DotNet.Models;

namespace DockerPanel.API.Services;

/// <summary>
/// 简单镜像仓库管理服务实现
/// </summary>
public class RegistryService : IRegistryService
{
    private readonly ILogger<RegistryService> _logger;
    private readonly IMemoryCache _cache;
    private readonly HttpClient _httpClient;
    private readonly DataBaseService _databaseService;
    private readonly IContainerEngine _containerEngine;

    public RegistryService(
        ILogger<RegistryService> logger,
        IMemoryCache cache,
        HttpClient httpClient,
        DataBaseService databaseService,
        IContainerEngine containerEngine)
    {
        _logger = logger;
        _cache = cache;
        _httpClient = httpClient;
        _databaseService = databaseService;
        _containerEngine = containerEngine;
    }

    /// <summary>
    /// 获取所有镜像仓库
    /// </summary>
    public async Task<IEnumerable<ImageRegistry>> GetRegistriesAsync()
    {
        try
        {
            _logger.LogInformation("获取镜像仓库列表");
            var cacheKey = "registry_list";
            if (_cache.TryGetValue(cacheKey, out List<ImageRegistry>? cachedRegistries))
            {
                return cachedRegistries!;
            }

            var registries = await GetRegistriesFromStorageAsync();
            _cache.Set(cacheKey, registries, TimeSpan.FromMinutes(10));
            return registries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取镜像仓库列表失败");
            throw;
        }
    }

    /// <summary>
    /// 根据ID获取镜像仓库
    /// </summary>
    public async Task<ImageRegistry?> GetRegistryByIdAsync(string id)
    {
        try
        {
            _logger.LogInformation("获取镜像仓库详情: {RegistryId}", id);
            var registries = await GetRegistriesAsync();
            return registries.FirstOrDefault(r => r.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取镜像仓库详情失败: {RegistryId}", id);
            throw;
        }
    }

    /// <summary>
    /// 创建镜像仓库
    /// </summary>
    public async Task<ImageRegistry> CreateRegistryAsync(CreateRegistryRequest request)
    {
        try
        {
            _logger.LogInformation("创建镜像仓库: {Name}", request.Name);

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ArgumentException("仓库名称不能为空", nameof(request.Name));
            }

            if (string.IsNullOrWhiteSpace(request.Domain))
            {
                throw new ArgumentException("仓库域名不能为空", nameof(request.Domain));
            }

            // 清理域名：移除可能的协议前缀
            var domain = request.Domain.Trim()
                .Replace("https://", "", StringComparison.OrdinalIgnoreCase)
                .Replace("http://", "", StringComparison.OrdinalIgnoreCase)
                .TrimEnd('/');

            var registry = new ImageRegistry
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                Domain = domain,
                Username = request.Username ?? string.Empty,
                Password = request.Password ?? string.Empty,
                Email = request.Email ?? string.Empty,
                IsDefault = request.IsDefault,
                IsSecure = request.IsSecure,
                IsPublic = request.IsPublic,
                Type = request.Type ?? DetermineRegistryType(domain),
                Description = request.Description ?? string.Empty,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = "user",
                UpdatedBy = "user",
                Status = "Active",
                Configuration = new RegistryConfig
                {
                    ApiVersion = "v2",
                    Namespace = null,
                    Mirrors = new List<string>(),
                    InsecureRegistries = new List<string>()
                },
                Metadata = new Dictionary<string, object>()
            };

            // 如果设置为默认，需要将其他仓库的默认状态设为false
            if (request.IsDefault)
            {
                await SetDefaultRegistryAsync(registry.Id);
            }

            // 测试当前配置连接
            var testResult = await TestRegistryConfigAsync(new TestRegistryConfigRequest
            {
                Domain = registry.Domain,
                Username = registry.Username,
                Password = registry.Password,
                IsSecure = registry.IsSecure
            });
            if (!testResult.IsConnected)
            {
                _logger.LogWarning("仓库连接测试失败: {Message}", testResult.Message);
            }

            await SaveRegistryToStorageAsync(registry);

            // 清除缓存
            _cache.Remove("registry_list");

            return registry;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建镜像仓库失败: {Name}", request.Name);
            throw;
        }
    }

    /// <summary>
    /// 更新镜像仓库
    /// </summary>
    public async Task<ImageRegistry> UpdateRegistryAsync(string id, UpdateRegistryRequest request)
    {
        try
        {
            _logger.LogInformation("更新镜像仓库: {RegistryId}", id);

            var registry = await GetRegistryByIdAsync(id);
            if (registry == null)
            {
                throw new ArgumentException($"仓库不存在: {id}", nameof(id));
            }

            if (request.Name != null) registry.Name = request.Name;
            if (request.Domain != null)
            {
                // 清理域名：移除可能的协议前缀
                var domain = request.Domain.Trim()
                    .Replace("https://", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("http://", "", StringComparison.OrdinalIgnoreCase)
                    .TrimEnd('/');
                registry.Domain = domain;
            }
            if (request.Username != null) registry.Username = request.Username;
            // 密码只在非空时更新（避免前端传空字符串清空密码）
            if (!string.IsNullOrEmpty(request.Password)) registry.Password = request.Password;
            if (request.IsSecure.HasValue) registry.IsSecure = request.IsSecure.Value;
            if (!string.IsNullOrEmpty(request.Type)) registry.Type = request.Type;
            if (request.IsDefault.HasValue && request.IsDefault.Value != registry.IsDefault)
            {
                await SetDefaultRegistryAsync(registry.Id);
            }

            registry.UpdatedAt = DateTime.UtcNow;

            // 测试当前配置连接
            var testResult = await TestRegistryConfigAsync(new TestRegistryConfigRequest
            {
                Domain = registry.Domain,
                Username = registry.Username,
                Password = registry.Password,
                IsSecure = registry.IsSecure
            });
            if (!testResult.IsConnected)
            {
                _logger.LogWarning("仓库连接测试失败: {Message}", testResult.Message);
            }

            await SaveRegistryToStorageAsync(registry);

            // 清除缓存
            _cache.Remove("registry_list");

            return registry;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新镜像仓库失败: {RegistryId}", id);
            throw;
        }
    }

    /// <summary>
    /// 删除镜像仓库
    /// </summary>
    public async Task<bool> DeleteRegistryAsync(string id)
    {
        try
        {
            _logger.LogInformation("删除镜像仓库: {RegistryId}", id);

            var registry = await GetRegistryByIdAsync(id);
            if (registry == null)
            {
                return false;
            }

            // 如果是默认仓库，不允许删除
            if (registry.IsDefault)
            {
                throw new InvalidOperationException("不能删除默认仓库");
            }

            await DeleteRegistryFromStorageAsync(id);

            // 清除缓存
            _cache.Remove("registry_list");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除镜像仓库失败: {RegistryId}", id);
            throw;
        }
    }

    /// <summary>
    /// 测试仓库连接
    /// </summary>
    public async Task<RegistryTestResult> TestRegistryConnectionAsync(string registryId)
    {
        try
        {
            _logger.LogInformation("测试仓库连接: {RegistryId}", registryId);

            var registry = await GetRegistryByIdAsync(registryId);
            if (registry == null)
            {
                return new RegistryTestResult
                {
                    IsConnected = false,
                    Message = "仓库不存在",
                    RegistryUrl = string.Empty,
                    TestTime = DateTime.UtcNow,
                    ResponseTimeMs = 0
                };
            }

            if (string.IsNullOrWhiteSpace(registry.Domain))
            {
                return new RegistryTestResult
                {
                    IsConnected = false,
                    Message = "仓库域名无效",
                    RegistryUrl = string.Empty,
                    TestTime = DateTime.UtcNow,
                    ResponseTimeMs = 0
                };
            }

            var stopwatch = Stopwatch.StartNew();
            var registryUrl = registry.Url;

            // 如果有凭据，使用 Docker API 验证
            if (!string.IsNullOrEmpty(registry.Username) && !string.IsNullOrEmpty(registry.Password))
            {
                _logger.LogInformation("使用 Docker API 验证凭据: {Domain}, 用户: {Username}", registry.Domain, registry.Username);
                
                var authResult = await _containerEngine.AuthenticateRegistryAsync(registry.Domain, registry.Username, registry.Password);
                stopwatch.Stop();
                
                if (authResult.IsValid)
                {
                    return new RegistryTestResult
                    {
                        IsConnected = true,
                        Message = "连接成功，凭据验证通过",
                        RegistryUrl = registryUrl,
                        TestTime = DateTime.UtcNow,
                        ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                        Details = new Dictionary<string, object>
                        {
                            ["Domain"] = registry.Domain,
                            ["AuthTest"] = "Passed",
                            ["Username"] = registry.Username
                        }
                    };
                }
                else
                {
                    return new RegistryTestResult
                    {
                        IsConnected = false,
                        Message = authResult.Message,
                        RegistryUrl = registryUrl,
                        TestTime = DateTime.UtcNow,
                        ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                        Details = new Dictionary<string, object>
                        {
                            ["Domain"] = registry.Domain,
                            ["AuthTest"] = "Failed"
                        }
                    };
                }
            }

            // 没有凭据，只测试基本连接
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                var response = await client.GetAsync($"{registryUrl}/v2/");
                stopwatch.Stop();
                
                var isConnected = response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized;
                return new RegistryTestResult
                {
                    IsConnected = isConnected,
                    Message = isConnected ? "连接成功（未配置凭据）" : $"连接失败: HTTP {(int)response.StatusCode}",
                    RegistryUrl = registryUrl,
                    TestTime = DateTime.UtcNow,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new RegistryTestResult
                {
                    IsConnected = false,
                    Message = $"连接失败: {ex.Message}",
                    RegistryUrl = registryUrl,
                    TestTime = DateTime.UtcNow,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试仓库连接失败: {RegistryId}", registryId);
            return new RegistryTestResult
            {
                IsConnected = false,
                Message = $"测试失败: {ex.Message}",
                RegistryUrl = string.Empty,
                TestTime = DateTime.UtcNow,
                ResponseTimeMs = 0
            };
        }
    }

    /// <summary>
    /// 测试仓库配置连接（无需保存）
    /// </summary>
    public async Task<RegistryTestResult> TestRegistryConfigAsync(TestRegistryConfigRequest request)
    {
        try
        {
            _logger.LogInformation("测试仓库配置连接: {Domain}", request.Domain);

            // 清理域名
            var domain = request.Domain.Trim()
                .Replace("https://", "", StringComparison.OrdinalIgnoreCase)
                .Replace("http://", "", StringComparison.OrdinalIgnoreCase)
                .TrimEnd('/');

            if (string.IsNullOrWhiteSpace(domain))
            {
                return new RegistryTestResult
                {
                    IsConnected = false,
                    Message = "仓库域名不能为空",
                    RegistryUrl = string.Empty,
                    TestTime = DateTime.UtcNow,
                    ResponseTimeMs = 0
                };
            }

            var registryUrl = request.IsSecure ? $"https://{domain}" : $"http://{domain}";
            var stopwatch = Stopwatch.StartNew();

            // 如果提供了凭据，使用 Docker API 验证
            if (!string.IsNullOrEmpty(request.Username) && !string.IsNullOrEmpty(request.Password))
            {
                _logger.LogInformation("使用 Docker API 验证凭据: {Domain}, 用户: {Username}", domain, request.Username);
                
                var authResult = await _containerEngine.AuthenticateRegistryAsync(domain, request.Username, request.Password);
                stopwatch.Stop();
                
                if (authResult.IsValid)
                {
                    return new RegistryTestResult
                    {
                        IsConnected = true,
                        Message = "连接成功，凭据验证通过",
                        RegistryUrl = registryUrl,
                        TestTime = DateTime.UtcNow,
                        ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                        Details = new Dictionary<string, object>
                        {
                            ["Domain"] = domain,
                            ["AuthTest"] = "Passed",
                            ["Username"] = request.Username
                        }
                    };
                }
                else
                {
                    return new RegistryTestResult
                    {
                        IsConnected = false,
                        Message = authResult.Message,
                        RegistryUrl = registryUrl,
                        TestTime = DateTime.UtcNow,
                        ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                        Details = new Dictionary<string, object>
                        {
                            ["Domain"] = domain,
                            ["AuthTest"] = "Failed"
                        }
                    };
                }
            }

            // 没有凭据，只测试基本连接
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                var response = await client.GetAsync($"{registryUrl}/v2/");
                stopwatch.Stop();
                
                var isConnected = response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized;
                return new RegistryTestResult
                {
                    IsConnected = isConnected,
                    Message = isConnected ? "连接成功（未配置凭据）" : $"连接失败: HTTP {(int)response.StatusCode}",
                    RegistryUrl = registryUrl,
                    TestTime = DateTime.UtcNow,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new RegistryTestResult
                {
                    IsConnected = false,
                    Message = $"连接失败: {ex.Message}",
                    RegistryUrl = registryUrl,
                    TestTime = DateTime.UtcNow,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试仓库配置连接失败: {Domain}", request.Domain);
            return new RegistryTestResult
            {
                IsConnected = false,
                Message = $"测试失败: {ex.Message}",
                RegistryUrl = string.Empty,
                TestTime = DateTime.UtcNow,
                ResponseTimeMs = 0
            };
        }
    }

    /// <summary>
    /// 获取仓库中的镜像列表
    /// </summary>
    public async Task<IEnumerable<RegistryImage>> GetRegistryImagesAsync(string registryId, string? search = null)
    {
        try
        {
            _logger.LogInformation("获取仓库镜像列表: {RegistryId}, 搜索: {Search}", registryId, search);

            var registry = await GetRegistryByIdAsync(registryId);
            if (registry == null)
            {
                return Enumerable.Empty<RegistryImage>();
            }

            // 使用 Docker Registry V2 API 读取目录和标签。
            var images = await GetImagesFromRegistryAsync(registry, search);
            return images;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取仓库镜像列表失败: {RegistryId}", registryId);
            throw;
        }
    }

    /// <summary>
    /// 设置默认仓库
    /// </summary>
    public async Task<bool> SetDefaultRegistryAsync(string registryId)
    {
        try
        {
            _logger.LogInformation("设置默认仓库: {RegistryId}", registryId);

            var registries = await GetRegistriesFromStorageAsync();
            foreach (var reg in registries)
            {
                if (reg.Id != registryId)
                {
                    reg.IsDefault = false;
                }
            }

            var targetRegistry = registries.FirstOrDefault(r => r.Id == registryId);
            if (targetRegistry != null)
            {
                targetRegistry.IsDefault = true;
            }

            await SaveRegistriesToStorageAsync(registries);
            _cache.Remove("registry_list");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置默认仓库失败: {RegistryId}", registryId);
            return false;
        }
    }

    /// <summary>
    /// 登录到私有仓库（验证认证信息是否有效）
    /// Docker 认证是在拉取镜像时通过 API 传递 AuthConfig，不需要单独执行 docker login
    /// </summary>
    public async Task<bool> LoginToRegistryAsync(string registryId, string? username = null, string? password = null)
    {
        try
        {
            _logger.LogInformation("验证仓库认证: {RegistryId}", registryId);

            var registry = await GetRegistryByIdAsync(registryId);
            if (registry == null)
            {
                _logger.LogWarning("仓库不存在: {RegistryId}", registryId);
                return false;
            }

            var user = username ?? registry.Username;
            var pass = password ?? registry.Password;

            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
            {
                _logger.LogWarning("仓库没有配置认证信息: {RegistryId}", registryId);
                return false;
            }

            var authResult = await _containerEngine.AuthenticateRegistryAsync(registry.Domain, user, pass);
            if (!authResult.IsValid)
            {
                _logger.LogWarning("仓库认证失败: {RegistryId}, Message={Message}", registryId, authResult.Message);
                return false;
            }

            registry.Username = user;
            registry.Password = pass;
            registry.UpdatedAt = DateTime.UtcNow;
            await SaveRegistryToStorageAsync(registry);
            _cache.Remove("registry_list");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证仓库认证失败: {RegistryId}", registryId);
            return false;
        }
    }

    /// <summary>
    /// 从私有仓库登出
    /// </summary>
    public async Task<bool> LogoutFromRegistryAsync(string registryId)
    {
        try
        {
            _logger.LogInformation("清除私有仓库凭据: {RegistryId}", registryId);

            var registry = await GetRegistryByIdAsync(registryId);
            if (registry == null)
            {
                return false;
            }

            registry.Username = string.Empty;
            registry.Password = string.Empty;
            registry.Email = string.Empty;
            registry.UpdatedAt = DateTime.UtcNow;
            await SaveRegistryToStorageAsync(registry);
            _cache.Remove("registry_list");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从私有仓库登出失败: {RegistryId}", registryId);
            return false;
        }
    }

    /// <summary>
    /// 验证仓库认证信息
    /// </summary>
    public async Task<RegistryAuthResult> ValidateRegistryAuthAsync(string registryId)
    {
        try
        {
            _logger.LogInformation("验证仓库认证: {RegistryId}", registryId);

            var registry = await GetRegistryByIdAsync(registryId);
            if (registry == null)
            {
                return new RegistryAuthResult
                {
                    IsValid = false,
                    Message = "仓库不存在",
                    AuthType = "none",
                    TestTime = DateTime.UtcNow
                };
            }

            // 验证认证信息
            var isValid = await ValidateRegistryCredentialsAsync(registry);

            return new RegistryAuthResult
            {
                IsValid = isValid,
                Message = isValid ? "认证有效" : "认证无效",
                AuthType = registry.IsSecure ? "https" : "http",
                TestTime = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证仓库认证失败: {RegistryId}", registryId);
            return new RegistryAuthResult
            {
                IsValid = false,
                Message = ex.Message,
                AuthType = "none",
                TestTime = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// 同步仓库镜像信息
    /// </summary>
    public async Task<RegistrySyncResult> SyncRegistryImagesAsync(string registryId)
    {
        try
        {
            _logger.LogInformation("同步仓库镜像信息: {RegistryId}", registryId);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var images = await GetRegistryImagesAsync(registryId);
            stopwatch.Stop();

            return new RegistrySyncResult
            {
                RegistryId = registryId,
                RegistryName = (await GetRegistryByIdAsync(registryId))?.Name ?? string.Empty,
                TotalImages = images.Count(),
                SyncedImages = images.Count(),
                NewImages = images.Count(),
                UpdatedImages = 0,
                SkippedImages = 0,
                Errors = new List<SyncError>(),
                SyncTime = DateTime.UtcNow,
                SyncDuration = stopwatch.Elapsed,
                IsSuccess = true,
                Summary = $"成功同步 {images.Count()} 个镜像"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步仓库镜像信息失败: {RegistryId}", registryId);
            return new RegistrySyncResult
            {
                RegistryId = registryId,
                RegistryName = string.Empty,
                TotalImages = 0,
                SyncedImages = 0,
                NewImages = 0,
                UpdatedImages = 0,
                SkippedImages = 0,
                Errors = new List<SyncError>
                {
                    new SyncError
                    {
                        ErrorType = "Exception",
                        Message = ex.Message,
                        ErrorTime = DateTime.UtcNow
                    }
                },
                SyncTime = DateTime.UtcNow,
                SyncDuration = TimeSpan.Zero,
                IsSuccess = false,
                Summary = "同步失败"
            };
        }
    }

    /// <summary>
    /// 获取仓库统计数据
    /// </summary>
    public async Task<RegistryStatistics> GetRegistryStatisticsAsync(string? registryId = null)
    {
        try
        {
            _logger.LogInformation("获取仓库统计数据: {RegistryId}", registryId);

            if (string.IsNullOrEmpty(registryId))
            {
                // 获取所有仓库的统计信息
                var registries = await GetRegistriesAsync();
                var totalImages = 0;
                var totalSize = 0L;
                var repositories = 0;
                var officialImages = 0;
                var privateImages = 0;
                var topRepositories = new List<string>();

                foreach (var registry in registries)
                {
                    var stats = await GetRegistryStatisticsAsync(registry.Id);
                    totalImages += stats.TotalImages;
                    totalSize += stats.TotalSize;
                    repositories += stats.Repositories;
                    officialImages += stats.OfficialImages;
                    privateImages += stats.PrivateImages;
                    topRepositories.AddRange(stats.TopRepositories.Take(5));
                }

                return new RegistryStatistics
                {
                    RegistryId = "all",
                    RegistryName = "所有仓库",
                    TotalImages = totalImages,
                    TotalSize = totalSize,
                    Repositories = repositories,
                    OfficialImages = officialImages,
                    PrivateImages = privateImages,
                    LastSync = DateTime.UtcNow,
                    SyncCount = registries.Count(),
                    IsHealthy = true,
                    SyncSuccessRate = 100.0,
                    TopRepositories = topRepositories.Distinct().Take(10).ToList(),
                    LastUpdated = DateTime.UtcNow
                };
            }
            else
            {
                // 获取特定仓库的统计信息
                var registry = await GetRegistryByIdAsync(registryId);
                if (registry == null)
                {
                    return new RegistryStatistics
                    {
                        RegistryId = registryId,
                        RegistryName = string.Empty,
                        TotalImages = 0,
                        TotalSize = 0,
                        Repositories = 0,
                        OfficialImages = 0,
                        PrivateImages = 0,
                        LastSync = DateTime.UtcNow,
                        SyncCount = 0,
                        IsHealthy = false,
                        SyncSuccessRate = 0,
                        TopRepositories = new List<string>(),
                        LastUpdated = DateTime.UtcNow
                    };
                }

                var images = await GetRegistryImagesAsync(registryId);
                var repositories = images.Select(i => i.Repository).Distinct().Count();
                var officialImages = images.Count(i => i.IsOfficial);
                var privateImages = images.Count(i => !i.IsOfficial);

                return new RegistryStatistics
                {
                    RegistryId = registryId,
                    RegistryName = registry.Name,
                    TotalImages = images.Count(),
                    TotalSize = images.Sum(i => i.Size),
                    Repositories = repositories,
                    OfficialImages = officialImages,
                    PrivateImages = privateImages,
                    LastSync = DateTime.UtcNow,
                    SyncCount = 1,
                    IsHealthy = true,
                    SyncSuccessRate = 100.0,
                    TopRepositories = images
                        .Where(i => !string.IsNullOrEmpty(i.Repository))
                        .GroupBy(i => i.Repository)
                        .OrderByDescending(g => g.Count())
                        .Take(5)
                        .Select(g => g.Key)
                        .ToList(),
                    LastUpdated = DateTime.UtcNow
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取仓库统计数据失败: {RegistryId}", registryId);
            throw;
        }
    }

    #region 私有方法

    private void InitializeDefaultRegistries()
    {
        // 这里可以初始化一些默认的公共仓库
        // 实际实现中可以从配置文件或数据库加载
    }

    private async Task<List<ImageRegistry>> GetRegistriesFromStorageAsync()
    {
        try
        {
            _logger.LogDebug("从数据库获取镜像仓库列表");
            var registries = _databaseService.Registries.FindAll().ToList();
            _logger.LogDebug("从数据库获取到 {Count} 个镜像仓库", registries.Count);
            return registries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从数据库获取镜像仓库列表失败");
            return new List<ImageRegistry>();
        }
    }

    private async Task SaveRegistryToStorageAsync(ImageRegistry registry)
    {
        try
        {
            _logger.LogDebug("保存镜像仓库到数据库: {Name}", registry.Name);
            _databaseService.Registries.Upsert(registry);
            _logger.LogDebug("镜像仓库保存成功: {Id}", registry.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存镜像仓库到数据库失败: {Name}", registry.Name);
            throw;
        }
    }

    private async Task DeleteRegistryFromStorageAsync(string registryId)
    {
        try
        {
            _logger.LogDebug("从数据库删除镜像仓库: {RegistryId}", registryId);
            var deleted = _databaseService.Registries.Delete(registryId);
            _logger.LogDebug("镜像仓库删除{Result}: {RegistryId}", deleted > 0 ? "成功" : "失败", registryId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从数据库删除镜像仓库失败: {RegistryId}", registryId);
            throw;
        }
    }

    private async Task SaveRegistriesToStorageAsync(List<ImageRegistry> registries)
    {
        try
        {
            _logger.LogDebug("批量保存 {Count} 个镜像仓库到数据库", registries.Count);
            foreach (var registry in registries)
            {
                _databaseService.Registries.Upsert(registry);
            }
            _logger.LogDebug("批量保存镜像仓库成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量保存镜像仓库到数据库失败");
            throw;
        }
    }

    private async Task<IEnumerable<RegistryImage>> GetImagesFromRegistryAsync(ImageRegistry registry, string? search)
    {
        try
        {
            var result = await SearchRegistryImagesAsync(registry.Id, search ?? "", limit: 100);
            return result.Results.Select(r => new RegistryImage
            {
                Name = r.Name,
                Repository = r.Name,
                Description = r.Description,
                Tags = r.Tags,
                IsOfficial = r.IsOfficial,
                IsAutomated = r.IsAutomated
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取仓库镜像列表失败: {Domain}", registry.Domain);
            return new List<RegistryImage>();
        }
    }

    /// <summary>
    /// 搜索仓库镜像 - 使用 Docker Registry V2 API
    /// </summary>
    public async Task<RegistrySearchResponse> SearchRegistryImagesAsync(string registryId, string query, int limit = 20, int offset = 0)
    {
        var registry = await GetRegistryByIdAsync(registryId);
        if (registry == null)
        {
            return new RegistrySearchResponse
            {
                Results = new List<RegistrySearchResultItem>(),
                Total = 0,
                Query = query,
                RegistryId = registryId,
                RegistryName = ""
            };
        }

        try
        {
            var allRepos = await GetCatalogFromRegistryAsync(registry);
            
            // 过滤搜索结果
            var filtered = string.IsNullOrWhiteSpace(query) 
                ? allRepos 
                : allRepos.Where(r => r.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

            var total = filtered.Count;
            var paged = filtered.Skip(offset).Take(limit).ToList();

            // 获取每个仓库的标签
            var results = new List<RegistrySearchResultItem>();
            foreach (var repo in paged)
            {
                var tags = await GetTagsFromRegistryAsync(registry, repo);
                results.Add(new RegistrySearchResultItem
                {
                    Name = repo,
                    Description = "",
                    Tags = tags,
                    IsOfficial = false,
                    IsAutomated = false
                });
            }

            return new RegistrySearchResponse
            {
                Results = results,
                Total = total,
                Query = query,
                RegistryId = registryId,
                RegistryName = registry.Name
            };
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("仓库认证失败或权限不足: {RegistryId}", registryId);
            return new RegistrySearchResponse
            {
                Results = new List<RegistrySearchResultItem>(),
                Total = 0,
                Query = query,
                RegistryId = registryId,
                RegistryName = registry.Name
            };
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("401"))
        {
            _logger.LogWarning("仓库认证失败: {RegistryId}", registryId);
            return new RegistrySearchResponse
            {
                Results = new List<RegistrySearchResultItem>(),
                Total = 0,
                Query = query,
                RegistryId = registryId,
                RegistryName = registry.Name
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "仓库不支持搜索或搜索失败: {RegistryId}", registryId);
            return new RegistrySearchResponse
            {
                Results = new List<RegistrySearchResultItem>(),
                Total = 0,
                Query = query,
                RegistryId = registryId,
                RegistryName = registry.Name
            };
        }
    }

    /// <summary>
    /// 从仓库获取目录列表 (Docker Registry V2 API)
    /// </summary>
    private async Task<List<string>> GetCatalogFromRegistryAsync(ImageRegistry registry)
    {
        try
        {
            var baseUrl = registry.IsSecure ? $"https://{registry.Domain}" : $"http://{registry.Domain}";
            var url = $"{baseUrl}/v2/_catalog?n=1000";

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            // 添加认证
            if (!string.IsNullOrEmpty(registry.Username) && !string.IsNullOrEmpty(registry.Password))
            {
                var authBytes = Encoding.ASCII.GetBytes($"{registry.Username}:{registry.Password}");
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
            }

            var response = await client.GetAsync(url);
            
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                // 可能需要获取 token
                var wwwAuthenticate = response.Headers.WwwAuthenticate.FirstOrDefault();
                if (wwwAuthenticate != null)
                {
                    var token = await GetAuthTokenAsync(wwwAuthenticate.Parameter, registry);
                    if (!string.IsNullOrEmpty(token))
                    {
                        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                        response = await client.GetAsync(url);
                    }
                    else
                    {
                        // Token 获取失败，返回空列表
                        return new List<string>();
                    }
                }
                else
                {
                    // 没有 WWW-Authenticate 头，返回空列表
                    return new List<string>();
                }
            }

            if (!response.IsSuccessStatusCode)
            {
                // 其他错误，返回空列表
                return new List<string>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(content);
            
            if (json.RootElement.TryGetProperty("repositories", out var reposElement))
            {
                return reposElement.EnumerateArray().Select(r => r.GetString() ?? "").ToList();
            }

            return new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "获取仓库目录失败: {Domain}", registry.Domain);
            return new List<string>();
        }
    }

    /// <summary>
    /// 获取仓库中镜像的标签列表
    /// </summary>
    private async Task<List<string>> GetTagsFromRegistryAsync(ImageRegistry registry, string repository)
    {
        try
        {
            var baseUrl = registry.IsSecure ? $"https://{registry.Domain}" : $"http://{registry.Domain}";
            var url = $"{baseUrl}/v2/{repository}/tags/list";

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            if (!string.IsNullOrEmpty(registry.Username) && !string.IsNullOrEmpty(registry.Password))
            {
                var authBytes = Encoding.ASCII.GetBytes($"{registry.Username}:{registry.Password}");
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
            }

            var response = await client.GetAsync(url);
            
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var wwwAuthenticate = response.Headers.WwwAuthenticate.FirstOrDefault();
                if (wwwAuthenticate != null)
                {
                    var token = await GetAuthTokenAsync(wwwAuthenticate.Parameter, registry);
                    if (!string.IsNullOrEmpty(token))
                    {
                        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                        response = await client.GetAsync(url);
                    }
                }
            }

            if (!response.IsSuccessStatusCode)
            {
                return new List<string>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(content);
            
            if (json.RootElement.TryGetProperty("tags", out var tagsElement))
            {
                return tagsElement.EnumerateArray().Select(t => t.GetString() ?? "").ToList();
            }

            return new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取镜像标签失败: {Repository}", repository);
            return new List<string>();
        }
    }

    /// <summary>
    /// 获取认证 Token (用于需要 Bearer 认证的仓库)
    /// </summary>
    private async Task<string?> GetAuthTokenAsync(string? wwwAuthenticateParam, ImageRegistry registry)
    {
        if (string.IsNullOrEmpty(wwwAuthenticateParam))
            return null;

        try
        {
            // 解析 WWW-Authenticate 参数，格式如: realm="https://auth.example.com/token",service="registry",scope="registry:catalog:*"
            var parameters = new Dictionary<string, string>();
            var parts = wwwAuthenticateParam.Split(',');
            foreach (var part in parts)
            {
                var keyValue = part.Split('=');
                if (keyValue.Length == 2)
                {
                    var key = keyValue[0].Trim();
                    var value = keyValue[1].Trim('"');
                    parameters[key] = value;
                }
            }

            if (!parameters.TryGetValue("realm", out var realm))
                return null;

            var tokenUrl = $"{realm}?service={parameters.GetValueOrDefault("service", "")}&scope={parameters.GetValueOrDefault("scope", "")}";

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            if (!string.IsNullOrEmpty(registry.Username) && !string.IsNullOrEmpty(registry.Password))
            {
                var authBytes = Encoding.ASCII.GetBytes($"{registry.Username}:{registry.Password}");
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
            }

            var response = await client.GetAsync(tokenUrl);
            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(content);

            if (json.RootElement.TryGetProperty("token", out var tokenElement))
            {
                return tokenElement.GetString();
            }
            else if (json.RootElement.TryGetProperty("access_token", out var accessTokenElement))
            {
                return accessTokenElement.GetString();
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取认证 Token 失败");
            return null;
        }
    }

    private async Task<bool> ValidateRegistryCredentialsAsync(ImageRegistry registry)
    {
        if (!string.IsNullOrWhiteSpace(registry.Username) && !string.IsNullOrWhiteSpace(registry.Password))
        {
            var authResult = await _containerEngine.AuthenticateRegistryAsync(registry.Domain, registry.Username, registry.Password);
            return authResult.IsValid;
        }

        var registryUrl = registry.IsSecure ? $"https://{registry.Domain}" : $"http://{registry.Domain}";
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        using var response = await client.GetAsync($"{registryUrl}/v2/");
        return response.IsSuccessStatusCode;
    }

    private string DetermineRegistryType(string domain)
    {
        if (domain.Contains("docker.io") || domain.Contains("registry-1.docker.io"))
        {
            return "DockerHub";
        }
        else if (domain.Contains("harbor"))
        {
            return "Harbor";
        }
        else if (domain.Contains("nexus"))
        {
            return "Nexus";
        }
        else if (domain.Contains("gcr.io"))
        {
            return "GCR";
        }
        else if (domain.Contains("quay.io"))
        {
            return "Quay";
        }
        else if (domain.Contains("aliyuncs.com"))
        {
            return "Aliyun";
        }
        else if (domain.Contains("tencentyun.com") || domain.Contains("ccr.ccs.tencentyun.com"))
        {
            return "Tencent";
        }
        else
        {
            return "Custom";
        }
    }

    #endregion
}