using System.Runtime.ExceptionServices;
using Silk.NET;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.BuiltIn.Backend;

/// <summary>OpenGL command consumer and manual Silk window host, owned by one render thread.</summary>
/// <remarks>Window/render APIs require that same thread. ExecuteRender never pumps the window.</remarks>
public sealed class SilkOpenGLBackend : RenderBackendBase, IRenderWindowHost
{
    private IWindow? _window;
    private GL? _gl;
    private Action? _execute;
    private ExceptionDispatchInfo? _callbackError;
    private bool _callbackRan;
    private int _ownerThread;

    public override string Name => "Silk.NET OpenGL";
    public bool IsClosing => _window?.IsClosing ?? true;

    protected override void OnInitialize(IGraphicsDevice device)
    {
        _ownerThread = Environment.CurrentManagedThreadId;

        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(800, 600);
        options.FramesPerSecond = 0;
        options.ShouldSwapAutomatically = false;
        options.VSync = true;

        _window = Window.Create(options);
        _window.Render += OnRender;
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

    /// <summary>Attempts a manual render event followed by exactly one explicit buffer swap.</summary>
    /// <returns>False if no callback ran or the window is closing; true after callback and swap return.</returns>
    /// <exception cref="ArgumentNullException">The callback is null.</exception>
    /// <exception cref="InvalidOperationException">Called on another thread or reentrantly.</exception>
    /// <remarks>Callback and swap errors propagate. No callback survives this call; no GPU idle wait occurs.</remarks>
    public bool TryRenderFrame(Action executeCommands)
    {
        ArgumentNullException.ThrowIfNull(executeCommands);
        EnsureOwner();

        var window = _window ?? throw new InvalidOperationException("Window is unavailable.");
        if (window.IsClosing)
            return false;

        if (_execute is not null)
            throw new InvalidOperationException("Reentrant window render.");

        _callbackRan = false;
        _callbackError = null;
        _execute = executeCommands;

        try
        {
            window.GLContext!.MakeCurrent();
            window.DoRender();
            _callbackError?.Throw();

            if (!_callbackRan)
                return false;

            window.GLContext!.SwapBuffers();
            return true;
        }
        finally
        {
            _execute = null;
        }
    }

    private void OnRender(double deltaSeconds)
    {
        if (_execute is null)
            return;

        if (_callbackRan)
        {
            _callbackError ??= ExceptionDispatchInfo.Capture(
                new InvalidOperationException("Silk render callback ran twice.")
            );
            return;
        }

        _callbackRan = true;

        try
        {
            _execute();
        }
        catch (Exception error)
        {
            _callbackError = ExceptionDispatchInfo.Capture(error);
        }
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

    protected override void OnWaitForIdle()
    {
        EnsureOwner();
        _gl?.Finish();
    }

    protected override void OnDisposeCore()
    {
        if (_ownerThread != 0)
        {
            EnsureOwner();
        }

        _execute = null;
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
            window.Render -= OnRender;
            // Even a faulted command stream can have prior GL work. Best-effort device cleanup
            // must not depend on the public WaitForIdle method accepting the faulted state.
            Cleanup(() => window.GLContext?.MakeCurrent());
            Cleanup(window.Dispose);
        }

        // if (window is not null)
        // {
        //     Cleanup(window.Dispose);
        // }

        _gl = null;
        _window = null;
        if (errors.Count > 0)
            throw new AggregateException("GL cleanup failed.", errors);
    }

    private void EnsureOwner()
    {
        if (_ownerThread != Environment.CurrentManagedThreadId)
            throw new InvalidOperationException(
                "GL/window access must remain on the owning render thread."
            );
    }
}
