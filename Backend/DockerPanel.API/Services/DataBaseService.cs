using TinyDb;
using TinyDb.Core;
using TinyDb.Collections;
using TinyDb.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using DockerPanel.API.Extensions;
using DockerPanel.API.Models;

namespace DockerPanel.API.Services;

/// <summary>
/// TinyDb 数据库服务
/// </summary>
public class DataBaseService : IDisposable
{
    private readonly ILogger<DataBaseService> _logger;
    private readonly TinyDbEngine _database;
    private readonly string _databasePath;

    public DataBaseService(ILogger<DataBaseService> logger, TinyDbEngine database, IConfiguration configuration)
    {
        _logger = logger;
        _database = database;
        _databasePath = ServiceCollectionExtensions.ResolveTinyDbPath(configuration);

        // 确保数据目录存在
        var dataDirectory = Path.GetDirectoryName(_databasePath);
        if (!Directory.Exists(dataDirectory))
        {
            Directory.CreateDirectory(dataDirectory!);
        }

        _logger.LogInformation("DataBaseService 使用共享 TinyDb 主数据库: {DatabasePath}", _databasePath);

        // TinyDb 使用属性自动创建索引，无需手动创建

        // 初始化默认数据
        InitializeDefaultData();
    }

    /// <summary>
    /// 初始化默认数据
    /// </summary>
    private void InitializeDefaultData()
    {
        try
        {
            // 注释掉默认数据初始化，系统将从空白状态开始
            // InitializeDefaultComposeFiles();
            // InitializeDefaultRegistries();
            _logger.LogInformation("数据库已初始化，无默认数据");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化默认数据失败");
            throw;
        }
    }

    /// <summary>
    /// 初始化默认 Compose 文件
    /// </summary>
    private void InitializeDefaultComposeFiles()
    {
        var composeFiles = _database.GetCollection<ComposeFile>("compose_files");

        // 检查是否已有数据
        if (composeFiles.Count() > 0)
        {
            _logger.LogInformation("Compose 文件数据已存在，跳过初始化");
            return;
        }

        var defaultComposeFiles = new[]
        {
            new ComposeFile
            {
                Id = "compose-1",
                Name = "Web应用示例",
                Description = "一个包含Nginx和Redis的Web应用示例",
                Content = @"version: '3.8'
services:
  web:
    image: nginx:alpine
    ports:
      - '80:80'
    depends_on:
      - redis
  redis:
    image: redis:alpine
    ports:
      - '6379:6379'",
                Path = "/opt/docker-compose/web-app/docker-compose.yml",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = "system",
                UpdatedBy = "system",
                FileSize = 1024,
                Hash = "sha256:abc123",
                IsActive = true,
                Status = ComposeStatus.Running,
                Version = "3.8",
                Services = new List<string> { "web", "redis" },
                Networks = new List<string> { "default" },
                Volumes = new List<string>(),
                NodeName = "localhost"
            },
            new ComposeFile
            {
                Id = "compose-2",
                Name = "数据库应用",
                Description = "PostgreSQL数据库服务",
                Content = @"version: '3.8'
services:
  db:
    image: postgres:15
    environment:
      POSTGRES_DB: myapp
      POSTGRES_USER: admin
      POSTGRES_PASSWORD: password
    ports:
      - '5432:5432'
    volumes:
      - postgres_data:/var/lib/postgresql/data
volumes:
  postgres_data:",
                Path = "/opt/docker-compose/database/docker-compose.yml",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = "system",
                UpdatedBy = "system",
                FileSize = 512,
                Hash = "sha256:def456",
                IsActive = false,
                Status = ComposeStatus.Stopped,
                Version = "3.8",
                Services = new List<string> { "db" },
                Networks = new List<string> { "default" },
                Volumes = new List<string> { "postgres_data" },
                NodeName = "localhost"
            }
        };

        foreach (var file in defaultComposeFiles)
        {
            composeFiles.Insert(file);
        }
        _logger.LogInformation("已初始化 {Count} 个默认 Compose 文件", defaultComposeFiles.Length);
    }

    /// <summary>
    /// 初始化默认镜像仓库
    /// </summary>
    private void InitializeDefaultRegistries()
    {
        var registries = _database.GetCollection<ImageRegistry>("image_registries");

        // 检查是否已有数据
        if (registries.Count() > 0)
        {
            _logger.LogInformation("镜像仓库数据已存在，跳过初始化");
            return;
        }

        var defaultRegistries = new[]
        {
            new ImageRegistry
            {
                Id = "registry-docker-hub",
                Name = "Docker Hub",
                Description = "Docker 官方公共镜像仓库",
                Domain = "registry-1.docker.io",
                Type = "DockerHub",
                IsPublic = true,
                IsDefault = true,
                IsSecure = true,
                Username = "",
                Password = "",
                Email = "",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = "system",
                UpdatedBy = "system",
                Status = "Active",
                Configuration = new RegistryConfig
                {
                    ApiVersion = "v2",
                    Namespace = null,
                    Mirrors = new List<string>(),
                    InsecureRegistries = new List<string>()
                },
                Metadata = new Dictionary<string, object>()
            },
            new ImageRegistry
            {
                Id = "registry-harbor-example",
                Name = "Harbor 私有仓库",
                Description = "企业级 Docker 私有镜像仓库示例",
                Domain = "harbor.example.com",
                Type = "Harbor",
                IsPublic = false,
                IsDefault = false,
                IsSecure = true,
                Username = "admin",
                Password = "Harbor12345",
                Email = "admin@example.com",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = "system",
                UpdatedBy = "system",
                Status = "Active",
                Configuration = new RegistryConfig
                {
                    ApiVersion = "v2",
                    Namespace = "library",
                    Mirrors = new List<string> { "https://registry-1.docker.io" },
                    InsecureRegistries = new List<string>()
                },
                Metadata = new Dictionary<string, object>
                {
                    ["project"] = "default",
                    ["replication"] = "enabled"
                }
            },
            new ImageRegistry
            {
                Id = "registry-nexus-example",
                Name = "Nexus 仓库",
                Description = "Sonatype Nexus Docker 仓库示例",
                Domain = "nexus.example.com",
                Type = "Nexus",
                IsPublic = false,
                IsDefault = false,
                IsSecure = true,
                Username = "admin",
                Password = "admin123",
                Email = "admin@example.com",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = "system",
                UpdatedBy = "system",
                Status = "Active",
                Configuration = new RegistryConfig
                {
                    ApiVersion = "v2",
                    Namespace = "docker",
                    Mirrors = new List<string>(),
                    InsecureRegistries = new List<string>()
                },
                Metadata = new Dictionary<string, object>
                {
                    ["repository"] = "docker-hosted",
                    ["proxy"] = "docker-proxy"
                }
            }
        };

        foreach (var registry in defaultRegistries)
        {
            registries.Insert(registry);
        }
        _logger.LogInformation("已初始化 {Count} 个默认镜像仓库", defaultRegistries.Length);
    }

    /// <summary>
    /// 获取 Compose 文件集合
    /// </summary>
    public ITinyCollection<ComposeFile> ComposeFiles => _database.GetCollection<ComposeFile>("compose_files");

    /// <summary>
    /// 获取 Compose 项目集合
    /// </summary>
    public ITinyCollection<ComposeProject> ComposeProjects => _database.GetCollection<ComposeProject>("compose_projects");

    /// <summary>
    /// 获取镜像仓库集合
    /// </summary>
    public ITinyCollection<ImageRegistry> Registries => _database.GetCollection<ImageRegistry>("image_registries");

    /// <summary>
    /// 获取 SSH 连接配置集合
    /// </summary>
    public ITinyCollection<SshConnectionConfigEntity> SshConnections => _database.GetCollection<SshConnectionConfigEntity>("ssh_connections");

    /// <summary>
    /// 获取 SSH 密钥对集合
    /// </summary>
    public ITinyCollection<SshKeyPair> SshKeyPairs => _database.GetCollection<SshKeyPair>("ssh_keypairs");

    /// <summary>
    /// 获取 SSH 操作日志集合
    /// </summary>
    public ITinyCollection<SshOperationLog> SshOperationLogs => _database.GetCollection<SshOperationLog>("ssh_operation_logs");

    /// <summary>
    /// 获取 SSH 主机密钥集合
    /// </summary>
    public ITinyCollection<SshHostKey> SshHostKeys => _database.GetCollection<SshHostKey>("ssh_host_keys");

    /// <summary>
    /// 获取数据库统计信息
    /// </summary>
    public Dictionary<string, long> GetStatistics()
    {
        return new Dictionary<string, long>
        {
            ["ComposeFiles"] = ComposeFiles.Count(),
            ["ComposeProjects"] = ComposeProjects.Count(),
            ["Registries"] = Registries.Count(),
            ["SshConnections"] = SshConnections.Count(),
            ["SshKeyPairs"] = SshKeyPairs.Count(),
            ["SshHostKeys"] = SshHostKeys.Count()
        };
    }

    /// <summary>
    /// 备份数据库
    /// </summary>
    public string BackupDatabase()
    {
        var backupPath = Path.Combine(
            Path.GetDirectoryName(_databasePath)!,
            $"dockerpanel_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.db"
        );

        using var source = new FileStream(_databasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var destination = new FileStream(backupPath, FileMode.Create, FileAccess.Write, FileShare.None);
        source.CopyTo(destination);
        _logger.LogInformation("数据库已备份到: {BackupPath}", backupPath);

        return backupPath;
    }

    /// <summary>
    /// 恢复数据库
    /// </summary>
    public void RestoreDatabase(string backupPath)
    {
        if (!File.Exists(backupPath))
        {
            throw new FileNotFoundException("备份文件不存在", backupPath);
        }

        _logger.LogWarning("当前服务使用共享 TinyDb 引擎，不能在运行时恢复数据库: {BackupPath}", backupPath);
        throw new NotSupportedException("当前数据库由共享 TinyDb 引擎管理，不能在运行时替换数据库文件。请停止服务后恢复备份，再重新启动应用。");
    }

    public void Dispose()
    {
        _logger.LogDebug("DataBaseService 已释放，TinyDb 引擎生命周期由 DI 容器管理");
    }
}
