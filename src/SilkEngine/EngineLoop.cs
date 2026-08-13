using System;
using System.Diagnostics;
using SilkEngine.Core;
using SilkEngine.Core.Assets;
using SilkEngine.InputSystem;
using SilkEngine.Render;
using SilkEngine.Threading;

namespace SilkEngine;

public class EngineLoop : IDisposable
{
    private static int Pid => Process.GetCurrentProcess().Id;
    private readonly IRenderBackend _backend;
    private readonly LogicLoop _logicLoop;
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

    public LogicLoop Logic => _logicLoop;
    public IWorkerScheduler Workers => _workerPool;
    public bool Embedded { get; set; } = false;
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
        _logicLoop = new LogicLoop(_sceneManager);
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

        // 过渡期（Part 4 移除）：ActiveRegistry 静态暂保留，Attach(registry, snapshot) 注入后删除
        SceneManager.ActiveRegistry = _registry;
        _sceneManager.RegisterScene(_registry);
        _snapshotManager.CommitPending(
            _registry,
            _sceneManager._destroyQueue,
            _sceneManager.ActiveScene,
            0f
        );

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

            _logicLoop.Tick(Time.DeltaTime, _snapshotManager.Current);
            OnRender();
            _logicLoop.LateTick(Time.DeltaTime, _snapshotManager.Current);

            // 帧末尾，记录快照
            _snapshotManager.CommitPending(
                _registry,
                _sceneManager._destroyQueue,
                _sceneManager.ActiveScene,
                Time.DeltaTime
            );

            // 帧末：资产加载完成拾取 + 引用归零条目 Unloaded 迁移
            _assetManager!.ProcessCompleted();
        }
    }

    protected virtual float GetDeltaTime()
    {
        var now = DateTime.UtcNow;
        float dt = (float)(now - _lastTime).TotalSeconds;
        _lastTime = now;
        return System.Math.Min(dt, 0.1f);
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
        _logicLoop.Dispose();
        // 反序：RenderSystem(渲染线程先停) → SnapshotManager/Registry → AssetManager → SceneManager(解绑) → WorkerPool(最后停)
        Services.Shutdown();
    }
}
