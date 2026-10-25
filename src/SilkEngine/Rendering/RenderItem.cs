using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.Internal;

namespace SilkEngine.Rendering;

/// <summary>Sorting family of a render item; internal because ordering is a renderer detail.</summary>
internal enum RenderItemSortMode : byte
{
    Opaque = 0,
    Transparent = 1,
}

/// <summary>
/// A single draw item, tagged with a sort key for ordering by the (not yet implemented) CPU
/// sorting queue.
/// </summary>
/// <remarks>
/// Mesh and material are typed handles: the previous <c>int</c>/<c>ushort</c> pair let an id
/// from one table be passed where the other was expected. The sort key still packs a 16-bit material
/// hash, so only the low 16 bits of <see cref="MaterialHandle.Id"/> influence ordering.
/// </remarks>
public readonly struct RenderItem
{
    /// <summary>Mesh drawn by this item.</summary>
    public readonly MeshHandle Mesh;

    /// <summary>Material bound for this item.</summary>
    public readonly MaterialHandle Material;

    /// <summary>Queue bucket used by the sort key.</summary>
    public readonly ushort Queue;

    /// <summary>Normalized depth in [0, 1] used by the sort key.</summary>
    public readonly float Depth01;

    internal readonly RenderItemSortMode SortMode;
    internal readonly RenderItemSortKey SortKey;

    /// <summary>Creates a render item; <paramref name="depth01"/> must be a normalized depth.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="depth01"/> is outside [0, 1]; the sort key packs the raw float bits, so an
    /// out-of-range value would silently reorder the whole queue (REV-047).
    /// </exception>
    private RenderItem(
        RenderItemSortMode mode,
        ushort queue,
        float depth01,
        MaterialHandle material,
        MeshHandle mesh
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(depth01, 0f);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(depth01, 1f);

        Mesh = mesh;
        Material = material;
        Queue = queue;
        Depth01 = depth01;
        SortMode = mode;
        SortKey = RenderItemSortKeyBuilder.Build(mode, queue, depth01, material.Id);
    }

    /// <summary>Creates an opaque item.</summary>
    /// <param name="q">Queue bucket.</param>
    /// <param name="d">Normalized depth.</param>
    /// <param name="mat">Material handle.</param>
    /// <param name="mesh">Mesh handle.</param>
    public static RenderItem Opaque(ushort q, float d, MaterialHandle mat, MeshHandle mesh) =>
        new(RenderItemSortMode.Opaque, q, d, mat, mesh);

    /// <summary>Creates a transparent item.</summary>
    /// <param name="q">Queue bucket.</param>
    /// <param name="d">Normalized depth.</param>
    /// <param name="mat">Material handle.</param>
    /// <param name="mesh">Mesh handle.</param>
    public static RenderItem Transparent(ushort q, float d, MaterialHandle mat, MeshHandle mesh) =>
        new(RenderItemSortMode.Transparent, q, d, mat, mesh);
}
