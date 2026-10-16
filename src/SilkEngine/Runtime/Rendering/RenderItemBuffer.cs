using System;

namespace SilkEngine.Runtime.Rendering;

/// <summary>
/// Stores a set of <see cref="RenderItem"/> instances and exposes them ordered by their packed sort key.
/// <br/>Used by <see cref="CommandBuffer"/> to collect draw items before submission.
/// </summary>
/// <seealso cref="RenderItem"/>
/// <seealso cref="CommandBuffer"/>
public sealed class RenderItemBuffer
{
    private const int DefaultCapacity = 256;
    private RenderItem[] _items;
    private ulong[] _keys;
    private int _count;
    private bool _isSorted = false;
    public int Count => _count;
    public int Capacity => _items.Length;

    public RenderItemBuffer(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException("Capacity should bigger than zero.");
        }
        _items = new RenderItem[capacity];
        _keys = new ulong[capacity];
        _count = 0;
    }

    /// <summary>
    /// Dirctory submit a <see cref="RenderItem"/>
    /// </summary>
    /// <param name="item"></param>
    public void Submit(in RenderItem item)
    {
        EnsureCapacity(_count + 1);

        _items[_count] = item;
        _keys[_count] = item.SortKey.PackedData;
        _count++;

        _isSorted = false;
    }

    /// <summary>
    /// Submit a <see cref="RenderItem"/>  by its' props.
    /// </summary>
    /// <param name="mode"></param>
    /// <param name="queue"></param>
    /// <param name="depth01"></param>
    /// <param name="materialId"></param>
    /// <param name="meshId"></param>
    public void Submit(
        RenderItemSortMode mode,
        ushort queue,
        float depth01,
        ushort materialId,
        int meshId
    )
    {
        Submit(new RenderItem(mode, queue, depth01, materialId, meshId));
    }

    public void Clear()
    {
        Array.Clear(_items, 0, _count);
        _count = 0;
        _isSorted = true;
    }

    public ReadOnlySpan<RenderItem> GetSortedRenderItems()
    {
        if (!_isSorted && _count > 1)
        {
            Array.Sort(_keys, _items, 0, _count);
            _isSorted = true;
        }
        return _items.AsSpan(0, _count);
    }

    private void EnsureCapacity(int required)
    {
        if (required <= _items.Length)
        {
            return;
        }

        int newCapacity = Math.Max(required, _items.Length * 2);

        Array.Resize(ref _items, newCapacity);
        Array.Resize(ref _keys, newCapacity);
    }
}
