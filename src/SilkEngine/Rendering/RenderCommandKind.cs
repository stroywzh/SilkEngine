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

    /// <summary>Binds a vertex buffer with a stride.</summary>
    BindVertexBuffer = 3,

    /// <summary>Binds an index buffer with an element format.</summary>
    BindIndexBuffer = 4,

    /// <summary>Binds a texture to a texture slot.</summary>
    BindTexture = 5,
}
