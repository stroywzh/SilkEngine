namespace SilkEngine.Asset.Abstractions;

/// <summary>Imports one source file format and writes the sidecar the catalog reads.</summary>
internal interface IAssetImporter
{
    /// <summary>File extension handled by this importer, including the leading dot (for example <c>.png</c>).</summary>
    string Extension { get; }

    /// <summary>Imports the source at <paramref name="sourcePath"/>; throws when the source is invalid or unreadable.</summary>
    AssetImportResult Import(string sourcePath);
}

