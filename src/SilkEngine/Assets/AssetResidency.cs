namespace SilkEngine.Assets;

/// <summary>
/// 资产驻留槽：渲染器等长生命周期持有者经 Bind 登记驻留，Dispose 释放。
/// 局部 C# 变量不构成驻留证据；无 Slot/Lease/Pin 的 Payload 是 <see cref="AssetManager.UnloadUnused"/> 的驱逐候选。
/// </summary>
/// <typeparam name="T">资产载荷类型</typeparam>
public sealed class AssetSlot<T> : IDisposable
    where T : class, IAssetPayload
{
    private readonly AssetManager _assets;
    private AssetHandle<T> _handle;

    /// <summary>创建驻留槽（未绑定；需 Bind）</summary>
    /// <param name="assets">资产管理器</param>
    internal AssetSlot(AssetManager assets) => _assets = assets;

    /// <summary>当前绑定的句柄（未绑定为 default）</summary>
    public AssetHandle<T> Handle => _handle;

    /// <summary>绑定句柄并登记驻留（重复绑定同一句柄幂等）</summary>
    /// <param name="handle">资产句柄</param>
    public void Bind(AssetHandle<T> handle)
    {
        _assets.AddResidency(handle.Id);
        _handle = handle;
    }

    /// <summary>释放驻留（幂等；未绑定 no-op）</summary>
    public void Dispose() => _assets.ReleaseResidency(_handle.Id);
}

/// <summary>
/// 资产租赁：跨场景保留的显式驻留证据（<see cref="AssetManager.Pin{T}"/> 返回）；
/// Dispose 释放驻留。局部 C# 变量不自动构成驻留保证。
/// </summary>
/// <typeparam name="T">资产载荷类型</typeparam>
public sealed class AssetLease<T> : IDisposable
    where T : class, IAssetPayload
{
    private readonly AssetManager _assets;
    private AssetHandle<T> _handle;

    /// <summary>创建租赁（未绑定；需 Bind）</summary>
    /// <param name="assets">资产管理器</param>
    internal AssetLease(AssetManager assets) => _assets = assets;

    /// <summary>当前绑定的句柄（未绑定为 default）</summary>
    public AssetHandle<T> Handle => _handle;

    /// <summary>绑定句柄并登记驻留（重复绑定同一句柄幂等）</summary>
    /// <param name="handle">资产句柄</param>
    public void Bind(AssetHandle<T> handle)
    {
        _assets.AddResidency(handle.Id);
        _handle = handle;
    }

    /// <summary>释放驻留（幂等；未绑定 no-op）</summary>
    public void Dispose() => _assets.ReleaseResidency(_handle.Id);
}
