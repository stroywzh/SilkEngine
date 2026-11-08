using SilkEngine.Asset.Abstractions;
using SilkEngine.Asset.Transfer;

namespace SilkEngine.Asset;

/// <summary>Texture import settings (the sidecar's <c>importSettings</c> node).</summary>
internal sealed record TextureImportSettings
{
    public bool Srgb { get; init; } = true;
}

/// <summary>Texture import record, schema v2: stable GUID plus settings hash; v1 records remain readable.</summary>
internal sealed class TextureSidecar : AssetSidecarBase
{
    public const int CurrentSchemaVersion = 2;
    public const string TypeName = "texture";
    public const string PngImporterName = "png";
    public const int CurrentImporterVersion = 2;

    private static readonly SidecarFileStore<TextureSidecar> s_store = new(
        new JsonSidecarCodec<TextureSidecar>(),
        Validate
    );

    public static ISidecarReader<TextureSidecar> Reader => s_store;
    public static ISidecarWriter<TextureSidecar> Writer => s_store;

    public override int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public override string AssetType => TypeName;

    public string Guid { get; init; } = string.Empty;
    public string ImportSettingsHash { get; init; } = string.Empty;
    public ArtifactRef? Artifact { get; init; }

    public string Importer { get; init; } = PngImporterName;
    public int ImporterVersion { get; init; } = CurrentImporterVersion;
    public int Width { get; init; }
    public int Height { get; init; }
    public TextureFormat Format { get; init; } = TextureFormat.RGBA32;
    public TextureColorSpace ColorSpace { get; init; } = TextureColorSpace.SRGB;
    public int MipCount { get; init; } = 1;
    public TextureImportSettings ImportSettings { get; init; } = new();

    /// <summary>Unbound runtime descriptor derived from the import record; the asset store binds the real id.</summary>
    internal Texture ToTexture() =>
        new()
        {
            Width = Width,
            Height = Height,
            Format = Format,
            ColorSpace = ColorSpace,
            MipCount = MipCount,
        };

    private static void Validate(TextureSidecar sidecar, string sidecarPath)
    {
        if (sidecar.SchemaVersion is not (1 or CurrentSchemaVersion))
            throw new InvalidDataException(
                $"Unsupported schemaVersion {sidecar.SchemaVersion} in '{sidecarPath}' (expected 1 or {CurrentSchemaVersion})."
            );

        if (!string.Equals(sidecar.AssetType, TypeName, StringComparison.Ordinal))
            throw new InvalidDataException(
                $"Unexpected assetType '{sidecar.AssetType}' in '{sidecarPath}' (expected '{TypeName}')."
            );

        if (string.IsNullOrWhiteSpace(sidecar.Source))
            throw new InvalidDataException($"Missing source in texture sidecar '{sidecarPath}'.");

        // v1 records carry no identity: they stay readable and are upgraded on the next import.
        if (sidecar.SchemaVersion == 1)
            return;

        if (!AssetGuid.TryParse(sidecar.Guid, out AssetGuid guid) || !guid.IsValid)
            throw new InvalidDataException(
                $"Missing or invalid guid in texture sidecar '{sidecarPath}'."
            );

        if (string.IsNullOrWhiteSpace(sidecar.ImportSettingsHash))
            throw new InvalidDataException(
                $"Missing importSettingsHash in texture sidecar '{sidecarPath}'."
            );

        if (sidecar.Width <= 0 || sidecar.Height <= 0 || sidecar.MipCount <= 0)
            throw new InvalidDataException(
                $"Invalid dimensions or mip count in texture sidecar '{sidecarPath}'."
            );
    }
}

