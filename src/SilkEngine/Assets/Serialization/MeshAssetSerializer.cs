using System.IO;

namespace SilkEngine.Assets.Serialization;

/// <summary>网格资产序列化器：编码名称与顶点/布局/索引数据（schema 版本 1；索引可缺省表示非索引绘制）</summary>
public sealed class MeshAssetSerializer : AssetSerializerBase
{
    /// <summary>网格资产类型标识</summary>
    public static readonly AssetTypeId StaticTypeId = new("mesh");

    /// <inheritdoc />
    public override AssetTypeId TypeId => StaticTypeId;

    /// <inheritdoc />
    public override int MinVersion => 1;

    /// <inheritdoc />
    public override int MaxVersion => 1;

    /// <summary>将网格资产编码为记录</summary>
    /// <param name="asset">网格资产；类型不匹配抛 <see cref="ArgumentException"/></param>
    /// <returns>序列化记录</returns>
    public override AssetSerializationRecord Serialize(object asset)
    {
        if (asset is not MeshAsset mesh)
            throw new ArgumentException($"期望 MeshAsset，实际 {asset.GetType().Name}", nameof(asset));

        var dto = new MeshDto
        {
            Name = mesh.Name,
            Vertices = mesh.Vertices,
            Layout = mesh.Layout,
            Indices = mesh.Indices,
        };

        return new AssetSerializationRecord
        {
            SchemaVersion = MaxVersion,
            TypeId = TypeId,
            Dependencies = [],
            Data = EncodeData(dto),
        };
    }

    /// <summary>从记录解码网格资产；类型/版本不匹配抛 <see cref="NotSupportedException"/>，数据损坏抛 <see cref="InvalidDataException"/></summary>
    /// <param name="record">序列化记录</param>
    /// <param name="resolver">依赖解析器（网格无依赖）</param>
    /// <returns>网格资产</returns>
    public override object Deserialize(AssetSerializationRecord record, IAssetReferenceResolver resolver)
    {
        EnsureCompatible(record);
        var dto = ParseData<MeshDto>(record);

        if (dto.Vertices == null || dto.Layout == null)
            throw new InvalidDataException($"网格记录缺少顶点或布局字段（资产 {record.AssetId.Value}）");

        return new MeshAsset(dto.Name ?? string.Empty, dto.Vertices, dto.Layout, dto.Indices);
    }

    /// <summary>网格编码载体（显式字段，禁止反射推断）</summary>
    private sealed class MeshDto
    {
        /// <summary>网格名称</summary>
        public string? Name { get; set; }

        /// <summary>顶点数据（按 Layout 分量顺序排列的连续 float 数组）</summary>
        public float[]? Vertices { get; set; }

        /// <summary>顶点属性布局（每属性分量数）</summary>
        public int[]? Layout { get; set; }

        /// <summary>索引数据（缺省为 null，表示非索引绘制）</summary>
        public int[]? Indices { get; set; }
    }
}
