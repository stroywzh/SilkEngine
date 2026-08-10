using System;
using System.Collections.Generic;
using System.Diagnostics;
using ProjectEngine.Render;

namespace ProjectEngine;

public class EngineLoop : IDisposable
{
    private readonly IRenderBackend _backend;
    private readonly IRenderPipeline _pipeline;
    private readonly MainLoop _mainLoop;
    private DateTime _lastTime;
    private volatile bool _stopRequested, _paused, _disposed;

    public IRenderBackend Backend => _backend;
    public IRenderPipeline Pipeline => _pipeline;
    public MainLoop MainLoop => _mainLoop;
    public bool Embedded { get; set; }
    public bool Paused { get => _paused; set => _paused = value; }

    public EngineLoop(IRenderBackend backend, IRenderPipeline pipeline)
    {
        _backend = backend;
        _pipeline = pipeline;
        _mainLoop = new MainLoop();
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
            if (!Embedded) _backend.ProcessWindowEvents();
            if (_paused) { Thread.Sleep(16); _lastTime = DateTime.UtcNow; continue; }

            float dt = GetDeltaTime();
            Time.UnscaledDeltaTime = dt;
            Time.DeltaTime = dt * Time.TimeScale;
            Time.FrameCount++;

            _mainLoop.Tick(Time.DeltaTime);
            OnRender();
            _mainLoop.LateTick(Time.DeltaTime);
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
        var drawCommands = new List<DrawCommand>();
        _pipeline.Render(drawCommands);
    }

    public void Stop() => _stopRequested = true;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stopRequested = true;
        _backend.Dispose();
        _pipeline.Dispose();
        _mainLoop.Dispose();
    }
}
