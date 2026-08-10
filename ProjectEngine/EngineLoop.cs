using System;
using System.Collections.Generic;
using ProjectEngine.Render;
using ProjectEngine.Threading;

namespace ProjectEngine;

public class EngineLoop : IDisposable
{
    private readonly RenderThreadLoop _renderThreadLoop;
    private readonly LogicLoop _logicLoop;
    private readonly EngineThreadPool _workerPool = new(2);
    private DateTime _lastTime;
    private volatile bool _stopRequested,
        _paused,
        _disposed,
        _canStart;

    public RenderThreadLoop Render => _renderThreadLoop;
    public LogicLoop Logic => _logicLoop;
    public IWorkerScheduler Workers => _workerPool;
    public bool Embedded { get; set; }
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

        Log.Info($"[EngineLoop] Started. Managed threads: Main(heartbeat) + {_workerPool} + RenderThread. Unnamed threads likely from GLFW/.NET runtime.");

        while (!_renderThreadLoop.ShouldClose && !_stopRequested)
        {
            if (!Embedded)
                _renderThreadLoop.PumpEvents();
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

            _logicLoop.Tick(Time.DeltaTime);
            OnRender();
            _logicLoop.LateTick(Time.DeltaTime);
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
        Camera? cam = null;
        SceneManager.ForEachComponent<Camera>(c =>
        {
            if (c.GameObject.IsActive)
                cam ??= c;
        });

        float aspect = (float)_renderThreadLoop.Width / _renderThreadLoop.Height;
        if (cam != null)
            cam.UpdateMatrices(aspect);

        var renderers = new List<MeshRenderer>();
        SceneManager.ForEachComponent<MeshRenderer>(r =>
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
