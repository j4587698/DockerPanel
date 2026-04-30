using DockerPanel.API.Extensions;
using DockerPanel.API.Services;
using DockerPanel.API.Services.Acme;
using DockerPanel.API.Serialization;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Yarp.ReverseProxy.Configuration;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using System.Net.Security;
using System.Security.Authentication;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Reflection;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

var jwtSecretProvider = new JwtSecretProvider(builder.Configuration, builder.Environment);
var loggingLevelSwitch = new LoggingLevelSwitch(LogEventLevel.Information);
builder.Services.AddSingleton(loggingLevelSwitch);



// 配置Serilog
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .MinimumLevel.ControlledBy(services.GetRequiredService<LoggingLevelSwitch>())
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// 添加服务到容器
builder.Services.AddControllers(options =>
    {
        options.Filters.Add(new AuthorizeFilter());
        options.Filters.Add<RoleWriteAccessFilter>();
        options.Filters.AddService<OperationAuditFilter>();
    })
    .AddJsonOptions(options =>
    {
        // 将枚举序列化为字符串而不是整数值
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// 添加Swagger/OpenAPI支持
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddDockerPanelSwagger();

// 添加CORS支持
builder.Services.AddDockerPanelCors(builder.Configuration);

// 添加API版本控制
builder.Services.AddDockerPanelApiVersioning();

// 添加TinyDB数据库
builder.Services.AddTinyDb(builder.Configuration);

// 添加健康检查
builder.Services.AddDockerPanelHealthChecks(builder.Configuration);

// 添加AutoMapper
builder.Services.AddDockerPanelAutoMapper();

// 添加FluentValidation
builder.Services.AddDockerPanelValidation();

// 添加SignalR
builder.Services.AddDockerPanelSignalR();

// 添加内存缓存
builder.Services.AddMemoryCache();

// 添加登录认证和 JWT 鉴权
builder.Services.AddSingleton(jwtSecretProvider);
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSecretProvider.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSecretProvider.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwtSecretProvider.CreateSigningKey(),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrWhiteSpace(accessToken) &&
                    (path.StartsWithSegments("/dockerpanelHub") ||
                     path.StartsWithSegments("/sshTerminalHub") ||
                     path.StartsWithSegments("/containerTerminalHub")))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrWhiteSpace(userId))
                {
                    context.Fail("Invalid user token.");
                    return Task.CompletedTask;
                }

                var dbContext = context.HttpContext.RequestServices.GetRequiredService<DockerPanel.API.Data.TinyDbContext>();
                var user = dbContext.UserAccounts.FindById(userId);
                if (user is not { IsActive: true })
                {
                    context.Fail("User is inactive or deleted.");
                    return Task.CompletedTask;
                }

                var tokenRole = context.Principal?.FindFirst(ClaimTypes.Role)?.Value;
                var normalizedRole = DockerPanel.API.Models.AuthRoles.Normalize(user.Role);
                if (!string.Equals(user.Role, normalizedRole, StringComparison.Ordinal))
                {
                    user.Role = normalizedRole;
                    user.UpdatedAt = DateTime.UtcNow;
                    dbContext.UserAccounts.Update(user);
                }

                if (!string.Equals(tokenRole, normalizedRole, StringComparison.Ordinal))
                {
                    context.Fail("User role has changed.");
                    return Task.CompletedTask;
                }

                var path = context.HttpContext.Request.Path;
                if (user.MustChangePassword &&
                    !path.StartsWithSegments("/api/auth/me") &&
                    !path.StartsWithSegments("/api/auth/change-password") &&
                    !path.StartsWithSegments("/api/auth/logout"))
                {
                    context.Fail("Password change required.");
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// 添加多语言本地化服务
builder.Services.AddLocalizationServices();

// 添加证书配置
builder.Services.Configure<DockerPanel.API.Models.CertificateSettings>(
    builder.Configuration.GetSection("Certificate"));

// 注册 Docker 引擎
builder.Services.AddSingleton<DockerEngine>();
// 将 IContainerEngine 绑定到 DockerEngine
builder.Services.AddSingleton<IContainerEngine>(sp => sp.GetRequiredService<DockerEngine>());

// 注册容器引擎工厂和管理器
builder.Services.AddSingleton<ContainerEngineFactory>();
builder.Services.AddSingleton<ContainerEngineManager>();

// 服务实现
builder.Services.AddScoped<IContainerService, ContainerService>();
builder.Services.AddScoped<INodeService, NodeService>();
builder.Services.AddScoped<IVolumeService, VolumeService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<INetworkService, NetworkService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IOperationAuditService, OperationAuditService>();
builder.Services.AddScoped<OperationAuditFilter>();

// 注册数据库服务
builder.Services.AddSingleton<DataBaseService>();

// 镜像仓库服务
builder.Services.AddScoped<IRegistryService, RegistryService>();

// Compose服务
builder.Services.AddScoped<IComposeService, ComposeService>();

// Compose部署服务 - 使用 Compose.NET 解析，Docker.DotNet 部署
builder.Services.AddScoped<IComposeDeployService, ComposeDeployService>();

// SSH服务
builder.Services.AddScoped<ISshService, SshService>();

// 节点资源监控服务
builder.Services.AddScoped<INodeResourceService, NodeResourceServiceImpl>();

// 节点分组和标签管理服务
builder.Services.AddScoped<INodeGroupService, NodeGroupServiceImpl>();

// 添加ACME证书管理服务 - 使用包含续期功能的实现
builder.Services.AddScoped<DockerPanel.API.Services.Acme.IAcmeService, DockerPanel.API.Services.Acme.CertesAcmeService>();
// 移除重复注册
// builder.Services.AddScoped<DockerPanel.API.Services.Acme.CertesAcmeService>();
// 使用 TinyDb 持久化存储替换内存存储，解决挑战文件丢失问题，使用主数据库
builder.Services.AddSingleton<DockerPanel.API.Services.Acme.IAcmeChallengeStore, DockerPanel.API.Services.Acme.TinyDbAcmeChallengeStore>();

// 添加挑战验证服务
builder.Services.AddScoped<DockerPanel.API.Services.Acme.IChallengeValidationService, DockerPanel.API.Services.Acme.ChallengeValidationService>();

// 添加 TLS-ALPN-01 挑战服务
builder.Services.AddSingleton<DockerPanel.API.Services.Acme.TlsAlpnChallengeService>();

// 添加 SNI 证书选择器（用于 HTTPS 动态证书选择）
builder.Services.AddSingleton<SniCertificateSelector>();

// 添加通配符证书服务
builder.Services.AddScoped<DockerPanel.API.Services.Acme.IWildcardCertificateService, DockerPanel.API.Services.Acme.WildcardCertificateService>();

// 添加证书管理服务
builder.Services.AddScoped<DockerPanel.API.Services.Acme.ICertificateManagementService, DockerPanel.API.Services.Acme.CertificateManagementService>();

// 添加证书自动申请和续期服务（单例，作为HostedService）
builder.Services.AddSingleton<DockerPanel.API.Services.Acme.CertificateAutoService>();
builder.Services.AddSingleton<DockerPanel.API.Services.Acme.ICertificateAutoService>(sp => sp.GetRequiredService<DockerPanel.API.Services.Acme.CertificateAutoService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<DockerPanel.API.Services.Acme.CertificateAutoService>());

// 添加证书申请进度跟踪服务
builder.Services.AddSingleton<DockerPanel.API.Services.Acme.ICertificateProgressService, DockerPanel.API.Services.Acme.CertificateProgressService>();

// 添加后台任务队列
builder.Services.AddBackgroundTaskQueue();

// 添加后台任务状态管理服务（单例，用于跟踪所有后台任务）
builder.Services.AddSingleton<BackgroundTaskService>();
builder.Services.AddHostedService<BackgroundTaskCleanupService>();

// 启用实时数据推送服务 - 用于推送容器统计信息和系统状态
builder.Services.AddHostedService<RealTimeDataPushService>();

// 日志流推送服务 - 用于实时推送容器日志
builder.Services.AddSingleton<LogStreamingService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<LogStreamingService>());

// 添加网络初始化服务
builder.Services.AddHostedService<NetworkInitializationService>();

// 添加证书超时检查服务
builder.Services.AddHostedService<CertificateTimeoutCheckerService>();

// 添加HttpClient工厂
builder.Services.AddHttpClient();

// 注册DNS提供商服务
builder.Services.AddSingleton<DockerPanel.API.Services.Acme.DnsProviders.CloudflareDnsProvider>();
builder.Services.AddSingleton<DockerPanel.API.Services.Acme.DnsProviders.AliyunDnsProvider>();
builder.Services.AddSingleton<DockerPanel.API.Services.Acme.DnsProviders.TencentDnsProvider>();
builder.Services.AddSingleton<DockerPanel.API.Services.Acme.DnsProviders.DnsPodDnsProvider>();
builder.Services.AddSingleton<DockerPanel.API.Services.Acme.DnsProviders.DnsPodTraditionalDnsProvider>();
builder.Services.AddSingleton<DockerPanel.API.Services.Acme.DnsProviders.AwsRoute53DnsProvider>();
builder.Services.AddSingleton<DockerPanel.API.Services.Acme.DnsProviders.AzureDnsProvider>();
builder.Services.AddSingleton<DockerPanel.API.Services.Acme.DnsProviders.GoDaddyDnsProvider>();

// 添加DockerPanel核心服务
builder.Services.AddDockerPanelServices();

// 添加DockerPanel HttpClient配置
builder.Services.AddDockerPanelHttpClients();

// 添加操作审计日志保留清理服务
builder.Services.AddHostedService<OperationAuditRetentionService>();

// 应用系统日志设置并清理过期日志文件
builder.Services.AddHostedService<LoggingSettingsApplyService>();
builder.Services.AddHostedService<LogFileRetentionService>();

// 添加YARP反向代理支持 - 仅在非Linux环境下启用HttpSys相关功能
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));


// 注册IReverseProxyFactory用于代理服务 - 使用单例模式确保同一实例用于IProxyConfigProvider
builder.Services.AddSingleton<ReverseProxyFactory>();
builder.Services.AddSingleton<IReverseProxyFactory>(sp => sp.GetRequiredService<ReverseProxyFactory>());
builder.Services.AddSingleton<IProxyConfigProvider>(sp => sp.GetRequiredService<ReverseProxyFactory>());

// 注册域名映射服务
builder.Services.AddScoped<DomainMappingService>();

// 注册自动升级服务
builder.Services.AddScoped<IAutoUpdateService, AutoUpdateService>();

// 健康检查已在 AddDockerPanelHealthChecks 扩展方法中注册，此处无需重复注册

// 配置 Kestrel HTTP/HTTPS (SNI 证书选择)
// 默认: HTTP 80 + HTTPS 443，从数据库加载证书，支持 TLS-ALPN-01 挑战
builder.WebHost.ConfigureKestrel((context, options) =>
{
    // 设置最大请求体大小为 1GB（用于上传大文件构建镜像）
    options.Limits.MaxRequestBodySize = 1_073_741_824;
    
    // HTTP 端口（始终启用）
    var httpPort = context.Configuration.GetValue("HTTP_PORT", 80);
    options.ListenAnyIP(httpPort);
    
    // HTTPS 端口（默认启用）
    var httpsPort = context.Configuration.GetValue("HTTPS_PORT", 443);
    var enableHttps = context.Configuration.GetValue("ENABLE_HTTPS", true);
    
    if (enableHttps)
    {
        options.ListenAnyIP(httpsPort, listenOptions =>
        {
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
            listenOptions.UseHttps(new TlsHandshakeCallbackOptions
            {
                OnConnection = connectionContext =>
                {
                    var sniHostName = connectionContext.ClientHelloInfo.ServerName;
                    
                    var options = new SslServerAuthenticationOptions
                    {
                        ServerCertificateSelectionCallback = (sender, hostName) =>
                        {
                            return SniCertificateSelectorLocator.Instance?.SelectCertificate(hostName ?? sniHostName)!;
                        },
                        // 支持 acme-tls/1 协议用于 TLS-ALPN-01 挑战验证
                        // 同时保留 h2 和 http/1.1 用于正常 HTTPS 流量
                        ApplicationProtocols = new List<SslApplicationProtocol>
                        {
                            new SslApplicationProtocol(TlsAlpnChallengeService.AcmeTlsAlpnProtocol),
                            SslApplicationProtocol.Http2,
                            SslApplicationProtocol.Http11
                        },
                        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    };
                    
                    return ValueTask.FromResult(options);
                }
            });
        });
    }
});

// API versioning is not enabled because current routes are unversioned and YARP proxy endpoints share the same pipeline.
// var apiVersioningBuilder = builder.Services.AddApiVersioning(options =>
// {
//     options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
//     options.AssumeDefaultVersionWhenUnspecified = true;
//     options.ReportApiVersions = true;
// });

// apiVersioningBuilder.AddApiExplorer(options =>
// {
//     options.GroupNameFormat = "'v'VVV";
//     options.SubstituteApiVersionInUrl = true;
// });


var app = builder.Build();

// 配置请求管道
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "DockerPanel API V1");
        c.RoutePrefix = "swagger";
        c.DisplayRequestDuration();
        c.EnableDeepLinking();
        c.ShowExtensions();
        c.ShowCommonExtensions();
    });

    // 开发环境允许详细错误页面
    app.UseDeveloperExceptionPage();
}
else
{
    // 生产环境使用异常处理中间件
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

// 使用HTTPS重定向（仅生产环境，开发环境禁用以便代理）
if (!app.Environment.IsDevelopment() && app.Configuration.GetValue("ENABLE_HTTPS", true))
{
    app.UseHttpsRedirection();
}

// 基础安全响应头。CSP 仅在非开发环境启用，避免影响 Swagger UI 调试。
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.TryAdd("X-Content-Type-Options", "nosniff");
    headers.TryAdd("X-Frame-Options", "DENY");
    headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
    headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");

    if (!app.Environment.IsDevelopment())
    {
        headers.TryAdd("Content-Security-Policy",
            "default-src 'self'; " +
            "script-src 'self'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: blob:; " +
            "font-src 'self' data:; " +
            "connect-src 'self' ws: wss: http: https:; " +
            "frame-ancestors 'none'; " +
            "base-uri 'self'; " +
            "form-action 'self'");
    }

    await next();
});

// 服务前端静态资源。Vite 生成的 /assets/* 文件名带 hash，可安全长期缓存；其它静态文件不缓存。
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        var headers = context.Context.Response.Headers;
        if (context.Context.Request.Path.StartsWithSegments("/assets"))
        {
            headers.CacheControl = "public,max-age=31536000,immutable";
        }
        else
        {
            headers.CacheControl = "no-cache";
        }
    }
});

// 添加请求日志中间件
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/images/build"))
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("请求到达: {Method} {Path}, ContentType: {ContentType}", 
            context.Request.Method, context.Request.Path, context.Request.ContentType);
    }
    await next();
});

// 使用多语言本地化中间件
app.UseLocalization();

// 使用CORS
app.UseCors();

// 使用路由
app.UseRouting();

// 全局错误处理（必须在控制器之前，以捕获控制器异常）
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        Log.Error(ex, "全局未处理异常");

        context.Response.ContentType = "application/json";

        // 尝试获取本地化服务
        var localizationService = context.RequestServices.GetService<ILocalizationService>();
        var errorMessage = localizationService?.GetMessage("error.serverError", "服务器内部错误") ?? "服务器内部错误";
        var defaultMessage = localizationService?.GetMessage("error.contactAdmin", "请联系管理员") ?? "请联系管理员";

        var errorResponse = new
        {
            error = errorMessage,
            message = app.Environment.IsDevelopment() ? ex.Message : defaultMessage,
            timestamp = DateTime.UtcNow,
            path = context.Request.Path
        };

        await context.Response.WriteAsJsonAsync(errorResponse);
    }
});

// 记录请求日志（必须在控制器之前）
app.Use(async (context, next) =>
{
    var startTime = DateTime.UtcNow;

    Log.Information("开始处理请求: {Method} {Path}", context.Request.Method, context.Request.Path);

    try
    {
        await next();

        var duration = DateTime.UtcNow - startTime;
        Log.Information("完成处理请求: {Method} {Path} - 状态码: {StatusCode} - 耗时: {Duration}ms",
            context.Request.Method, context.Request.Path, context.Response.StatusCode, duration.TotalMilliseconds);
    }
    catch (Exception ex)
    {
        var duration = DateTime.UtcNow - startTime;
        Log.Error(ex, "处理请求异常: {Method} {Path} - 耗时: {Duration}ms",
            context.Request.Method, context.Request.Path, duration.TotalMilliseconds);
        throw;
    }
});

app.UseAuthentication();
app.UseAuthorization();

// 启用YARP反向代理 - 使用数据库配置的路由
app.MapReverseProxy();

// 添加直接的ACME挑战端点映射
app.MapGet("/.well-known/acme-challenge/{token}", async (string token, IAcmeChallengeStore challengeStore, ILogger<Program> logger) =>
{
    logger.LogInformation("收到 ACME HTTP-01 挑战请求，Token: {Token}", token);

    var keyAuthorization = await challengeStore.GetHttpChallengeAsync(token);

    if (!string.IsNullOrEmpty(keyAuthorization))
    {
        logger.LogInformation("成功返回 ACME 挑战响应，Token: {Token}", token);
        return Results.Content(keyAuthorization, "text/plain");
    }

    logger.LogWarning("未找到 Token 对应的挑战数据: {Token}", token);
    return Results.NotFound(new { error = "挑战文件不存在", token });
});

// 映射控制器
app.MapControllers();

// 映射SignalR Hub
app.MapHub<DockerPanel.API.Hubs.DockerPanelHub>("/dockerpanelHub").RequireAuthorization();
app.MapHub<DockerPanel.API.Hubs.SshTerminalHub>("/sshTerminalHub").RequireAuthorization();
app.MapHub<DockerPanel.API.Hubs.ContainerTerminalHub>("/containerTerminalHub").RequireAuthorization();

// 健康检查端点
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

// API信息端点
var applicationVersion = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
    ?? "unknown";

app.MapGet("/api/info", (IConfiguration config) => new
{
    Application = "DockerPanel API",
    Version = applicationVersion,
    Environment = app.Environment.EnvironmentName,
    Timestamp = DateTime.UtcNow,
    Configuration = new
    {
        Logging = config["Logging:LogLevel:Default"],
        AllowedHosts = config["AllowedHosts"]
    }
}).RequireAuthorization();

// SPA 后备路由
app.MapFallbackToFile("index.html");

// 初始化 SNI 证书选择器（用于 HTTPS）
var sniSelector = app.Services.GetRequiredService<SniCertificateSelector>();
SniCertificateSelectorLocator.Initialize(sniSelector);
sniSelector.WarmupCache(); // 预热证书缓存

// 启动应用程序
try
{
    Log.Information("正在启动 DockerPanel API 服务...");
    app.Run();
    Log.Information("DockerPanel API 服务已停止");
}
catch (Exception ex)
{
    Log.Fatal(ex, "DockerPanel API 服务启动失败");
}
finally
{
    Log.CloseAndFlush();
}

