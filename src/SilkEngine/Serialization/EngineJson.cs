using System.Text.Json;
using System.Text.Json.Serialization;

namespace SilkEngine.Serialization;

/// <summary>引擎统一的 JSON 选项；两个实例创建后只读，运行期修改会立即抛异常。</summary>
internal static class EngineJson
{
    public static JsonSerializerOptions HumanReadable { get; } = Create(writeIndented: true);

    public static JsonSerializerOptions Compact { get; } = Create(writeIndented: false);

    private static JsonSerializerOptions Create(bool writeIndented)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = writeIndented,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(), new TextureJsonConverter() },
        };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
