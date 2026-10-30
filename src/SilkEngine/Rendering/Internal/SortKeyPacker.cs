using System;
using System.Runtime.CompilerServices;

namespace SilkEngine.Rendering.Internal;

internal static class SortKeyPacker
{
    /// <summary>Converts a float in [0,1] to a monotonically increasing uint bit pattern.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint MonotonicFloatBits(float f)
    {
        // Contract f ∈ [0,1]; Debug asserts guard the range, no cost in Release.
        System.Diagnostics.Debug.Assert(f >= 0f && f <= 1f, "Depth01 越界，排序会错");

        return BitConverter.SingleToUInt32Bits(f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Pack16_16_32(ushort high, ushort mid, uint low) =>
        ((ulong)high << 48) | ((ulong)mid << 32) | low;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Pack16_32_16(ushort high, uint mid, ushort low) =>
        ((ulong)high << 48) | ((ulong)mid << 16) | low;
}
