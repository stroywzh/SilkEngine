using System;
using System.Collections.Generic;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace SilkEngine.Render.OpenGL;

/// <summary>
/// OpenGL渲染后端
/// <br/>仅负责窗口创建、上下文切换与一帧的绘制执行，线程调度由外部 RenderThreadLoop 管理。
/// </summary>
public class OpenGLRenderBackend : RenderBackendBase
{
    private IWindow? _window;
    private GL? _gl;

    private readonly Dictionary<Shader, OpenGLShader> _shaderCache = new();
    private readonly Dictionary<Mesh, OpenGLMesh> _meshCache = new();
    private readonly Dictionary<Material, OpenGLMaterial> _materialCache = new();
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
            if (cmd.Shader == null || cmd.Mesh == null)
                continue;

            if (!_shaderCache.TryGetValue(cmd.Shader, out var glShader))
            {
                glShader = new OpenGLShader(_gl, cmd.Shader);
                _shaderCache[cmd.Shader] = glShader;
            }

            if (!_meshCache.TryGetValue(cmd.Mesh, out var glMesh))
            {
                glMesh = new OpenGLMesh(_gl, cmd.Mesh);
                _meshCache[cmd.Mesh] = glMesh;
            }

            OpenGLMaterial? glMaterial = null;
            if (cmd.Material != null)
            {
                if (!_materialCache.TryGetValue(cmd.Material, out glMaterial))
                {
                    glMaterial = new OpenGLMaterial(_gl, cmd.Material, glShader, _textureRegistry);
                    _materialCache[cmd.Material] = glMaterial;
                }
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
            glMesh.Draw();
        }
    }

    private void UploadMatrix(OpenGLShader glShader, string name, Math.Matrix4x4 m)
    {
        int loc = _gl!.GetUniformLocation(glShader.GetProgram(), name);
        if (loc == -1)
            return;
        unsafe
        {
            float[] mat =
            [
                m.M11, m.M12, m.M13, m.M14,
                m.M21, m.M22, m.M23, m.M24,
                m.M31, m.M32, m.M33, m.M34,
                m.M41, m.M42, m.M43, m.M44,
            ];
            fixed (float* p = mat)
            {
                _gl.UniformMatrix4(loc, 1, true, p);
            }
        }
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        foreach (var s in _shaderCache.Values)
        {
            s.Dispose();
        }
        foreach (var m in _meshCache.Values)
        {
            m.Dispose();
        }
        foreach (var m in _materialCache.Values)
        {
            m.Dispose();
        }
        _shaderCache.Clear();
        _meshCache.Clear();
        _materialCache.Clear();

        foreach (var t in _textureRegistry.Values)
        {
            t.Dispose();
        }

        _window?.Dispose();
        _gl?.Dispose();
        base.Dispose();
    }
}
