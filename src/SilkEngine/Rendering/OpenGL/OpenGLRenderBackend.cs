using System;
using System.Collections.Generic;
using System.Threading;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SilkEngine.Core;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.Backend;
using DefaultWindowOption = SilkEngine.Render.DefaultWindowOption;

namespace SilkEngine.Rendering.OpenGL;

/// <summary>
/// OpenGL 渲染后端：实现 Rendering 契约（IRenderBackend/IRenderDevice/IWindowSurface）。
/// 只按无资产语义的 Render Handle 查 native 资源；资源经 CreateXxx 创建、经 Release 释放，
/// 不解析任何资产类型、不查询服务定位器。
/// GL 上下文归属渲染线程（Initialize 在渲染线程 MakeCurrent）；Dispose 幂等。
/// </summary>
public sealed class OpenGLRenderBackend : IRenderBackend, IRenderDevice, IWindowSurface
{
    private readonly Dictionary<ulong, IDisposable> _resources = new();
    private IWindow? _window;
    private GL? _gl;
    private ulong _nextHandle = 1;

    private float _clearR = 0.1f,
        _clearG = 0.1f,
        _clearB = 0.1f,
        _clearA = 1.0f;
    private int _disposed;

    /// <inheritdoc />
    public Silk.NET.Windowing.IWindow? NativeWindow => _window;

    /// <inheritdoc />
    public bool ShouldClose => _window?.IsClosing ?? false;

    /// <inheritdoc />
    public int Width => _window?.Size.X ?? 800;

    /// <inheritdoc />
    public int Height => _window?.Size.Y ?? 600;

    /// <inheritdoc />
    public void PumpWindowEvents() => _window?.DoEvents();

    /// <summary>OpenGL API 实例（Initialize 后非 null）。</summary>
    internal GL GL => _gl!;

    /// <summary>设置清除颜色。</summary>
    public void SetClearColor(float r, float g, float b, float a)
    {
        _clearR = r;
        _clearG = g;
        _clearB = b;
        _clearA = a;
    }

    /// <summary>
    /// 初始化后端（渲染线程启动阶段调用一次）：创建窗口、获取 GL API 并绑定上下文。
    /// </summary>
    public void Initialize()
    {
        _window = Silk.NET.Windowing.Window.Create(DefaultWindowOption.DefaultOpenGLOption);
        _window.Initialize();
        _gl = GL.GetApi(_window);
        _window.IsContextControlDisabled = true;
        _window.MakeCurrent();
    }

    /// <summary>创建纹理资源（渲染线程上下文内调用）。</summary>
    /// <param name="request">无资产语义的纹理创建请求</param>
    /// <returns>纹理 GPU 句柄</returns>
    public RenderTextureHandle CreateTexture(RenderTextureCreateRequest request)
        => new(Register(new OpenGLTexture(RequireGl(), request)));

    /// <summary>创建着色器资源（渲染线程上下文内调用）。</summary>
    /// <param name="request">无资产语义的着色器创建请求</param>
    /// <returns>着色器 GPU 句柄</returns>
    public RenderShaderHandle CreateShader(RenderShaderCreateRequest request)
        => new(Register(new OpenGLShader(RequireGl(), request)));

    /// <summary>创建网格资源（渲染线程上下文内调用）。</summary>
    /// <param name="request">无资产语义的网格创建请求</param>
    /// <returns>网格 GPU 句柄</returns>
    public RenderMeshHandle CreateMesh(RenderMeshCreateRequest request)
        => new(Register(new OpenGLMesh(RequireGl(), request)));

    /// <summary>按释放请求释放 GPU 资源（种类 + 句柄；未登记 no-op）。</summary>
    /// <param name="request">无资产语义的释放请求</param>
    public void Release(RenderResourceReleaseRequest request)
    {
        if (request.Handle != 0 && _resources.Remove(request.Handle, out var resource))
            resource.Dispose();
    }

    /// <summary>提交一个渲染包：按 Render Handle 解析 GL 资源并执行单次绘制。</summary>
    /// <param name="packet">不可变渲染提交数据（仅句柄/参数/矩阵）</param>
    public void Execute(RenderPacket packet)
    {
        var gl = _gl;
        if (gl == null)
            return;
        if (!_resources.TryGetValue(packet.Shader.Value, out var shaderRes) || shaderRes is not OpenGLShader glShader)
        {
            Log.Warn($"[Render] Skip packet: shader handle {packet.Shader.Value} 未创建");
            return;
        }
        if (!_resources.TryGetValue(packet.Mesh.Value, out var meshRes) || meshRes is not OpenGLMesh glMesh)
        {
            Log.Warn($"[Render] Skip packet: mesh handle {packet.Mesh.Value} 未创建");
            return;
        }
        try
        {
            gl.Enable(GLEnum.DepthTest);
            gl.Viewport(0, 0, (uint)Width, (uint)Height);
            gl.ClearColor(_clearR, _clearG, _clearB, _clearA);
            gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

            glShader.Use();
            UploadParameters(glShader, packet.Material);
            BindTexture(gl, packet.Texture, glShader);
            UploadMatrix(glShader, "uModel", packet.ModelMatrix);
            glMesh.Draw();
        }
        catch (Exception ex)
        {
            // 单包失败不中断后续绘制
            Log.Warn($"[Render] Execute packet failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public void Present() => _window?.SwapBuffers();

    /// <summary>释放全部 GPU 资源、窗口与 GL 实例（幂等；渲染线程 finally 调用）。</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        if (_window != null && _gl != null)
            _window.MakeCurrent(); // 删除 GPU 资源前确保当前线程拥有 GL 上下文（wgl 允许任意线程 MakeCurrent 同一 HGLRC）

        foreach (var resource in _resources.Values)
            resource.Dispose();
        _resources.Clear();

        if (_window != null && _gl != null)
            _window.ClearContext();
        _window?.Dispose();
        _gl?.Dispose();
    }

    private GL RequireGl() =>
        _gl ?? throw new InvalidOperationException("OpenGLRenderBackend 未初始化（Initialize 在渲染线程调用）");

    private ulong Register(IDisposable resource)
    {
        var handle = _nextHandle++;
        _resources[handle] = resource;
        return handle;
    }

    /// <summary>上传材质 float 参数（渲染值集合，无资产语义）。</summary>
    private void UploadParameters(OpenGLShader shader, RenderMaterialParameters parameters)
    {
        var program = shader.GetProgram();
        foreach (var (name, value) in parameters.Enumerate())
        {
            int loc = _gl!.GetUniformLocation(program, name);
            if (loc != -1)
                _gl.Uniform1(loc, value.FloatValue);
        }
    }

    /// <summary>绑定主纹理采样器（uMainTex；无纹理句柄或未创建时跳过）。</summary>
    private void BindTexture(GL gl, RenderTextureHandle texture, OpenGLShader shader)
    {
        if (texture == default)
            return;
        if (!_resources.TryGetValue(texture.Value, out var res) || res is not OpenGLTexture glTex)
            return;
        int samplerLoc = gl.GetUniformLocation(shader.GetProgram(), "uMainTex");
        if (samplerLoc == -1)
            return;
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTargets.Texture2dTarget, glTex.Handle);
        gl.Uniform1(samplerLoc, 0);
    }

    private void UploadMatrix(OpenGLShader glShader, string name, Math.Matrix4x4 m)
    {
        int loc = _gl!.GetUniformLocation(glShader.GetProgram(), name);
        if (loc == -1)
            return;
        unsafe
        {
            // Matrix4x4 为 Sequential 布局（16 个连续 float），参数按值传入已固定，零分配直传
            _gl.UniformMatrix4(loc, 1, true, &m.M11);
        }
    }
}
