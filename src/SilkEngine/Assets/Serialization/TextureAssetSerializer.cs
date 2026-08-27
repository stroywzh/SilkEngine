using System.IO;

namespace SilkEngine.Assets.Serialization;

/// <summary>纹理资产序列化器：编码名称与 RGBA8 图像数据（schema 版本 1）</summary>
public sealed class TextureAssetSerializer : AssetSerializerBase
{
    /// <summary>纹理资产类型标识（与导入器注册表一致）</summary>
    public static readonly AssetTypeId StaticTypeId = new("texture");

    /// <inheritdoc />
    public override AssetTypeId TypeId => StaticTypeId;

    /// <inheritdoc />
    public override int MinVersion => 1;

    /// <inheritdoc />
    public override int MaxVersion => 1;

    /// <summary>将纹理资产编码为记录；像素数据以 base64 嵌入</summary>
    /// <param name="asset">纹理资产；类型不匹配抛 <see cref="ArgumentException"/></param>
    /// <returns>序列化记录</returns>
    public override AssetSerializationRecord Serialize(object asset)
    {
        if (asset is not TextureAsset texture)
            throw new ArgumentException($"期望 TextureAsset，实际 {asset.GetType().Name}", nameof(asset));

        var dto = new TextureDto
        {
            Name = texture.Name,
            Width = texture.Data.Width,
            Height = texture.Data.Height,
            Pixels = Convert.ToBase64String(texture.Data.RawBytes),
        };

        return new AssetSerializationRecord
        {
            SchemaVersion = MaxVersion,
            TypeId = TypeId,
            Dependencies = [],
            Data = EncodeData(dto),
        };
    }

    /// <summary>从记录解码纹理资产；类型/版本不匹配抛 <see cref="NotSupportedException"/>，数据损坏抛 <see cref="InvalidDataException"/></summary>
    /// <param name="record">序列化记录</param>
    /// <param name="resolver">依赖解析器（纹理无依赖）</param>
    /// <returns>纹理资产</returns>
    public override object Deserialize(AssetSerializationRecord record, IAssetReferenceResolver resolver)
    {
        EnsureCompatible(record);
        var dto = ParseData<TextureDto>(record);

        if (string.IsNullOrEmpty(dto.Name))
            throw new InvalidDataException($"纹理记录缺少名称（资产 {record.AssetId.Value}）");
        if (string.IsNullOrEmpty(dto.Pixels))
            throw new InvalidDataException($"纹理记录缺少像素数据（资产 {record.AssetId.Value}）");

        byte[] pixels;
        try
        {
            pixels = Convert.FromBase64String(dto.Pixels);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException($"纹理像素数据损坏（资产 {record.AssetId.Value}）：{ex.Message}", ex);
        }

        ImageData data;
        try
        {
            data = new ImageData(dto.Width, dto.Height, pixels);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException(
                $"纹理尺寸与像素长度不匹配（{dto.Width}x{dto.Height}，{pixels.Length} 字节；资产 {record.AssetId.Value}）：{ex.Message}", ex);
        }

        return new TextureAsset(dto.Name, data);
    }

    /// <summary>纹理编码载体（显式字段，禁止反射推断）</summary>
    private sealed class TextureDto
    {
        /// <summary>纹理名称</summary>
        public string? Name { get; set; }

        /// <summary>像素宽</summary>
        public int Width { get; set; }

        /// <summary>像素高</summary>
        public int Height { get; set; }

        /// <summary>RGBA8 像素数据（base64）</summary>
        public string? Pixels { get; set; }
    }
}
