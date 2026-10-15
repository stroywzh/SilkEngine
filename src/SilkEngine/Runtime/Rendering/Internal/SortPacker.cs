using System;
using System.Runtime.CompilerServices;

namespace SilkEngine.Runtime.Rendering.Internal;

/// <summary>
/// 通用 64 位排序键打包器。
/// 默认布局：low 16bit | mid 32bit | high 16bit。
/// 升序排序时：high 优先，其次 mid，最后 low。
/// </summary>
public static class SortKeyPacker
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Pack(ushort low16, uint mid32, ushort high16) =>
        (ulong)low16 | ((ulong)mid32 << 16) | ((ulong)high16 << 48);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort GetLow16(ulong key) => (ushort)(key & ushort.MaxValue);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetMid32(ulong key) => (uint)((key >> 16) & uint.MaxValue);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort GetHigh16(ulong key) => (ushort)((key >> 48) & ushort.MaxValue);

    /// <summary>
    /// 把 float 位模式变成单调 uint：
    /// 正数保持，负数翻转符号相关位，使大小关系单调。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint MonotonicFloatBits(float value)
    {
        uint bits = BitConverter.SingleToUInt32Bits(value);

        // 负数时翻转除符号位外所有位；正数时只翻转符号位。
        uint mask = (uint)((int)bits >> 31) | 0x8000_0000u;
        bits ^= mask;

        return bits;
    }
}
