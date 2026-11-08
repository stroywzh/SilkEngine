using System.Text.Json;
using System.Text.Json.Serialization;
using SilkEngine.Asset;

namespace SilkEngine.Serialization;

/// <summary>
/// Serializes the runtime texture descriptor as the catalog header document, without its runtime
/// <see cref="AssetId"/>: an unbound descriptor is the persisted form.
/// </summary>
/// <remarks>Written by hand so the header document keeps exactly the historical shape
/// (<c>width</c>, <c>height</c>, <c>format</c>, <c>colorSpace</c>, <c>mipCount</c>) and old catalogs
/// stay readable. Decoded descriptors always carry <see cref="AssetId.Invalid"/>; the store binds the
/// real id when it registers the asset.</remarks>
internal sealed class TextureJsonConverter : JsonConverter<Texture>
{
    public override Texture Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException("Texture header must be a JSON object.");

        return new Texture()
        {
            Width = ReadInt32(root, "width"),
            Height = ReadInt32(root, "height"),
            Format = ReadEnum(root, "format", TextureFormat.RGBA32),
            ColorSpace = ReadEnum(root, "colorSpace", TextureColorSpace.SRGB),
            MipCount = ReadOptionalInt32(root, "mipCount", 1),
        };
    }

    public override void Write(Utf8JsonWriter writer, Texture value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("width", value.Width);
        writer.WriteNumber("height", value.Height);
        writer.WriteString("format", value.Format.ToString());
        writer.WriteString("colorSpace", value.ColorSpace.ToString());
        writer.WriteNumber("mipCount", value.MipCount);
        writer.WriteEndObject();
    }

    private static int ReadInt32(JsonElement root, string propertyName)
    {
        if (!TryGetProperty(root, propertyName, out JsonElement element))
            throw new JsonException($"Texture header is missing '{propertyName}'.");

        if (!element.TryGetInt32(out int value))
            throw new JsonException($"Texture header '{propertyName}' must be an integer.");

        return value;
    }

    private static int ReadOptionalInt32(JsonElement root, string propertyName, int fallback) =>
        TryGetProperty(root, propertyName, out JsonElement element) && element.TryGetInt32(out int value)
            ? value
            : fallback;

    private static TEnum ReadEnum<TEnum>(JsonElement root, string propertyName, TEnum fallback)
        where TEnum : struct, Enum
    {
        if (!TryGetProperty(root, propertyName, out JsonElement element))
            return fallback;

        if (element.ValueKind == JsonValueKind.String)
        {
            string? text = element.GetString();
            if (Enum.TryParse(text, ignoreCase: true, out TEnum parsed))
                return parsed;
        }
        else if (element.TryGetInt32(out int number))
        {
            return (TEnum)Enum.ToObject(typeof(TEnum), number);
        }

        throw new JsonException($"Texture header '{propertyName}' has an unsupported value.");
    }

    private static bool TryGetProperty(JsonElement root, string name, out JsonElement element)
    {
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                element = property.Value;
                return true;
            }
        }

        element = default;
        return false;
    }
}
