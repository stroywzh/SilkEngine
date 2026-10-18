namespace SilkEngine.Rendering;

/// <summary>Identifies the semantic operation stored in a recorded command payload.</summary>
public enum RenderCommandKind : byte
{
    /// <summary>Clears a render target to a color.</summary>
    ClearColor = 0,

    /// <summary>Sets the viewport rectangle.</summary>
    SetViewport = 1,

    /// <summary>Draws a range of vertices from the currently bound geometry.</summary>
    Draw = 2,
}
