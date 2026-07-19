using System;
using System.IO;

namespace DockerPanel.API.Utils;

public static class AppPathResolver
{
    public static string GetDataDirectory()
    {
        var envDir = Environment.GetEnvironmentVariable("DOCKERPANEL_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(envDir))
        {
            return envDir;
        }

        return Path.Combine(AppContext.BaseDirectory, "Data");
    }

    public static string GetDbPath() => Path.Combine(GetDataDirectory(), "DockerPanel.db");
    
    public static string GetJwtSecretPath() => Path.Combine(GetDataDirectory(), "jwt-secret.key");
    
    public static string GetDefaultCertPath() => Path.Combine(GetDataDirectory(), "default-cert.pfx");
}
