using System;

namespace SilkEngine.Runtime.Rendering;

public sealed class RenderCommandQueue
{
    private const int DefaultCapacity = 256;
    private RenderItem[] _items;
    private ulong[] _keys;
    private int _count;
    public int Count => _count;
    public int Capacity => _items.Length;

    public RenderCommandQueue(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException("Capacity should bigger than zero.");
        }
        _items = new RenderItem[capacity];
        _keys = new ulong[capacity];
        _count = 0;
    }

    public void Submit(in RenderItem item)
    {
        EnsureCapacity(_count + 1);

        _items[_count] = item;
        _keys[_count] = item.SortKey.PackedData;
        _count++;
    }

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
    }

    public ReadOnlySpan<RenderItem> GetSortedRenderItems()
    {
        if (_count > 1)
        {
            Array.Sort(_keys, _items, 0, _count);
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
