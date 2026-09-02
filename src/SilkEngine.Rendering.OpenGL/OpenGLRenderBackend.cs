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
public sealed class OpenGLRenderBackend : IRenderBackend, IRenderDevice, IRenderFrameExecutor, IWindowSurface
{
    private const string ViewUniform = "uView";
    private const string ProjectionUniform = "uProjection";
    private const string ModelUniform = "uModel";
    private const string TextureUniform = "uMainTex";

    private readonly Dictionary<ulong, IDisposable> _resources = new();
    private readonly IShaderCompiler? _shaderCompiler;
    private IWindow? _window;
    private GL? _gl;
    private IOpenGlFrameCalls? _frameCalls;
    private ulong _nextHandle = 1;

    /// <summary>创建 OpenGL 渲染后端。</summary>
    /// <param name="shaderCompiler">着色器编译器（HLSL→SPIR-V）；null 时按 PATH/DxcPath 探测默认 DXC。</param>
    public OpenGLRenderBackend(IShaderCompiler? shaderCompiler = null) => _shaderCompiler = shaderCompiler;

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
    /// 测试注入帧调用接缝后跳过真实窗口（仅验证帧执行路径）。
    /// </summary>
    public void Initialize()
    {
        if (_frameCalls is not null)
            return; // 测试注入：无窗口路径
        _window = Silk.NET.Windowing.Window.Create(DefaultWindowOption.DefaultOpenGLOption);
        _window.Initialize();
        _gl = GL.GetApi(_window);
        _window.IsContextControlDisabled = true;
        _window.MakeCurrent();
        _frameCalls = new OpenGlFrameCalls(_gl);
    }

    /// <summary>创建纹理资源（渲染线程上下文内调用）。</summary>
    /// <param name="request">无资产语义的纹理创建请求</param>
    /// <returns>纹理 GPU 句柄</returns>
    public RenderTextureHandle CreateTexture(RenderTextureCreateRequest request)
        => new(Register(new OpenGLTexture(RequireGl(), request)));

    /// <summary>创建着色器资源（渲染线程上下文内调用）：HLSL → SPIR-V（DXC）→ glShaderBinary/glSpecializeShader 链接。</summary>
    /// <param name="request">无资产语义的编译创建请求（单 HLSL 源 + 入口 + profile + 后端）</param>
    /// <returns>着色器 GPU 句柄</returns>
    /// <exception cref="ShaderCompilationException">编译或 GL 加载失败（消息段含 source path/入口/profile/backend）</exception>
    public RenderShaderHandle CreateShader(RenderShaderCreateRequest request)
    {
        var gl = RequireGl();
        var compileRequest = ToCompileRequest(request);
        var compiled = CompileSpirv(compileRequest);
        return new RenderShaderHandle(Register(OpenGLShaderCompiler.Create(gl, compileRequest, compiled)));
    }

    /// <summary>无资产语义编译请求形态转换（Abstraction 中性请求 → Rendering.Backend 编译器契约请求；字段一一对应）。</summary>
    private static ShaderCompileRequest ToCompileRequest(RenderShaderCreateRequest request) => new(
        request.SourcePath,
        request.HlslSource,
        request.VertexEntryPoint,
        request.FragmentEntryPoint,
        request.Profile,
        request.Defines,
        request.Backend);

    /// <summary>渲染线程同步执行 DXC 编译；失败/不支持转为携带请求上下文与阶段的 <see cref="ShaderCompilationException"/>。</summary>
    private IReadOnlyList<byte> CompileSpirv(ShaderCompileRequest request)
    {
        // 原型阶段渲染为同步阻塞（P0 已知）；渲染线程无同步上下文，GetResult 无死锁风险
        var compiler = _shaderCompiler ?? new DxcHlslCompiler();
        var result = compiler.CompileAsync(request, CancellationToken.None).GetAwaiter().GetResult();
        if (result.State != ShaderCompileState.Succeeded || result.SpirV is null)
            throw new ShaderCompilationException(
                "hlsl-compile",
                result.Error?.Message ?? $"[{request.Backend}] DXC 编译失败 '{request.SourcePath}'：无错误详情");
        return result.SpirV;
    }

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

    /// <summary>提交单个渲染包（兼容旧路径）：包内使用恒等相机块与帧首清屏语义。</summary>
    /// <param name="packet">不可变渲染提交数据（仅句柄/参数/矩阵）</param>
    public void Execute(RenderPacket packet)
        => ExecuteFrame(new RenderSubmission(
            FrameCameraBlock.Identity, [packet], RenderResourceCreateBatch.Empty));

    /// <summary>
    /// 执行整帧渲染提交（RenderThreadHost 帧路径）：帧首清屏 → 上传相机矩阵（uView/uProjection）→
    /// 逐包上传 uModel 并绘制。矩阵 upload 全部 transpose=true（行主序 → GL 列主序）。
    /// </summary>
    /// <param name="submission">本帧不可变提交（相机块 + 渲染包）</param>
    public void ExecuteFrame(RenderSubmission submission)
    {
        var calls = _frameCalls;
        if (calls is null)
            return;
        calls.SetupFrame(_clearR, _clearG, _clearB, _clearA, Width, Height);
        foreach (var packet in submission.Packets)
            ExecutePacket(calls, submission.Camera, packet);
    }

    /// <summary>执行单个渲染包：按 Render Handle 解析 GL 资源并完成一次绘制。</summary>
    private void ExecutePacket(IOpenGlFrameCalls calls, FrameCameraBlock camera, RenderPacket packet)
    {
        if (!_resources.TryGetValue(packet.Shader.Value, out var shaderRes) || shaderRes is not IOpenGlShaderResource glShader)
        {
            Log.Warning($"[Render] Skip packet: shader handle {packet.Shader.Value} 未创建");
            return;
        }
        if (!_resources.TryGetValue(packet.Mesh.Value, out var meshRes) || meshRes is not IOpenGlMeshResource glMesh)
        {
            Log.Warning($"[Render] Skip packet: mesh handle {packet.Mesh.Value} 未创建");
            return;
        }
        try
        {
            calls.UseProgram(glShader.Program);
            UploadParameters(calls, glShader.Program, packet.Material);
            BindTexture(calls, glShader.Program, packet.Texture);
            UploadMatrix(calls, glShader.Program, ViewUniform, camera.View);
            UploadMatrix(calls, glShader.Program, ProjectionUniform, camera.Projection);
            UploadMatrix(calls, glShader.Program, ModelUniform, packet.ModelMatrix);
            glMesh.Draw();
        }
        catch (Exception ex)
        {
            // 单包失败不中断后续绘制
            Log.Warning($"[Render] Execute packet failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public void Present() => _window?.SwapBuffers();

    /// <summary>释放全部 GPU 资源、窗口与 GL 实例（幂等；渲染线程 finally 调用）。</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _frameCalls?.Dispose();
        _frameCalls = null;
        if (_window != null && _gl != null)
        {
            _window.MakeCurrent(); // 删除 GPU 资源前确保当前线程拥有 GL 上下文（wgl 允许任意线程 MakeCurrent 同一 HGLRC）

            foreach (var resource in _resources.Values)
                resource.Dispose();
            _resources.Clear();

            _window.ClearContext();
            _window.Dispose();
            _gl.Dispose();
            _window = null;
            _gl = null;
        }
    }

    private GL RequireGl() =>
        _gl ?? throw new InvalidOperationException("OpenGLRenderBackend 未初始化（Initialize 在渲染线程调用）");

    private ulong Register(IDisposable resource)
    {
        var handle = _nextHandle++;
        _resources[handle] = resource;
        return handle;
    }

    /// <summary>注入帧调用接缝（测试专用）：跳过真实窗口与 GL 上下文，直接驱动帧执行路径。</summary>
    internal void SetFrameCallsForTests(IOpenGlFrameCalls frameCalls) => _frameCalls = frameCalls;

    /// <summary>直接登记模拟资源（测试专用）：以句柄映射到资源接缝实例。</summary>
    internal void RegisterResourceForTests(ulong handle, IDisposable resource) => _resources[handle] = resource;

    /// <summary>上传材质参数（渲染值集合，无资产语义；float → Uniform1，Vector3 → Uniform3）。</summary>
    private void UploadParameters(IOpenGlFrameCalls calls, uint program, RenderMaterialParameters parameters)
    {
        foreach (var (name, value) in parameters.Enumerate())
        {
            int loc = calls.GetUniformLocation(program, name);
            if (loc == -1)
                continue;
            if (value.TryGetVector3(out var v3))
                calls.Uniform3(loc, v3);
            else
                calls.Uniform1(loc, value.FloatValue);
        }
    }

    /// <summary>绑定主纹理采样器（uMainTex；无纹理句柄或未创建时跳过）。</summary>
    private void BindTexture(IOpenGlFrameCalls calls, uint program, RenderTextureHandle texture)
    {
        if (texture == default)
            return;
        if (!_resources.TryGetValue(texture.Value, out var res) || res is not IOpenGlTextureResource glTex)
            return;
        int samplerLoc = calls.GetUniformLocation(program, TextureUniform);
        if (samplerLoc == -1)
            return;
        calls.Uniform1(samplerLoc, 0);
        glTex.Bind(0);
    }

    private void UploadMatrix(IOpenGlFrameCalls calls, uint program, string name, Math.Matrix4x4 m)
    {
        int loc = calls.GetUniformLocation(program, name);
        if (loc == -1)
            return;
        calls.UniformMatrix4(loc, true, m);
    }
}
