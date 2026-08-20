using System;
using Silk.NET.OpenGL;
using SilkEngine.Core.Assets;

namespace SilkEngine.Render.OpenGL;

/// <summary>
/// IMaterial 的 OpenGL 实现
/// <br/>在渲染线程绑定着色器并设置 Material 数据的 uniform 值
/// </summary>
public class OpenGLMaterial : IMaterial
{
    /// <summary>主纹理采样器 uniform 名</summary>
    public const string SamplerUniformName = "uMainTex";

    private readonly GL _gl;
    private readonly Material _data;
    private readonly OpenGLShader _shader;
    private readonly OpenGLTextureRegistry _textures;
    private bool _disposed;

    /// <summary>
    /// 从 Material 数据创建 OpenGL 材质，绑定指定着色器
    /// </summary>
    public OpenGLMaterial(GL gl, Material data, OpenGLShader shader, OpenGLTextureRegistry textures)
    {
        _gl = gl;
        _data = data;
        _shader = shader;
        _textures = textures;
    }

    /// <inheritdoc />
    public void Apply()
    {
        _shader.Use();
        foreach (var kv in _data.Floats)
        {
            int loc = _gl.GetUniformLocation(_shader.GetProgram(), kv.Key);
            if (loc != -1)
            {
                _gl.Uniform1(loc, kv.Value);
            }
        }
        foreach (var kv in _data.Vectors)
        {
            int loc = _gl.GetUniformLocation(_shader.GetProgram(), kv.Key);
            if (loc != -1)
            {
                _gl.Uniform3(loc, kv.Value.X, kv.Value.Y, kv.Value.Z);
            }
        }
        unsafe
        {
            foreach (var kv in _data.Matrices)
            {
                int loc = _gl.GetUniformLocation(_shader.GetProgram(), kv.Key);
                if (loc != -1)
                {
                    fixed (float* ptr = kv.Value)
                    {
                        _gl.UniformMatrix4(loc, 1, true, ptr);
                    }
                }
            }
        }

        int samplerLoc = _gl.GetUniformLocation(_shader.GetProgram(), SamplerUniformName);
        if (samplerLoc != -1)
        {
            var texture = ResolveTexture(_data);
            var glTex = _textures.GetOrCreate(texture);
            glTex.EnsureCreated(_gl);
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, glTex.Handle);
            _gl.Uniform1(samplerLoc, 0);
        }
    }

    /// <summary>
    /// 解析材质实际绑定纹理：无主纹理（含 LazyAsync 未就绪）→ 引擎白色占位
    /// </summary>
    internal static Texture2D ResolveTexture(Material material) =>
        material.MainTexture ?? DefaultTextures.White;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
    }
}
