using Silk.NET.OpenGL;
using SilkEngine.Math;

namespace SilkEngine.Rendering.OpenGL;

/// <summary>
/// OpenGL 帧绘制调用门面（internal 测试注入点）：生产适配真实 GL 调用；
/// 测试经注入录制桩验证帧内 uniform 上传序列与参数，不依赖真实 GL 上下文。
/// </summary>
internal interface IOpenGlFrameCalls : IDisposable
{
    /// <summary>帧首状态：深度测试、视口与清屏。</summary>
    void SetupFrame(float r, float g, float b, float a, int width, int height);

    /// <summary>绑定着色器程序。</summary>
    void UseProgram(uint program);

    /// <summary>查询 uniform 位置（未命中为 -1）。</summary>
    int GetUniformLocation(uint program, string name);

    /// <summary>上传 float uniform。</summary>
    void Uniform1(int location, float value);

    /// <summary>上传 vec3 uniform。</summary>
    void Uniform3(int location, Vector3 value);

    /// <summary>上传 mat4 uniform（transpose 由调用方按行主序约定传 true）。</summary>
    void UniformMatrix4(int location, bool transpose, Matrix4x4 matrix);
}

/// <summary>着色器帧资源视图：仅暴露 program 句柄（帧路径不感知资源创建细节）。</summary>
internal interface IOpenGlShaderResource
{
    /// <summary>GL 程序句柄。</summary>
    uint Program { get; }
}

/// <summary>网格帧资源视图：绑定 VAO 并绘制。</summary>
internal interface IOpenGlMeshResource
{
    /// <summary>执行一次绘制。</summary>
    void Draw();
}

/// <summary>纹理帧资源视图：绑定到指定纹理单元。</summary>
internal interface IOpenGlTextureResource
{
    /// <summary>绑定纹理到指定纹理单元。</summary>
    /// <param name="unit">纹理单元序号（0 起）</param>
    void Bind(uint unit);
}

/// <summary>真实 GL 适配（仅渲染线程上下文内调用；GL 生命周期归后端，Dispose 为空实现）。</summary>
internal sealed class OpenGlFrameCalls(GL gl) : IOpenGlFrameCalls
{
    /// <inheritdoc />
    public void SetupFrame(float r, float g, float b, float a, int width, int height)
    {
        gl.Enable(GLEnum.DepthTest);
        gl.Viewport(0, 0, (uint)width, (uint)height);
        gl.ClearColor(r, g, b, a);
        gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
    }

    /// <inheritdoc />
    public void UseProgram(uint program) => gl.UseProgram(program);

    /// <inheritdoc />
    public int GetUniformLocation(uint program, string name) => gl.GetUniformLocation(program, name);

    /// <inheritdoc />
    public void Uniform1(int location, float value) => gl.Uniform1(location, value);

    /// <inheritdoc />
    public void Uniform3(int location, Vector3 value) => gl.Uniform3(location, value.X, value.Y, value.Z);

    /// <inheritdoc />
    public unsafe void UniformMatrix4(int location, bool transpose, Matrix4x4 matrix)
    {
        // Matrix4x4 为 Sequential 布局（16 个连续 float），参数按值传入已固定，零分配直传；
        // transpose=true：引擎行主序 → GL 列主序（AGENTS 约定）
        gl.UniformMatrix4(location, 1, transpose, &matrix.M11);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // GL 实例由 OpenGLRenderBackend 释放
    }
}
