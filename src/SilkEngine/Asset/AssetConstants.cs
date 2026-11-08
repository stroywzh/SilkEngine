namespace SilkEngine.Asset;

/// <summary>Stable names of the asset project layout; shared by the engine, content pipeline and tools.</summary>
internal static class AssetConstants
{
    /// <summary>Sidecar suffix appended to a source file name.</summary>
    public const string AssetExtensionName = ".asset";

    /// <summary>Folder under the project root that contains content sources and sidecars.</summary>
    public const string AssetsFolderName = "Assets";

    /// <summary>Derived-data folder under the project root.</summary>
    public const string LibraryFolderName = "Library";

    /// <summary>Content-addressed imported artifacts folder under <see cref="LibraryFolderName"/>.</summary>
    public const string ArtifactsFolderName = "artifacts";

    /// <summary>Catalog index file name under <see cref="LibraryFolderName"/>.</summary>
    public const string CatalogFileName = "index.json";

    /// <summary>Imported artifact file suffix.</summary>
    public const string ArtifactExtensionName = ".bin";

    internal const string TextureAssetName = "texture";
    internal const string MeshAssetName = "mesh";
}
