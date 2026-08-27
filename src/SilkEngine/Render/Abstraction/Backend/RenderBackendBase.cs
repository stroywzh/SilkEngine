using System;
using System.Collections.Generic;
using SilkEngine.Assets;

namespace SilkEngine.Render;

/// <summary>
/// 渲染后端抽象基类
/// <br/>子类实现 InitWindow / MakeContextCurrent / ClearContext / PumpWindowEvents / ExecutePass、CreateBuffer 和窗口属性。
/// </summary>
public abstract class RenderBackendBase : IRenderBackend
{
    /// <summary>是否已释放</summary>
    protected bool _disposed;

    /// <inheritdoc />
    public virtual Silk.NET.Windowing.IWindow? NativeWindow => null;

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
    public virtual void ExecutePass(IReadOnlyList<DrawCommand> commands) =>
        throw new NotSupportedException($"[{GetType().Name}] ExecutePass 未实现");

    /// <inheritdoc />
    public virtual void Present() =>
        throw new NotSupportedException($"[{GetType().Name}] Present 未实现");

    /// <inheritdoc />
    public abstract IRenderBuffer CreateBuffer(int sizeBytes);

    /// <inheritdoc />
    public virtual void DrawIndirect(IRenderBuffer buffer, int offset, int drawCount) =>
        throw new NotSupportedException($"[{GetType().Name}] DrawIndirect 未实现");

    /// <inheritdoc />
    public virtual void ReleaseTexture(TextureAsset texture) =>
        throw new NotSupportedException($"[{GetType().Name}] ReleaseTexture 未实现");

    /// <inheritdoc />
    public virtual void ReleaseGpuResource(IAsset asset) =>
        throw new NotSupportedException($"[{GetType().Name}] ReleaseGpuResource 未实现");

    /// <inheritdoc />
    public virtual void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
    }
}
