using System;

namespace SilkEngine.Render;

/// <summary>GPU 缓冲句柄：带生命周期（Dispose 幂等）；释放后访问抛 ObjectDisposedException。</summary>
public interface IRenderBuffer : IDisposable
{
    /// <summary>缓冲大小（字节）</summary>
    int SizeBytes { get; }

    /// <summary>是否已释放（释放后访问抛 ObjectDisposedException）</summary>
    bool IsDisposed { get; }
}
