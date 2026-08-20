using System;
using System.Diagnostics;
using SilkEngine.Core;
using SilkEngine.Core.Assets;
using SilkEngine.InputSystem;
using SilkEngine.Render;
using SilkEngine.Scene;
using SilkEngine.Threading;

namespace SilkEngine.Core;

public class EngineLoop : IDisposable
{
    private static int Pid => Process.GetCurrentProcess().Id;
    private readonly IRenderBackend _backend;
    private readonly FixedStepAccumulator _fixedStep = new();
    private ComponentRegistry _registry = null!; // Initialize 从 Services 取（[Service] 自动注册）
    private FrameSnapshotManager _snapshotManager = null!; // 同上
    private readonly SceneManager _sceneManager;
    private ThreadManager _threadManager;
    private AssetManager? _assetManager;
    private RenderSystem _renderSystem;
    private DateTime _lastTime;
    private volatile bool _stopRequested,
        _paused,
        _disposed,
        _canStart;

    /// <summary>线程管理器实例（[Service] 自动注册，Initialize 取用；未初始化访问抛异常）</summary>
    public ThreadManager Threads =>
        _threadManager ?? throw new InvalidOperationException("EngineLoop.Initialize 尚未执行");
    public bool Embedded { get; set; } = false;

    /// <summary>固定步长（秒）；与 Time.FixedDeltaTime 双向同步（替代 LogicLoop.FixedDeltaTime）。</summary>
    public float FixedDeltaTime
    {
        get => _fixedStep.FixedDeltaTime;
        set
        {
            _fixedStep.FixedDeltaTime = value;
            Time.FixedDeltaTime = value;
        }
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

    /// <summary>资产管理器实例（Initialize 创建并注入共享工作池；未初始化访问抛异常）</summary>
    public AssetManager AssetManager =>
        _assetManager ?? throw new InvalidOperationException("EngineLoop.Initialize 尚未执行");

    public EngineLoop(IRenderBackend backend)
    {
        _backend = backend;

        Time.FixedDeltaTime = _fixedStep.FixedDeltaTime;
        _lastTime = DateTime.UtcNow;
        _canStart = false;

        _threadManager = Services.Get<ThreadManager>();
        _threadManager.RegisterMainThread();

        _registry = Services.Get<ComponentRegistry>();
        _snapshotManager = Services.Get<FrameSnapshotManager>();

        _renderSystem = new RenderSystem(_backend, _threadManager);

        _assetManager = new AssetManager(
            (SilkEngine.Core.ITaskScheduler)
                _threadManager.Request<ITaskExecutor>(
                    new ThreadRequest("Workers", ThreadKind.WorkerPool)
                )
        );

        _sceneManager = new SceneManager();
    }

    public EngineLoop Initialize()
    {
        _sceneManager.Attach(_registry, _snapshotManager);
        _renderSystem.Initialize();

        //TODO:初始化逻辑后面要改，改成基于Editor启动和游戏启动
        if (_renderSystem.Backend.NativeWindow is { } win)
        {
            var inputProvider = new SilkInputProvider();
            inputProvider.Initialize(win);
            Input.SetProvider(inputProvider);
        }

        _sceneManager.RegisterScene();
        CommitFrame();

        _stopRequested = false;
        _lastTime = DateTime.UtcNow;
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
            Log.Info(
                $"[EngineLoop]: EngineLoop Started. \nManaged threads: \nMain(heartbeat):PID{Pid}\nWorkers:ThreadPool\nRenderThread:PID{_renderSystem.RenderThreadContext.NativeThreadId}."
            );

        if (LogConfig.EngineLoop)
            Log.Info("[EngineLoop] Run started");

        while (!_renderSystem!.ShouldClose && !_stopRequested)
        {
            if (!Embedded)
            {
                _renderSystem.PumpEvents();
            }

            if (_paused)
            {
                Thread.Sleep(16);
                _lastTime = DateTime.UtcNow;
                continue;
            }

            float dt = GetDeltaTime();
            Time.UnscaledDeltaTime = dt;
            Time.DeltaTime = dt * Time.TimeScale;
            Time.FrameCount++;

            // 所有的游戏逻辑从这里开始处理
            Input.Update();
            TickFrame();
            OnRender();
            _sceneManager.PostRender(_snapshotManager.Current);

            // 帧末尾，记录快照
            CommitFrame();
        }

        if (LogConfig.EngineLoop)
            Log.Info("[EngineLoop] Run finished");
    }

    /// <summary>帧末提交：销毁处理 → 注册应用 → 快照 swap → 资产完成拾取（原 Run 尾部两段内联逻辑）。</summary>
    private void CommitFrame()
    {
        _snapshotManager.CommitPending(
            _registry,
            _sceneManager._destroyQueue,
            _sceneManager.ActiveScene,
            Time.DeltaTime
        );
        AssetManager.ProcessCompleted();
    }

    protected virtual float GetDeltaTime()
    {
        var now = DateTime.UtcNow;
        float dt = (float)(now - _lastTime).TotalSeconds;
        _lastTime = now;
        return System.Math.Min(dt, 0.1f);
    }

    /// <summary>固定步长逻辑帧：累加器驱动 FixedTick，随后 Tick/LateTick（原 LogicLoop.Tick 语义）。</summary>
    private void TickFrame()
    {
        int steps = _fixedStep.Advance(Time.DeltaTime);
        for (int i = 0; i < steps; i++)
            _sceneManager.FixedTick(_snapshotManager.Current, _fixedStep.FixedDeltaTime);
        _sceneManager.Tick(_snapshotManager.Current, Time.DeltaTime);
        _sceneManager.LateTick(_snapshotManager.Current);
    }

    protected virtual void OnRender()
    {
        _renderSystem!.Render(_snapshotManager.Current);
    }

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
