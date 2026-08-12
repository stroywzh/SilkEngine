using System;
using System.Collections.Generic;
using System.Diagnostics;
using SilkEngine.InputSystem;
using SilkEngine.Render;
using SilkEngine.Threading;

namespace SilkEngine;

public class EngineLoop : IDisposable
{
    private static int Pid => Process.GetCurrentProcess().Id;
    private readonly RenderThreadLoop _renderThreadLoop;
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

    public RenderThreadLoop Render => _renderThreadLoop;
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
        _renderThreadLoop = new RenderThreadLoop(backend);
        _logicLoop = new LogicLoop();
        _lastTime = DateTime.UtcNow;
        _canStart = false;
    }

    public EngineLoop Initialize()
    {
        _renderThreadLoop.Initialize();

        //TODO:初始化逻辑后面要改，改成基于Editor启动和游戏启动
        if (Render.Backend.NativeWindow is { } win)
        {
            var inputProvider = new SilkInputProvider();
            inputProvider.Initialize(win);
            Input.SetProvider(inputProvider);
        }

        _stopRequested = false;
        _lastTime = DateTime.UtcNow;

        _renderSystem = new RenderSystem(Render.Backend);
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
            $"[EngineLoop]: EngineLoop Started. \nManaged threads: \nMain(heartbeat):PID{Pid}\nWorkerThreadCount:{_workerPool.WorkerThreadCount}\nRenderThread:PID{_renderThreadLoop.PID}."
        );

        while (!_renderThreadLoop.ShouldClose && !_stopRequested)
        {
            if (!Embedded)
            {
                _renderThreadLoop.PumpEvents();
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

            _logicLoop.TickWithSnapshot(Time.DeltaTime, _snapshotManager.Current, _registry);
            _renderSystem!.Render(_snapshotManager.Current);
            _logicLoop.LateTickWithSnapshot(Time.DeltaTime, _snapshotManager.Current, _registry);

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

    // Camera mainCam = new();
    /// <summary>
    /// 很显然这里这个东西需要拆除去，就光凭这个camera每帧都要寻找就是纯拖累来的
    /// </summary>
    protected virtual void OnRender()
    {
        Camera? cam = null;
        SceneManager.Instance.ForEachComponent<Camera>(c =>
        {
            if (c.GameObject.IsActive)
                cam ??= c;
        });

        if (cam == null)
        {
            cam = new Camera();
            // cam.Transform.LocalPosition = Math.Vector3.Zero;
        }

        float aspect = (float)_renderThreadLoop.Width / _renderThreadLoop.Height;

        cam.UpdateMatrices(aspect);

        var renderers = new List<MeshRenderer>();
        SceneManager.Instance.ForEachComponent<MeshRenderer>(r =>
        {
            if (r.Enabled && r.GameObject.IsActive)
                renderers.Add(r);
        });

        var drawCommands = new List<DrawCommand>();
        foreach (var mr in renderers)
        {
            if (mr.Material != null && cam != null)
            {
                mr.Material.SetMatrix4x4("uView", cam.ViewMatrix);
                mr.Material.SetMatrix4x4("uProjection", cam.ProjectionMatrix);
            }
            drawCommands.Add(
                new SingleDrawCommand
                {
                    Shader = mr.Shader,
                    Mesh = mr.Mesh,
                    Material = mr.Material,
                    Enabled = mr.Enabled,
                    ModelMatrix = mr.Transform.LocalToWorldMatrix,
                }
            );
        }
        _renderThreadLoop.SubmitFrame(drawCommands);
    }

    public void Stop() => _stopRequested = true;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _stopRequested = true;
        _workerPool.Dispose();
        _renderThreadLoop.Dispose();
        _logicLoop.Dispose();
    }
}
