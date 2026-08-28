using System.Threading;

namespace SilkEngine.Host;

/// <summary>
/// 引擎唯一宿主入口：Create 只装配配置（不启动运行时、不访问全局服务），
/// Initialize 完成运行时对象图装配（单次生效），Run/Stop/Dispose 驱动与关闭。
/// 状态机：0=New（未初始化）→ 1=Initialized → 2=Disposed。
/// </summary>
public sealed class EngineHost : IDisposable
{
    private int _state;
    private readonly EngineOptions _options;

    internal EngineHost(EngineOptions options)
    {
        _options = options;
    }

    /// <summary>引擎启动配置（只读快照）。</summary>
    public EngineOptions Options => _options;

    /// <summary>运行时是否已完成初始化（Initialize 单次生效后为 true）。</summary>
    public bool IsInitialized => Volatile.Read(ref _state) >= 1;

    /// <summary>宿主是否已释放（Dispose 幂等）。</summary>
    public bool IsDisposed => Volatile.Read(ref _state) == 2;

    /// <summary>
    /// 创建引擎宿主（仅装配配置，不启动线程、不扫描 VFS、不注册全局服务）。
    /// </summary>
    /// <param name="configure">可选的配置回调（经 <see cref="EngineBuilder"/> 装配选项）。</param>
    /// <returns>未初始化的宿主实例。</returns>
    public static EngineHost Create(Action<EngineBuilder>? configure = null)
    {
        var builder = new EngineBuilder();
        configure?.Invoke(builder);
        return builder.Build();
    }

    /// <summary>
    /// 初始化引擎：完成运行时对象图装配与握手。重复调用或 Dispose 后调用抛
    /// <see cref="InvalidOperationException"/>。
    /// </summary>
    public void Initialize()
    {
        if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
            throw new InvalidOperationException("EngineHost has already been initialized or disposed.");
        _options.Validate();
        BuildRuntime();
    }

    /// <summary>请求停止引擎心跳（幂等；未初始化时为安全空操作）。</summary>
    public void Stop()
    {
    }

    /// <summary>释放引擎（幂等；反序释放运行时资源）。</summary>
    public void Dispose()
    {
        Interlocked.Exchange(ref _state, 2);
    }

    /// <summary>
    /// 装配运行时对象图（当前切片仅占位；后续任务接入真实组合根）。
    /// </summary>
    private void BuildRuntime()
    {
        // 任务 2 提供具体运行时对象图；本切片只拥有生命周期状态机。
    }
}