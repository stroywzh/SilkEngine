using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Asset.Sidecar;

/// <summary>Texture import settings (the sidecar's <c>importSettings</c> node).</summary>
public sealed record TextureImportSettings
{
    /// <summary>Whether colors are interpreted as sRGB (default true).</summary>
    public bool Srgb { get; init; } = true;
}

public sealed class TextureSidecar : AssetSidecarBase
{
    public const int CurrentSchemaVersion = 1;
    public const string TypeName = "texture";
    public const string PngImporterName = "png";
    public const int CurrentImporterVersion = 1;

    // ★ 新增：文件存储入口，Importer 和 Loader 都从这里走
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

    // ★ 新增：只校验“这份 JSON 到底是不是一张 texture sidecar”，
    //        不再承担“是否需要重导入”的语义（那属于 TextureImporter）。
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
