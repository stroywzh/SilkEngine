using System;
using System.Collections.Generic;
using System.Threading;
using ProjectEngine.Render;

namespace ProjectEngine.Threading;

public class RenderThreadLoop : IDisposable
{
    private readonly IRenderBackend _backend;
    private Thread? _renderThread;
    private volatile bool _rendering;
    private readonly ManualResetEventSlim _commandsReady = new(false);
    private readonly ManualResetEventSlim _frameDone = new(false);
    private IReadOnlyList<DrawCommand>? _pendingCommands;
    private bool _disposed;

    public bool ShouldClose => _backend.ShouldClose;
    public int Width => _backend.Width;
    public int Height => _backend.Height;

    public RenderThreadLoop(IRenderBackend backend) => _backend = backend;

    public void Initialize()
    {
        _backend.InitWindow();
        _renderThread = ThreadFactory.CreateThread(RenderLoop, "RenderThread");
        _rendering = true;
        _renderThread.Start();
        Log.Info("[RenderThread] RenderThread Initialize Finished");
    }

    public void PumpEvents() => _backend.PumpWindowEvents();

    public void SubmitFrame(IReadOnlyList<DrawCommand> commands)
    {
        _pendingCommands = commands;
        _commandsReady.Set();
        _frameDone.Wait();
        _frameDone.Reset();
    }

    private void RenderLoop()
    {
        _backend.MakeContextCurrent();
        while (_rendering)
        {
            _commandsReady.Wait();
            _commandsReady.Reset();
            if (!_rendering) break;
            _backend.ExecuteFrame(_pendingCommands!);
            _frameDone.Set();
        }
        _backend.ClearContext();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _rendering = false;
        _commandsReady.Set();
        _renderThread?.Join(2000);
        _commandsReady.Dispose();
        _frameDone.Dispose();
        _backend.Dispose();
    }
}
