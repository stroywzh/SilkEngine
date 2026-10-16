using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.Internal;

namespace SilkEngine.Rendering;

public enum RenderItemSortMode : byte
{
    Opaque = 0,
    Transparent = 1,
}

/// <summary>
/// A single draw item, tagged with a sort key for ordering by <see cref="RenderItemBuffer"/>.
/// </summary>
public readonly struct RenderItem
{
    public readonly int MeshId;
    public readonly ushort MaterialId;
    public readonly ushort Queue;
    public readonly float Depth01;
    internal readonly RenderItemSortMode SortMode;
    internal readonly RenderItemSortKey SortKey;

    public RenderItem(
        RenderItemSortMode mode,
        ushort queue,
        float depth01,
        ushort materialId,
        int meshId
    )
    {
        MeshId = meshId;
        MaterialId = materialId;
        Queue = queue;
        Depth01 = depth01;
        SortMode = mode;

        SortKey = RenderItemSortKeyBuilder.BuildKey(mode, queue, depth01, materialId);
    }

    public static RenderItem Opaque(ushort queue, float depth01, ushort materialId, int meshId) =>
        new(RenderItemSortMode.Opaque, queue, depth01, materialId, meshId);

    public static RenderItem Transparent(
        ushort queue,
        float depth01,
        ushort materialId,
        int meshId
    ) => new(RenderItemSortMode.Transparent, queue, depth01, materialId, meshId);
}
