using System.Text.Json;
using System.Text.Json.Serialization;

namespace SilkEngine.Asset.Abstractions;

public interface ISidecarCodec<T>
    where T : AssetSidecarBase
{
    T Decode(ReadOnlySpan<byte> content, string sidecarPath);
    byte[] Encode(T sidecar);
}

public sealed class JsonSidecarCodec<T> : ISidecarCodec<T>
    where T : AssetSidecarBase
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public T Decode(ReadOnlySpan<byte> content, string sidecarPath)
    {
        T? value;
        try
        {
            value = JsonSerializer.Deserialize<T>(content, s_options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Malformed sidecar '{sidecarPath}': {ex.Message}", ex);
        }
        if (value is null)
            throw new InvalidDataException($"Empty sidecar '{sidecarPath}'.");
        return value;
    }

    public byte[] Encode(T sidecar) => JsonSerializer.SerializeToUtf8Bytes(sidecar, s_options);
}
