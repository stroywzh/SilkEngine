using System.IO;
using System.Text;
using SilkEngine.Asset.Abstractions;
using SilkEngine.Serialization;

namespace SilkEngine.Asset;

/// <summary>Read-only index of the project's imported assets, keyed by persistent identity and by project-relative path.</summary>
/// <remarks>The catalog is a derived index: sidecars are authoritative and a stale or missing index is rebuilt by
/// scanning <c>Assets/</c>. Instances are immutable once constructed.</remarks>
internal sealed class AssetCatalog : IAssetCatalog
{
    /// <summary>Current on-disk schema version of <c>Library/index.json</c>.</summary>
    public const int CurrentSchemaVersion = 1;

    private static readonly StringComparer s_pathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly string _root;
    private readonly Dictionary<AssetGuid, AssetCatalogEntry> _byGuid;
    private readonly Dictionary<string, AssetCatalogEntry> _byPath;

    private AssetCatalog(string root, IEnumerable<AssetCatalogEntry> entries)
    {
        _root = root;
        _byGuid = new Dictionary<AssetGuid, AssetCatalogEntry>();
        _byPath = new Dictionary<string, AssetCatalogEntry>(s_pathComparer);

        foreach (AssetCatalogEntry entry in entries)
        {
            _byGuid[entry.Guid] = entry;
            _byPath[entry.ProjectPath] = entry;
        }
    }

    /// <summary>Number of indexed assets.</summary>
    public int Count => _byGuid.Count;

    /// <summary>Indexed assets, one per source with a readable sidecar.</summary>
    public IReadOnlyCollection<AssetCatalogEntry> Entries => _byGuid.Values;

    /// <summary>Indexes every imported source under <c>&lt;projectRoot&gt;/Assets</c>; unreadable sidecars are skipped.</summary>
    /// <remarks>A source participates only while its <c>&lt;source&gt;.asset</c> sidecar exists; unsupported files are
    /// ignored, so the result does not depend on the importer registry.</remarks>
    public static AssetCatalog Build(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        string root = Path.GetFullPath(projectRoot);
        string assetsRoot = Path.Combine(root, AssetConstants.AssetsFolderName);

        var entries = new List<AssetCatalogEntry>();
        if (!Directory.Exists(assetsRoot))
            return new AssetCatalog(root, entries);

        foreach (
            string sourcePath in Directory.EnumerateFiles(
                assetsRoot,
                "*",
                SearchOption.AllDirectories
            )
        )
        {
            if (
                sourcePath.EndsWith(
                    AssetConstants.AssetExtensionName,
                    StringComparison.OrdinalIgnoreCase
                )
            )
                continue;

            string sidecarPath = sourcePath + AssetConstants.AssetExtensionName;
            if (!File.Exists(sidecarPath))
            {
                Log.Debug($"Skipping source '{sourcePath}': no sidecar found.");
                continue;
            }

            TextureSidecar sidecar;
            try
            {
                sidecar = TextureSidecar.Reader.Read(sidecarPath);
            }
            catch (Exception error) when (error is InvalidDataException or IOException)
            {
                Log.Warning($"Skipping sidecar '{sidecarPath}': {error.Message}");
                continue;
            }

            if (sidecar.Artifact is not { } artifact)
            {
                Log.Warning(
                    $"Skipping sidecar '{sidecarPath}': no artifact. Reimport the asset and rebuild the catalog "
                        + "(the runtime loader no longer decodes sources)."
                );
                continue;
            }

            if (!AssetGuid.TryParse(sidecar.Guid, out AssetGuid guid) || !guid.IsValid)
            {
                Log.Warning($"Skipping sidecar '{sidecarPath}': missing or invalid guid.");
                continue;
            }

            entries.Add(
                new AssetCatalogEntry(
                    guid,
                    sidecar.AssetType,
                    Path.GetRelativePath(root, sourcePath),
                    artifact.Hash,
                    artifact.Length,
                    Encoding.UTF8.GetString(
                        JsonCodec.Encode(sidecar.ToTexture(), humanReadable: false)
                    ),
                    sidecar.SourceHash,
                    sidecar.ImportSettingsHash,
                    sidecar.ImporterVersion
                )
            );
        }

        return new AssetCatalog(root, entries);
    }

    /// <summary>Loads <c>&lt;projectRoot&gt;/Library/index.json</c>.</summary>
    /// <exception cref="InvalidDataException">The index is malformed or its schema version is unsupported.</exception>
    public static AssetCatalog Open(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        string root = Path.GetFullPath(projectRoot);
        string indexPath = Path.Combine(
            root,
            AssetConstants.LibraryFolderName,
            AssetConstants.CatalogFileName
        );
        CatalogDocument document = JsonCodec.Decode<CatalogDocument>(
            File.ReadAllBytes(indexPath),
            indexPath
        );

        if (document.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException(
                $"Unsupported catalog schemaVersion {document.SchemaVersion} in '{indexPath}' "
                    + $"(expected {CurrentSchemaVersion})."
            );

        var entries = new List<AssetCatalogEntry>();
        foreach (CatalogEntryDto dto in document.Entries ?? [])
        {
            if (!AssetGuid.TryParse(dto.Guid, out AssetGuid guid) || !guid.IsValid)
            {
                Log.Warning(
                    $"Skipping catalog entry for '{dto.ProjectPath}': missing or invalid guid."
                );
                continue;
            }

            entries.Add(
                new AssetCatalogEntry(
                    guid,
                    dto.AssetType,
                    dto.ProjectPath,
                    dto.ArtifactHash,
                    dto.ArtifactLength,
                    dto.HeaderJson,
                    dto.SourceHash,
                    dto.SettingsHash,
                    dto.ImporterVersion
                )
            );
        }

        return new AssetCatalog(root, entries);
    }

    /// <summary>Loads the index, or rebuilds and saves it when the index is missing, unreadable or outdated.</summary>
    public static AssetCatalog OpenOrBuild(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        try
        {
            return Open(projectRoot);
        }
        catch (Exception error) when (error is IOException or InvalidDataException)
        {
            Log.Warning($"Rebuilding asset catalog for '{projectRoot}': {error.Message}");
        }

        AssetCatalog catalog = Build(projectRoot);
        catalog.Save();
        return catalog;
    }

    /// <summary>Writes <c>&lt;projectRoot&gt;/Library/index.json</c> atomically.</summary>
    public void Save()
    {
        string libraryRoot = Path.Combine(_root, AssetConstants.LibraryFolderName);
        string target = Path.Combine(libraryRoot, AssetConstants.CatalogFileName);
        Directory.CreateDirectory(libraryRoot);

        var document = new CatalogDocument
        {
            SchemaVersion = CurrentSchemaVersion,
            Entries = _byGuid
                .Values.OrderBy(static entry => entry.ProjectPath, StringComparer.Ordinal)
                .Select(ToDto)
                .ToList(),
        };
        byte[] content = JsonCodec.Encode(document, humanReadable: false);

        // Same-directory temp file so the rename stays atomic on one volume.
        string temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temp, content);
            File.Move(temp, target, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <inheritdoc />
    public bool TryGet(AssetGuid guid, out AssetCatalogEntry entry) =>
        _byGuid.TryGetValue(guid, out entry!);

    /// <inheritdoc />
    public bool TryGet(string sourcePath, out AssetCatalogEntry entry)
    {
        entry = null!;
        if (string.IsNullOrWhiteSpace(sourcePath))
            return false;

        string relative;
        try
        {
            string fullPath = Path.IsPathRooted(sourcePath)
                ? Path.GetFullPath(sourcePath)
                : Path.GetFullPath(sourcePath, _root);
            relative = Path.GetRelativePath(_root, fullPath);
        }
        catch (Exception error)
            when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        return _byPath.TryGetValue(relative, out entry!);
    }

    /// <inheritdoc />
    public bool TryGetSource(AssetGuid guid, out AssetSource source)
    {
        if (!TryGet(guid, out AssetCatalogEntry entry))
        {
            source = default;
            return false;
        }

        source = CreateSource(entry);
        return true;
    }

    /// <inheritdoc />
    public bool TryGetSource(string sourcePath, out AssetSource source)
    {
        if (!TryGet(sourcePath, out AssetCatalogEntry entry))
        {
            source = default;
            return false;
        }

        source = CreateSource(entry);
        return true;
    }

    private AssetSource CreateSource(AssetCatalogEntry entry)
    {
        string projectPath = Path.GetFullPath(Path.Combine(_root, entry.ProjectPath));
        string artifactPath = string.IsNullOrEmpty(entry.ArtifactHash)
            ? string.Empty
            : ArtifactStore.PathForProject(_root, entry.ArtifactHash);

        return new AssetSource(
            projectPath,
            projectPath + AssetConstants.AssetExtensionName,
            artifactPath,
            entry.HeaderJson
        );
    }

    private static CatalogEntryDto ToDto(AssetCatalogEntry entry) =>
        new()
        {
            Guid = entry.Guid.ToString(),
            AssetType = entry.AssetType,
            ProjectPath = entry.ProjectPath,
            ArtifactHash = entry.ArtifactHash,
            ArtifactLength = entry.ArtifactLength,
            HeaderJson = entry.HeaderJson,
            SourceHash = entry.SourceHash,
            SettingsHash = entry.SettingsHash,
            ImporterVersion = entry.ImporterVersion,
        };

    private sealed class CatalogDocument
    {
        public int SchemaVersion { get; init; }

        public List<CatalogEntryDto>? Entries { get; init; }
    }

    private sealed class CatalogEntryDto
    {
        public string Guid { get; init; } = string.Empty;
        public string AssetType { get; init; } = string.Empty;
        public string ProjectPath { get; init; } = string.Empty;
        public string ArtifactHash { get; init; } = string.Empty;
        public long ArtifactLength { get; init; }
        public string HeaderJson { get; init; } = string.Empty;
        public string SourceHash { get; init; } = string.Empty;
        public string SettingsHash { get; init; } = string.Empty;
        public int ImporterVersion { get; init; }
    }
}
