namespace SilkEngine.Mathematics;

/// <summary>
/// 8-bit RGBA color. Mirrors UnityEngine.Color32.
/// </summary>
/// <remarks>
/// Pure value type: <c>readonly struct</c> with <c>public readonly</c> components, no in-place
/// mutators. <c>operator ==</c>, <see cref="Equals(Color32)"/> and <see cref="GetHashCode"/> are exact
/// and mutually consistent (byte-exact, no tolerance; quantisation is the explicit
/// <c>Color -&gt; Color32</c> conversion's job).
/// </remarks>
[MathValue("RGBA", "F0", "R", "G", "B", "A")]
public readonly partial struct Color32 : IEquatable<Color32>, IFormattable
{
    public readonly byte R,
        G,
        B,
        A;

    public Color32(byte r, byte g, byte b, byte a = 255)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    /// <summary>
    /// (255, 255, 255, 255)
    /// </summary>
    public static readonly Color32 White = new(255, 255, 255, 255);

    /// <summary>
    /// (0, 0, 0, 255)
    /// </summary>
    public static readonly Color32 Black = new(0, 0, 0, 255);

    /// <summary>
    /// (0, 0, 0, 0)
    /// </summary>
    public static readonly Color32 Clear = new(0, 0, 0, 0);

    /// <summary>
    /// (255, 0, 0, 255)
    /// </summary>
    public static readonly Color32 Red = new(255, 0, 0, 255);

    /// <summary>
    /// (0, 255, 0, 255)
    /// </summary>
    public static readonly Color32 Green = new(0, 255, 0, 255);

    /// <summary>
    /// (0, 0, 255, 255)
    /// </summary>
    public static readonly Color32 Blue = new(0, 0, 255, 255);

    /// <summary>
    /// (255, 235, 4, 255)
    /// </summary>
    public static readonly Color32 Yellow = new(255, 235, 4, 255);

    /// <summary>
    /// (0, 255, 255, 255)
    /// </summary>
    public static readonly Color32 Cyan = new(0, 255, 255, 255);

    /// <summary>
    /// (255, 0, 255, 255)
    /// </summary>
    public static readonly Color32 Magenta = new(255, 0, 255, 255);

    /// <summary>
    /// (128, 128, 128, 255)
    /// </summary>
    public static readonly Color32 Gray = new(128, 128, 128, 255);

    public static Color32 Lerp(Color32 a, Color32 b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Color32(
            ToByte(a.R + (b.R - a.R) * t),
            ToByte(a.G + (b.G - a.G) * t),
            ToByte(a.B + (b.B - a.B) * t),
            ToByte(a.A + (b.A - a.A) * t)
        );
    }

    public static Color32 LerpUnclamped(Color32 a, Color32 b, float t) =>
        new(
            ToByte(a.R + (b.R - a.R) * t),
            ToByte(a.G + (b.G - a.G) * t),
            ToByte(a.B + (b.B - a.B) * t),
            ToByte(a.A + (b.A - a.A) * t)
        );

    /// <summary>Rounds a normalized component into a byte; NaN maps to 0.</summary>
    /// <returns>The clamped byte.</returns>
    internal static byte ToByte(float value)
    {
        if (float.IsNaN(value))
            return 0;
        return (byte)Math.Clamp(value + 0.5f, 0f, 255f);
    }

}
