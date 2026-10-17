using System;
using System.Runtime.CompilerServices;

namespace SilkEngine.Rendering.Internal;

internal static class SortKeyPacker
{
    /// <summary>
    /// 把 float 转成单调递增的 uint 位模式。
    /// 前提：f ∈ [0, 1]（Depth01 的约定）。此时 IEEE 754 正数的位模式本身就是单调递增的，
    /// 直接取 bits 即可，不需要翻转。
    /// 若允许负数，必须加符号处理：bits & 0x80000000 != 0 ? ~bits : bits | 0x80000000。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint MonotonicFloatBits(float f)
    {
        // 约定 f ∈ [0,1]；Debug 断言防越界，Release 下无开销
        System.Diagnostics.Debug.Assert(f >= 0f && f <= 1f, "Depth01 越界，排序会错");

        return BitConverter.SingleToUInt32Bits(f);
    }

    /// <summary>
    /// 把 float ∈ [0,1] 量化成 [0, 65535] 的 ushort。
    /// 用于 Opaque 只做粗粒度深度分桶的场景。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort QuantizeDepth16(float f)
    {
        System.Diagnostics.Debug.Assert(f >= 0f && f <= 1f, "Depth01 越界");
        return (ushort)(f * 65535f + 0.5f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Pack16_16_32(ushort high, ushort mid, uint low) =>
        ((ulong)high << 48) | ((ulong)mid << 32) | low;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Pack16_32_16(ushort high, uint mid, ushort low) =>
        ((ulong)high << 48) | ((ulong)mid << 16) | low;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Pack32_32(uint low, uint high) => (high << 32) | low;
}
