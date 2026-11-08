using System.IO;
using SilkEngine.Asset.Abstractions;
using SilkEngine.Core.IO;
using SilkEngine.Serialization;

namespace SilkEngine.Asset;

/// <summary>Read-only index of the project's imported assets, keyed by persistent identity and by project-relative path.</summary>
/// <remarks>The catalog is a derived index: sidecars are authoritative and a stale or missing index is rebuilt by
/// scanning <c>Assets/</c>. Entries are produced by <see cref="AssetCatalogAdapterRegistry"/> so the catalog itself
/// does not hardcode any concrete sidecar type.</remarks>
internal sealed class AssetCatalog : IAssetCatalog
{
    /// <summary>Current on-disk schema version of <c>Library/index.json</c>.</summary>
    public const int CurrentSchemaVersion = 1;

    private readonly ContentLayout _layout;
    private readonly Dictionary<AssetGuid, AssetCatalogEntry> _byGuid;
    private readonly Dictionary<string, AssetCatalogEntry> _byPath;

    private AssetCatalog(
        ContentLayout layout,
        IEnumerable<AssetCatalogEntry> entries,
        bool strict
    )
    {
        _layout = layout;
        _byGuid = new Dictionary<AssetGuid, AssetCatalogEntry>();
        _byPath = new Dictionary<string, AssetCatalogEntry>(PathUtil.Comparer);

        foreach (AssetCatalogEntry entry in entries)
            Add(entry, strict);
    }

    /// <summary>Number of indexed assets.</summary>
    public int Count => _byGuid.Count;

    /// <summary>Indexed assets, one per source with a readable sidecar.</summary>
    public IReadOnlyCollection<AssetCatalogEntry> Entries => _byGuid.Values;

    /// <summary>Indexes every imported source under <c>&lt;projectRoot&gt;/Assets</c>; unreadable sidecars are skipped.</summary>
    /// <remarks>A source participates only while its sidecar exists; adapters decide which sources are supported.</remarks>
    public static AssetCatalog Build(ContentLayout layout, bool strict = false)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var entries = new List<AssetCatalogEntry>();
        if (!Directory.Exists(layout.AssetsRoot))
            return new AssetCatalog(layout, entries, strict);

        foreach (
            string sourcePath in Directory.EnumerateFiles(
                layout.AssetsRoot,
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

            if (
                !AssetCatalogAdapterRegistry.TryGet(
                    Path.GetExtension(sourcePath),
                    out IAssetCatalogAdapter adapter
                )
            )
                continue;

            if (!adapter.TryCreateEntry(layout, sourcePath, out AssetCatalogEntry entry))
                continue;

            entries.Add(entry);
        }

        return new AssetCatalog(layout, entries, strict);
    }

    /// <summary>Convenience overload for tools/tests: derives the layout from a project root.</summary>
    public static AssetCatalog Build(string projectRoot, bool strict = false) =>
        Build(ContentLayout.Create(projectRoot), strict);

    /// <summary>Loads <c>&lt;projectRoot&gt;/Library/index.json</c>.</summary>
    /// <exception cref="InvalidDataException">The index is malformed or its schema version is unsupported.</exception>
    public static AssetCatalog Open(ContentLayout layout, bool strict = false)
    {
        ArgumentNullException.ThrowIfNull(layout);

        CatalogDocument document = JsonCodec.Decode<CatalogDocument>(
            File.ReadAllBytes(layout.CatalogPath),
            layout.CatalogPath
        );

        if (document.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException(
                $"Unsupported catalog schemaVersion {document.SchemaVersion} in '{layout.CatalogPath}' "
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

        return new AssetCatalog(layout, entries, strict);
    }

    /// <summary>Convenience overload for tools/tests: derives the layout from a project root.</summary>
    public static AssetCatalog Open(string projectRoot, bool strict = false) =>
        Open(ContentLayout.Create(projectRoot), strict);

    /// <summary>Loads the index, or rebuilds and saves it when the index is missing, unreadable or outdated.</summary>
    public static AssetCatalog OpenOrBuild(ContentLayout layout, bool strict = false)
    {
        ArgumentNullException.ThrowIfNull(layout);

        try
        {
            return Open(layout, strict);
        }
        catch (Exception error) when (error is IOException or InvalidDataException)
        {
            Log.Warning($"Rebuilding asset catalog for '{layout.ProjectRoot}': {error.Message}");
        }

        AssetCatalog catalog = Build(layout, strict);
        catalog.Save();
        return catalog;
    }

    /// <summary>Convenience overload for tools/tests: derives the layout from a project root.</summary>
    public static AssetCatalog OpenOrBuild(string projectRoot, bool strict = false) =>
        OpenOrBuild(ContentLayout.Create(projectRoot), strict);

    /// <summary>Writes <c>&lt;projectRoot&gt;/Library/index.json</c> atomically.</summary>
    public void Save()
    {
        Directory.CreateDirectory(_layout.LibraryRoot);

        var document = new CatalogDocument
        {
            SchemaVersion = CurrentSchemaVersion,
            Entries = _byGuid
                .Values.OrderBy(static entry => entry.ProjectPath, StringComparer.Ordinal)
                .Select(ToDto)
                .ToList(),
        };
        byte[] content = JsonCodec.Encode(document, humanReadable: false);

        AtomicFile.WriteAllBytes(_layout.CatalogPath, content, overwrite: true);
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
                : Path.GetFullPath(sourcePath, _layout.ProjectRoot);
            relative = Path.GetRelativePath(_layout.ProjectRoot, fullPath);
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

    private void Add(AssetCatalogEntry entry, bool strict)
    {
        if (_byGuid.ContainsKey(entry.Guid))
        {
            ReportDuplicate(
                $"Catalog entry for '{entry.ProjectPath}' reuses guid '{entry.Guid}'.",
                strict
            );
            return;
        }

        if (_byPath.ContainsKey(entry.ProjectPath))
        {
            ReportDuplicate(
                $"Catalog path '{entry.ProjectPath}' is registered more than once.",
                strict
            );
            return;
        }

        _byGuid.Add(entry.Guid, entry);
        _byPath.Add(entry.ProjectPath, entry);
    }

    private static void ReportDuplicate(string message, bool strict)
    {
        if (strict)
            throw new InvalidDataException(message);

        Log.Warning($"{message} Keeping the first entry.");
    }

    private AssetSource CreateSource(AssetCatalogEntry entry)
    {
        string projectPath = Path.GetFullPath(
            Path.Combine(_layout.ProjectRoot, entry.ProjectPath)
        );
        string artifactPath = string.IsNullOrEmpty(entry.ArtifactHash)
            ? string.Empty
            : ArtifactStore.PathFor(_layout, entry.ArtifactHash);

        return new AssetSource(
            projectPath,
            projectPath + AssetConstants.AssetExtensionName,
            artifactPath,
            entry.HeaderJson,
            entry.AssetType
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
