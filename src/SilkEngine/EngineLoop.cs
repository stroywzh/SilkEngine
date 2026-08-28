using System;
using System.Diagnostics;
using System.Linq;
using SilkEngine.Assets;
using SilkEngine.Assets.Importer;
using SilkEngine.Assets.Serialization;
using SilkEngine.Assets.VirtualFileSystem;
using SilkEngine.InputSystem;
using SilkEngine.Render;
using SilkEngine.Rendering;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.Backend;
using SilkEngine.Rendering.Pipeline;
using SilkEngine.Scene;
using SilkEngine.Threading;
using IRenderBackend = SilkEngine.Rendering.Backend.IRenderBackend;

namespace SilkEngine.Core;

public class EngineLoop : IDisposable
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
    // RenderCollector不应有主线程持有
    private readonly RenderCollector _collector = new();
    private Camera? _defaultCamera; // 实际无用逻辑
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

    /// <summary>
    /// 创建引擎心跳驱动器（兼容路径）：从全局服务注册表解析核心依赖后装配。
    /// </summary>
    /// <param name="backend">渲染后端（窗口/上下文/绘制执行）</param>
    /// <param name="assetRoot">资产根目录（缺省 "Assets"）</param>
    public EngineLoop(IRenderBackend backend, string? assetRoot = null)
        : this(
            backend,
            assetRoot,
            Services.Get<ThreadRuntime>(),
            Services.Get<ComponentRegistry>(),
            Services.Get<FrameSnapshotManager>())
    {
    }

    /// <summary>
    /// 创建引擎心跳驱动器（Host 组合根显式装配）：依赖经构造注入，不经全局服务解析。
    /// </summary>
    /// <param name="backend">渲染后端（窗口/上下文/绘制执行）</param>
    /// <param name="assetRoot">资产根目录（null 时缺省 "Assets"）</param>
    /// <param name="threadRuntime">线程运行时（线程资源唯一属主）</param>
    /// <param name="registry">组件注册表（帧原子性核心）</param>
    /// <param name="snapshotManager">帧快照管理器（双缓冲）</param>
    internal EngineLoop(
        IRenderBackend backend,
        string? assetRoot,
        ThreadRuntime threadRuntime,
        ComponentRegistry registry,
        FrameSnapshotManager snapshotManager)
    {
        _backend = backend;
        _threadRuntime = threadRuntime;
        _registry = registry;
        _snapshotManager = snapshotManager;
        _renderSystem = new RenderSystem(_backend, _threadRuntime);
        var files = new DiskAssetFileSystem(assetRoot ?? "Assets");
        var pipeline = new AssetPipeline(
            files,
            new InMemoryVirtualFileIndex(),
            new AssetCatalog(),
            new AssetImporterRegistry(),
            _threadRuntime.Background,
            _threadRuntime.MainThread,
            _threadRuntime);
        pipeline.ApplyScan(files.Scan());
        _assetManager = new AssetManager(pipeline, _threadRuntime.MainThread, _threadRuntime, new AssetSerializerRegistry());
        // release-request 队列承接：渲染线程帧首排空 → backend.Release（Rendering 域零 Assets 引用，主线程接线）
        _renderSystem.RenderHost.DrainUnloadQueue = _assetManager.ProcessUnloadQueue;
        _sceneManager = new SceneManager();
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
            Log.Info($"[EngineLoop]: EngineLoop Started. \nManaged threads: \nMain(heartbeat):PID{Pid}\nWorkers:ThreadPool\nRenderThread:PID{_renderSystem.RenderHost.Thread?.ManagedThreadId ?? -1}.");
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
        _frameScheduler.Tick(Time.DeltaTime, fdt => _sceneManager.FixedTick(_snapshotManager.Current, fdt), d => _sceneManager.Tick(_snapshotManager.Current, d), () => _sceneManager.LateTick(_snapshotManager.Current));
        _threadRuntime.Drain(MainThreadPhase.PreRender);
        OnRender();
        _sceneManager.PostRender(_snapshotManager.Current);
        _frameCommitter.Commit(_snapshotManager, _registry, _sceneManager, _threadRuntime);
        _threadRuntime.Drain(MainThreadPhase.Continuation);
    }

    /// <summary>
    /// 帧渲染桥接（Render 域零 Scene 依赖）：Scene 域查询活跃相机与渲染器（含默认相机回退），
    /// 经 RenderCollector 组装批次后交 RenderSystem（ICameraView/IRenderable 接口消费）。
    /// </summary>
    protected virtual void OnRender()
    {
        var snapshot = _snapshotManager.Current;
        var cameras = snapshot.GetComponents<Camera>().Where(c => c.GameObject.IsActiveInHierarchy).ToList();
        if (cameras.Count == 0)
            cameras.Add(GetDefaultCamera());
        var renderables = snapshot.GetComponents<MeshRenderer>().Where(r => r.Enabled && r.GameObject.IsActiveInHierarchy).ToList();
        _collector.Gather(cameras, renderables, out var camera, out var batches);
        var surface = _renderSystem.Surface;
        var createBatch = _assetManager?.DrainCreateBatch() ?? RenderResourceCreateBatch.Empty;
        _renderSystem.Render(surface is null ? 1f : (float)surface.Width / surface.Height, camera, batches, createBatch);
        // Main 域应用渲染线程回传的创建结果（按 RequestId 发布 GPU 句柄或入队释放）
        if (_assetManager is { } assets)
            assets.ApplyCreateResults(_renderSystem.RenderHost.LastCreateResults);
    }

    private Camera GetDefaultCamera() =>
        _defaultCamera ??= new GameObject("Default Camera").AddComponent<Camera>();
    public void Stop() => _stopRequested = true;
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
