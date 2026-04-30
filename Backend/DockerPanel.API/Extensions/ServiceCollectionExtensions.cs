using System.Net.Http;
using TinyDb;
using TinyDb.Core;
using DockerPanel.API.Data;
using DockerPanel.API.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DockerPanel.API.Extensions;

/// <summary>
/// 服务集合扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加TinyDb服务
    /// </summary>
    public static IServiceCollection AddTinyDb(this IServiceCollection services, IConfiguration configuration)
    {
        var dbPath = ResolveTinyDbPath(configuration);
        
        // 确保数据目录存在
        var dataDirectory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dataDirectory) && !Directory.Exists(dataDirectory))
        {
            Directory.CreateDirectory(dataDirectory);
        }

        // 兼容旧版本数据格式，避免索引重建时因重复主键导致启动失败
        TinyDbDataRepair.Repair(dbPath);

        // 注册TinyDb数据库实例
        services.AddSingleton<TinyDbEngine>(new TinyDbEngine(dbPath));

        // 注册数据库上下文
        services.AddScoped<TinyDbContext>(provider =>
        {
            var database = provider.GetRequiredService<TinyDbEngine>();
            var context = new TinyDbContext(database);

            // TinyDb 使用属性自动创建索引，无需手动创建
            return context;
        });

        return services;
    }

    /// <summary>
    /// 解析主 TinyDb 数据库路径，确保所有服务使用同一个数据库文件。
    /// </summary>
    public static string ResolveTinyDbPath(IConfiguration configuration)
    {
        var configuredPath = configuration["TinyDb:Path"] ?? configuration["LiteDB:Path"] ?? "Data/DockerPanel.db";

        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        // 查找项目根目录（包含 appsettings.json 或 .csproj 的目录）
        var baseDirectory = AppContext.BaseDirectory;
        var projectRoot = baseDirectory;
        var normalizedBaseDirectory = baseDirectory
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var binSegment = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}";
        var binIndex = normalizedBaseDirectory.IndexOf(binSegment, StringComparison.OrdinalIgnoreCase);

        if (binIndex > 0)
        {
            projectRoot = baseDirectory.Substring(0, binIndex);
        }
        else
        {
            // 尝试向上查找包含 .csproj 文件的目录；容器发布目录中找不到时保留 AppContext.BaseDirectory。
            var currentDir = new DirectoryInfo(baseDirectory);
            while (currentDir != null && !currentDir.GetFiles("*.csproj").Any())
            {
                currentDir = currentDir.Parent;
            }

            if (currentDir != null)
            {
                projectRoot = currentDir.FullName;
            }
        }

        return Path.Combine(projectRoot, configuredPath);
    }

    /// <summary>
    /// 添加跨域配置
    /// </summary>
    public static IServiceCollection AddDockerPanelCors(this IServiceCollection services, IConfiguration configuration)
    {
        var corsOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "*" };

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                if (corsOrigins.Contains("*"))
                {
                    builder.AllowAnyOrigin()
                           .AllowAnyMethod()
                           .AllowAnyHeader();
                }
                else
                {
                    builder.WithOrigins(corsOrigins)
                           .AllowAnyMethod()
                           .AllowAnyHeader()
                           .AllowCredentials();
                }
            });
        });

        return services;
    }

    /// <summary>
    /// 添加API版本控制
    /// </summary>
    public static IServiceCollection AddDockerPanelApiVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ApiVersionReader = Asp.Versioning.ApiVersionReader.Combine(
                new Asp.Versioning.UrlSegmentApiVersionReader(),
                new Asp.Versioning.QueryStringApiVersionReader("api-version"),
                new Asp.Versioning.HeaderApiVersionReader("X-Version"));
        }).AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        return services;
    }

    /// <summary>
    /// 添加Swagger文档
    /// </summary>
    public static IServiceCollection AddDockerPanelSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new()
            {
                Title = "DockerPanel API",
                Version = "v1",
                Description = "Docker容器管理面板API文档",
                Contact = new()
                {
                    Name = "DockerPanel Team",
                    Email = "support@dockerpanel.com"
                }
            });

            // 包含XML注释
            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }

            // 添加安全定义
            options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
            {
                Description = "JWT授权 (示例: Bearer {token})",
                Name = "Authorization",
                In = Microsoft.OpenApi.ParameterLocation.Header,
                Type = Microsoft.OpenApi.SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });

            options.AddSecurityRequirement(openApiDocument => new Microsoft.OpenApi.OpenApiSecurityRequirement
            {
                {
                    new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", openApiDocument, null),
                    new List<string>()
                }
            });

            // 启用Swagger注释
            options.EnableAnnotations();
        });

        return services;
    }

    /// <summary>
    /// 添加SignalR服务
    /// </summary>
    public static IServiceCollection AddDockerPanelSignalR(this IServiceCollection services)
    {
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = true;
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        })
        .AddJsonProtocol(options =>
        {
            // 使用 camelCase 命名约定，与前端 JavaScript 保持一致
            options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        });

        return services;
    }

    /// <summary>
    /// 添加健康检查
    /// </summary>
    public static IServiceCollection AddDockerPanelHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks()
            .AddCheck("Self", () => HealthCheckResult.Healthy("应用进程正常"), tags: new[] { "live" })
            .AddCheck<Services.DatabaseHealthCheck>("Database", tags: new[] { "ready" })
            .AddCheck<Services.DockerHealthCheck>("Docker", tags: new[] { "ready", "docker" });

        return services;
    }

    /// <summary>
    /// 添加AutoMapper配置
    /// </summary>
    public static IServiceCollection AddDockerPanelAutoMapper(this IServiceCollection services)
    {
        return services;
    }

    /// <summary>
    /// 添加FluentValidation
    /// </summary>
    public static IServiceCollection AddDockerPanelValidation(this IServiceCollection services)
    {
        // FluentValidation 当前未引入，保留扩展点以便后续接入。
        // services.AddValidatorsFromAssemblyContaining<Program>();

        return services;
    }

    /// <summary>
    /// 添加DockerPanel核心服务
    /// </summary>
    public static IServiceCollection AddDockerPanelServices(this IServiceCollection services)
    {
        // 主要服务已在 Program.cs 中按实际实现注册；此扩展仅保留兼容入口。
        // services.AddSingleton<IContainerEngineFactory, ContainerEngineFactory>();
        // services.AddSingleton<IContainerEngine, PodmanEngine>();
        // services.AddScoped<ISettingsService, SimpleSettingsService>();
        // services.AddScoped<IContainerService, SimpleContainerService>();
        // services.AddScoped<IImageService, SimpleImageService>();
        // services.AddScoped<INetworkService, SimpleNetworkService>();
        // services.AddScoped<IVolumeService, SimpleVolumeService>();
        // services.AddScoped<INodeService, SimpleNodeService>();
        // services.AddScoped<IRegistryService, SimpleRegistryService>();
        // services.AddScoped<IComposeService, SimpleComposeService>();
        // services.AddScoped<IEnhancedRegistryService, SimpleEnhancedRegistryService>();
        // services.AddScoped<ISshService, SimpleSshService>();
        // services.AddScoped<INodeGroupService, SimpleNodeGroupService>();
        // services.AddScoped<IProxyService, SimpleProxyService>();

        // 注册ACME服务 - 注意：IAcmeService 在 Program.cs 中已经注册为 CertesAcmeService
        // services.AddScoped<Services.Acme.IAcmeService, Services.Acme.AcmeService>(); // 已移除，使用 CertesAcmeService
        // services.AddScoped<Services.Acme.ICertificateAutoService, Services.Acme.CertificateAutoService>(); // 已移除 - 在 Program.cs 中注册为 Singleton
        services.AddScoped<Services.Acme.ICertificateManagementService, Services.Acme.CertificateManagementService>();
        services.AddScoped<Services.Acme.IWildcardCertificateService, Services.Acme.WildcardCertificateService>();
        services.AddScoped<Services.Acme.IChallengeValidationService, Services.Acme.ChallengeValidationService>();

        // 🔧 修复：移除重复注册，BackgroundTaskQueue 已在 Program.cs 中通过 AddBackgroundTaskQueue() 注册
        // services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>(); // 已移除 - 重复注册会创建多个实例
        // services.AddHostedService<BackgroundTaskQueue>(); // 已移除 - 与上面配合会导致任务队列无法正确工作

        // Background Services


        return services;
    }

    /// <summary>
    /// 添加HttpClient配置
    /// </summary>
    public static IServiceCollection AddDockerPanelHttpClients(this IServiceCollection services)
    {
        services.AddHttpClient("RegistryClient", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "DockerPanel/1.0");
        });

        // AutoUpdateService 专用 HttpClient（不使用代理）
        services.AddHttpClient("AutoUpdateClient", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.Add("User-Agent", "DockerPanel/1.0");
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            UseProxy = false // 禁用代理
        });

        return services;
    }
}