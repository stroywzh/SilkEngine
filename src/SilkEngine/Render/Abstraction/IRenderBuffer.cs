using System;

namespace SilkEngine.Render;

/// <summary>GPU 缓冲句柄：带生命周期（Dispose 幂等）；释放后访问抛 ObjectDisposedException。</summary>
public interface IRenderBuffer : IDisposable
{
    int SizeBytes { get; }
    bool IsDisposed { get; }
}
