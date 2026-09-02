namespace SilkEngine.Assets.Serialization;

/// <summary>
/// 资产序列化记录：版本化、类型化、依赖感知的序列化交换载体。
/// 依赖列表在写入时复制（防御外部修改）；相等性按内容比较（含依赖序列与数据），与实例身份无关。
/// </summary>
public record AssetSerializationRecord
{
    private readonly int _schemaVersion;
    private readonly AssetTypeId _typeId;
    private readonly AssetId _assetId;
    private readonly VirtualNodeId? _sourceNodeId;
    private readonly IReadOnlyList<UntypedAssetHandle> _dependencies;
    private readonly string _data;
    private readonly string? _buildKey;
    private readonly string? _sourceFingerprint;
    private readonly ulong? _importerRevision;

    /// <summary>记录 schema 版本（非负）；序列化器声明支持的版本范围必须覆盖该值</summary>
    /// <exception cref="ArgumentOutOfRangeException">版本为负时抛出</exception>
    public int SchemaVersion
    {
        get => _schemaVersion;
        init
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(SchemaVersion), value, "SchemaVersion 必须为非负整数");
            _schemaVersion = value;
        }
    }

    /// <summary>资产类型标识（非空）</summary>
    /// <exception cref="ArgumentException">类型 ID 为 null 或空时抛出</exception>
    public AssetTypeId TypeId
    {
        get => _typeId;
        init
        {
            if (string.IsNullOrEmpty(value.Value))
                throw new ArgumentException("TypeId 不能为 null 或空", nameof(TypeId));
            _typeId = value;
        }
    }

    /// <summary>资产唯一标识</summary>
    public AssetId AssetId
    {
        get => _assetId;
        init => _assetId = value;
    }

    /// <summary>源虚拟节点标识（可选；缺失表示无源节点，如程序化生成资产）</summary>
    public VirtualNodeId? SourceNodeId
    {
        get => _sourceNodeId;
        init => _sourceNodeId = value;
    }

    /// <summary>依赖句柄只读列表（写入时复制为快照，读取不暴露可变引用）</summary>
    public IReadOnlyList<UntypedAssetHandle> Dependencies
    {
        get => _dependencies;
        init => _dependencies = value?.ToArray() ?? [];
    }

    /// <summary>序列化数据载体（字符串编码的载荷）</summary>
    /// <exception cref="ArgumentNullException">数据为 null 时抛出</exception>
    public string Data
    {
        get => _data;
        init => _data = value ?? throw new ArgumentNullException(nameof(Data));
    }

    /// <summary>构建键（可选；记录来源构建的缓存键，命中校验用）</summary>
    public string? BuildKey
    {
        get => _buildKey;
        init => _buildKey = value;
    }

    /// <summary>源内容指纹（可选；构建时源文件 SHA-256，命中校验用）</summary>
    public string? SourceFingerprint
    {
        get => _sourceFingerprint;
        init => _sourceFingerprint = value;
    }

    /// <summary>导入器修订号（可选；构建时导入器修订，命中校验用）</summary>
    public ulong? ImporterRevision
    {
        get => _importerRevision;
        init => _importerRevision = value;
    }

    /// <summary>按内容比较两条记录（schema 版本、类型、资产 ID、源节点、依赖序列、数据及构建键语义字段均一致）</summary>
    /// <param name="other">待比较记录</param>
    /// <returns>内容完全一致时为 true</returns>
    public virtual bool Equals(AssetSerializationRecord? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;

        return _schemaVersion == other._schemaVersion
            && _typeId == other._typeId
            && _assetId == other._assetId
            && _sourceNodeId == other._sourceNodeId
            && _data == other._data
            && _buildKey == other._buildKey
            && _sourceFingerprint == other._sourceFingerprint
            && _importerRevision == other._importerRevision
            && DependenciesEqual(_dependencies, other._dependencies);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_schemaVersion);
        hash.Add(_typeId);
        hash.Add(_assetId);
        hash.Add(_sourceNodeId);
        hash.Add(_data);
        hash.Add(_buildKey);
        hash.Add(_sourceFingerprint);
        hash.Add(_importerRevision);
        foreach (var dependency in _dependencies)
            hash.Add(dependency);
        return hash.ToHashCode();
    }

    private static bool DependenciesEqual(IReadOnlyList<UntypedAssetHandle> a, IReadOnlyList<UntypedAssetHandle> b)
    {
        if (a.Count != b.Count)
            return false;
        for (var i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
                return false;
        }
        return true;
    }
}
