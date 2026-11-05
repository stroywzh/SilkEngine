using System.Runtime.InteropServices;

namespace SilkEngine.Rendering;

/// <summary>
/// Per-draw constants read by the textured shader: NDC half extent, center offset, tint and alpha.
/// </summary>
/// <remarks>
/// The layout mirrors the shader's std140 uniform block exactly: two vec4 (32 B). Changing fields,
/// order or size requires updating the shader and the size assertion in <see cref="TexturedQuad"/>.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public readonly struct DrawConstants2D
{
    /// <summary>Size of the std140 block in bytes.</summary>
    public const int SizeBytes = 32;

    /// <summary>NDC half extent on x (uTransform.x).</summary>
    public readonly float ScaleX;

    /// <summary>NDC half extent on y (uTransform.y).</summary>
    public readonly float ScaleY;

    /// <summary>NDC center offset on x (uTransform.z).</summary>
    public readonly float OffsetX;

    /// <summary>NDC center offset on y (uTransform.w).</summary>
    public readonly float OffsetY;

    /// <summary>Fragment tint red (uTint.r).</summary>
    public readonly float TintR;

    /// <summary>Fragment tint green (uTint.g).</summary>
    public readonly float TintG;

    /// <summary>Fragment tint blue (uTint.b).</summary>
    public readonly float TintB;

    /// <summary>Fragment alpha (uTint.a).</summary>
    public readonly float Alpha;

    /// <summary>Creates the per-draw constant block.</summary>
    /// <param name="scaleX">NDC half extent on x.</param>
    /// <param name="scaleY">NDC half extent on y.</param>
    /// <param name="offsetX">NDC center offset on x.</param>
    /// <param name="offsetY">NDC center offset on y.</param>
    /// <param name="alpha">Fragment alpha in [0, 1].</param>
    /// <param name="tintR">Fragment tint red.</param>
    /// <param name="tintG">Fragment tint green.</param>
    /// <param name="tintB">Fragment tint blue.</param>
    public DrawConstants2D(
        float scaleX,
        float scaleY,
        float offsetX,
        float offsetY,
        float alpha,
        float tintR,
        float tintG,
        float tintB
    )
    {
        ScaleX = scaleX;
        ScaleY = scaleY;
        OffsetX = offsetX;
        OffsetY = offsetY;
        TintR = tintR;
        TintG = tintG;
        TintB = tintB;
        Alpha = alpha;
    }
}
