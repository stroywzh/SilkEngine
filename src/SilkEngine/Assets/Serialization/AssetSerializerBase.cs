using System.IO;
using System.Text.Json;

namespace SilkEngine.Assets.Serialization;

/// <summary>
/// 资产序列化器基类：统一类型/版本兼容检查与 JSON 数据编解码。
/// 编码使用显式 DTO（<see cref="System.Text.Json"/>），禁止反射推断字段；
/// 类型/版本不匹配抛 <see cref="NotSupportedException"/>，数据损坏抛 <see cref="InvalidDataException"/>。
/// </summary>
public abstract class AssetSerializerBase : IAssetSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <inheritdoc />
    public abstract AssetTypeId TypeId { get; }

    /// <inheritdoc />
    public abstract int MinVersion { get; }

    /// <inheritdoc />
    public abstract int MaxVersion { get; }

    /// <inheritdoc />
    public bool SupportsVersion(int schemaVersion) => schemaVersion >= MinVersion && schemaVersion <= MaxVersion;

    /// <inheritdoc />
    public abstract AssetSerializationRecord Serialize(object asset);

    /// <inheritdoc />
    public abstract object Deserialize(AssetSerializationRecord record, IAssetReferenceResolver resolver);

    /// <summary>校验记录类型与 schema 版本与序列化器匹配；不匹配抛 <see cref="NotSupportedException"/></summary>
    /// <param name="record">待校验记录</param>
    protected void EnsureCompatible(AssetSerializationRecord record)
    {
        if (record.TypeId != TypeId)
            throw new NotSupportedException(
                $"序列化器 '{TypeId.Value}' 无法处理类型 '{record.TypeId.Value}' 的记录（资产 {record.AssetId.Value}）");

        if (!SupportsVersion(record.SchemaVersion))
            throw new NotSupportedException(
                $"类型 '{TypeId.Value}' 不支持 schema 版本 {record.SchemaVersion}（支持范围 {MinVersion}~{MaxVersion}；资产 {record.AssetId.Value}）");
    }

    /// <summary>将 DTO 编码为记录数据 JSON 字符串</summary>
    /// <param name="dto">显式数据载体</param>
    /// <typeparam name="T">DTO 类型</typeparam>
    /// <returns>JSON 字符串</returns>
    protected static string EncodeData<T>(T dto) => JsonSerializer.Serialize(dto, JsonOptions);

    /// <summary>从记录数据解析 DTO；JSON 损坏或结构非法抛 <see cref="InvalidDataException"/></summary>
    /// <param name="record">记录</param>
    /// <typeparam name="T">DTO 类型</typeparam>
    /// <returns>解析后的 DTO</returns>
    protected static T ParseData<T>(AssetSerializationRecord record)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(record.Data, JsonOptions)
                ?? throw new InvalidDataException(
                    $"记录数据为空（类型 {record.TypeId.Value}，资产 {record.AssetId.Value}）");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                $"记录数据损坏（类型 {record.TypeId.Value}，资产 {record.AssetId.Value}）：{ex.Message}", ex);
        }
    }
}
