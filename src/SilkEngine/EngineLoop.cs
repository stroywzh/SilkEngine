using System;
using System.Diagnostics;
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

    public EngineLoop(IRenderBackend backend)
    {
        _backend = backend;
        _logicLoop = new LogicLoop();
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

        _stopRequested = false;
        _lastTime = DateTime.UtcNow;

        SceneManager.Instance.RegisterScene(_registry);
        _snapshotManager.CommitPending(_registry, SceneManager._destroyQueue, SceneManager.ActiveScene, 0f);

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

            Input.Update();

            _logicLoop.Tick(Time.DeltaTime, _snapshotManager.Current, _registry);
            _renderSystem!.Render(_snapshotManager.Current);
            _logicLoop.LateTick(Time.DeltaTime, _snapshotManager.Current, _registry);

            _snapshotManager.CommitPending(_registry, SceneManager._destroyQueue, SceneManager.ActiveScene, Time.DeltaTime);
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
        _workerPool.Dispose();
        _renderSystem?.Dispose();
        _logicLoop.Dispose();
    }
}
