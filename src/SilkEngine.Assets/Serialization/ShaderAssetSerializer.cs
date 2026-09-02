using System.IO;

namespace SilkEngine.Assets.Serialization;

/// <summary>着色器资产序列化器：编码名称、HLSL 源码与入口/profile（schema 版本 1）</summary>
public sealed class ShaderAssetSerializer : AssetSerializerBase
{
    /// <summary>着色器资产类型标识</summary>
    public static readonly AssetTypeId StaticTypeId = new("shader");

    /// <inheritdoc />
    public override AssetTypeId TypeId => StaticTypeId;

    /// <inheritdoc />
    public override int MinVersion => 1;

    /// <inheritdoc />
    public override int MaxVersion => 1;

    /// <summary>将着色器资产编码为记录</summary>
    /// <param name="asset">着色器资产；类型不匹配抛 <see cref="ArgumentException"/></param>
    /// <returns>序列化记录</returns>
    public override AssetSerializationRecord Serialize(object asset)
    {
        if (asset is not ShaderAsset shader)
            throw new ArgumentException($"期望 ShaderAsset，实际 {asset.GetType().Name}", nameof(asset));

        var dto = new ShaderDto
        {
            Name = shader.Name,
            Source = shader.Source,
            VertexEntryPoint = shader.VertexEntryPoint,
            FragmentEntryPoint = shader.FragmentEntryPoint,
            Profile = shader.Profile,
        };

        return new AssetSerializationRecord
        {
            SchemaVersion = MaxVersion,
            TypeId = TypeId,
            Dependencies = [],
            Data = EncodeData(dto),
        };
    }

    /// <summary>从记录解码着色器资产；类型/版本不匹配抛 <see cref="NotSupportedException"/>，数据损坏抛 <see cref="InvalidDataException"/></summary>
    /// <param name="record">序列化记录</param>
    /// <param name="resolver">依赖解析器（着色器无依赖）</param>
    /// <returns>着色器资产</returns>
    public override object Deserialize(AssetSerializationRecord record, IAssetReferenceResolver resolver)
    {
        EnsureCompatible(record);
        var dto = ParseData<ShaderDto>(record);

        if (dto.Source == null)
            throw new InvalidDataException($"着色器记录缺少源码字段（资产 {record.AssetId.Value}）");

        return new ShaderAsset(
            dto.Name ?? string.Empty,
            dto.Source,
            dto.VertexEntryPoint ?? "vert",
            dto.FragmentEntryPoint ?? "frag",
            dto.Profile ?? "sm_6_0");
    }

    /// <summary>着色器编码载体（显式字段，禁止反射推断）</summary>
    private sealed class ShaderDto
    {
        /// <summary>着色器名称</summary>
        public string? Name { get; set; }

        /// <summary>HLSL 源码</summary>
        public string? Source { get; set; }

        /// <summary>顶点着色器入口函数名</summary>
        public string? VertexEntryPoint { get; set; }

        /// <summary>片段着色器入口函数名</summary>
        public string? FragmentEntryPoint { get; set; }

        /// <summary>着色模型配置文件</summary>
        public string? Profile { get; set; }
    }
}