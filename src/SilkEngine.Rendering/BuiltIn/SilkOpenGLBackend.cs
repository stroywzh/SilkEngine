using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.BuiltIn.Backend;

/// <summary>OpenGL command consumer and Silk window owner, driven by one render thread.</summary>
/// <remarks>Window/GL calls must stay on the owning render thread. The backend presents its own
/// frame after consuming commands; there is no window render event and no callback entry.</remarks>
public sealed class SilkOpenGLBackend : RenderBackendBase, IRenderWindowHost
{
    private IWindow? _window;
    private GL? _gl;

    public override string Name => "Silk.NET OpenGL";
    public bool IsClosing => _window?.IsClosing ?? true;

    protected override void OnInitialize(IGraphicsDevice device)
    {
        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(800, 600);
        options.FramesPerSecond = 0;
        options.ShouldSwapAutomatically = false;
        options.VSync = true;

        _window = Window.Create(options);
        _window.Initialize();
        _window.GLContext!.MakeCurrent();
        _gl = _window.CreateOpenGL();
    }

    /// <summary>Polls native events on the window-owning thread.</summary>
    /// <exception cref="InvalidOperationException">Called on another thread or before initialization.</exception>
    public void PumpEvents()
    {
        EnsureOwner();
        (_window ?? throw new InvalidOperationException("Window is unavailable.")).DoEvents();
    }

    /// <summary>Swaps the back buffer once; called by <see cref="RenderBackendBase.ExecuteRender"/>.</summary>
    protected override void OnPresent()
    {
        EnsureOwner();
        (
            _window ?? throw new InvalidOperationException("Window is unavailable.")
        ).GLContext!.SwapBuffers();
    }

    protected override void ExecuteCommands(ReadOnlySpan<RenderCommand> commands)
    {
        EnsureOwner();

        foreach (ref readonly var cmd in commands)
        {
            BuildRenderCommand(cmd);
        }
    }

    private void BuildRenderCommand(RenderCommand cmd)
    {
        var gl = _gl!;
        switch (cmd.Kind)
        {
            case RenderCommandKind.ClearColor:
                gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                gl.ClearColor(cmd.Color.X, cmd.Color.Y, cmd.Color.Z, cmd.Color.W);
                gl.Clear(ClearBufferMask.ColorBufferBit);
                break;
        }
    }

    protected override void OnDisposeCore()
    {
        var errors = new List<Exception>();

        void Cleanup(Action action)
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
        }

        var window = _window;
        var gl = _gl;

        if (gl is not null)
        {
            Cleanup(gl.Finish);
            Cleanup(gl.Dispose);
        }

        if (window is not null)
        {
            // A recording fault can leave prior GL work queued. Best-effort device cleanup must
            // not depend on any public synchronization method accepting that state.
            Cleanup(() => window.GLContext?.MakeCurrent());
            Cleanup(window.Dispose);
        }

        _gl = null;
        _window = null;
        if (errors.Count > 0)
            throw new AggregateException("GL cleanup failed.", errors);
    }
}
