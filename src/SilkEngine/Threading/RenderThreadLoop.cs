using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using SilkEngine.Core;
using SilkEngine.Core.Assets;
using SilkEngine.Render;

namespace SilkEngine.Threading;

/// <summary>
/// 渲染工作器：仅负责后端生命周期、帧同步握手与 Passes 执行；线程控制权归 ThreadManager
/// （Initialize 绑定 ILoopExecutor，本类不创建/持有/释放线程）。
/// </summary>
public class RenderThreadLoop : IDisposable
{
    private readonly IRenderBackend _backend;

    public ILoopExecutor ThreadLoop => _executor;
    private ILoopExecutor _executor;
    private volatile bool _rendering;
    private readonly ManualResetEventSlim _commandsReady = new(false);
    private readonly ManualResetEventSlim _frameDone = new(false);
    private IReadOnlyList<RenderPass>? _pendingPasses;
    private bool _disposed;
    private bool _contextBound;

    public bool ShouldClose => _backend.ShouldClose;
    public int Width => _backend.Width;
    public int Height => _backend.Height;

    /// <summary>渲染后端实例</summary>
    public IRenderBackend Backend => _backend;

    public RenderThreadLoop(IRenderBackend backend, ILoopExecutor executor)
    {
        _backend = backend;
        _executor = executor;
    }

    /// <summary>绑定执行者并启动渲染循环（executor.Run(RenderFrame)，返回 false 退出）。</summary>
    public void Initialize()
    {
        _backend.InitWindow();
        _rendering = true;
        _executor.Run(RenderFrame);
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

    /// <summary>渲染循环单帧（执行者线程调用；返回 false 退出循环）。</summary>
    private bool RenderFrame()
    {
        if (!_contextBound)
        {
            _backend.MakeContextCurrent();
            _contextBound = true;
        }
        _commandsReady.Wait();
        _commandsReady.Reset();
        if (!_rendering)
        {
            _backend.ClearContext();
            return false;
        }

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
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _rendering = false;
        _commandsReady.Set(); // 唤醒阻塞帧 → RenderFrame 返回 false → 线程退出
        _executor?.Stop();
        _executor?.Join();
        _commandsReady.Dispose();
        _frameDone.Dispose();
        _backend.Dispose();
    }
}
