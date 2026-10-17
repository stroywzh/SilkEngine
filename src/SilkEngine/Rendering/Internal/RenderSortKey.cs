using System;

namespace SilkEngine.Rendering.Internal;

public readonly struct RenderItemSortKey
    : IComparable<RenderItemSortKey>,
        IEquatable<RenderItemSortKey>
{
    public readonly ulong PackedData;

    public RenderItemSortKey(ulong key)
    {
        PackedData = key;
    }

    public int CompareTo(RenderItemSortKey other) => PackedData.CompareTo(other.PackedData);

    public bool Equals(RenderItemSortKey other) => PackedData == other.PackedData;

    public override bool Equals(object? obj) => obj is RenderItemSortKey other && Equals(other);

    public override int GetHashCode() => PackedData.GetHashCode();

    public override string ToString() => $"SortKey(0x{PackedData:X16})";
}
