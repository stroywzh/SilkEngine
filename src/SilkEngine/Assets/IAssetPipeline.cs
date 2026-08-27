namespace SilkEngine.Assets;

/// <summary>
/// 资产管线契约：按 <see cref="AssetBuildKey"/> 请求资产载荷。
/// 实现方负责 Main 域请求去重、Worker 执行 Read/Import/Validate 与过期结果丢弃。
/// </summary>
public interface IAssetPipeline
{
    /// <summary>请求构建指定键的资产载荷（同键去重；返回业务安全操作）</summary>
    /// <typeparam name="T">资产载荷类型</typeparam>
    /// <param name="key">构建键</param>
    /// <param name="cancellationToken">取消令牌（只取消当前调用方视角）</param>
    /// <returns>安全资产操作</returns>
    AssetOperation<T> Request<T>(AssetBuildKey key, CancellationToken cancellationToken = default)
        where T : class, IAssetPayload;
}
