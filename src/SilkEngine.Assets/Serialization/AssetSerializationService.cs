using System.IO;
using System.Text;
using System.Text.Json;

namespace SilkEngine.Assets.Serialization;

/// <summary>
/// 资产反序列化服务：读取序列化记录 → 解析序列化器与 schema → 以 visited/active 集合深度优先遍历依赖
/// → 全部依赖成功后反序列化并写入已发布字典（原子发布：任一失败不留下半成品）。
/// 幂等：已发布资产重复反序列化直接返回既有实例，不重复解析依赖。
/// 实例可被多 Worker 共享：发布字典与图遍历经内部锁串行化。
/// </summary>
public sealed class AssetSerializationService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IAssetSerializerRegistry _registry;
    private readonly IAssetReferenceResolver _resolver;
    private readonly Dictionary<AssetId, object> _published = [];
    private readonly object _gate = new();

    /// <summary>创建反序列化服务</summary>
    /// <param name="registry">序列化器注册表</param>
    /// <param name="resolver">引用解析器（兼记录目录）</param>
    public AssetSerializationService(IAssetSerializerRegistry registry, IAssetReferenceResolver resolver)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    /// <summary>
    /// 按资产 ID 反序列化资产（含全部依赖并原子发布）。
    /// 失败语义：记录缺失抛 <see cref="KeyNotFoundException"/>；循环依赖或数据损坏抛 <see cref="InvalidDataException"/>；
    /// 未知类型或版本不支持抛 <see cref="NotSupportedException"/>。错误消息携带资产 ID、类型、源节点与依赖 ID。
    /// </summary>
    /// <param name="assetId">资产 ID</param>
    /// <returns>反序列化结果（IsSuccess 恒为 true；失败以异常抛出）</returns>
    public AssetDeserializationResult Deserialize(AssetId assetId)
    {
        lock (_gate)
        {
            var asset = DeserializeCore(assetId, [], seed: null);
            return new AssetDeserializationResult(IsSuccess: true, asset, assetId);
        }
    }

    /// <summary>
    /// 从给定根记录反序列化资产（含全部依赖并原子发布）；依赖记录仍经 resolver 查询。
    /// 语义与按 ID 反序列化一致；用于构建产物缓存命中路径（根记录已从磁盘解码，无需再查存储）。
    /// </summary>
    /// <param name="record">根记录；null 抛 <see cref="ArgumentNullException"/></param>
    /// <returns>反序列化结果（IsSuccess 恒为 true；失败以异常抛出）</returns>
    public AssetDeserializationResult Deserialize(AssetSerializationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        lock (_gate)
        {
            var asset = DeserializeCore(record.AssetId, [], record);
            return new AssetDeserializationResult(IsSuccess: true, asset, record.AssetId);
        }
    }

    /// <summary>判断资产是否已成功反序列化并发布</summary>
    /// <param name="assetId">资产 ID</param>
    /// <returns>已发布返回 true</returns>
    public bool Contains(AssetId assetId)
    {
        lock (_gate)
        {
            return _published.ContainsKey(assetId);
        }
    }

    /// <summary>
    /// 将序列化记录编码为派生字节（显式 DTO 的 UTF-8 JSON；供构建产物缓存持久化）。
    /// 编码不接触 GPU/Scene 对象，只搬运记录字段。
    /// </summary>
    /// <param name="record">待编码记录；null 抛 <see cref="ArgumentNullException"/></param>
    /// <returns>派生字节</returns>
    public byte[] EncodeRecord(AssetSerializationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var dto = new RecordDto
        {
            SchemaVersion = record.SchemaVersion,
            TypeId = record.TypeId.Value,
            AssetId = record.AssetId.Value.ToString("N"),
            SourceNodeId = record.SourceNodeId?.Value.ToString("N"),
            Dependencies = record.Dependencies
                .Select(dep => new DependencyDto { Id = dep.Id.Value.ToString("N"), TypeId = dep.TypeId.Value })
                .ToList(),
            Data = record.Data,
            BuildKey = record.BuildKey,
            SourceFingerprint = record.SourceFingerprint,
            ImporterRevision = record.ImporterRevision,
        };
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dto, JsonOptions));
    }

    /// <summary>将派生字节解码为序列化记录；字节损坏或结构非法抛 <see cref="InvalidDataException"/></summary>
    /// <param name="bytes">派生字节</param>
    /// <returns>解码后的记录</returns>
    public AssetSerializationRecord DecodeRecord(ReadOnlyMemory<byte> bytes)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<RecordDto>(bytes.Span, JsonOptions)
                ?? throw new InvalidDataException("派生字节为空");
            if (dto.SchemaVersion < 0)
                throw new InvalidDataException($"派生字节 schema 版本非法：{dto.SchemaVersion}");
            if (string.IsNullOrEmpty(dto.TypeId))
                throw new InvalidDataException("派生字节缺少类型标识");
            if (!Guid.TryParse(dto.AssetId, out var assetId))
                throw new InvalidDataException($"派生字节资产 ID 非法：'{dto.AssetId}'");
            if (dto.Data is null)
                throw new InvalidDataException("派生字节缺少序列化数据");

            var dependencies = new List<UntypedAssetHandle>(dto.Dependencies.Count);
            foreach (var dependency in dto.Dependencies)
            {
                if (!Guid.TryParse(dependency.Id, out var dependencyId))
                    throw new InvalidDataException($"派生字节依赖 ID 非法：'{dependency.Id}'");
                dependencies.Add(new UntypedAssetHandle(
                    new AssetId(dependencyId),
                    dependency.TypeId is null ? default : new AssetTypeId(dependency.TypeId)));
            }

            return new AssetSerializationRecord
            {
                SchemaVersion = dto.SchemaVersion,
                TypeId = new AssetTypeId(dto.TypeId),
                AssetId = new AssetId(assetId),
                SourceNodeId = dto.SourceNodeId is { } sourceNodeId
                    ? TryParseNodeId(sourceNodeId)
                    : null,
                Dependencies = dependencies,
                Data = dto.Data,
                BuildKey = dto.BuildKey,
                SourceFingerprint = dto.SourceFingerprint,
                ImporterRevision = dto.ImporterRevision,
            };
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"派生字节 JSON 损坏：{ex.Message}", ex);
        }
        catch (NotSupportedException ex)
        {
            throw new InvalidDataException($"派生字节 JSON 不受支持：{ex.Message}", ex);
        }
    }

    private VirtualNodeId? TryParseNodeId(string value)
        => Guid.TryParse(value, out var id) ? new VirtualNodeId(id) : null;

    private object DeserializeCore(AssetId assetId, HashSet<AssetId> active, AssetSerializationRecord? seed)
    {
        if (_published.TryGetValue(assetId, out var existing))
            return existing;

        var record = seed ?? _resolver.TryGetRecord(assetId)
            ?? throw new KeyNotFoundException($"资产 {assetId.Value} 未找到序列化记录");

        if (!active.Add(assetId))
            throw new InvalidDataException(
                $"检测到资产依赖循环：资产 {record.AssetId.Value}（类型 {record.TypeId.Value}，源节点 {record.SourceNodeId?.Value}）");

        var serializer = _registry.Resolve(record.TypeId, record.SchemaVersion);

        foreach (var dependency in record.Dependencies)
        {
            try
            {
                DeserializeCore(dependency.Id, active, seed: null);
            }
            catch (KeyNotFoundException)
            {
                throw new KeyNotFoundException(
                    $"资产 {record.AssetId.Value}（类型 {record.TypeId.Value}，源节点 {record.SourceNodeId?.Value}）的依赖 {dependency.Id.Value}（类型 {dependency.TypeId.Value}）未找到序列化记录");
            }

            _resolver.Resolve(dependency);
        }

        var asset = serializer.Deserialize(record, _resolver);
        _published[assetId] = asset;
        active.Remove(assetId);
        return asset;
    }

    /// <summary>记录编码载体（显式字段，禁止反射推断）</summary>
    private sealed class RecordDto
    {
        /// <summary>记录 schema 版本</summary>
        public int SchemaVersion { get; set; }

        /// <summary>类型标识</summary>
        public string TypeId { get; set; } = string.Empty;

        /// <summary>资产 ID（GUID 十六进制）</summary>
        public string AssetId { get; set; } = string.Empty;

        /// <summary>源节点 ID（GUID 十六进制；可 null）</summary>
        public string? SourceNodeId { get; set; }

        /// <summary>依赖句柄列表</summary>
        public List<DependencyDto> Dependencies { get; set; } = [];

        /// <summary>序列化数据载体</summary>
        public string Data { get; set; } = string.Empty;

        /// <summary>构建键（可 null）</summary>
        public string? BuildKey { get; set; }

        /// <summary>源内容指纹（可 null）</summary>
        public string? SourceFingerprint { get; set; }

        /// <summary>导入器修订号（可 null）</summary>
        public ulong? ImporterRevision { get; set; }
    }

    /// <summary>依赖句柄编码载体（显式字段，禁止反射推断）</summary>
    private sealed class DependencyDto
    {
        /// <summary>依赖资产 ID（GUID 十六进制）</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>依赖类型标识（可 null）</summary>
        public string? TypeId { get; set; }
    }
}

/// <summary>反序列化结果：IsSuccess 恒为 true（失败路径以异常抛出）</summary>
/// <param name="IsSuccess">是否成功</param>
/// <param name="Asset">反序列化出的资产实例</param>
/// <param name="AssetId">资产 ID</param>
public readonly record struct AssetDeserializationResult(bool IsSuccess, object Asset, AssetId AssetId);