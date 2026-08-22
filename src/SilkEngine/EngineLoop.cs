using System;
using System.Diagnostics;
using System.Linq;
using SilkEngine.Core;
using SilkEngine.Assets;
using SilkEngine.InputSystem;
using SilkEngine.Render;
using SilkEngine.Scene;
using SilkEngine.Threading;

namespace SilkEngine.Core;

public class EngineLoop : IDisposable
{
    private static int Pid => Environment.CurrentManagedThreadId;
    private readonly IRenderBackend _backend;
    private readonly FrameClock _clock = new();
    private readonly FrameScheduler _frameScheduler = new();
    private readonly FrameCommitter _frameCommitter = new();
    private ComponentRegistry _registry = null!; // Initialize 从 Services 取（[Service] 自动注册）

    // 我还是不太能理解为什么MainLoop需要直接持有这么多Manager的引用,
    // 按道理来讲，ThreadManager可以有但是AssetManager/RenderSystem不该有，因为设计上它们应该是主线程创建接口通讯（草拟的AI完全没写接口）
    private FrameSnapshotManager _snapshotManager = null!;
    private readonly SceneManager _sceneManager;
    private ThreadManager _threadManager = null!;
    private AssetManager? _assetManager;
    private RenderSystem _renderSystem = null!;
    // RenderCollector不应有主线程持有
    private readonly RenderCollector _collector = new();
    private Camera? _defaultCamera; // 实际无用逻辑
    private volatile bool _stopRequested, _paused, _disposed, _canStart;

    /// <summary>线程管理器实例（[Service] 自动注册，ctor 已赋值，恒非空）</summary>
    public ThreadManager Threads =>
        _threadManager ?? throw new InvalidOperationException("EngineLoop.Initialize 尚未执行");
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

    public EngineLoop(IRenderBackend backend)
    {
        _backend = backend;
        _threadManager = Services.Get<ThreadManager>();
        _threadManager.RegisterMainThread();
        _registry = Services.Get<ComponentRegistry>();
        _snapshotManager = Services.Get<FrameSnapshotManager>();
        _renderSystem = new RenderSystem(_backend, _threadManager);
        _assetManager = new AssetManager((SilkEngine.Core.ITaskScheduler)_threadManager.Request<ITaskExecutor>(new ThreadRequest("Workers", ThreadKind.WorkerPool)));
        _sceneManager = new SceneManager();
    }

    public EngineLoop Initialize()
    {
        _sceneManager.Attach(_registry, _snapshotManager);
        _renderSystem.Initialize();
        if (_renderSystem.Backend.NativeWindow is { } win)
        {
            var inputProvider = new SilkInputProvider();
            inputProvider.Initialize(win);
            Input.SetProvider(inputProvider);
        }
        _sceneManager.RegisterScene();
        _frameCommitter.Commit(_snapshotManager, _registry, _sceneManager, AssetManager);
        _stopRequested = false;
        _clock.Reset();
        _canStart = true;
        return this;
    }

    public void Run()
    {
        if (!_canStart)
        {
            Log.Warn("[EngineLoop]: Invalid Operation,EngineLoop haven't Initialized yet.");
            return;
        }
        if (LogConfig.EngineLoop)
        {
            Log.Info($"[EngineLoop]: EngineLoop Started. \nManaged threads: \nMain(heartbeat):PID{Pid}\nWorkers:ThreadPool\nRenderThread:PID{_renderSystem.RenderThreadContext.NativeThreadId}.");
            Log.Info("[EngineLoop] Run started");
        }
        while (!_renderSystem!.ShouldClose && !_stopRequested)
        {
            if (!Embedded)
                _renderSystem.PumpEvents();
            if (_paused)
            {
                Thread.Sleep(16);
                _clock.Reset();
                continue;
            }
            _clock.Tick();
            Input.Update();
            _frameScheduler.Tick(Time.DeltaTime, fdt => _sceneManager.FixedTick(_snapshotManager.Current, fdt), d => _sceneManager.Tick(_snapshotManager.Current, d), () => _sceneManager.LateTick(_snapshotManager.Current));
            OnRender();
            _sceneManager.PostRender(_snapshotManager.Current);
            _frameCommitter.Commit(_snapshotManager, _registry, _sceneManager, AssetManager);
        }
        if (LogConfig.EngineLoop)
            Log.Info("[EngineLoop] Run finished");
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
        _renderSystem!.Render((float)_renderSystem.Backend.Width / _renderSystem.Backend.Height, camera, batches);
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
        // 反序：RenderSystem(渲染线程先停) → SnapshotManager/Registry → AssetManager → SceneManager(解绑) → ThreadManager(最后停)
        Services.Shutdown();
    }
}
