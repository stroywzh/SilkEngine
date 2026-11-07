using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Asset;

/// <summary>Maps source file extensions to the importer that handles them.</summary>
/// <remarks>Importer registration is startup-only: register every importer before scanning or importing. Lookups are
/// case-insensitive and ignore whether the extension was supplied with a leading dot.</remarks>
internal static class AssetImporterRegistry
{
    private static readonly Lock s_lock = new();
    private static readonly Dictionary<string, IAssetImporter> s_importers = new(
        StringComparer.OrdinalIgnoreCase
    );

    /// <summary>Registered importers, one per extension.</summary>
    public static IReadOnlyCollection<IAssetImporter> Importers
    {
        get
        {
            lock (s_lock)
                return s_importers.Values.ToArray();
        }
    }

    /// <summary>Registers <paramref name="importer"/> for its extension; a later registration replaces the earlier one.</summary>
    /// <exception cref="ArgumentException">The importer declares an empty extension.</exception>
    public static void Register(IAssetImporter importer)
    {
        ArgumentNullException.ThrowIfNull(importer);

        string extension = Normalize(importer.Extension);
        if (extension.Length == 0)
            throw new ArgumentException("Importer extension must not be empty.", nameof(importer));

        lock (s_lock)
            s_importers[extension] = importer;
    }

    /// <summary>Finds the importer registered for <paramref name="extension"/>.</summary>
    public static bool TryGet(string extension, out IAssetImporter importer)
    {
        string normalized = Normalize(extension);
        if (normalized.Length == 0)
        {
            importer = null!;
            return false;
        }

        lock (s_lock)
            return s_importers.TryGetValue(normalized, out importer!);
    }

    /// <summary>Registers the built-in importers (PNG today).</summary>
    public static void RegisterDefaults() => Register(new PngAssetImporter());

    private static string Normalize(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return string.Empty;

        string trimmed = extension.Trim();
        return trimmed.StartsWith('.') ? trimmed : "." + trimmed;
    }
}
