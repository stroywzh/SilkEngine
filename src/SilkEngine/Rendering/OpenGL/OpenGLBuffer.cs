using System;
using SilkEngine.Render;

namespace SilkEngine.Rendering.OpenGL;

/// <summary>OpenGL 缓冲句柄；删除回调构造注入（GL 上下文操作由后端提供，测试免真实 GL）。
/// 兼容文件：实现旧 <see cref="IRenderBuffer"/> 契约，待最终删除。</summary>
public sealed class OpenGLBuffer : IRenderBuffer
{
    private readonly Action _delete;
    private bool _disposed;

    /// <summary>缓冲大小（字节）</summary>
    public int SizeBytes { get; }

    /// <summary>是否已释放（Dispose 幂等）</summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// 以缓冲大小与删除回调创建句柄；删除回调于 Dispose 时调用一次
    /// （GL 上下文操作由后端注入，测试免真实 GL）
    /// </summary>
    /// <param name="sizeBytes">缓冲大小（字节）</param>
    /// <param name="delete">GL 删除回调（null 抛 ArgumentNullException）</param>
    public OpenGLBuffer(int sizeBytes, Action delete)
    {
        SizeBytes = sizeBytes;
        _delete = delete ?? throw new ArgumentNullException(nameof(delete));
    }

    /// <summary>已释放时抛 ObjectDisposedException（句柄访问前调用）</summary>
    public void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(OpenGLBuffer));
    }

    /// <summary>释放句柄（幂等）：仅首次调用执行删除回调</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _delete();
    }
}

