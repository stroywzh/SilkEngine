using System;
using Silk.NET.OpenGL;
using SilkEngine.Core;
using SilkEngine.Assets;

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
    internal Texture2D Data => _data;

    /// <summary>GL 纹理句柄（EnsureCreated 前为 0）</summary>
    public uint Handle => _handle;

    /// <summary>是否已释放</summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// 惰性创建：glGenTexture + 线性过滤 + glTexImage2D(RGBA8)
    /// 幂等：已创建或已释放时直接返回；宽高非法（≤0）时以 1x1 白色占位创建（避免 GL_INVALID_VALUE）
    /// </summary>
    public unsafe void EnsureCreated(GL gl)
    {
        if (_disposed || _handle != 0)
            return;
        _gl = gl;
        var img = _data.ImageData;
        int width = img.Width;
        int height = img.Height;
        byte[] pixels = img.Pixels;
        if (width <= 0 || height <= 0)
        {
            Log.Warn(
                $"[Render] Texture '{_data.Name}' 尺寸无效 ({width}x{height})，使用 1x1 白色占位"
            );
            width = 1;
            height = 1;
            pixels = new byte[] { 255, 255, 255, 255 };
        }
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
        fixed (byte* p = pixels)
        {
            gl.TexImage2D(
                TextureTarget.Texture2D,
                0,
                InternalFormat.Rgba8,
                (uint)width,
                (uint)height,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                p
            );
        }
        gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    /// <summary>释放 GL 纹理句柄（幂等；未创建或无上下文时无操作）</summary>
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
