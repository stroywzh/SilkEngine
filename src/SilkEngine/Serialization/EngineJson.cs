using System.Text.Json;
using System.Text.Json.Serialization;

namespace SilkEngine.Serialization;

/// <summary>Engine-wide JSON options; converters come from <see cref="JsonConverterRegistry"/>.</summary>
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
        };
        options.Converters.Add(new JsonStringEnumConverter());
        foreach (JsonConverter converter in JsonConverterRegistry.Snapshot())
            options.Converters.Add(converter);
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
