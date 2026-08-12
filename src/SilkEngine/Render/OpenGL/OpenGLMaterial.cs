using System;
using Silk.NET.OpenGL;

namespace SilkEngine.Render.OpenGL;

/// <summary>
/// IMaterial 的 OpenGL 实现
/// <br/>在渲染线程绑定着色器并设置 Material 数据的 uniform 值
/// </summary>
public class OpenGLMaterial : IMaterial
{
    private readonly GL _gl;
    private readonly Material _data;
    private readonly OpenGLShader _shader;
    private bool _disposed;

    /// <summary>
    /// 从 Material 数据创建 OpenGL 材质，绑定指定着色器
    /// </summary>
    public OpenGLMaterial(GL gl, Material data, OpenGLShader shader)
    {
        _gl = gl;
        _data = data;
        _shader = shader;
    }

    /// <inheritdoc />
    public void Apply()
    {
        _shader.Use();
        foreach (var kv in _data.Floats)
        {
            int loc = _gl.GetUniformLocation(_shader.GetProgram(), kv.Key);
            if (loc != -1) _gl.Uniform1(loc, kv.Value);
        }
        foreach (var kv in _data.Vectors)
        {
            int loc = _gl.GetUniformLocation(_shader.GetProgram(), kv.Key);
            if (loc != -1) _gl.Uniform3(loc, kv.Value.X, kv.Value.Y, kv.Value.Z);
        }
        unsafe
        {
            foreach (var kv in _data.Matrices)
            {
                int loc = _gl.GetUniformLocation(_shader.GetProgram(), kv.Key);
                if (loc != -1)
                {
                    fixed (float* ptr = kv.Value)
                        _gl.UniformMatrix4(loc, 1, false, ptr);
                }
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
