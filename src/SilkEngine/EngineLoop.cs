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
    private readonly EngineThreadPool _workerPool = new(2);
    private readonly FrameSnapshotManager _snapshotManager = new();
    private readonly ComponentRegistry _registry = new();
    private readonly SceneManager _sceneManager;
    private AssetManager? _assetManager;
    private RenderSystem? _renderSystem;
    private DateTime _lastTime;
    private volatile bool _stopRequested,
        _paused,
        _disposed,
        _canStart;

    public IWorkerScheduler Workers => _workerPool;
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
        set => _paused = value;
    }

    /// <summary>场景管理器实例（ctor 创建并订阅 Object.DestroyHandler；宿主经此取用）</summary>
    public SceneManager SceneManager => _sceneManager;

    /// <summary>资产管理器实例（Initialize 创建并注入共享工作池；未初始化访问抛异常）</summary>
    public AssetManager AssetManager =>
        _assetManager ?? throw new InvalidOperationException("EngineLoop.Initialize 尚未执行");

    public EngineLoop(IRenderBackend backend)
    {
        _backend = backend;
        _sceneManager = new SceneManager();
        Time.FixedDeltaTime = _fixedStep.FixedDeltaTime;
        _lastTime = DateTime.UtcNow;
        _canStart = false;
    }

    public EngineLoop Initialize()
    {
        _renderSystem = new RenderSystem(_backend);
        _renderSystem.Initialize();

        //TODO:初始化逻辑后面要改，改成基于Editor启动和游戏启动
        if (_renderSystem.Backend.NativeWindow is { } win)
        {
            var inputProvider = new SilkInputProvider();
            inputProvider.Initialize(win);
            Input.SetProvider(inputProvider);
        }

        _assetManager = new AssetManager(_workerPool);
        Services.Register(_workerPool);
        Services.Register(_sceneManager);
        Services.Register(_assetManager);
        Services.Register(_registry);
        Services.Register(_snapshotManager);
        Services.Register(_renderSystem);

        // 注入注册表与快照管理器（Part 4：替代原 ActiveRegistry 静态赋值）
        _sceneManager.Attach(_registry, _snapshotManager);
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
            Log.Warn("[EngineLoop]: Invaild Operation,EngineLoop haven't Initialzed yet.");
            return;
        }

        Log.Info(
            $"[EngineLoop]: EngineLoop Started. \nManaged threads: \nMain(heartbeat):PID{Pid}\nWorkerThreadCount:{_workerPool.WorkerThreadCount}\nRenderThread:PID{Pid}."
        );

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
        // 反序：RenderSystem(渲染线程先停) → SnapshotManager/Registry → AssetManager → SceneManager(解绑) → WorkerPool(最后停)
        Services.Shutdown();
    }
}
