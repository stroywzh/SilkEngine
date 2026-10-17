using System;
using System.Runtime.InteropServices;
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

    private RenderItem(
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
        SortKey = RenderItemSortKeyBuilder.Build(mode, queue, depth01, materialId);
    }

    public static RenderItem Opaque(ushort q, float d, ushort mat, int mesh) =>
        new(RenderItemSortMode.Opaque, q, d, mat, mesh);

    public static RenderItem Transparent(ushort q, float d, ushort mat, int mesh) =>
        new(RenderItemSortMode.Transparent, q, d, mat, mesh);
}
