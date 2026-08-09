using System;
using Silk.NET.OpenGL;

namespace ProjectEngine.Render.OpenGL;

/// <summary>
/// IShader 的 OpenGL 实现
/// <br/>在渲染线程将 Shader 数据编译为 GLSL 顶点+片段着色器。
/// </summary>
public class OpenGLShader : IShader
{
    private readonly GL _gl;
    private readonly uint _program;
    private bool _disposed;

    /// <inheritdoc />
    public bool IsCompiled { get; private set; }

    /// <summary>
    /// 从 Shader 数据编译 GLSL 程序
    /// </summary>
    public OpenGLShader(GL gl, Shader data)
    {
        _gl = gl;
        uint vs = CompileStage(gl, data.VertexSource, ShaderType.VertexShader);
        uint fs = CompileStage(gl, data.FragmentSource, ShaderType.FragmentShader);
        _program = gl.CreateProgram();
        gl.AttachShader(_program, vs);
        gl.AttachShader(_program, fs);
        gl.LinkProgram(_program);

        gl.GetProgram(_program, ProgramPropertyARB.LinkStatus, out int status);
        if (status == 0)
        {
            string info = gl.GetProgramInfoLog(_program);
            throw new InvalidOperationException($"Shader link failed: {info}");
        }

        gl.DeleteShader(vs);
        gl.DeleteShader(fs);
        IsCompiled = true;
    }

    /// <summary>
    /// 编译单个着色器阶段（顶点或片段）
    /// </summary>
    private static uint CompileStage(GL gl, string source, ShaderType type)
    {
        uint handle = gl.CreateShader(type);
        gl.ShaderSource(handle, source);
        gl.CompileShader(handle);
        gl.GetShader(handle, ShaderParameterName.CompileStatus, out int status);
        if (status == 0)
        {
            string info = gl.GetShaderInfoLog(handle);
            gl.DeleteShader(handle);
            throw new InvalidOperationException($"Shader compile failed: {info}");
        }
        return handle;
    }

    /// <inheritdoc />
    public void Use() => _gl.UseProgram(_program);

    /// <summary>
    /// 获取 OpenGL 程序句柄
    /// </summary>
    internal uint GetProgram() => _program;

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _gl.DeleteProgram(_program);
            _disposed = true;
        }
    }
}
