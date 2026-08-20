using System;

namespace SilkEngine.Render.OpenGL;

/// <summary>OpenGL 缓冲句柄；删除回调构造注入（GL 上下文操作由后端提供，测试免真实 GL）。</summary>
public sealed class OpenGLBuffer : IRenderBuffer
{
    private readonly Action _delete;
    private bool _disposed;

    public int SizeBytes { get; }
    public bool IsDisposed => _disposed;

    public OpenGLBuffer(int sizeBytes, Action delete)
    {
        SizeBytes = sizeBytes;
        _delete = delete ?? throw new ArgumentNullException(nameof(delete));
    }

    public void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(OpenGLBuffer));
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _delete();
    }
}
