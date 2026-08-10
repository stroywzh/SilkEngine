using System;
using System.Collections.Generic;

namespace ProjectEngine.Render;

/// <summary>
/// 渲染后端抽象基类
/// <br/>为 OpenGL、Vulkan 等后端提供共享的缓冲区句柄管理与释放状态。
/// 子类实现 InitWindow / MakeContextCurrent / ClearContext / PumpWindowEvents / ExecuteFrame 和窗口属性。
/// </summary>
public abstract class RenderBackendBase : IRenderBackend
{
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
    public abstract void InitWindow();

    /// <inheritdoc />
    public abstract void MakeContextCurrent();

    /// <inheritdoc />
    public abstract void ClearContext();

    /// <inheritdoc />
    public abstract void PumpWindowEvents();

    /// <inheritdoc />
    public abstract void ExecuteFrame(IReadOnlyList<DrawCommand> commands);

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
    }
}
