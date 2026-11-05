using System.IO;
using SilkEngine.Core.AssetSystem.Internal.Png;

namespace SilkEngine.Asset.Import;

/// <summary>Development-time import tool: writes PNG metadata to the sidecar next to the source (<c>&lt;source&gt;.asset</c>, schema v1).</summary>
public static class TextureImporter
{
    /// <summary>Importer version.</summary>
    public const int ImporterVersion = TextureSidecar.CurrentImporterVersion;

    /// <summary>Imports the source PNG with the default settings (sRGB).</summary>
    /// <returns>Import result.</returns>
    public static ImportResult Import(string sourcePath) =>
        Import(sourcePath, new TextureImportSettings());

    /// <summary>Imports the source PNG.</summary>
    /// <returns>Import result.</returns>
    public static ImportResult Import(string sourcePath, TextureImportSettings settings)
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
        string sourceHash = TextureSidecar.ComputeSourceHash(fullSourcePath);
        string sidecarPath = TextureSidecar.PathFor(fullSourcePath);

        TextureSidecar? current = TryLoadCurrentSidecar(
            sidecarPath,
            sourceName,
            sourceHash,
            settings
        );
        if (current is not null)
            return new ImportResult(sidecarPath, current, Changed: false);

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
        sidecar.Save(sidecarPath);

        return new ImportResult(sidecarPath, sidecar, Changed: true);
    }

    private static TextureSidecar? TryLoadCurrentSidecar(
        string sidecarPath,
        string sourceName,
        string sourceHash,
        TextureImportSettings settings
    )
    {
        if (!File.Exists(sidecarPath))
            return null;

        try
        {
            TextureSidecar sidecar = TextureSidecar.Load(sidecarPath);
            return sidecar.IsCurrentFor(sourceName, sourceHash, settings) ? sidecar : null;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            return null;
        }
    }
}
