using System.Runtime.CompilerServices;

namespace SilkEngine.Rendering.Internal;

internal static class RenderItemSortKeyBuilder
{
    /// <summary>
    /// Build a Opaque sort key: [queue:16][psoHash:16][depth:32]
    /// <br/>psoHash came from materialId.
    /// </summary>
    /// <param name="queue"></param>
    /// <param name="materialId"></param>
    /// <param name="depth01"></param>
    /// <returns></returns>
    public static ulong BuildOpaque(ushort queue, uint materialId, float depth01)
    {
        ushort psoHash = PsoHash16(materialId);
        uint depthBits = SortKeyPacker.MonotonicFloatBits(depth01);
        return SortKeyPacker.Pack16_16_32(queue, psoHash, depthBits);
    }

    /// <summary>
    /// Build a Transparent sort key: [queue:16][~depth:32][psoHash:16]
    /// <br/>psoHash came from materialId.
    /// </summary>
    /// <param name="queue"></param>
    /// <param name="materialId"></param>
    /// <param name="depth01"></param>
    /// <returns></returns>
    public static ulong BuildTransparent(ushort queue, uint materialId, float depth01)
    {
        ushort psoHash = PsoHash16(materialId);
        uint depthBits = ~SortKeyPacker.MonotonicFloatBits(depth01);
        return SortKeyPacker.Pack16_32_16(queue, depthBits, psoHash);
    }

    /// <summary>
    /// Build RenderItem's sort key.
    /// </summary>
    /// <param name="mode"></param>
    /// <param name="queue"></param>
    /// <param name="depth01"></param>
    /// <param name="materialId"></param>
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
