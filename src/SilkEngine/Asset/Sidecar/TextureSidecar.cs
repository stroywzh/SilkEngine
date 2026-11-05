using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Asset.Sidecar;

/// <summary>Texture import settings (the sidecar's <c>importSettings</c> node).</summary>
public sealed record TextureImportSettings
{
    public bool Srgb { get; init; } = true;
}

public sealed class TextureSidecar : AssetSidecarBase
{
    public const int CurrentSchemaVersion = 1;
    public const string TypeName = "texture";
    public const string PngImporterName = "png";
    public const int CurrentImporterVersion = 1;

    private static readonly SidecarFileStore<TextureSidecar> s_store = new(
        new JsonSidecarCodec<TextureSidecar>(),
        Validate
    );

    public static ISidecarReader<TextureSidecar> Reader => s_store;
    public static ISidecarWriter<TextureSidecar> Writer => s_store;

    public override int SchemaVersion => CurrentSchemaVersion;
    public override string AssetType => TypeName;

    public string Importer { get; init; } = PngImporterName;
    public int ImporterVersion { get; init; } = CurrentImporterVersion;
    public int Width { get; init; }
    public int Height { get; init; }
    public TextureFormat Format { get; init; } = TextureFormat.RGBA32;
    public TextureColorSpace ColorSpace { get; init; } = TextureColorSpace.SRGB;
    public int MipCount { get; init; } = 1;
    public TextureImportSettings ImportSettings { get; init; } = new();

    private static void Validate(TextureSidecar sidecar, string sidecarPath)
    {
        if (sidecar.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException(
                $"Unsupported schemaVersion {sidecar.SchemaVersion} in '{sidecarPath}' (expected {CurrentSchemaVersion})."
            );

        if (!string.Equals(sidecar.AssetType, TypeName, StringComparison.Ordinal))
            throw new InvalidDataException(
                $"Unexpected assetType '{sidecar.AssetType}' in '{sidecarPath}' (expected '{TypeName}')."
            );

        if (string.IsNullOrWhiteSpace(sidecar.Source))
            throw new InvalidDataException($"Missing source in texture sidecar '{sidecarPath}'.");
    }
}
