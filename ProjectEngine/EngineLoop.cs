using System;
using System.Collections.Generic;
using System.Diagnostics;
using ProjectEngine.Render;
using ProjectEngine.Threading;

namespace ProjectEngine;

public class EngineLoop : IDisposable
{
    private readonly IRenderBackend _backend;
    private readonly IRenderPipeline _pipeline;
    private readonly LogicLoop _logicLoop;
    private DateTime _lastTime;
    private volatile bool _stopRequested,
        _paused,
        _disposed;

    public IRenderBackend Backend => _backend;
    public IRenderPipeline Pipeline => _pipeline;
    public LogicLoop LogicLoop => _logicLoop;
    public bool Embedded { get; set; }
    public bool Paused
    {
        get => _paused;
        set => _paused = value;
    }

    public EngineLoop(IRenderBackend backend, IRenderPipeline pipeline)
    {
        _backend = backend;
        _pipeline = pipeline;
        _logicLoop = new LogicLoop();
        _pipeline.Initialize(backend);
        _lastTime = DateTime.UtcNow;
    }

    public void Run()
    {
        _backend.Initialize(IntPtr.Zero);
        _stopRequested = false;
        _lastTime = DateTime.UtcNow;

        while (!_backend.ShouldClose && !_stopRequested)
        {
            if (!Embedded)
                _backend.ProcessWindowEvents();
            if (_paused)
            {
                Thread.Sleep(16);
                _lastTime = DateTime.UtcNow;
                continue;
            }

            float dt = GetDeltaTime();
            Time.UnscaledDeltaTime = dt;
            Time.DeltaTime = dt * Time.TimeScale;

            if (Time.FrameCount == Int128.MaxValue)
                Time.FrameLoopCount += 1;

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

        float aspect = (float)_backend.Width / _backend.Height;
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
                mr.Material.SetMatrix4x4("uModel", mr.Transform.LocalToWorldMatrix);
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
                }
            );
        }
        _pipeline.Render(drawCommands);
    }

    public void Stop() => _stopRequested = true;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _stopRequested = true;
        _backend.Dispose();
        _pipeline.Dispose();
        _logicLoop.Dispose();
    }
}
