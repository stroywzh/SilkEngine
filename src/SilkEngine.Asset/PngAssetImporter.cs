namespace SilkEngine.Asset;

/// <summary>Importer for PNG texture sources.</summary>
internal sealed class PngAssetImporter : Abstractions.IAssetImporter
{
    /// <summary>PNG file extension.</summary>
    public const string PngExtension = ".png";

    /// <inheritdoc />
    public string Extension => PngExtension;

    /// <inheritdoc />
    public AssetImportResult Import(string sourcePath) => TextureImporter.Import(sourcePath);
}

