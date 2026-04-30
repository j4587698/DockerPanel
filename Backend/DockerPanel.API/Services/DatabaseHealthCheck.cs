using Microsoft.Extensions.Diagnostics.HealthChecks;
using TinyDb;
using TinyDb.Attributes;
using TinyDb.Core;
using TinyDb.Collections;

namespace DockerPanel.API.Services;

/// <summary>
/// 健康检查实体（用于数据库连接测试）
/// </summary>
[Entity]
public class HealthCheckEntity
{
    public string Id { get; set; } = string.Empty;
    public DateTime CheckTime { get; set; }
}

/// <summary>
/// 数据库健康检查
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly TinyDbEngine _database;

    public DatabaseHealthCheck(TinyDbEngine database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 尝试执行简单的数据库操作
            var userCollection = _database.GetCollection<HealthCheckEntity>("health_check");
            var count = userCollection.Count();

            return Task.FromResult(HealthCheckResult.Healthy(
                "数据库连接正常",
                new Dictionary<string, object>
                {
                    { "DatabaseType", "TinyDb" },
                    { "CollectionCount", count },
                    { "Timestamp", DateTime.UtcNow }
                }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "数据库连接失败",
                ex,
                new Dictionary<string, object>
                {
                    { "ErrorType", ex.GetType().Name },
                    { "ErrorMessage", ex.Message },
                    { "Timestamp", DateTime.UtcNow }
                }));
        }
    }
}

/// <summary>
/// Docker健康检查
/// </summary>
public class DockerHealthCheck : IHealthCheck
{
    private readonly ILogger<DockerHealthCheck> _logger;
    private readonly IContainerEngine _engine;

    public DockerHealthCheck(ILogger<DockerHealthCheck> logger, IContainerEngine engine)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (await _engine.IsAvailableAsync())
            {
                var version = await _engine.GetVersionAsync();
                return HealthCheckResult.Healthy(
                    "Docker daemon 正常",
                    new Dictionary<string, object>
                    {
                        { "Version", version.Version },
                        { "ApiVersion", version.ApiVersion },
                        { "Timestamp", DateTime.UtcNow }
                    });
            }

            return HealthCheckResult.Degraded(
                "Docker daemon 不可用",
                null,
                new Dictionary<string, object>
                {
                    { "Timestamp", DateTime.UtcNow }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Docker健康检查失败");
            return HealthCheckResult.Unhealthy(
                "Docker服务不可用",
                ex,
                new Dictionary<string, object>
                {
                    { "ErrorType", ex.GetType().Name },
                    { "ErrorMessage", ex.Message },
                    { "Timestamp", DateTime.UtcNow }
                });
        }
    }
}