using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Asset;

/// <summary>Maps source extensions to the adapter that can catalog them.</summary>
internal static class AssetCatalogAdapterRegistry
{
    private static readonly object s_lock = new();
    private static readonly Dictionary<string, IAssetCatalogAdapter> s_adapters = new(
        StringComparer.OrdinalIgnoreCase
    );

    static AssetCatalogAdapterRegistry()
    {
        Register(new TextureSidecarAdapter());
    }

    /// <summary>Registers <paramref name="adapter"/> for its source extension.</summary>
    internal static void Register(IAssetCatalogAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);

        string extension = Normalize(adapter.SourceExtension);
        if (extension.Length == 0)
            throw new ArgumentException("Catalog adapter extension must not be empty.", nameof(adapter));

        lock (s_lock)
            s_adapters[extension] = adapter;
    }

    internal static bool TryGet(string extension, out IAssetCatalogAdapter adapter)
    {
        string normalized = Normalize(extension);
        if (normalized.Length == 0)
        {
            adapter = null!;
            return false;
        }

        lock (s_lock)
            return s_adapters.TryGetValue(normalized, out adapter!);
    }

    private static string Normalize(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return string.Empty;

        string trimmed = extension.Trim();
        return trimmed.StartsWith('.') ? trimmed : "." + trimmed;
    }
}
