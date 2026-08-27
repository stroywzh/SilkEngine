using System.Collections.Concurrent;

namespace SilkEngine.Assets;

/// <summary>
/// AssetId → AssetEntry 的并发缓存（主线程读写；并发字典保证安全性）。
/// 加载/提交/失效生命周期以 AssetId 为主键。
/// </summary>
public sealed class AssetCache
{
    private readonly ConcurrentDictionary<AssetId, AssetEntry> _entries = new();

    /// <summary>按资产 ID 查找条目；不存在返回 null</summary>
    public AssetEntry? Find(AssetId assetId) =>
        _entries.TryGetValue(assetId, out var entry) ? entry : null;

    /// <summary>取或建条目（新建条目 State=Loading）</summary>
    public AssetEntry GetOrAdd(AssetId assetId) =>
        _entries.GetOrAdd(assetId, static id => new AssetEntry { AssetId = id });

    /// <summary>条目数量（测试断言用）</summary>
    internal int Count => _entries.Count;

    /// <summary>移除条目；不存在返回 false</summary>
    public bool Remove(AssetId assetId) => _entries.TryRemove(assetId, out _);

    /// <summary>全部条目快照（测试断言用）</summary>
    internal IEnumerable<AssetEntry> All() => _entries.Values;

    /// <summary>赋值 Payload；引擎内 Payload 写入唯一入口</summary>
    /// <param name="entry">目标条目</param>
    /// <param name="payload">新载荷；null 表示清空</param>
    internal void SetPayload(AssetEntry entry, IAssetPayload? payload) => entry.Payload = payload;
}
