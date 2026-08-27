using System;
using System.Collections.Generic;
using SilkEngine.Core;
using SilkEngine.Assets;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace SilkEngine.Render.OpenGL;

/// <summary>
/// 绘制命令分派结果：单实例绘制 / GPU 实例化绘制 / 未知命令类型
/// </summary>
internal enum DrawCommandKind
{
    /// <summary>单实例路径（SingleDrawCommand）</summary>
    DrawOnce,

    /// <summary>GPU 实例化路径（InstancedDrawCommand）</summary>
    DrawInstanced,

    /// <summary>未知命令类型（告警跳过）</summary>
    Unknown,
}

/// <summary>
/// OpenGL渲染后端
/// <br/>仅负责窗口创建、上下文切换与一帧的绘制执行，线程调度由 ThreadManager 分配的专用执行者管理。
/// <br/>GL 上下文归属渲染线程（MakeContextCurrent 绑定，ClearContext 解绑）；
/// Dispose 前若上下文已 ClearContext，则先 MakeCurrent 再删除 GPU 资源（wgl 允许任意线程 MakeCurrent 同一 HGLRC）。
/// </summary>
public class OpenGLRenderBackend : RenderBackendBase
{
    private IWindow? _window;
    private GL? _gl;

    private readonly GpuResourceRegistry _registry = new();
    private readonly OpenGLTextureRegistry _textureRegistry = new(t => new OpenGLTexture(t));
    private readonly Func<AssetHandle<TextureAsset>, TextureAsset?>? _materialTextureResolver;

    private float _clearR = 0.1f,
        _clearG = 0.1f,
        _clearB = 0.1f,
        _clearA = 1.0f;

    /// <summary>
    /// 创建 OpenGL 渲染后端
    /// </summary>
    /// <param name="materialTextureResolver">材质主纹理句柄 → TextureAsset 解析委托（缺省 null → 白色占位回落；TextureAsset→GL 通道属后续资产管线）</param>
    public OpenGLRenderBackend(Func<AssetHandle<TextureAsset>, TextureAsset?>? materialTextureResolver = null) =>
        _materialTextureResolver = materialTextureResolver;

    /// <summary>OpenGL API 实例</summary>
    internal GL GL => _gl!;

    /// <summary>窗口体实例</summary>
    internal IWindow Window => _window!;

    /// <summary>原生窗口句柄（供 Editor 嵌入使用）</summary>
    internal IntPtr WindowHandle => _window?.Native?.Win32?.Hwnd ?? IntPtr.Zero;

    /// <inheritdoc />
    public override Silk.NET.Windowing.IWindow? NativeWindow => _window;

    /// <inheritdoc />
    public override bool ShouldClose => _window?.IsClosing ?? false;

    /// <inheritdoc />
    public override int Width => _window?.Size.X ?? 800;

    /// <inheritdoc />
    public override int Height => _window?.Size.Y ?? 600;

    /// <summary>
    /// 单点分派：按命令类型分类绘制路径（纯逻辑，无 GL 依赖，可无头测试）
    /// </summary>
    /// <param name="cmd">绘制命令</param>
    /// <returns>绘制路径分类；未知命令类型返回 <see cref="DrawCommandKind.Unknown"/></returns>
    internal static DrawCommandKind Classify(DrawCommand cmd) => cmd switch
    {
        SingleDrawCommand => DrawCommandKind.DrawOnce,
        InstancedDrawCommand => DrawCommandKind.DrawInstanced,
        _ => DrawCommandKind.Unknown,
    };

    /// <summary>设置清除颜色</summary>
    public void SetClearColor(float r, float g, float b, float a)
    {
        _clearR = r;
        _clearG = g;
        _clearB = b;
        _clearA = a;
    }

    /// <inheritdoc />
    public override void InitWindow()
    {
        _window = Silk.NET.Windowing.Window.Create(DefaultWindowOption.DefaultOpenGLOption);
        _window.Initialize();
        _gl = GL.GetApi(_window);

        _window.IsContextControlDisabled = true;
        _window.ClearContext();
    }

    /// <inheritdoc />
    public override void MakeContextCurrent() => _window!.MakeCurrent();

    /// <inheritdoc />
    public override void ClearContext() => _window!.ClearContext();

    /// <inheritdoc />
    public override void PumpWindowEvents() => _window?.DoEvents();

    /// <inheritdoc />
    public override void ExecutePass(IReadOnlyList<DrawCommand> commands) => ExecuteCommands(commands);

    /// <inheritdoc />
    public override void Present() => _window!.SwapBuffers();

    /// <inheritdoc />
    public override void ReleaseTexture(TextureAsset texture)
    {
        if (_textureRegistry.TryRemove(texture, out var glTex))
        {
            glTex.Dispose();
            Log.Info($"[Render] Released GL texture: {texture.Name}");
        }
    }

    /// <summary>通用 GPU 资源释放入口（过渡期遗留：旧 IAsset 实例驱逐；Payload 纹理经无资产语义释放请求流程）。</summary>
    public override void ReleaseGpuResource(IAsset asset) => _registry.Evict(asset);

    /// <inheritdoc />
    public override IRenderBuffer CreateBuffer(int sizeBytes)
    {
        if (_gl == null)
            throw new InvalidOperationException("后端未初始化");
        uint id = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, id);
        unsafe
        {
            _gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)sizeBytes,
                null,
                BufferUsageARB.DynamicDraw
            );
        }
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        return new OpenGLBuffer(sizeBytes, () =>
        {
            _gl.DeleteBuffer(id);
        });
    }

    /// <summary>纹理缓存（渲染线程与测试使用）</summary>
    internal OpenGLTextureRegistry TextureRegistry => _textureRegistry;

    private void ExecuteCommands(IReadOnlyList<DrawCommand> commands)
    {
        if (_gl == null)
            return;

        _gl.Enable(GLEnum.DepthTest);
        _gl.Viewport(0, 0, (uint)Width, (uint)Height);
        _gl.ClearColor(_clearR, _clearG, _clearB, _clearA);
        _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

        // uView/uProjection 为 Pass 级相机矩阵：每 Pass 每 ShaderProgram 仅上传一次（上传次数收敛）
        var cameraUploadedPrograms = new HashSet<uint>();

        foreach (var cmd in commands)
        {
            if (!cmd.Enabled)
                continue;
            if (cmd.Shader == null || cmd.Mesh == null)
                continue;
            try
            {
                OpenGLShader glShader = _registry.GetOrCreate(cmd.Shader, s => new OpenGLShader(_gl, s));
                OpenGLMesh glMesh = _registry.GetOrCreate(cmd.Mesh, m => new OpenGLMesh(_gl, m));

                OpenGLMaterial? glMaterial = null;
                if (cmd.Material != null)
                {
                    glMaterial = _registry.GetOrCreate(
                        cmd.Material, mat => new OpenGLMaterial(_gl, mat, glShader, _textureRegistry, _materialTextureResolver)
                    );
                }

                if (glMaterial != null)
                {
                    glMaterial.Apply();
                }
                else
                {
                    glShader.Use();
                }

                switch (Classify(cmd))
                {
                    case DrawCommandKind.DrawOnce:
                        DrawSingle(glShader, glMesh, (SingleDrawCommand)cmd, cameraUploadedPrograms);
                        break;
                    case DrawCommandKind.DrawInstanced:
                        DrawInstanced(glMesh, (InstancedDrawCommand)cmd);
                        break;
                    default:
                        Log.Warn($"[Render] Unknown draw command type skipped: {cmd.GetType().Name}");
                        break;
                }
            }
            catch (Exception ex)
            {
                // 单命令失败不中断整批绘制
                Log.Warn($"[Render] Draw command failed ({cmd.GetType().Name}): {ex.Message}");
            }
        }
    }

    /// <summary>单实例路径：Pass 级相机矩阵（每程序一次）+ 按命令的 uModel/uMVP + 一次绘制</summary>
    /// <param name="glShader">已绑定的着色器</param>
    /// <param name="glMesh">网格</param>
    /// <param name="cmd">单实例命令</param>
    /// <param name="cameraUploadedPrograms">本 Pass 已上传相机矩阵的程序集合</param>
    private void DrawSingle(
        OpenGLShader glShader,
        OpenGLMesh glMesh,
        SingleDrawCommand cmd,
        HashSet<uint> cameraUploadedPrograms
    )
    {
        uint program = glShader.GetProgram();
        if (cameraUploadedPrograms.Add(program))
        {
            if (cmd.ViewMatrix.HasValue)
                UploadMatrix(glShader, "uView", cmd.ViewMatrix.Value);
            if (cmd.ProjectionMatrix.HasValue)
                UploadMatrix(glShader, "uProjection", cmd.ProjectionMatrix.Value);
        }
        if (cmd.ModelMatrix.HasValue)
            UploadMatrix(glShader, "uModel", cmd.ModelMatrix.Value);
        if (cmd.ViewMatrix.HasValue && cmd.ProjectionMatrix.HasValue && cmd.ModelMatrix.HasValue)
        {
            UploadMatrix(
                glShader,
                "uMVP",
                Math.Matrix4x4.ComposeMVP(
                    cmd.ProjectionMatrix.Value,
                    cmd.ViewMatrix.Value,
                    cmd.ModelMatrix.Value
                )
            );
        }
        glMesh.Draw();
    }

    /// <summary>实例化路径：一次 GPU 调用绘制 InstanceCount 个实例</summary>
    /// <param name="glMesh">网格</param>
    /// <param name="cmd">实例化命令</param>
    private void DrawInstanced(OpenGLMesh glMesh, InstancedDrawCommand cmd)
    {
        // 预留：InstanceData(PerInstanceData[]) → 实例缓冲上传未实现（需 OpenGLMesh 实例缓冲 + glVertexAttribDivisor 扩展）；
        // 当前先实现 InstanceCount 维度——OpenGLMesh.DrawInstanced 一次调用绘制全部实例
        glMesh.DrawInstanced(cmd.InstanceCount);
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

    /// <inheritdoc />
    public override void Dispose()
    {
        // 渲染线程已退出并 ClearContext：删除 GPU 资源前确保当前线程拥有 GL 上下文（wgl 允许任意线程 MakeCurrent 同一 HGLRC）
        if (_window != null && _gl != null)
            _window.MakeCurrent();

        _registry.ReleaseAll();
        foreach (var t in _textureRegistry.Values)
            t.Dispose();

        if (_window != null && _gl != null)
            _window.ClearContext();
        _window?.Dispose();
        _gl?.Dispose();
        base.Dispose();
    }
}
