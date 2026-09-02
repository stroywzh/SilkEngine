using System;
using System.Diagnostics;
using System.Linq;
using SilkEngine.Assets;
using SilkEngine.Assets.VirtualFileSystem;
using SilkEngine.InputSystem;
using SilkEngine.Rendering;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.Backend;
using SilkEngine.Scene;
using SilkEngine.Threading;
using IRenderBackend = SilkEngine.Rendering.Backend.IRenderBackend;

namespace SilkEngine.Core;

internal sealed class EngineLoop : IDisposable
{
    private static int Pid => Environment.CurrentManagedThreadId;
    private readonly IRenderBackend _backend;
    private readonly FrameClock _clock = new();
    private readonly FrameScheduler _frameScheduler = new();
    private readonly FrameCommitter _frameCommitter = new();
    private ComponentRegistry _registry = null!; // Initialize 从 Services 取（[Service] 自动注册）

    private FrameSnapshotManager _snapshotManager = null!;
    private readonly SceneManager _sceneManager;
    private ThreadRuntime _threadRuntime = null!;
    private AssetManager? _assetManager;
    private RenderSystem _renderSystem = null!;
    private SceneRenderWorld _sceneRenderWorld = null!;
    private readonly IAssetChangeSource? _assetChangeSource;
    private readonly TimeSpan _assetChangeScanInterval;
    private DateTime _lastAssetChangeScanUtc = DateTime.MinValue;
    private volatile bool _stopRequested, _paused, _disposed, _canStart;

    public bool Embedded { get; set; } = false;
    /// <summary>固定步长（秒）；与 Time.FixedDeltaTime 双向同步（FrameScheduler 持有）。</summary>
    public float FixedDeltaTime
    {
        get => _frameScheduler.FixedDeltaTime;
        set => _frameScheduler.FixedDeltaTime = value;
    }
    public bool Paused
    {
        get => _paused;
        set
        {
            if (_paused == value)
                return;
            _paused = value;
            if (LogConfig.EngineLoop)
                Log.Info(value ? "[EngineLoop] Paused" : "[EngineLoop] Resumed");
        }
    }
    /// <summary>场景管理器实例（ctor 创建并订阅 Object.DestroyHandler；宿主经此取用）</summary>
    public SceneManager SceneManager => _sceneManager;
    /// <summary>资产管理器实例（ctor 创建并注入共享工作池，恒非空）</summary>
    public AssetManager AssetManager =>
        _assetManager ?? throw new InvalidOperationException("EngineLoop.Initialize 尚未执行");

    /// <summary>渲染系统实例（宿主集中装配用；不参与热路径）。</summary>
    internal RenderSystem RenderSystem => _renderSystem;

    /// <summary>
    /// 创建引擎心跳驱动器（Host 组合根显式装配）：依赖经构造注入，不经全局服务解析。
    /// </summary>
    /// <param name="backend">渲染后端（窗口/上下文/绘制执行）</param>
    /// <param name="assetManager">资产管理器（Host 经 <see cref="AssetManager.CreateDiskBacked"/> 构造；管线组合收在 Assets 域）</param>
    /// <param name="threadRuntime">线程运行时（线程资源唯一属主）</param>
    /// <param name="registry">组件注册表（帧原子性核心）</param>
    /// <param name="snapshotManager">帧快照管理器（双缓冲）</param>
    /// <param name="assetChangeSource">资产变更源（null 时不启用热重载扫描）</param>
    /// <param name="assetChangeScanInterval">低频扫描槽间隔（EngineHost 传入 EngineOptions.AssetChangeScanInterval）</param>
    internal EngineLoop(
        IRenderBackend backend,
        AssetManager assetManager,
        ThreadRuntime threadRuntime,
        ComponentRegistry registry,
        FrameSnapshotManager snapshotManager,
        IAssetChangeSource? assetChangeSource = null,
        TimeSpan assetChangeScanInterval = default)
    {
        _backend = backend;
        _assetManager = assetManager;
        _threadRuntime = threadRuntime;
        _registry = registry;
        _snapshotManager = snapshotManager;
        _assetChangeSource = assetChangeSource;
        _assetChangeScanInterval = assetChangeScanInterval;
        _renderSystem = new RenderSystem(_backend, _threadRuntime);
        var rendererProvider = new SceneRendererProvider(snapshotManager);
        _sceneRenderWorld = new SceneRenderWorld(snapshotManager, [rendererProvider]);
        // release-request 队列承接：渲染线程帧首排空 → backend.Release（Rendering 域零 Assets 引用，主线程接线）
        _renderSystem.SetUnloadQueueDrain(_assetManager.ProcessUnloadQueue);
        _sceneManager = new SceneManager { AssetService = _assetManager };
    }

    public EngineLoop Initialize()
    {
        _sceneManager.Attach(_registry, _snapshotManager);
        _threadRuntime.RegisterMainThread();
        _renderSystem.Initialize();
        if (_renderSystem.Surface?.NativeWindow is { } win)
        {
            var inputProvider = new SilkInputProvider();
            inputProvider.Initialize(win);
            Input.SetProvider(inputProvider);
        }
        Input.SetActionService(new InputActionService(Input.Keyboard, Input.Mouse));
        _sceneManager.RegisterScene();
        _frameCommitter.Commit(_snapshotManager, _registry, _sceneManager, _threadRuntime);
        _stopRequested = false;
        _clock.Reset();
        _canStart = true;
        return this;
    }

    public void Run()
    {
        if (!_canStart)
        {
            Log.Warning("[EngineLoop]: Invalid Operation,EngineLoop haven't Initialized yet.");
            return;
        }
        if (LogConfig.EngineLoop)
        {
            Log.Info($"[EngineLoop]: EngineLoop Started. \nManaged threads: \nMain(heartbeat):PID{Pid}\nWorkers:ThreadPool\nRenderThread:PID{_renderSystem.RenderThreadId}.");
            Log.Info("[EngineLoop] Run started");
        }
        while (!_renderSystem.ShouldClose && !_stopRequested)
        {
            StepFrame();
        }
        if (LogConfig.EngineLoop)
            Log.Info("[EngineLoop] Run finished");
    }

    /// <summary>
    /// 驱动单帧心跳（Host/Embedded 与测试步进共用；Run 循环逐帧调用）：
    /// 时钟 → Input → Tick 派发 → PreRender → 渲染提交（相机 + 渲染包 + 资源创建批次）→
    /// 结果发布 → PostRender → 帧末提交 → Continuation 排空。
    /// </summary>
    internal void StepFrame()
    {
        if (!Embedded)
            _renderSystem.PumpEvents();
        if (_paused)
        {
            Thread.Sleep(16);
            _clock.Reset();
            return;
        }
        _clock.Tick();
        Input.Update();
        CheckAssetChanges();
        _frameScheduler.Tick(Time.DeltaTime, fdt => _sceneManager.FixedTick(_snapshotManager.Current, fdt), d => _sceneManager.Tick(_snapshotManager.Current, d), () => _sceneManager.LateTick(_snapshotManager.Current));
        _threadRuntime.Drain(MainThreadPhase.PreRender);
        OnRender();
        _sceneManager.PostRender(_snapshotManager.Current);
        _frameCommitter.Commit(_snapshotManager, _registry, _sceneManager, _threadRuntime);
        // 帧末驱逐：应用管线结果（FrameCommit 已排空）后驱逐无持有者 Payload，GPU 释放请求于下帧渲染帧首排空
        _assetManager?.UnloadUnused();
        _threadRuntime.Drain(MainThreadPhase.Continuation);
    }

    /// <summary>
    /// 帧渲染桥接（Render 域零 Scene 依赖）：SceneRenderWorld 从快照构建只读渲染源
    /// （活跃相机 + provider 渲染器），收集组装收在 RenderSystem 内部，EngineLoop 只消费结果。
    /// </summary>
    private void OnRender()
    {
        var source = _sceneRenderWorld.BuildSnapshot();
        var surface = _renderSystem.Surface;
        var createBatch = _assetManager?.DrainCreateBatch() ?? RenderResourceCreateBatch.Empty;
        _renderSystem.Render(surface is null ? 1f : (float)surface.Width / surface.Height, source, createBatch);
        // Main 域应用渲染线程回传的创建结果（按 RequestId 发布 GPU 句柄或入队释放）
        if (_assetManager is { } assets)
            assets.ApplyCreateResults(_renderSystem.LastCreateResults);
    }

    public void Stop() => _stopRequested = true;

    /// <summary>
    /// 低频资产变更扫描槽（Main 域）：间隔未到直接返回；到点后调用变更源收敛快照，
    /// 有变更时交给 <see cref="AssetManager.ApplyAssetChanges"/> 消费（扫描对账/失效/重建收在 Assets 域，
    /// 本类不做资产类型识别、不引入 importer/渲染逻辑）。
    /// </summary>
    private void CheckAssetChanges()
    {
        if (_assetChangeSource is null || _assetManager is null)
            return;
        var now = DateTime.UtcNow;
        if (now - _lastAssetChangeScanUtc < _assetChangeScanInterval)
            return;
        _lastAssetChangeScanUtc = now;
        var changes = _assetChangeSource.Poll();
        if (changes.HasChanges)
            _assetManager.ApplyAssetChanges(changes);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _stopRequested = true;
        // 反序：RenderSystem(渲染线程先停) → SnapshotManager/Registry → AssetManager → SceneManager(解绑) → ThreadRuntime(最后停)
        Services.Shutdown();
        _threadRuntime.Dispose(); // Host 显式注入的运行时不在 Services 注册表内，由此兜底释放（幂等）
    }
}
