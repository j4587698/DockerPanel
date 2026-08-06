using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DockerPanel.API.Serialization
{
    /// <summary>
    /// 将 object 动态值写入 JSON，供 Dictionary&lt;string, object&gt; 的 AOT 安全序列化复用。
    /// 覆盖原始值、JsonElement、JsonNode、嵌套字典/集合，以及任意 .NET 对象（回退到 options）。
    /// </summary>
    internal static class JsonValueWriter
    {
        private const int MaxDepth = 64;

        public static string ToJsonString(object? value, JsonSerializerOptions options)
        {
            var buffer = new System.Buffers.ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                WriteValue(writer, value, options, 0);
            }
            return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        }

        public static void WriteDictionary(Utf8JsonWriter writer, Dictionary<string, object> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (var kvp in value)
            {
                writer.WritePropertyName(kvp.Key);
                WriteValue(writer, kvp.Value, options, 0);
            }
            writer.WriteEndObject();
        }

        public static void WriteValue(Utf8JsonWriter writer, object? value, JsonSerializerOptions options, int depth)
        {
            if (depth > MaxDepth)
            {
                throw new JsonException("JSON 嵌套深度超过限制");
            }

            switch (value)
            {
                case null:
                    writer.WriteNullValue();
                    break;
                case string s:
                    writer.WriteStringValue(s);
                    break;
                case bool b:
                    writer.WriteBooleanValue(b);
                    break;
                case int i:
                    writer.WriteNumberValue(i);
                    break;
                case long l:
                    writer.WriteNumberValue(l);
                    break;
                case short s16:
                    writer.WriteNumberValue(s16);
                    break;
                case byte b8:
                    writer.WriteNumberValue(b8);
                    break;
                case uint u:
                    writer.WriteNumberValue(u);
                    break;
                case ulong ul:
                    writer.WriteNumberValue(ul);
                    break;
                case float f:
                    writer.WriteNumberValue(f);
                    break;
                case double db:
                    writer.WriteNumberValue(db);
                    break;
                case decimal dm:
                    writer.WriteNumberValue(dm);
                    break;
                case JsonElement element:
                    element.WriteTo(writer);
                    break;
                case JsonNode node:
                    node.WriteTo(writer);
                    break;
                case Dictionary<string, object> dict:
                    WriteDictionary(writer, dict, options);
                    break;
                case IDictionary<string, object?> dictN:
                    writer.WriteStartObject();
                    foreach (var kvp in dictN)
                    {
                        writer.WritePropertyName(kvp.Key);
                        WriteValue(writer, kvp.Value, options, depth + 1);
                    }
                    writer.WriteEndObject();
                    break;
                case byte[] bytes:
                    writer.WriteBase64StringValue(bytes);
                    break;
                case IDictionary nonGenericDict:
                    writer.WriteStartObject();
                    foreach (System.Collections.DictionaryEntry entry in nonGenericDict)
                    {
                        writer.WritePropertyName(Convert.ToString(entry.Key, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
                        WriteValue(writer, entry.Value, options, depth + 1);
                    }
                    writer.WriteEndObject();
                    break;
                case IEnumerable seq:
                    writer.WriteStartArray();
                    foreach (var item in seq)
                    {
                        WriteValue(writer, item, options, depth + 1);
                    }
                    writer.WriteEndArray();
                    break;
                default:
                    JsonSerializer.Serialize(writer, value, value.GetType(), options);
                    break;
            }
        }
    }
}