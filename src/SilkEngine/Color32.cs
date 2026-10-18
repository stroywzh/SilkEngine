namespace SilkEngine;

/// <summary>
/// 8-bit RGBA color. Mirrors UnityEngine.Color32.
/// </summary>
public struct Color32 : IEquatable<Color32>
{
    public byte R,
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

    public byte this[int i]
    {
        get =>
            i switch
            {
                0 => R,
                1 => G,
                2 => B,
                3 => A,
                _ => throw new IndexOutOfRangeException(),
            };
        set
        {
            switch (i)
            {
                case 0:
                    R = value;
                    break;
                case 1:
                    G = value;
                    break;
                case 2:
                    B = value;
                    break;
                case 3:
                    A = value;
                    break;
                default:
                    throw new IndexOutOfRangeException();
            }
        }
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
        t = EngineMath.Clamp01(t);
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

    private static byte ToByte(float value)
    {
        if (float.IsNaN(value))
            return 0;
        return (byte)EngineMath.Clamp(value + 0.5f, 0f, 255f);
    }

    public static implicit operator Color(Color32 c) =>
        new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);

    public static implicit operator Color32(Color c) =>
        new(ToByte(c.R * 255f), ToByte(c.G * 255f), ToByte(c.B * 255f), ToByte(c.A * 255f));

    public static bool operator ==(Color32 lhs, Color32 rhs) =>
        lhs.R == rhs.R && lhs.G == rhs.G && lhs.B == rhs.B && lhs.A == rhs.A;

    public static bool operator !=(Color32 lhs, Color32 rhs) => !(lhs == rhs);

    public bool Equals(Color32 other) =>
        R == other.R && G == other.G && B == other.B && A == other.A;

    public override bool Equals(object? obj) => obj is Color32 c && Equals(c);

    public override int GetHashCode()
    {
        unchecked
        {
            int h = R.GetHashCode();
            h = h * 397 ^ G.GetHashCode();
            h = h * 397 ^ B.GetHashCode();
            h = h * 397 ^ A.GetHashCode();
            return h;
        }
    }

    public override string ToString() => $"RGBA({R}, {G}, {B}, {A})";
}
