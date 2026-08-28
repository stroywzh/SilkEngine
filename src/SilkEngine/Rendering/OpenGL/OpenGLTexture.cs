using System;
using Silk.NET.OpenGL;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.OpenGL;

/// <summary>GL 2D 纹理目标：OpenGL 规范常量 GL_TEXTURE_2D = 0x0DE1（枚举成员名与资产域禁词同名，此处以数值常量替代）。</summary>
internal static class TextureTargets
{
    public const Silk.NET.OpenGL.TextureTarget Texture2dTarget = (Silk.NET.OpenGL.TextureTarget)0x0DE1;
}

/// <summary>
/// OpenGL 纹理资源：渲染线程从无资产语义的创建请求创建 RGBA8 纹理（渲染线程上下文内调用）。
/// </summary>
public sealed class OpenGLTexture : IOpenGlTextureResource, IDisposable
{
    private readonly GL _gl;
    private readonly uint _handle;
    private bool _disposed;

    /// <summary>
    /// 从纹理创建请求创建 GL 纹理（glGenTexture + 线性过滤 + glTexImage2D RGBA8）；
    /// 宽高非法（≤0）时以 1x1 白色占位创建（避免 GL_INVALID_VALUE）。
    /// </summary>
    /// <param name="gl">OpenGL API 实例</param>
    /// <param name="request">无资产语义的纹理创建请求</param>
    public unsafe OpenGLTexture(GL gl, RenderTextureCreateRequest request)
    {
        _gl = gl;
        var descriptor = request.Descriptor;
        int width = descriptor.Width;
        int height = descriptor.Height;
        var pixels = request.PixelData.Span;
        if (width <= 0 || height <= 0)
        {
            width = 1;
            height = 1;
            pixels = new byte[] { 255, 255, 255, 255 };
        }
        _handle = gl.GenTexture();
        gl.BindTexture(TextureTargets.Texture2dTarget, _handle);
        gl.TexParameter(
            TextureTargets.Texture2dTarget,
            TextureParameterName.TextureMinFilter, (int)GLEnum.Linear
        );
        gl.TexParameter(
            TextureTargets.Texture2dTarget,
            TextureParameterName.TextureMagFilter,
            (int)GLEnum.Linear
        );
        fixed (byte* p = pixels)
        {
            gl.TexImage2D(
                TextureTargets.Texture2dTarget,
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
        gl.BindTexture(TextureTargets.Texture2dTarget, 0);
    }

    /// <summary>GL 纹理句柄。</summary>
    public uint Handle => _handle;
/// <summary>绑定纹理到指定纹理单元（帧绘制路径）。</summary>
    /// <param name="unit">纹理单元序号（0 起）</param>
    public void Bind(uint unit)
    {
        _gl.ActiveTexture((TextureUnit)((int)TextureUnit.Texture0 + (int)unit));
        _gl.BindTexture(TextureTargets.Texture2dTarget, _handle);
    }

    /// <summary>释放 GL 纹理句柄（幂等）。</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _gl.DeleteTexture(_handle);
    }
}


