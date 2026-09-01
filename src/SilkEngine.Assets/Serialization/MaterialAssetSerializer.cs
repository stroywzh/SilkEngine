using System.IO;
using System.Linq;
using SilkEngine.Math;
using SilkEngine.Render;

namespace SilkEngine.Assets.Serialization;

/// <summary>
/// 材质资产序列化器：只编码 untyped 依赖句柄（着色器/主纹理）、默认参数与修订号元数据；
/// 绝不写入 Material 实例覆盖、GPU 对象、AssetManager 引用或运行时 lease（schema 版本 1）。
/// </summary>
public sealed class MaterialAssetSerializer : AssetSerializerBase
{
    /// <summary>材质资产类型标识</summary>
    public static readonly AssetTypeId StaticTypeId = new("material");

    /// <inheritdoc />
    public override AssetTypeId TypeId => StaticTypeId;

    /// <inheritdoc />
    public override int MinVersion => 1;

    /// <inheritdoc />
    public override int MaxVersion => 1;

    /// <summary>将材质资产编码为记录；依赖为着色器（必需）与主纹理（可选）句柄</summary>
    /// <param name="asset">材质资产；类型不匹配抛 <see cref="ArgumentException"/></param>
    /// <returns>序列化记录</returns>
    public override AssetSerializationRecord Serialize(object asset)
    {
        if (asset is not MaterialAsset material)
            throw new ArgumentException($"期望 MaterialAsset，实际 {asset.GetType().Name}", nameof(asset));

        var dependencies = new List<UntypedAssetHandle>
        {
            new(material.Shader.Id, ShaderAssetSerializer.StaticTypeId),
        };
        if (material.MainTexture is { } texture)
            dependencies.Add(new UntypedAssetHandle(texture.Id, TextureAssetSerializer.StaticTypeId));

        var dto = new MaterialDto
        {
            Revision = material.Revision,
            Parameters = material.Defaults.Select(p => new ParameterDto
            {
                Name = p.Name,
                Kind = KindOf(p.Value),
                Value = ValueOf(p.Value),
            }).ToList(),
        };

        return new AssetSerializationRecord
        {
            SchemaVersion = MaxVersion,
            TypeId = TypeId,
            AssetId = material.Id,
            Dependencies = dependencies,
            Data = EncodeData(dto),
        };
    }

    /// <summary>从记录解码材质资产；类型/版本不匹配抛 <see cref="NotSupportedException"/>，数据损坏或依赖缺失抛 <see cref="InvalidDataException"/></summary>
    /// <param name="record">序列化记录</param>
    /// <param name="resolver">依赖解析器（材质重建仅需句柄，不解析对象）</param>
    /// <returns>材质资产</returns>
    public override object Deserialize(AssetSerializationRecord record, IAssetReferenceResolver resolver)
    {
        EnsureCompatible(record);
        var dto = ParseData<MaterialDto>(record);

        var shaderDep = record.Dependencies.FirstOrDefault(d => d.TypeId == ShaderAssetSerializer.StaticTypeId);
        if (shaderDep.Id == default)
            throw new InvalidDataException(
                $"材质记录缺少着色器依赖（资产 {record.AssetId.Value}，源节点 {record.SourceNodeId?.Value}）");

        var textureDep = record.Dependencies.FirstOrDefault(d => d.TypeId == TextureAssetSerializer.StaticTypeId);
        AssetHandle<TextureAsset>? mainTexture = textureDep.Id == default
            ? null
            : new AssetHandle<TextureAsset>(textureDep.Id);

        MaterialParameterSnapshot defaults;
        try
        {
            defaults = new MaterialParameterSnapshot(dto.Parameters.Select(p => (p.Name, ToValue(p))));
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ArgumentException or NullReferenceException)
        {
            throw new InvalidDataException($"材质记录默认参数损坏（资产 {record.AssetId.Value}）：{ex.Message}", ex);
        }

        return new MaterialAsset(
            record.AssetId,
            new AssetHandle<ShaderAsset>(shaderDep.Id),
            mainTexture,
            defaults,
            dto.Revision);
    }

    private static string KindOf(MaterialValue value) => value.Kind switch
    {
        MaterialValue.ValueKind.Float => "float",
        MaterialValue.ValueKind.Vector3 => "vector3",
        MaterialValue.ValueKind.Matrix4x4 => "matrix4x4",
        _ => throw new InvalidDataException($"未知材质参数类型 {value.Kind}"),
    };

    private static float[] ValueOf(MaterialValue value)
    {
        if (value.TryGetFloat(out var f))
            return [f];
        if (value.TryGetVector3(out var v))
            return [v.X, v.Y, v.Z];
        if (value.TryGetMatrix4x4(out var m))
            return ToRowMajor(m);
        throw new InvalidDataException($"未知材质参数类型 {value.Kind}");
    }

    private static MaterialValue ToValue(ParameterDto parameter)
    {
        switch (parameter.Kind)
        {
            case "float" when parameter.Value.Length == 1:
                return MaterialValue.Float(parameter.Value[0]);
            case "vector3" when parameter.Value.Length == 3:
                return MaterialValue.Vector3(new Vector3(parameter.Value[0], parameter.Value[1], parameter.Value[2]));
            case "matrix4x4" when parameter.Value.Length == 16:
                return MaterialValue.Matrix4x4(FromRowMajor(parameter.Value));
            default:
                throw new InvalidDataException(
                    $"未知参数类型 '{parameter.Kind}' 或值长度 {parameter.Value.Length}（参数 '{parameter.Name}'）");
        }
    }

    private static float[] ToRowMajor(Matrix4x4 m) =>
    [
        m.M11, m.M12, m.M13, m.M14,
        m.M21, m.M22, m.M23, m.M24,
        m.M31, m.M32, m.M33, m.M34,
        m.M41, m.M42, m.M43, m.M44,
    ];

    private static Matrix4x4 FromRowMajor(float[] v) => new()
    {
        M11 = v[0], M12 = v[1], M13 = v[2], M14 = v[3],
        M21 = v[4], M22 = v[5], M23 = v[6], M24 = v[7],
        M31 = v[8], M32 = v[9], M33 = v[10], M34 = v[11],
        M41 = v[12], M42 = v[13], M43 = v[14], M44 = v[15],
    };

    /// <summary>材质编码载体（显式字段，禁止反射推断）</summary>
    private sealed class MaterialDto
    {
        /// <summary>源资产修订号</summary>
        public ulong Revision { get; set; }

        /// <summary>默认参数列表</summary>
        public List<ParameterDto> Parameters { get; set; } = [];
    }

    /// <summary>参数编码载体（显式字段，禁止反射推断）</summary>
    private sealed class ParameterDto
    {
        /// <summary>参数名称</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>参数类型（float / vector3 / matrix4x4）</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>参数值（float 1 个 / vector3 3 个 / matrix4x4 16 个行主序 float）</summary>
        public float[] Value { get; set; } = [];
    }
}
