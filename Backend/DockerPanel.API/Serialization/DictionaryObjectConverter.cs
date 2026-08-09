using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DockerPanel.API.Serialization
{
    /// <summary>
    /// 自定义JSON转换器，用于处理Dictionary&lt;string, object&gt;的 AOT 安全序列化/反序列化。
    /// 不使用反射，值对象按运行时类型分派，保证 NativeAOT 下可用。
    /// </summary>
    public class DictionaryObjectConverter : JsonConverter<Dictionary<string, object>>
    {
        public override Dictionary<string, object> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"Expected StartObject token but got {reader.TokenType}");
            }

            var dictionary = new Dictionary<string, object>();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException($"Expected PropertyName token but got {reader.TokenType}");
                }

                var propertyName = reader.GetString() ?? throw new JsonException("Property name is null");

                if (!reader.Read())
                {
                    throw new JsonException("Unexpected end of JSON");
                }

                dictionary[propertyName] = ReadValue(ref reader)!;
            }

            return dictionary;
        }

        public override void Write(Utf8JsonWriter writer, Dictionary<string, object> value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            JsonValueWriter.WriteDictionary(writer, value, options);
        }

        /// <summary>
    /// 直接复用转换器完成 Dictionary&lt;string, object&gt; 反序列化，绕过解析器查找，AOT 安全。
    /// </summary>
    internal static Dictionary<string, object>? ReadInstance(ref Utf8JsonReader reader, JsonSerializerOptions options)
        => new DictionaryObjectConverter().Read(ref reader, typeof(Dictionary<string, object>), options);

    private static object? ReadValue(ref Utf8JsonReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    return reader.GetString();
                case JsonTokenType.Number:
                    if (reader.TryGetInt64(out var longValue))
                        return longValue;
                    return reader.GetDouble();
                case JsonTokenType.True:
                    return true;
                case JsonTokenType.False:
                    return false;
                case JsonTokenType.Null:
                    return null;
                case JsonTokenType.StartArray:
                    var list = new List<object>();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        list.Add(ReadValue(ref reader)!);
                    }
                    return list;
                case JsonTokenType.StartObject:
                    return ReadObject(ref reader);
                default:
                    throw new JsonException($"Unsupported token type: {reader.TokenType}");
            }
        }

        private static Dictionary<string, object> ReadObject(ref Utf8JsonReader reader)
        {
            var dictionary = new Dictionary<string, object>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                var propertyName = reader.GetString() ?? string.Empty;
                if (!reader.Read())
                {
                    break;
                }

                dictionary[propertyName] = ReadValue(ref reader)!;
            }

            return dictionary;
        }
    }
}