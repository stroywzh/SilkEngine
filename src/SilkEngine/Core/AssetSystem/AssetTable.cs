using System.Diagnostics.CodeAnalysis;
using SilkEngine.Asset;

namespace SilkEngine.Core.AssetSystem;

/// <summary>Runtime asset table: <see cref="AssetId"/> → <see cref="RawDataContainer"/>.</summary>
public static class AssetTable
{
    private static readonly Dictionary<ulong, RawDataContainer> s_containers = new(64);

    private static IReadOnlyDictionary<ulong, RawDataContainer> s_snapshot =
        new Dictionary<ulong, RawDataContainer>(0);

    private static ulong s_lastId;

    /// <summary>Registers the payload container and allocates an asset id.</summary>
    /// <returns>Newly allocated asset id.</returns>
    /// <remarks>Asset thread only.</remarks>
    public static AssetId Register(RawDataContainer container)
    {
        ArgumentNullException.ThrowIfNull(container);

        ulong id = unchecked(++s_lastId);
        if (id == 0)
            throw new InvalidOperationException("AssetTable: asset id space exhausted.");

        s_containers.Add(id, container);
        Publish();

        return new AssetId(id);
    }

    /// <summary>Reads the payload container by asset id.</summary>
    /// <returns>True when the id is valid and still registered.</returns>
    public static bool TryGet(
        AssetId assetId,
        [MaybeNullWhen(false)] out RawDataContainer container
    )
    {
        if (!assetId.IsValid)
        {
            container = null;
            return false;
        }

        return Volatile.Read(ref s_snapshot).TryGetValue(assetId.Value, out container);
    }

    /// <summary>Removes the entry.</summary>
    /// <returns>True when the id existed and was removed.</returns>
    /// <remarks>Asset thread only.</remarks>
    public static bool Unregister(AssetId assetId)
    {
        if (!assetId.IsValid)
            return false;

        if (!s_containers.Remove(assetId.Value))
            return false;

        Publish();
        return true;
    }

    /// <summary>Current live entry count.</summary>
    public static int Count => Volatile.Read(ref s_snapshot).Count;

    private static void Publish() =>
        Volatile.Write(ref s_snapshot, new Dictionary<ulong, RawDataContainer>(s_containers));
}
