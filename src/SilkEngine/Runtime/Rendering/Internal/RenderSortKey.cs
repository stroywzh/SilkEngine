using System;

namespace SilkEngine.Runtime.Rendering.Internal;

public readonly struct RenderItemSortKey
    : IComparable<RenderItemSortKey>,
        IEquatable<RenderItemSortKey>
{
    public readonly ulong PackedData;

    public RenderItemSortKey(ulong key)
    {
        PackedData = key;
    }

    public ushort Low16 => (ushort)(PackedData & ushort.MaxValue);
    public uint Mid32 => (uint)((PackedData >> 16) & uint.MaxValue);
    public ushort High16 => (ushort)((PackedData >> 48) & ushort.MaxValue);

    public int CompareTo(RenderItemSortKey other) => PackedData.CompareTo(other.PackedData);

    public bool Equals(RenderItemSortKey other) => PackedData == other.PackedData;

    public override bool Equals(object? obj) => obj is RenderItemSortKey other && Equals(other);

    public override int GetHashCode() => PackedData.GetHashCode();

    public override string ToString() => $"SortKey(0x{PackedData:X16})";
}
