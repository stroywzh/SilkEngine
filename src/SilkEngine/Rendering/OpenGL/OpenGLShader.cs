using System;
using Silk.NET.OpenGL;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.OpenGL;

/// <summary>
/// OpenGL 着色器资源：渲染线程从无资产语义的创建请求编译 GLSL 程序。
/// </summary>
public sealed class OpenGLShader : IOpenGlShaderResource, IDisposable
{
    private readonly GL _gl;
    private readonly uint _program;
    private bool _disposed;

    /// <inheritdoc />
    public bool IsCompiled { get; private set; }

    /// <summary>
    /// 从着色器创建请求编译 GLSL 程序（顶点 + 片元）；链接失败抛 <see cref="InvalidOperationException"/>，
    /// 且 finally 确保已创建的 program/shader 句柄被释放（防泄漏）。
    /// </summary>
    /// <param name="gl">OpenGL API 实例</param>
    /// <param name="request">无资产语义的着色器创建请求</param>
    public OpenGLShader(GL gl, RenderShaderCreateRequest request)
    {
        _gl = gl;
        var descriptor = request.Descriptor;
        uint vs = CompileStage(gl, descriptor.VertexSource, ShaderType.VertexShader);
        uint fs = CompileStage(gl, descriptor.FragmentSource, ShaderType.FragmentShader);
        _program = gl.CreateProgram();
        try
        {
            gl.AttachShader(_program, vs);
            gl.AttachShader(_program, fs);
            gl.LinkProgram(_program);

            gl.GetProgram(_program, ProgramPropertyARB.LinkStatus, out int status);
            if (status == 0)
            {
                string info = gl.GetProgramInfoLog(_program);
                throw new InvalidOperationException($"Shader link failed: {info}");
            }
            IsCompiled = true;
        }
        finally
        {
            gl.DeleteShader(vs);
            gl.DeleteShader(fs);
            if (!IsCompiled)
                gl.DeleteProgram(_program); // 失败路径：program 不会由 Dispose 释放（构造未完成）
        }
    }

    /// <summary>编译单个着色器阶段（顶点或片元）。</summary>
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

    /// <summary>绑定程序。</summary>
    public void Use() => _gl.UseProgram(_program);

    /// <summary>GL 程序句柄（帧路径 uniform 上传用）。</summary>
    public uint Program => _program;

    /// <summary>释放 GL 程序句柄（幂等）。</summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _gl.DeleteProgram(_program);
            _disposed = true;
        }
    }
}
