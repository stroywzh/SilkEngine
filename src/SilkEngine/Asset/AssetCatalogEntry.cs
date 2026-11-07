namespace SilkEngine.Asset;

/// <summary>One indexed asset: persistent identity plus the source and artifact data the loader needs.</summary>
/// <remarks><see cref="ProjectPath"/> is project-relative; <see cref="ArtifactHash"/> is empty for a
/// legacy sidecar that has no artifact.</remarks>
internal sealed record AssetCatalogEntry(
    AssetGuid Guid,
    string AssetType,
    string ProjectPath,
    string ArtifactHash,
    long ArtifactLength,
    string HeaderJson,
    string SourceHash,
    string SettingsHash,
    int ImporterVersion
);
