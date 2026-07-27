using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DockerPanel.API.Services;

/// <summary>
/// 多语言本地化服务接口
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// 获取当前请求的语言
    /// </summary>
    string GetCurrentLanguage();

    /// <summary>
    /// 设置当前请求的语言
    /// </summary>
    void SetCurrentLanguage(string language);

    /// <summary>
    /// 获取本地化的错误消息
    /// </summary>
    string GetErrorMessage(string code, params object[] args);

    /// <summary>
    /// 获取本地化的消息
    /// </summary>
    string GetMessage(string key, string? defaultValue = null);
}

/// <summary>
/// 语言包 JSON 序列化上下文（源生成，兼容 AOT/裁剪）
/// </summary>
[JsonSourceGenerationOptions(ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class LocalizationJsonContext : JsonSerializerContext
{
}

/// <summary>
/// 多语言本地化服务实现
/// <para>
/// 语言包来源（按顺序加载，后者按 key 覆盖前者）：
/// 1. 程序集内嵌资源 Locales/*.json —— 始终存在，作为兜底；
/// 2. 应用目录下的 Locales/*.json —— 可选，供用户挂载卷自定义或新增语言。
/// </para>
/// </summary>
public class LocalizationService : ILocalizationService
{
    private static readonly ConcurrentDictionary<string, Dictionary<string, string>> _translations = new();
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string DefaultLanguage = "zh-CN";
    private const string LanguageKey = "CurrentLanguage";
    private const string LocalesDirectoryName = "Locales";
    private const string ResourceMarker = ".Locales.";
    private const string JsonExtension = ".json";

    /// <summary>
    /// 全进程仅执行一次，确保静态成员 <see cref="GetTranslatedMessage"/> 在任何时机都可用
    /// </summary>
    static LocalizationService()
    {
        LoadEmbeddedTranslations();
        LoadExternalTranslations();
    }

    public LocalizationService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// 已加载的语言列表
    /// </summary>
    public static IReadOnlyCollection<string> SupportedLanguages => _translations.Keys.ToArray();

    private static void LoadEmbeddedTranslations()
    {
        var assembly = typeof(LocalizationService).Assembly;

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            var markerIndex = resourceName.IndexOf(ResourceMarker, StringComparison.Ordinal);
            if (markerIndex < 0 || !resourceName.EndsWith(JsonExtension, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var start = markerIndex + ResourceMarker.Length;
            var language = resourceName[start..^JsonExtension.Length];
            if (string.IsNullOrWhiteSpace(language))
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                continue;
            }

            LoadInto(language, stream, $"内嵌资源 {resourceName}");
        }
    }

    private static void LoadExternalTranslations()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, LocalesDirectoryName);
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*" + JsonExtension))
        {
            var language = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrWhiteSpace(language))
            {
                continue;
            }

            try
            {
                using var stream = File.OpenRead(file);
                LoadInto(language, stream, file);
            }
            catch (Exception ex)
            {
                // 外部语言包不可读不应阻断启动，内嵌兜底仍然有效
                Console.Error.WriteLine($"[Localization] 读取语言文件失败 {file}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 反序列化并按 key 合并进指定语言（已存在的条目被覆盖，未提及的条目保留）
    /// </summary>
    private static void LoadInto(string language, Stream stream, string source)
    {
        Dictionary<string, string>? incoming;
        try
        {
            incoming = JsonSerializer.Deserialize(stream, LocalizationJsonContext.Default.DictionaryStringString);
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"[Localization] 语言文件格式错误 {source}: {ex.Message}");
            return;
        }

        if (incoming is null || incoming.Count == 0)
        {
            return;
        }

        if (_translations.TryGetValue(language, out var existing))
        {
            var merged = new Dictionary<string, string>(existing, StringComparer.OrdinalIgnoreCase);
            foreach (var pair in incoming)
            {
                merged[pair.Key] = pair.Value;
            }
            _translations[language] = merged;
        }
        else
        {
            _translations[language] = new Dictionary<string, string>(incoming, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 静态方法：根据指定语言获取翻译消息（供 SignalR Hub 等无法依赖注入的场景使用）
    /// </summary>
    public static string GetTranslatedMessage(string key, string language, string? defaultValue = null)
    {
        return Lookup(key, NormalizeLanguage(language), defaultValue);
    }

    public string GetCurrentLanguage()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Items.TryGetValue(LanguageKey, out var language) == true && language is string lang)
        {
            return lang;
        }
        return DefaultLanguage;
    }

    public void SetCurrentLanguage(string language)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            httpContext.Items[LanguageKey] = NormalizeLanguage(language);
        }
    }

    public string GetErrorMessage(string code, params object[] args)
    {
        var key = $"error.{code}";
        var message = GetMessage(key);

        if (args.Length > 0 && message != key)
        {
            try
            {
                return string.Format(message, args);
            }
            catch (FormatException)
            {
                return message;
            }
        }

        return message;
    }

    public string GetMessage(string key, string? defaultValue = null)
    {
        return Lookup(key, GetCurrentLanguage(), defaultValue);
    }

    private static string Lookup(string key, string language, string? defaultValue)
    {
        if (_translations.TryGetValue(language, out var translations)
            && translations.TryGetValue(key, out var message))
        {
            return message;
        }

        // 回退默认语言
        if (_translations.TryGetValue(DefaultLanguage, out var defaultTranslations)
            && defaultTranslations.TryGetValue(key, out var fallback))
        {
            return fallback;
        }

        return defaultValue ?? key;
    }

    /// <summary>
    /// 将任意语言标记规范化为已加载的语言：先精确匹配，再按主语言匹配，最后回退默认语言
    /// </summary>
    private static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return DefaultLanguage;
        }

        foreach (var supported in _translations.Keys)
        {
            if (supported.Equals(language, StringComparison.OrdinalIgnoreCase))
            {
                return supported;
            }
        }

        // "zh" / "zh-TW" -> "zh-CN"，"en-GB" -> "en-US"
        var separatorIndex = language.IndexOf('-');
        var primary = separatorIndex > 0 ? language[..separatorIndex] : language;

        foreach (var supported in _translations.Keys)
        {
            if (supported.Equals(primary, StringComparison.OrdinalIgnoreCase)
                || (supported.Length > primary.Length
                    && supported[primary.Length] == '-'
                    && supported.AsSpan(0, primary.Length).Equals(primary, StringComparison.OrdinalIgnoreCase)))
            {
                return supported;
            }
        }

        return DefaultLanguage;
    }
}

/// <summary>
/// 本地化扩展方法
/// </summary>
public static class LocalizationExtensions
{
    /// <summary>
    /// 添加本地化服务
    /// </summary>
    public static IServiceCollection AddLocalizationServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ILocalizationService, LocalizationService>();
        return services;
    }

    /// <summary>
    /// 使用本地化中间件
    /// </summary>
    public static IApplicationBuilder UseLocalization(this IApplicationBuilder app)
    {
        return app.UseMiddleware<LocalizationMiddleware>();
    }
}

/// <summary>
/// 本地化中间件 - 解析 Accept-Language 头
/// </summary>
public class LocalizationMiddleware
{
    private readonly RequestDelegate _next;

    public LocalizationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ILocalizationService localizationService)
    {
        // 格式: "zh-CN, zh;q=0.9, en-US;q=0.8"，只取首选项，避免为超长请求头分配数组
        var acceptLanguage = context.Request.Headers.AcceptLanguage.FirstOrDefault();

        if (!string.IsNullOrEmpty(acceptLanguage))
        {
            var candidate = acceptLanguage.AsSpan();

            var commaIndex = candidate.IndexOf(',');
            if (commaIndex >= 0)
            {
                candidate = candidate[..commaIndex];
            }

            var semicolonIndex = candidate.IndexOf(';');
            if (semicolonIndex >= 0)
            {
                candidate = candidate[..semicolonIndex];
            }

            candidate = candidate.Trim();

            if (!candidate.IsEmpty)
            {
                localizationService.SetCurrentLanguage(candidate.ToString());
            }
        }

        await _next(context);
    }
}
