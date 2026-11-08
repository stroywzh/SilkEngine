using System.IO;
using SilkEngine.Asset;
using SilkEngine.Asset.Decoder.Png;
using SilkEngine.Core.IO;
using SilkEngine.Serialization;

namespace SilkEngine.Asset;

/// <summary>Development-time import tool: writes PNG metadata to the sidecar next to the source (<c>&lt;source&gt;.asset</c>, schema v2).</summary>
internal static class TextureImporter
{
    /// <summary>Importer version.</summary>
    public const int ImporterVersion = TextureSidecar.CurrentImporterVersion;

    /// <summary>Imports the source PNG with the default settings (sRGB).</summary>
    public static AssetImportResult Import(string sourcePath) =>
        Import(sourcePath, ContentLayout.FromSourcePath(sourcePath));

    /// <summary>Imports the source PNG using the project layout for the artifact store.</summary>
    public static AssetImportResult Import(string sourcePath, ContentLayout layout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(layout);

        return Import(sourcePath, new TextureImportSettings(), layout);
    }

    /// <summary>Imports the source PNG with explicit settings.</summary>
    public static AssetImportResult Import(string sourcePath, TextureImportSettings settings) =>
        Import(sourcePath, settings, ContentLayout.FromSourcePath(sourcePath));

    /// <summary>Imports the source PNG with explicit settings and project layout.</summary>
    public static AssetImportResult Import(
        string sourcePath,
        TextureImportSettings settings,
        ContentLayout layout
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(layout);

        string fullSourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullSourcePath))
            throw new FileNotFoundException(
                $"Source PNG not found: '{sourcePath}'.",
                fullSourcePath
            );

        string sourceName = Path.GetFileName(fullSourcePath);
        string sourceHash = ContentHash.Sha256(fullSourcePath);
        string settingsHash = ContentHash.Sha256(JsonCodec.Encode(settings, humanReadable: false));
        string sidecarPath = SidecarPath.ForSource(fullSourcePath);

        TextureSidecar? existing = LoadExisting(sidecarPath);
        if (
            existing is not null
            && IsUpToDate(sidecarPath, layout, existing, sourceName, sourceHash, settingsHash)
        )
        {
            return new AssetImportResult(sidecarPath, existing, Changed: false);
        }

        PngDecodedImage decoded;
        using (var stream = File.OpenRead(fullSourcePath))
            decoded = PngDecoder.Decode(stream);

        // 顺序：先物化产物，再写 sidecar；sidecar 只在产物落盘后才指向它。
        Transfer.ArtifactRef artifact = ArtifactStore.Write(layout, decoded.Rgba32);

        // GUID 只在没有可用旧值时生成；重导入/改设置/升级导入器都必须保留它。
        string guid =
            existing is not null
            && AssetGuid.TryParse(existing.Guid, out AssetGuid existingGuid)
            && existingGuid.IsValid
                ? existingGuid.ToString()
                : AssetGuid.New().ToString();

        var sidecar = new TextureSidecar
        {
            Source = sourceName,
            SourceHash = sourceHash,
            Guid = guid,
            ImportSettingsHash = settingsHash,
            Artifact = artifact,
            Width = decoded.Width,
            Height = decoded.Height,
            Format = TextureFormat.RGBA32,
            ColorSpace = settings.Srgb ? TextureColorSpace.SRGB : TextureColorSpace.Linear,
            MipCount = 1,
            ImportSettings = settings,
        };

        TextureSidecar.Writer.Write(sidecarPath, sidecar);

        return new AssetImportResult(sidecarPath, sidecar, Changed: true);
    }

    // 缺失或损坏的 sidecar 都视为“需要重导入”——这是 importer 的语义，
    // 不是读取层的容错。Read 本身仍然直接抛。
    private static TextureSidecar? LoadExisting(string sidecarPath)
    {
        if (!File.Exists(sidecarPath))
            return null;

        try
        {
            return TextureSidecar.Reader.Read(sidecarPath);
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    // importer 策略，留在 importer 层；契约只描述数据。
    // 已存在的 sidecar 为当前 schema、GUID 有效，且源文件/设置哈希/导入器一致时为 true：跳过解码，只复用。
    private static bool IsUpToDate(
        string sidecarPath,
        ContentLayout layout,
        TextureSidecar existing,
        string sourceName,
        string sourceHash,
        string settingsHash
    )
    {
        // v2 sidecar 必须带产物；artifact: null 的旧 v2 记录靠这次重导入补齐。
        return existing.SchemaVersion == TextureSidecar.CurrentSchemaVersion
            && existing.Artifact is { } artifact
            && ArtifactStore.Exists(layout, artifact)
            && AssetGuid.TryParse(existing.Guid, out AssetGuid guid)
            && guid.IsValid
            && string.Equals(
                existing.ImportSettingsHash,
                settingsHash,
                StringComparison.OrdinalIgnoreCase
            )
            && string.Equals(
                existing.Importer,
                TextureSidecar.PngImporterName,
                StringComparison.Ordinal
            )
            && existing.ImporterVersion == TextureSidecar.CurrentImporterVersion
            && string.Equals(existing.Source, sourceName, StringComparison.Ordinal)
            && string.Equals(existing.SourceHash, sourceHash, StringComparison.OrdinalIgnoreCase)
            && existing.Format == TextureFormat.RGBA32
            && existing.MipCount == 1
            && existing.Width > 0
            && existing.Height > 0;
    }
}
