namespace SilkEngine.Assets.Serialization;

/// <summary>
/// 资产引用解析器：反序列化期间提供序列化记录目录查询与依赖句柄解析。
/// 记录目录用于按资产 ID 读取待反序列化的记录；句柄解析要求依赖先于当前资产完成反序列化。
/// </summary>
public interface IAssetReferenceResolver
{
    /// <summary>按资产 ID 获取序列化记录</summary>
    /// <param name="assetId">资产 ID</param>
    /// <returns>序列化记录；未命中返回 null</returns>
    AssetSerializationRecord? TryGetRecord(AssetId assetId);

    /// <summary>按强类型句柄解析依赖资产；依赖必须已完成反序列化</summary>
    /// <param name="handle">强类型依赖句柄</param>
    /// <typeparam name="T">资产类型</typeparam>
    /// <returns>已解析的依赖资产实例</returns>
    T Resolve<T>(AssetHandle<T> handle)
        where T : class;

    /// <summary>按非泛型句柄解析依赖资产；依赖必须已完成反序列化</summary>
    /// <param name="handle">非泛型依赖句柄</param>
    /// <returns>已解析的依赖资产实例</returns>
    object Resolve(UntypedAssetHandle handle);
}
