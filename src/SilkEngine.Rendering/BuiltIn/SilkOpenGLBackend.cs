using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.BuiltIn.Backend;

/// <summary>Silk window owner and CPU command consumer, driven by one render thread.</summary>
/// <remarks>Window/GL calls must stay on the owning render thread. The RHI built here owns device
/// work, including presenting the frame; the backend only supplies it and pumps window events.</remarks>
public sealed class SilkOpenGLBackend : RenderBackendBase, IRenderWindowHost
{
    private IWindow? _window;
    private GL? _gl;

    public override string Name => "Silk.NET OpenGL";
    public bool IsClosing => _window?.IsClosing ?? true;

    public (int Width, int Height) FramebufferSize
    {
        get
        {
            if (_window is null)
                return (0, 0);

            var size = _window.FramebufferSize;
            return (size.X, size.Y);
        }
    }

    protected override void OnInitialize()
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

    protected override IRenderHardware CreateHardware() =>
        new OpenGLHardware(_gl!, _window!.GLContext!);

    /// <summary>Polls native events on the window-owning thread.</summary>
    /// <exception cref="InvalidOperationException">Called on another thread or before initialization.</exception>
    public void PumpEvents()
    {
        EnsureOwner();
        (_window ?? throw new InvalidOperationException("Window is unavailable.")).DoEvents();
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
