using SilkEngine.Assets.Database;
using SilkEngine.Assets.VirtualFileSystem;

namespace SilkEngine.Assets;

/// <summary>
/// 资产目录：按 (源节点, 资产类型) 登记资产，同一组合返回稳定 AssetId，支持按 AssetId 查询。
/// 磁盘模式（传入项目命名空间与虚拟索引）经 <see cref="AssetIdFactory"/> 生成确定性 ID，
/// 接入数据库时在登记点单事务对账 FileNodes 与 Assets；默认构造保持瞬态随机 ID。
/// </summary>
public sealed class AssetCatalog
{
    private readonly Dictionary<(VirtualNodeId Source, AssetTypeId Type), AssetRecord> _bySourceAndType = [];
    private readonly Dictionary<AssetId, AssetRecord> _byId = [];
    private readonly string? _projectNamespace;
    private readonly IVirtualFileIndex? _index;
    private readonly IAssetDatabase? _database;
    private readonly object _databaseGate = new();

    /// <summary>创建瞬态目录：随机生成 AssetId，不接入资产数据库（瞬态资产路径）</summary>
    public AssetCatalog()
    {
    }

    /// <summary>创建磁盘目录：按 (项目命名空间, 规范化逻辑路径, 类型) 生成确定性 AssetId</summary>
    /// <param name="projectNamespace">项目命名空间（磁盘资产身份输入之一）</param>
    /// <param name="index">虚拟文件索引（解析源节点逻辑路径与内容指纹）</param>
    /// <param name="database">资产数据库；非 null 时登记新资产即单事务对账 FileNodes 与 Assets</param>
    /// <exception cref="ArgumentException">projectNamespace 为 null/空白时抛出</exception>
    /// <exception cref="ArgumentNullException">index 为 null 时抛出</exception>
    internal AssetCatalog(string projectNamespace, IVirtualFileIndex index, IAssetDatabase? database = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectNamespace);
        _projectNamespace = projectNamespace;
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _database = database;
    }

    /// <summary>获取或登记源节点的指定类型资产：已存在则返回原记录，否则新建并生成新 AssetId（磁盘模式为确定性 ID，瞬态为随机 ID）。</summary>
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
            AssetId = CreateAssetId(sourceNodeId, assetTypeId),
            SourceNodeId = sourceNodeId,
            AssetTypeId = assetTypeId,
        };
        _bySourceAndType[key] = record;
        _byId[record.AssetId] = record;
        ReconcileDatabase(record);
        return record;
    }

    /// <summary>生成资产 ID：磁盘模式且节点在索引中时经 <see cref="AssetIdFactory"/> 生成，否则回退随机 ID（瞬态）</summary>
    private AssetId CreateAssetId(VirtualNodeId sourceNodeId, AssetTypeId assetTypeId)
    {
        if (_projectNamespace is null || _index is null
            || !_index.TryGet(sourceNodeId, out var node) || node is null)
        {
            return new AssetId(Guid.NewGuid());
        }
        return AssetIdFactory.Create(_projectNamespace, node.LogicalPath, assetTypeId);
    }

    /// <summary>
    /// 登记对账：接入数据库时按规范化逻辑路径与类型单事务更新 FileNodes 与 Assets（内容指纹取自节点元数据）。
    /// 同步等待安全：Microsoft.Data.Sqlite 的异步 API 同步完成（本地库无真正异步 IO），不会跨线程死锁。
    /// </summary>
    /// <param name="record">待对账的资产记录（登记新资产与源变更后的文件指纹/修订更新共用）</param>
    internal void ReconcileDatabase(AssetRecord record)
    {
        if (_database is null || _index is null
            || !_index.TryGet(record.SourceNodeId, out var node) || node is null)
        {
            return;
        }

        var normalizedPath = AssetIdFactory.NormalizePath(node.LogicalPath);
        var fileNode = new AssetDbFileNodeRecord(record.SourceNodeId, normalizedPath);
        var asset = new AssetDbAssetRecord(
            record.AssetId,
            normalizedPath,
            record.AssetTypeId,
            node.MetaData?.SourceFingerprint ?? string.Empty,
            record.SourceRevision);
        lock (_databaseGate)
        {
            _database.ReconcileAsync(fileNode, asset, CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    /// <summary>按资产 ID 查询记录。</summary>
    /// <param name="assetId">资产 ID</param>
    /// <param name="record">命中的资产记录（未命中为 null）</param>
    /// <returns>是否命中</returns>
    public bool TryGet(AssetId assetId, out AssetRecord? record) => _byId.TryGetValue(assetId, out record);

    /// <summary>
    /// 按指定 AssetId 登记记录（目录恢复/测试种子路径；已存在的 (源节点, 类型) 组合返回既有记录）。
    /// </summary>
    /// <param name="sourceNodeId">源虚拟文件系统节点</param>
    /// <param name="assetTypeId">资产类型</param>
    /// <param name="assetId">指定资产 ID</param>
    /// <returns>该 (源节点, 类型) 组合对应的资产记录</returns>
    internal AssetRecord Seed(VirtualNodeId sourceNodeId, AssetTypeId assetTypeId, AssetId assetId)
    {
        var key = (Source: sourceNodeId, Type: assetTypeId);
        if (_bySourceAndType.TryGetValue(key, out var existing))
        {
            return existing;
        }
        var record = new AssetRecord
        {
            AssetId = assetId,
            SourceNodeId = sourceNodeId,
            AssetTypeId = assetTypeId,
        };
        _bySourceAndType[key] = record;
        _byId[record.AssetId] = record;
        return record;
    }

    /// <summary>已登记记录数量（测试断言用）</summary>
    internal int Count => _byId.Count;

    /// <summary>源节点变更：将该源节点的全部资产记录 SourceRevision 递增（缓存失效信号，由 AssetManager.Invalidate 调用）</summary>
    /// <param name="sourceNodeId">源虚拟文件系统节点</param>
    public void InvalidateSource(VirtualNodeId sourceNodeId)
    {
        foreach (var record in _bySourceAndType.Values)
            if (record.SourceNodeId == sourceNodeId)
                record.SourceRevision++;
    }

    /// <summary>枚举指定源节点的全部已登记资产记录（未登记返回空列表；不新建记录，供变更对账使用）</summary>
    /// <param name="sourceNodeId">源虚拟文件系统节点</param>
    /// <returns>该源节点下的资产记录列表</returns>
    internal IReadOnlyList<AssetRecord> GetForSourceNode(VirtualNodeId sourceNodeId)
    {
        var records = new List<AssetRecord>();
        foreach (var (key, record) in _bySourceAndType)
            if (key.Source == sourceNodeId)
                records.Add(record);
        return records;
    }
}
