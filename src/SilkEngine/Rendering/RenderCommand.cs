using System.Numerics;

namespace SilkEngine.Rendering;

/// <summary>Identifies the semantic operation stored in a <see cref="RenderCommand"/>.</summary>
public enum RenderCommandKind : byte
{
    /// <summary>Clears a render target to a color.</summary>
    ClearColor = 0,
    SetViewport = 1,
}

/// <summary>A backend-agnostic, CPU-side render command.</summary>
public readonly struct RenderCommand
{
    private RenderCommand(RenderCommandKind kind, RenderTargetHandle target, Vector4 color)
    {
        Kind = kind;
        Target = target;
        Color = color;
    }

    /// <summary>Gets the semantic operation kind.</summary>
    public RenderCommandKind Kind { get; }

    /// <summary>Gets the render target affected by this command.</summary>
    public RenderTargetHandle Target { get; }

    /// <summary>Gets the clear color; meaningful only for <see cref="RenderCommandKind.ClearColor"/>.</summary>
    public Vector4 Color { get; }

    /// <summary>Creates a clear-color command.</summary>
    /// <param name="target">Render target to clear.</param>
    /// <param name="color">Color written to the target.</param>
    /// <returns>A command that records the clear request.</returns>
    public static RenderCommand ClearColor(RenderTargetHandle target, Vector4 color) =>
        new(RenderCommandKind.ClearColor, target, color);
}
