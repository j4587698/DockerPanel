using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Configuration;

namespace DockerPanel.API.Extensions;
/// <summary>
/// AOT fallback: provides weak JsonTypeInfo for binding-only types (IFormFile etc.)
/// that cannot be source-generated, so OpenAPI schema generation works for upload endpoints.
/// </summary>
internal sealed class BindingFallbackJsonTypeInfoResolver : IJsonTypeInfoResolver
{
    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        if (type == typeof(Microsoft.AspNetCore.Http.IFormFile)
            || type == typeof(Microsoft.AspNetCore.Http.IFormFileCollection)
            || type == typeof(Microsoft.AspNetCore.Http.IFormFile[]))
        {
            return JsonTypeInfo.CreateJsonTypeInfo(type, options);
        }

        return null;
    }
}

/// <summary>
/// AOT 安全辅助：把非 AOT 专属的反射路径集中到带守卫的方法里，
/// 通过 [UnconditionalSuppressMessage] 消除保守的分析器警告。
/// 这些调用点在 AOT（IsDynamicCodeSupported == false）下永远不会执行。
/// </summary>
internal static class AotSafeHelpers
{
    /// <summary>
    /// 双模式 JSON 解析器：非 AOT 叠加反射兜底；AOT 仅用源生成上下文。
    /// </summary>
    [UnconditionalSuppressMessage("IL2026", "IL2026",
        Justification = "DefaultJsonTypeInfoResolver 仅在非 AOT（IsDynamicCodeSupported）分支构造，AOT 下不执行")]
    public static IJsonTypeInfoResolver CreateDualModeResolver(IJsonTypeInfoResolver context)
    {
        if (RuntimeFeature.IsDynamicCodeSupported)
        {
            return JsonTypeInfoResolver.Combine(context, new DefaultJsonTypeInfoResolver());
        }
        return context;
    }

    /// <summary>
    /// 原始类型配置读取：直接用索引器 + TryParse，替代 ConfigurationBinder.GetValue
    /// （后者带 RequiresUnreferencedCode，且不被 binder 源生成器覆盖）。
    /// 行为对齐：缺失/空值/无法转换时返回默认值。
    /// </summary>
    public static int GetInt(this IConfiguration configuration, string key, int defaultValue)
        => int.TryParse(configuration[key], out var value) ? value : defaultValue;

    public static bool GetBool(this IConfiguration configuration, string key, bool defaultValue)
        => !bool.TryParse(configuration[key], out var value) || value;

    public static int? GetNullableInt(this IConfiguration configuration, string key)
        => int.TryParse(configuration[key], out var value) ? value : null;

    /// <summary>
    /// 非 AOT 下对未知运行时对象做反射序列化（Dictionary&lt;string, object&gt; 值兜底）。
    /// </summary>
    [UnconditionalSuppressMessage("IL2026", "IL2026", Justification = "仅非 AOT 分支可达")]
    [UnconditionalSuppressMessage("IL3050", "IL3050", Justification = "仅非 AOT 分支可达")]
    public static void SerializeDynamicValue(System.Text.Json.Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, value!.GetType(), options);
}
