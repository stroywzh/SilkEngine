using Silk.NET;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.BuiltIn.Backend;

/// <summary>
/// Placeholder for the future Silk.NET OpenGL backend. It implements the protocol but explicitly
/// refuses initialization and execution until a real graphics device is integrated.
/// </summary>
public sealed class SilkOpenGLBackend : RenderBackendBase
{
    private WindowOptions options = WindowOptions.Default;
    private GL gL;
    private IWindow window;

    /// <inheritdoc />
    public override string Name => "Silk.NET OpenGL (placeholder)";

    /// <inheritdoc />
    protected override void OnInitialize(IGraphicsDevice device)
    {
        options.Size = new Silk.NET.Maths.Vector2D<int>(800, 600);
        window = Window.Create(options);
        window.Load += () =>
        {
            gL = window.CreateOpenGL();
        };
        Log.Info("Backend Init finished.");
        window.Initialize();
    }

    protected override void DoRender()
    {
        window.DoRender();
        Log.Info($"Backend Render in {window.Time}");
    }

    /// <inheritdoc />
    protected override void ExecuteCommands(ReadOnlySpan<RenderCommand> commands)
    {
        Thread.Sleep(2000);
        Log.Info($"ExcutedCommand:{commands.Length} in 1000ms");
    }

    /// <inheritdoc />
    protected override void OnDisposeCore()
    {
        gL.Dispose();
        window.Dispose();
    }
}
