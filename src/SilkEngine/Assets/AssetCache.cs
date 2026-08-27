using System.Collections.Concurrent;

namespace SilkEngine.Assets;

/// <summary>
/// AssetId → AssetEntry 的并发缓存（主线程读写；并发字典保证安全性）。
/// 加载/提交/失效生命周期以 AssetId 为主键；实例反向索引仅服务引用计数面（TryAddRef/TryRelease/TryGetAssetId），
/// 材质等实例资产耦合由 M4 任务重构。
/// </summary>
public sealed class AssetCache
{
    private readonly ConcurrentDictionary<AssetId, AssetEntry> _entries = new();

    /// <summary>实例引用 → 条目（引用标识，与 GpuResourceRegistry 同模式）；经 SetData 维护，FindByAsset 未命中时回退线性扫描自愈</summary>
    private readonly ConcurrentDictionary<IAsset, AssetEntry> _byAsset =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>按资产 ID 查找条目；不存在返回 null</summary>
    public AssetEntry? Find(AssetId assetId) =>
        _entries.TryGetValue(assetId, out var entry) ? entry : null;

    /// <summary>取或建条目（新建条目 State=Loading）</summary>
    public AssetEntry GetOrAdd(AssetId assetId) =>
        _entries.GetOrAdd(assetId, static id => new AssetEntry { AssetId = id });

    /// <summary>
    /// 按资产实例引用查找条目（O(1) 索引直查）；未命中回退线性扫描并自愈索引，
    /// 兼容未经 SetData 的直接 entry.Data 赋值（测试夹具模式）。仅供引用计数面使用。
    /// </summary>
    /// <param name="asset">资产实例</param>
    /// <returns>持有该实例的条目；未命中为 null</returns>
    public AssetEntry? FindByAsset(IAsset asset)
    {
        if (asset is null)
            return null;
        if (_byAsset.TryGetValue(asset, out var entry))
        {
            if (ReferenceEquals(entry.Data, asset))
                return entry;
            _byAsset.TryRemove(asset, out _);
        }
        foreach (var candidate in _entries.Values)
            if (ReferenceEquals(candidate.Data, asset))
            {
                _byAsset[asset] = candidate;
                return candidate;
            }
        return null;
    }

    /// <summary>条目数量（测试断言用）</summary>
    internal int Count => _entries.Count;

    /// <summary>移除条目（同步清理反向索引）；不存在返回 false</summary>
    public bool Remove(AssetId assetId)
    {
        if (!_entries.TryRemove(assetId, out var entry))
            return false;
        if (entry.Data is { } data)
            _byAsset.TryRemove(data, out _);
        return true;
    }

    /// <summary>全部条目快照（引用查找/测试断言用）</summary>
    internal IEnumerable<AssetEntry> All() => _entries.Values;

    /// <summary>赋值 Data 并同步反向索引（旧值移除、新值写入）；引擎内 Data 写入唯一入口</summary>
    /// <param name="entry">目标条目</param>
    /// <param name="data">新资产实例；null 表示清空</param>
    internal void SetData(AssetEntry entry, IAsset? data)
    {
        var old = entry.Data;
        if (ReferenceEquals(old, data))
            return;
        entry.Data = data;
        if (old is not null)
            _byAsset.TryRemove(old, out _);
        if (data is not null)
            _byAsset[data] = entry;
    }
}
