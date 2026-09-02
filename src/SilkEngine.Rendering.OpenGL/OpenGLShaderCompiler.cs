using System;
using System.Collections.Generic;
using Silk.NET.OpenGL;
using SilkEngine.Rendering.Backend;

namespace SilkEngine.Rendering.OpenGL;

/// <summary>
/// OpenGL 4.6 SPIR-V 着色器编译器：经 <c>glShaderBinary</c>（SPIR_V 二进制格式）+
/// <c>glSpecializeShader</c> 加载 DXC 产出的两阶段 SPIR-V 并链接为可绘制 program。
/// 失败抛 <see cref="ShaderCompilationException"/>（消息含 source path/入口/profile/backend）。
/// 渲染线程上下文内调用；本类实现该路径但缺 GL 上下文时不执行（断言交给任务 12 的窗口测试）。
/// </summary>
public sealed class OpenGLShaderCompiler : IOpenGlShaderResource, IDisposable
{
    private readonly GL _gl;
    private readonly uint _program;
    private bool _disposed;

    private OpenGLShaderCompiler(GL gl, uint program)
    {
        _gl = gl;
        _program = program;
    }

    /// <summary>着色器是否已成功链接（失败路径为 false）。</summary>
    public bool IsCompiled { get; private set; }

    /// <summary>GL 程序句柄（帧路径 uniform 上传用）。</summary>
    public uint Program => _program;

    /// <summary>加载并链接 SPIR-V 着色器（顶点 + 片元）。</summary>
    /// <param name="gl">OpenGL API 实例</param>
    /// <param name="request">编译请求（错误上下文与入口名来源）</param>
    /// <param name="spirv">两阶段 SPIR-V 包（<see cref="DxcHlslCompiler.PackStages"/> 布局）</param>
    /// <exception cref="ShaderCompilationException">加载/链接失败（含请求上下文与阶段）</exception>
    public static OpenGLShaderCompiler Create(GL gl, ShaderCompileRequest request, IReadOnlyList<byte> spirv)
    {
        var (vertexBinary, fragmentBinary) = DxcHlslCompiler.UnpackStages(spirv);
        uint vertex = CreateSpecializedShader(
            gl, request, ShaderType.VertexShader, vertexBinary, request.VertexEntryPoint);
        uint fragment = CreateSpecializedShader(
            gl, request, ShaderType.FragmentShader, fragmentBinary, request.FragmentEntryPoint);

        uint program = gl.CreateProgram();
        var compiled = new OpenGLShaderCompiler(gl, program);
        try
        {
            gl.AttachShader(program, vertex);
            gl.AttachShader(program, fragment);
            gl.LinkProgram(program);
            gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int status);
            if (status == 0)
                throw Failure(request, "gl-specialize", $"GL 链接失败: {gl.GetProgramInfoLog(program)}");
            compiled.IsCompiled = true;
        }
        finally
        {
            gl.DeleteShader(vertex);
            gl.DeleteShader(fragment);
            if (!compiled.IsCompiled)
                gl.DeleteProgram(program); // 失败路径：program 不会由 Dispose 释放（构造未完成）
        }
        return compiled;
    }

    /// <summary>释放 GL 程序句柄（幂等）。</summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _gl.DeleteProgram(_program);
            _disposed = true;
        }
    }

    /// <summary>创建并特化单个 SPIR-V 阶段着色器；全部失败路径释放句柄（防泄漏）。</summary>
    private static uint CreateSpecializedShader(
        GL gl,
        ShaderCompileRequest request,
        ShaderType type,
        byte[] binary,
        string entryPoint)
    {
        uint handle = gl.CreateShader(type);
        try
        {
            gl.ShaderBinary(new ReadOnlySpan<uint>(in handle), GLEnum.ShaderBinaryFormatSpirV, binary);

            // specialization 常量为零：索引/值指针由 GL 忽略，传 ref 占位即可（常量化展开由任务 12 窗口测试覆盖）
            uint placeholder = 0;
            gl.SpecializeShader(handle, entryPoint, 0, ref placeholder, ref placeholder);

            gl.GetShader(handle, ShaderParameterName.CompileStatus, out int status);
            if (status == 0)
                throw Failure(
                    request,
                    "gl-specialize",
                    $"{type} 特化失败 '{entryPoint}': {gl.GetShaderInfoLog(handle)}");
            return handle;
        }
        catch (Exception ex)
        {
            gl.DeleteShader(handle);
            if (ex is ShaderCompilationException compilation)
                throw compilation;
            throw Failure(request, "gl-specialize", $"{type} 加载失败 '{entryPoint}': {ex.Message}");
        }
    }

    /// <summary>编译管线失败异常：消息含请求上下文（path/入口/profile/backend）。</summary>
    private static ShaderCompilationException Failure(ShaderCompileRequest request, string stage, string detail)
        => new(
            stage,
            $"[{request.Backend}] GL 着色器加载失败 '{request.SourcePath}'（vert='{request.VertexEntryPoint}', frag='{request.FragmentEntryPoint}', profile='{request.Profile}'）: {detail}");
}