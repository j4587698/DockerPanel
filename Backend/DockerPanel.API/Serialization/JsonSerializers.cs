using System.Text.Json;
using System.Text.Json.Serialization;

namespace DockerPanel.API.Serialization
{
    /// <summary>
    /// 应用内共享的 JSON 序列化选项：源生成上下文 + Dictionary 转换器，AOT 安全。
    /// </summary>
    public static class JsonSerializers
    {
        public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = DockerPanelJsonContext.Default
        };

        public static readonly JsonSerializerOptions Indented = new(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = DockerPanelJsonContext.Default,
            WriteIndented = true
        };

        static JsonSerializers()
        {
            Options.Converters.Add(new DictionaryObjectConverter());
            Indented.Converters.Add(new DictionaryObjectConverter());
        }
    }
}