using System;
using System.Collections.Generic;
using SilkEngine.Core;
using SilkEngine.Core.Assets;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace SilkEngine.Render.OpenGL;

/// <summary>
/// OpenGL渲染后端
/// <br/>仅负责窗口创建、上下文切换与一帧的绘制执行，线程调度由 ThreadManager 分配的专用执行者管理。
/// </summary>
public class OpenGLRenderBackend : RenderBackendBase
{
    private IWindow? _window;
    private GL? _gl;

    private readonly GpuResourceRegistry _registry = new();
    private readonly OpenGLTextureRegistry _textureRegistry = new(t => new OpenGLTexture(t));

    private float _clearR = 0.1f,
        _clearG = 0.1f,
        _clearB = 0.1f,
        _clearA = 1.0f;

    /// <summary>OpenGL API 实例</summary>
    public GL GL => _gl!;

    /// <summary>窗口体实例</summary>
    public IWindow Window => _window!;

    /// <summary>原生窗口句柄（供 Editor 嵌入使用）</summary>
    public IntPtr WindowHandle => _window?.Native?.Win32?.Hwnd ?? IntPtr.Zero;

    /// <inheritdoc />
    public override Silk.NET.Windowing.IWindow? NativeWindow => _window;

    /// <inheritdoc />
    public override bool ShouldClose => _window?.IsClosing ?? false;

    /// <inheritdoc />
    public override int Width => _window?.Size.X ?? 800;

    /// <inheritdoc />
    public override int Height => _window?.Size.Y ?? 600;

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
    public override void ReleaseTexture(Texture2D texture)
    {
        if (_textureRegistry.TryRemove(texture, out var glTex))
        {
            glTex.Dispose();
            Log.Info($"[Render] Released GL texture: {texture.Name}");
        }
    }

    /// <summary>通用 GPU 资源释放入口（渲染线程帧首卸载队列回调）。</summary>
    public override void ReleaseGpuResource(IAsset asset)
    {
        if (asset is Texture2D tex)
            ReleaseTexture(tex);
        else
            _registry.Evict(asset);
    }

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
                        cmd.Material, mat => new OpenGLMaterial(_gl, mat, glShader, _textureRegistry)
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

                if (cmd is SingleDrawCommand sdc && sdc.ModelMatrix.HasValue)
                {
                    UploadMatrix(glShader, "uModel", sdc.ModelMatrix.Value);
                }
                if (cmd is SingleDrawCommand sdc2 && sdc2.ViewMatrix.HasValue)
                {
                    UploadMatrix(glShader, "uView", sdc2.ViewMatrix.Value);
                }
                if (cmd is SingleDrawCommand sdc3 && sdc3.ProjectionMatrix.HasValue)
                {
                    UploadMatrix(glShader, "uProjection", sdc3.ProjectionMatrix.Value);
                }
                if (
                    cmd is SingleDrawCommand sdc4
                    && sdc4.ProjectionMatrix.HasValue
                    && sdc4.ViewMatrix.HasValue
                    && sdc4.ModelMatrix.HasValue
                )
                {
                    UploadMatrix(
                        glShader,
                        "uMVP",
                        Math.Matrix4x4.ComposeMVP(
                            sdc4.ProjectionMatrix.Value,
                            sdc4.ViewMatrix.Value,
                            sdc4.ModelMatrix.Value
                        )
                    );
                }
                glMesh.Draw();
            }
            catch (Exception ex)
            {
                // 单命令失败不中断整批绘制
                Log.Warn($"[Render] Draw command failed ({cmd.GetType().Name}): {ex.Message}");
            }
        }
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
