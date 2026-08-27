namespace SilkEngine.Assets;

/// <summary>资产目录：按 (源节点, 资产类型) 登记资产，同一组合返回稳定 AssetId，支持按 AssetId 查询</summary>
public sealed class AssetCatalog
{
    private readonly Dictionary<(VirtualNodeId Source, AssetTypeId Type), AssetRecord> _bySourceAndType = [];
    private readonly Dictionary<AssetId, AssetRecord> _byId = [];

    /// <summary>获取或登记源节点的指定类型资产：已存在则返回原记录，否则新建并生成新 AssetId。</summary>
    /// <param name="sourceNodeId">源虚拟文件系统节点</param>
    /// <param name="assetTypeId">资产类型</param>
    /// <returns>该 (源节点, 类型) 组合对应的资产记录</returns>
    public AssetRecord GetOrAdd(VirtualNodeId sourceNodeId, AssetTypeId assetTypeId)
    {
        var key = (Source: sourceNodeId, Type: assetTypeId);
        if (_bySourceAndType.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var record = new AssetRecord
        {
            AssetId = new AssetId(Guid.NewGuid()),
            SourceNodeId = sourceNodeId,
            AssetTypeId = assetTypeId,
        };
        _bySourceAndType[key] = record;
        _byId[record.AssetId] = record;
        return record;
    }

    /// <summary>按资产 ID 查询记录。</summary>
    /// <param name="assetId">资产 ID</param>
    /// <param name="record">命中的资产记录（未命中为 null）</param>
    /// <returns>是否命中</returns>
    public bool TryGet(AssetId assetId, out AssetRecord? record) => _byId.TryGetValue(assetId, out record);
}
