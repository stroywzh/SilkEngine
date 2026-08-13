using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using SilkEngine.Core;
using SilkEngine.Core.Assets;
using SilkEngine.Render;

namespace SilkEngine.Threading;

public class RenderThreadLoop : IDisposable
{
    private readonly IRenderBackend _backend;
    private Thread? _renderThread;
    private volatile bool _rendering;
    private readonly ManualResetEventSlim _commandsReady = new(false);
    private readonly ManualResetEventSlim _frameDone = new(false);
    private IReadOnlyList<RenderPass>? _pendingPasses;
    private bool _disposed;

    public bool ShouldClose => _backend.ShouldClose;
    public int Width => _backend.Width;
    public int Height => _backend.Height;

    public int PID => Process.GetCurrentProcess().Id;

    /// <summary>渲染后端实例</summary>
    public IRenderBackend Backend => _backend;

    public RenderThreadLoop(IRenderBackend backend) => _backend = backend;

    public void Initialize()
    {
        _backend.InitWindow();
        _renderThread = ThreadFactory.CreateThread(RenderLoop, "RenderThread");
        _rendering = true;
        _renderThread.Start();
        if (LogConfig.Render)
            Log.Info("[RenderThread] RenderThread Initialize Finished");
    }

    public void PumpEvents() => _backend.PumpWindowEvents();

    public void SubmitFrame(IReadOnlyList<RenderPass> passes)
    {
        _pendingPasses = passes;
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
            if (!_rendering)
                break;

            // 帧首：处理资产释放队列（GL 释放由后端接入；无注册管理器（测试）时跳过）
            if (Services.TryGet<AssetManager>(out var assetManager))
                assetManager.ProcessUnloadQueue(_backend.ReleaseTexture);
            try
            {
                if (_pendingPasses != null)
                {
                    foreach (var pass in _pendingPasses.OrderBy(p => p.SortOrder))
                    {
                        pass.BeforeCommands?.Invoke(_backend);
                        _backend.ExecutePass(pass.Commands);
                        pass.AfterCommands?.Invoke(_backend);
                    }
                    _backend.Present();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RenderThread] ExecutePass failed: {ex}");
            }
            if (LogConfig.Render)
                Log.Info($"[Render] Frame submitted (passes: {_pendingPasses?.Count ?? 0})");
            _frameDone.Set();
        }
        if (LogConfig.Render)
            Log.Info("[Render] Render thread stopped");
        _backend.ClearContext();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _rendering = false;
        _commandsReady.Set();
        _renderThread?.Join(2000);
        _commandsReady.Dispose();
        _frameDone.Dispose();
        _backend.Dispose();
    }
}
