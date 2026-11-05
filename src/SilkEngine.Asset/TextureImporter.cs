using System.IO;
using SilkEngine.Asset.Sidecar;
using SilkEngine.Asset.Decoder.Png;

namespace SilkEngine.Asset;

/// <summary>Development-time import tool: writes PNG metadata to the sidecar next to the source (<c>&lt;source&gt;.asset</c>, schema v1).</summary>
public static class TextureImporter
{
    /// <summary>Importer version.</summary>
    public const int ImporterVersion = TextureSidecar.CurrentImporterVersion;

    /// <summary>Imports the source PNG with the default settings (sRGB).</summary>
    public static AssetImportResult Import(string sourcePath) =>
        Import(sourcePath, new TextureImportSettings());

    /// <summary>Imports the source PNG.</summary>
    public static AssetImportResult Import(string sourcePath, TextureImportSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(settings);

        string fullSourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullSourcePath))
            throw new FileNotFoundException(
                $"Source PNG not found: '{sourcePath}'.",
                fullSourcePath
            );

        string sourceName = Path.GetFileName(fullSourcePath);
        string sourceHash = ContentHash.Sha256(fullSourcePath);
        string sidecarPath = SidecarPath.ForSource(fullSourcePath);

        TextureSidecar? existing = LoadExisting(sidecarPath);
        if (
            existing is not null
            && ShouldReimport(existing, sourceName, sourceHash, settings) is false
        )
        {
            return new AssetImportResult(sidecarPath, existing, Changed: false);
        }

        PngDecodedImage decoded;
        using (var stream = File.OpenRead(fullSourcePath))
            decoded = PngDecoder.Decode(stream);

        var sidecar = new TextureSidecar
        {
            Source = sourceName,
            SourceHash = sourceHash,
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
    private static bool ShouldReimport(
        TextureSidecar existing,
        string sourceName,
        string sourceHash,
        TextureImportSettings settings
    )
    {
        return string.Equals(
                existing.Importer,
                TextureSidecar.PngImporterName,
                StringComparison.Ordinal
            )
            && existing.ImporterVersion == TextureSidecar.CurrentImporterVersion
            && string.Equals(existing.Source, sourceName, StringComparison.Ordinal)
            && string.Equals(existing.SourceHash, sourceHash, StringComparison.OrdinalIgnoreCase)
            && existing.ImportSettings == settings
            && existing.Format == TextureFormat.RGBA32
            && existing.MipCount == 1
            && existing.Width > 0
            && existing.Height > 0;
    }
}
