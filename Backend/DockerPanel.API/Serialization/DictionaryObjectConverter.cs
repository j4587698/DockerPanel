using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DockerPanel.API.Serialization
{
    /// <summary>
    /// 自定义JSON转换器，用于处理Dictionary<string, object>中的复杂对象序列化
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

                var propertyName = reader.GetString();

                if (propertyName == null)
                {
                    throw new JsonException("Property name is null");
                }

                if (!reader.Read())
                {
                    throw new JsonException("Unexpected end of JSON");
                }

                var value = ReadValue(ref reader, options);
                dictionary[propertyName] = value;
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

            writer.WriteStartObject();

            foreach (var kvp in value)
            {
                writer.WritePropertyName(kvp.Key);
                WriteValue(writer, kvp.Value, options);
            }

            writer.WriteEndObject();
        }

        private object ReadValue(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    return reader.GetString()!;
                case JsonTokenType.Number:
                    if (reader.TryGetInt64(out var longValue))
                        return longValue;
                    if (reader.TryGetDouble(out var doubleValue))
                        return doubleValue;
                    return reader.GetDecimal();
                case JsonTokenType.True:
                case JsonTokenType.False:
                    return reader.GetBoolean();
                case JsonTokenType.Null:
                    return null!;
                case JsonTokenType.StartArray:
                    var list = new List<object>();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        list.Add(ReadValue(ref reader, options));
                    }
                    return list;
                case JsonTokenType.StartObject:
                    return Read(ref reader, typeof(Dictionary<string, object>), options);
                default:
                    throw new JsonException($"Unsupported token type: {reader.TokenType}");
            }
        }

        private void WriteValue(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            switch (value)
            {
                case string stringValue:
                    writer.WriteStringValue(stringValue);
                    break;
                case bool boolValue:
                    writer.WriteBooleanValue(boolValue);
                    break;
                case int intValue:
                    writer.WriteNumberValue(intValue);
                    break;
                case long longValue:
                    writer.WriteNumberValue(longValue);
                    break;
                case double doubleValue:
                    writer.WriteNumberValue(doubleValue);
                    break;
                case decimal decimalValue:
                    writer.WriteNumberValue(decimalValue);
                    break;
                case null:
                    writer.WriteNullValue();
                    break;
                case List<object> list:
                    writer.WriteStartArray();
                    foreach (var item in list)
                    {
                        WriteValue(writer, item, options);
                    }
                    writer.WriteEndArray();
                    break;
                case Dictionary<string, object> dict:
                    Write(writer, dict, options);
                    break;
                default:
                    // 对于其他复杂对象，尝试序列化为字符串
                    writer.WriteStringValue(value.ToString());
                    break;
            }
        }
    }
}