using System.Runtime.CompilerServices;

namespace SilkEngine.Rendering.Internal;

internal static class RenderItemSortKeyBuilder
{
    /// <summary>Builds the opaque sort key.</summary>
    public static ulong BuildOpaque(ushort queue, uint materialId, float depth01)
    {
        ushort psoHash = PsoHash16(materialId);
        uint depthBits = SortKeyPacker.MonotonicFloatBits(depth01);
        return SortKeyPacker.Pack16_16_32(queue, psoHash, depthBits);
    }

    /// <summary>Builds the transparent sort key.</summary>
    public static ulong BuildTransparent(ushort queue, uint materialId, float depth01)
    {
        ushort psoHash = PsoHash16(materialId);
        uint depthBits = ~SortKeyPacker.MonotonicFloatBits(depth01);
        return SortKeyPacker.Pack16_32_16(queue, depthBits, psoHash);
    }

    /// <summary>
    /// Build RenderItem's sort key.
    /// </summary>
    /// <returns></returns>
    public static RenderItemSortKey Build(
        RenderItemSortMode mode,
        ushort queue,
        float depth01,
        uint materialId
    ) =>
        mode switch
        {
            RenderItemSortMode.Transparent => SetUpRenderItemSortKey(
                BuildTransparent(queue, materialId, depth01)
            ),
            RenderItemSortMode.Opaque => SetUpRenderItemSortKey(
                BuildOpaque(queue, materialId, depth01)
            ),
            _ => throw new ArgumentOutOfRangeException($"Invalid RenderItemSortMode:{mode}"),
        };

    private const uint HashBase = 2166136261u;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort PsoHash16(uint materialId)
    {
        uint h = HashBase;
        h = (h ^ materialId) * 16777619u;
        return (ushort)h;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static RenderItemSortKey SetUpRenderItemSortKey(ulong packedData) =>
        new RenderItemSortKey(packedData);
}
