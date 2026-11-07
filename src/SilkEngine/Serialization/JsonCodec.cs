using System.Text.Json;

namespace SilkEngine.Serialization;

/// <summary>面向文档的 JSON 编解码；读取始终走可读变体，写入可按需紧凑。</summary>
internal static class JsonCodec
{
    public static T Decode<T>(ReadOnlySpan<byte> content, string documentPath)
        where T : class
    {
        T? value;
        try
        {
            value = JsonSerializer.Deserialize<T>(content, EngineJson.HumanReadable);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                $"Malformed document '{documentPath}': {ex.Message}",
                ex
            );
        }

        if (value is null)
            throw new InvalidDataException($"Empty document '{documentPath}'.");

        return value;
    }

    public static byte[] Encode<T>(T value, bool humanReadable = true)
        where T : class
    {
        JsonSerializerOptions options = humanReadable
            ? EngineJson.HumanReadable
            : EngineJson.Compact;
        return JsonSerializer.SerializeToUtf8Bytes(value, options);
    }
}
