using System.Threading;

namespace SilkEngine.Assets;

/// <summary>
/// Unity 式静态资产门面：业务代码经 <see cref="Assets"/> 访问当前宿主的 <see cref="AssetManager"/>，无需持有实例引用。
/// 门面自身无状态（仅持有单槽绑定）、不创建缓存/线程/数据库连接，也不参与释放路径；
/// 绑定生命周期由 EngineHost 组合根驱动（Initialize 尾部 Bind、Dispose Unbind）。
/// 未绑定时所有访问抛 <see cref="InvalidOperationException"/>（fail-fast，不静默回退）。
/// </summary>
public static class Assets
{
    private static AssetManager? _current;

    /// <summary>完全同步加载（转发当前宿主 <see cref="AssetManager.Load{T}"/>；仅建议启动初始化与小资产）。</summary>
    /// <typeparam name="T">资产载荷类型</typeparam>
    /// <param name="path">资产逻辑路径（相对资产根目录）</param>
    /// <returns>规范 Payload 实例</returns>
    public static T Load<T>(string path)
        where T : class, IAssetPayload => Current.Load<T>(path);

    /// <summary>异步加载（转发当前宿主 <see cref="AssetManager.LoadAsync{T}"/>；同键请求合并）。</summary>
    /// <typeparam name="T">资产载荷类型</typeparam>
    /// <param name="path">资产逻辑路径（相对资产根目录）</param>
    /// <param name="token">取消令牌（只取消当前调用方视角）</param>
    /// <returns>安全资产操作</returns>
    public static AssetOperation<T> LoadAsync<T>(string path, CancellationToken token = default)
        where T : class, IAssetPayload => Current.LoadAsync<T>(path, token);

    /// <summary>解析已索引路径为稳定资产句柄（转发当前宿主 <see cref="AssetManager.GetHandle{T}"/>；不触发加载）。</summary>
    /// <typeparam name="T">资产载荷类型</typeparam>
    /// <param name="path">资产逻辑路径（相对资产根目录）</param>
    /// <returns>资产句柄</returns>
    public static AssetHandle<T> GetHandle<T>(string path)
        where T : class, IAssetPayload => Current.GetHandle<T>(path);

    /// <summary>注册瞬态资产（转发当前宿主 <see cref="AssetManager.RegisterTransient{T}"/>；不经 VFS/目录）。</summary>
    /// <typeparam name="T">资产载荷类型</typeparam>
    /// <param name="payload">规范载荷实例</param>
    /// <returns>稳定资产句柄</returns>
    public static AssetHandle<T> RegisterTransient<T>(T payload)
        where T : class, IAssetPayload => Current.RegisterTransient(payload);

    /// <summary>绑定静态门面到宿主资产管理器（EngineHost.Initialize 尾部调用；仅允许一个宿主持有）。</summary>
    /// <param name="manager">宿主构建的资产管理器</param>
    /// <exception cref="InvalidOperationException">已有另一个未释放的宿主持有门面时抛出</exception>
    internal static void Bind(AssetManager manager)
    {
        var previous = Interlocked.CompareExchange(ref _current, manager, null);
        if (previous is not null && !ReferenceEquals(previous, manager))
            throw new InvalidOperationException("Only one EngineHost may own the Assets facade.");
    }

    /// <summary>解绑静态门面（EngineHost.Dispose 调用；仅当当前持有者为该管理器时生效，幂等）。</summary>
    /// <param name="manager">宿主构建的资产管理器</param>
    internal static void Unbind(AssetManager manager) =>
        Interlocked.CompareExchange(ref _current, null, manager);

    /// <summary>测试专用：强制重置绑定槽为未绑定（不进业务公开 API）。</summary>
    internal static void ResetForTests() => Volatile.Write(ref _current, null);

    /// <summary>当前绑定的资产管理器；未绑定时抛出（消息含 initialized，调用方可据此识别未初始化误用）。</summary>
    private static AssetManager Current =>
        Volatile.Read(ref _current)
        ?? throw new InvalidOperationException("Assets facade requires an initialized EngineHost.");
}
