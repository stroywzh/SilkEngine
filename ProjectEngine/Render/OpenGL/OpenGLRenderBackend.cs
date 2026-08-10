using System;
using System.Collections.Generic;
using System.Threading;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace ProjectEngine.Render.OpenGL;

/// <summary>
/// OpenGL渲染后端
/// <br/>管理专用渲染线程（持有 GL 上下文），在渲染线程上缓存 GPU 资源。
/// <br/>当 parentHandle 非零时，将窗口嵌入为子窗口。
/// </summary>
public class OpenGLRenderBackend : RenderBackendBase
{
    private IWindow? _window;
    private GL? _gl;

    private readonly Dictionary<Shader, OpenGLShader> _shaderCache = new();
    private readonly Dictionary<Mesh, OpenGLMesh> _meshCache = new();
    private readonly Dictionary<Material, OpenGLMaterial> _materialCache = new();

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
    public override void Initialize(IntPtr parentHandle)
    {
        _window = Silk.NET.Windowing.Window.Create(DefaultWindowOption.DefaultOpenGLOption);
        _window.Initialize();
        _gl = GL.GetApi(_window);

        _window.IsContextControlDisabled = true;
        _window.ClearContext();

        _renderThread = new Thread(RenderLoop) { Name = "RenderThread", IsBackground = true };
        _rendering = true;
        _renderThread.Start();
    }

    /// <summary>渲染线程主循环</summary>
    private void RenderLoop()
    {
        _window!.MakeCurrent();
        while (_rendering)
        {
            _commandsReady.Wait();
            _commandsReady.Reset();
            if (!_rendering)
                break;
            ExecuteFrame();
            _frameDone.Set();
        }
        _window!.ClearContext();
    }

    /// <inheritdoc />
    public override void ProcessWindowEvents() => _window?.DoEvents();

    /// <inheritdoc />
    public override void ExecuteFrame()
    {
        if (_gl == null)
            return;
        _gl.ClearColor(_clearR, _clearG, _clearB, _clearA);
        _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

        var cmds = _pendingCommands;
        if (cmds == null)
        {
            _window!.SwapBuffers();
            return;
        }

        foreach (var cmd in cmds)
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
                    glMaterial = new OpenGLMaterial(_gl, cmd.Material, glShader);
                    _materialCache[cmd.Material] = glMaterial;
                }
            }

            if (glMaterial != null)
                glMaterial.Apply();
            else
                glShader.Use();
            glMesh.Draw();
        }

        _pendingCommands = null;
        _window!.SwapBuffers();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        foreach (var s in _shaderCache.Values)
            s.Dispose();
        foreach (var m in _meshCache.Values)
            m.Dispose();
        foreach (var m in _materialCache.Values)
            m.Dispose();
        _shaderCache.Clear();
        _meshCache.Clear();
        _materialCache.Clear();

        _window?.Dispose();
        _gl?.Dispose();
        base.Dispose();
    }
}
