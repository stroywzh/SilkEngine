using System;
using Silk.NET.OpenGL;
using SilkEngine.Core.Assets;

namespace SilkEngine.Render.OpenGL;

/// <summary>
/// OpenGL 纹理资源：渲染线程惰性创建（同 OpenGLMesh 缓存模式）
/// 构造与 Dispose 无需 GL 上下文；EnsureCreated 必须在渲染线程（上下文当前）调用
/// </summary>
public sealed class OpenGLTexture : IDisposable
{
    private readonly Texture2D _data;
    private GL? _gl;
    private uint _handle;
    private bool _disposed;

    public OpenGLTexture(Texture2D data) => _data = data;

    /// <summary>CPU 侧纹理数据</summary>
    public Texture2D Data => _data;

    /// <summary>GL 纹理句柄（EnsureCreated 前为 0）</summary>
    public uint Handle => _handle;

    /// <summary>GL 资源是否已创建</summary>
    public bool IsCreated => _handle != 0;

    /// <summary>是否已释放</summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// 惰性创建：glGenTexture + 线性过滤 + glTexImage2D(RGBA8)
    /// 幂等：已创建或已释放时直接返回
    /// </summary>
    public unsafe void EnsureCreated(GL gl)
    {
        if (_disposed || _handle != 0)
            return;
        _gl = gl;
        var img = _data.ImageData;
        _handle = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, _handle);
        gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMinFilter,
            (int)GLEnum.Linear
        );
        gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMagFilter,
            (int)GLEnum.Linear
        );
        fixed (byte* p = img.Pixels)
        {
            gl.TexImage2D(
                TextureTarget.Texture2D,
                0,
                InternalFormat.Rgba8,
                (uint)img.Width,
                (uint)img.Height,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                p
            );
        }
        gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_handle != 0)
            _gl?.DeleteTexture(_handle);
        _handle = 0;
    }
}
