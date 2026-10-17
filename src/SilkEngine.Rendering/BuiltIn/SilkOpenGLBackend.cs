using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.BuiltIn.Backend;

/// <summary>
/// Placeholder for the future Silk.NET OpenGL backend. It implements the protocol but explicitly
/// refuses initialization and execution until a real graphics device is integrated.
/// </summary>
public sealed class SilkOpenGLBackend : RenderBackendBase
{
    /// <inheritdoc />
    public override string Name => "Silk.NET OpenGL (placeholder)";

    /// <inheritdoc />
    protected override void OnInitialize(IGraphicsDevice device) =>
        throw new NotSupportedException(
            "OpenGL backend initialization is not implemented in this milestone."
        );

    /// <inheritdoc />
    protected override void ExecuteCommands(ReadOnlySpan<RenderCommand> commands) =>
        throw new NotSupportedException(
            "OpenGL command execution is not implemented in this milestone."
        );

    /// <inheritdoc />
    protected override void OnDisposeCore() { }
}

