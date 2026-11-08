using System.Text.Json.Serialization;

namespace SilkEngine.Serialization;

/// <summary>Converters registered by engine/business asset modules before the JSON options are first built.</summary>
internal static class JsonConverterRegistry
{
    private static readonly object s_gate = new();
    private static readonly List<JsonConverter> s_converters = new();

    internal static void Register(JsonConverter converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        lock (s_gate)
            s_converters.Add(converter);
    }

    internal static JsonConverter[] Snapshot()
    {
        lock (s_gate)
            return s_converters.ToArray();
    }
}
