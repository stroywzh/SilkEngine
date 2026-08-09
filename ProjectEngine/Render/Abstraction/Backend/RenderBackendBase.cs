using System;
using System.Collections.Generic;
using System.Threading;

namespace ProjectEngine.Render;

/// <summary>
/// 渲染后端抽象基类
/// <br/>为 OpenGL、Vulkan 等后端提供共享的渲染线程管理、命令队列和同步信号。
/// 子类实现 Initialize / ProcessWindowEvents / ExecuteFrame 和窗口属性。
/// </summary>
public abstract class RenderBackendBase : IRenderBackend
{
    /// <summary>渲染线程是否应继续运行</summary>
    protected volatile bool _rendering;

    /// <summary>专用渲染线程</summary>
    protected Thread? _renderThread;

    /// <summary>命令队列锁</summary>
    protected readonly object _commandLock = new();

    /// <summary>待处理的绘制命令批次</summary>
    protected IReadOnlyList<DrawCommand>? _pendingCommands;

    /// <summary>命令就绪信号（主线程 → 渲染线程）</summary>
    protected readonly ManualResetEventSlim _commandsReady = new(false);

    /// <summary>帧完成信号（渲染线程 → 主线程）</summary>
    protected readonly ManualResetEventSlim _frameDone = new(false);

    /// <summary>是否已释放</summary>
    protected bool _disposed;

    /// <summary>缓冲区句柄计数器</summary>
    protected int _bufferCounter = 1;

    /// <inheritdoc />
    public abstract bool ShouldClose { get; }

    /// <inheritdoc />
    public abstract int Width { get; }

    /// <inheritdoc />
    public abstract int Height { get; }

    /// <inheritdoc />
    public abstract void Initialize(IntPtr windowHandle);

    /// <inheritdoc />
    public abstract void ProcessWindowEvents();

    /// <inheritdoc />
    public abstract void ExecuteFrame();

    /// <inheritdoc />
    public void SubmitCommands(IReadOnlyList<DrawCommand> commands)
    {
        lock (_commandLock)
        {
            _pendingCommands = commands;
        }
        _commandsReady.Set();
    }

    /// <inheritdoc />
    public void WaitForFrame()
    {
        _frameDone.Wait();
        _frameDone.Reset();
    }

    /// <inheritdoc />
    public IntPtr CreateBuffer(int sizeBytes) => (IntPtr)(_bufferCounter++);

    /// <inheritdoc />
    public virtual void DrawIndirect(IntPtr buffer, int offset, int drawCount) { }

    /// <inheritdoc />
    public virtual void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _rendering = false;
        _commandsReady.Set();
        _renderThread?.Join(2000);
        _commandsReady.Dispose();
        _frameDone.Dispose();
    }
}
