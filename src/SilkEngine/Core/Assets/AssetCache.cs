using System.Collections.Concurrent;

namespace SilkEngine.Core.Assets;

/// <summary>GUID → AssetEntry 的并发缓存（主线程读写；并发字典保证安全性）</summary>
public sealed class AssetCache
{
    private readonly ConcurrentDictionary<Guid, AssetEntry> _entries = new();

    /// <summary>按 GUID 查找条目；不存在返回 null</summary>
    public AssetEntry? Find(Guid guid) =>
        _entries.TryGetValue(guid, out var entry) ? entry : null;

    /// <summary>取或建条目（新建条目 State=Loading）</summary>
    public AssetEntry GetOrAdd(Guid guid) =>
        _entries.GetOrAdd(guid, static g => new AssetEntry { Guid = g });

    /// <summary>条目数量（测试断言用）</summary>
    public int Count => _entries.Count;

    /// <summary>移除条目；不存在返回 false</summary>
    public bool Remove(Guid guid) => _entries.TryRemove(guid, out _);

    /// <summary>全部条目快照（引用查找/测试断言用）</summary>
    internal IEnumerable<AssetEntry> All() => _entries.Values;
}
