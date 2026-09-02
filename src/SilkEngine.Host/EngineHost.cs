using System.Threading;
using SilkEngine.Assets;
using SilkEngine.Assets.VirtualFileSystem;
using SilkEngine.Core;
using SilkEngine.Rendering;
using SilkEngine.Rendering.OpenGL;
using SilkEngine.Scene;
using SilkEngine.Threading;
using IRenderBackend = SilkEngine.Rendering.Backend.IRenderBackend;

namespace SilkEngine.Host;

// 类名与命名空间末段同名（Unity 式门面）：裸标识符 Assets 会按外层命名空间成员解析为命名空间；
// 编译单元级别名在查找顺序上晚于外层命名空间成员，故别名须置于文件范围命名空间声明之后
using Assets = SilkEngine.Assets.Assets;

/// <summary>
/// 引擎唯一宿主入口：Create 只装配配置（不启动运行时、不访问全局服务），
/// Initialize 完成运行时对象图装配与握手（单次生效），Run/Stop/Dispose 驱动与关闭。
/// 状态机：0=New（未初始化）→ 1=Initialized → 2=Disposed。
/// </summary>
public sealed class EngineHost : IDisposable
{
    private int _state;
    private readonly EngineOptions _options;
    private EngineLoop? _loop;

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

    /// <summary>内部心跳驱动器（测试经 friend access 步进；业务使用 SceneManager/AssetManager 门面）。</summary>
    internal EngineLoop Loop => _loop ?? throw new InvalidOperationException("EngineHost.Initialize 尚未执行");

    /// <summary>场景管理器门面（Initialize 后可用）。</summary>
    public SceneManager SceneManager => Loop.SceneManager;

    /// <summary>资产管理器门面（Initialize 后可用）。</summary>
    public AssetManager AssetManager => Loop.AssetManager;

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
    /// 初始化引擎：完成运行时对象图装配与握手（渲染线程启动、场景注册表注入、帧末首次提交）。
    /// 重复调用或 Dispose 后调用抛 <see cref="InvalidOperationException"/>。
    /// </summary>
    public void Initialize()
    {
        if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
            throw new InvalidOperationException("EngineHost has already been initialized or disposed.");
        _options.Validate();
        BuildRuntime();
    }

    /// <summary>驱动引擎心跳直至 Stop 或窗口关闭（阻塞；须先 Initialize）。</summary>
    public void Run()
    {
        if (Volatile.Read(ref _state) != 1)
            throw new InvalidOperationException("EngineHost.Run 需要先调用 Initialize。");
        _loop!.Run();
    }

    /// <summary>请求停止引擎心跳（幂等；未初始化时为安全空操作）。</summary>
    public void Stop()
    {
        if (Volatile.Read(ref _state) == 1)
            _loop?.Stop();
    }

    /// <summary>
    /// 释放引擎（幂等）：关闭运行时对象图 —— AssetManager.Dispose 先停新请求/取消 Worker/丢弃过期
    /// ResultBatch，最后才解绑静态 Assets 门面（业务关门面访问在管理器关闭之后，顺序契约）。
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _state, 2) == 2)
            return;
        if (_loop is { } loop)
        {
            loop.Dispose();
            Assets.Unbind(loop.AssetManager); // 最后一步：解绑静态门面
        }
        _loop = null;
    }

    /// <summary>
    /// 装配运行时对象图：按选项选择后端（Headless → 无窗口桩；否则 OpenGL），
    /// 显式构造 ThreadRuntime/ComponentRegistry/FrameSnapshotManager 与 EngineLoop
    /// （渲染系统、资产管线与管理器）并执行 Initialize 握手。
    /// </summary>
    private void BuildRuntime()
    {
        IRenderBackend backend = _options.BackendOverrideForTests
            ?? (_options.Headless
                ? new HeadlessRenderBackend()
                : new OpenGLRenderBackend(
                    _options.ShaderCompilerOverride ?? new DxcHlslCompiler(_options.DxcPath)));
        var runtime = new ThreadRuntime();
        // LibraryRoot 为 AssetDB 存储目录（默认 "Library"；任务 5 起接线到磁盘资产管线）
        var assets = AssetManager.CreateDiskBacked(_options.AssetRoot, runtime, libraryRoot: _options.LibraryRoot);
        // 资产变更源：测试可注入内存源；默认磁盘轮询变更源按 AssetChangeScanInterval 低频扫描
        var changeSource = _options.AssetChangeSourceOverride
            ?? new DiskAssetFileSystem(_options.AssetRoot).CreatePollingChangeSource(
                _options.AssetChangeScanInterval);
        var loop = new EngineLoop(
            backend,
            assets,
            runtime,
            new ComponentRegistry(),
            new FrameSnapshotManager(),
            changeSource,
            _options.AssetChangeScanInterval)
        {
            Embedded = _options.Embedded,
        };
        loop.Initialize();
        // 兼容阶段集中注册：静态门面（AssetOperation/Input 等）经 Services 取用；业务经 EngineLoop 公开属性
        Services.Register(loop.RenderSystem);
        Services.Register(loop.AssetManager);
        Services.Register(loop.SceneManager);
        _loop = loop;
        // 静态 Assets 门面绑定（AssetManager 构建并完成启动扫描后；Dispose 时解绑）
        Assets.Bind(assets);
    }
}