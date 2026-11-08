namespace SilkEngine.Asset;

/// <summary>Everything a loader needs to read one asset; all paths are absolute.</summary>
/// <remarks>A missing header or artifact means the entry predates the artifact model; loaders require
/// both and report that the asset must be reimported, so they never decode the project path.</remarks>
internal readonly record struct AssetSource(
    string ProjectPath,
    string SidecarPath,
    string ArtifactPath,
    string HeaderJson,
    string AssetType
);
