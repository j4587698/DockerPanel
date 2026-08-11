using System;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace DockerPanel.API.Tests;

/// <summary>
/// 集成测试 WebApplicationFactory：
/// - 每个测试类(ClassFixture)使用独立临时数据目录，互不污染、不触碰开发库；
/// - 覆盖 TinyDb/LiteDB 路径与 JWT 密钥文件到临时目录；
/// - 不依赖 Docker 引擎（CI/本地无 Docker 亦可运行，依赖 Docker 的后台服务自带容错）。
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public string DataDirectory { get; }

    public TestWebApplicationFactory()
    {
        DataDirectory = Path.Combine(Path.GetTempPath(), "dp-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DataDirectory);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // host 级配置（优先级高于 appsettings.json），确保每个测试类使用独立数据库
        var dbPath = Path.Combine(DataDirectory, "test.db");
        builder.UseSetting("TinyDb:Path", dbPath);
        builder.UseSetting("LiteDB:Path", dbPath);
        builder.UseSetting("Auth:JwtSecretFile", Path.Combine(DataDirectory, "jwt-secret.key"));
        builder.UseSetting("Serilog:MinimumLevel:Default", "Warning");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            try
            {
                if (Directory.Exists(DataDirectory))
                {
                    Directory.Delete(DataDirectory, true);
                }
            }
            catch (IOException)
            {
                // 忽略清理失败（Windows 文件句柄释放延迟）
            }
        }
    }
}
