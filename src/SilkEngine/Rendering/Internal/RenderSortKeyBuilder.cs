using System.Runtime.CompilerServices;

namespace SilkEngine.Rendering.Internal;

internal static class RenderItemSortKeyBuilder
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RenderItemSortKey BuildKey(
        RenderItemSortMode mode,
        ushort queue,
        float depth01,
        ushort materialId
    )
    {
        return new RenderItemSortKey(Build(mode, queue, depth01, materialId));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Build(
        RenderItemSortMode mode,
        ushort queue,
        float depth01,
        ushort materialId
    )
    {
        uint depthBits = SortKeyPacker.MonotonicFloatBits(depth01);

        // Opaque：近的先画，depthBits 保持升序。
        // Transparent：远的先画，对 depthBits 取反，让大深度得到小 key。
        if (mode == RenderItemSortMode.Transparent)
        {
            depthBits = ~depthBits;
        }

        // high16 = queue，mid32 = depth，low16 = materialId
        return SortKeyPacker.Pack(materialId, depthBits, queue);
    }
}
