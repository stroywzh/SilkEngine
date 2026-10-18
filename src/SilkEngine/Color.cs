namespace SilkEngine;

/// <summary>
/// Floating-point RGBA color (usually in [0,1], HDR values may exceed 1). Mirrors UnityEngine.Color.
/// </summary>
public struct Color : IEquatable<Color>
{
    public float R,
        G,
        B,
        A;

    public Color(float r, float g, float b, float a = 1f)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public static readonly Color White = new(1f, 1f, 1f, 1f);
    public static readonly Color Black = new(0f, 0f, 0f, 1f);
    public static readonly Color Clear = new(0f, 0f, 0f, 0f);
    public static readonly Color Red = new(1f, 0f, 0f, 1f);
    public static readonly Color Green = new(0f, 1f, 0f, 1f);
    public static readonly Color Blue = new(0f, 0f, 1f, 1f);
    public static readonly Color Yellow = new(1f, 0.9215686f, 0.015686275f, 1f);
    public static readonly Color Cyan = new(0f, 1f, 1f, 1f);
    public static readonly Color Magenta = new(1f, 0f, 1f, 1f);
    public static readonly Color Gray = new(0.5f, 0.5f, 0.5f, 1f);
    public static readonly Color Grey = new(0.5f, 0.5f, 0.5f, 1f);

    public float this[int i]
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

    /// <summary>Luminance using the standard Rec.601 weights.</summary>
    public float Grayscale => 0.299f * R + 0.587f * G + 0.114f * B;

    /// <summary>Largest R, G or B component.</summary>
    public float MaxColorComponent => EngineMath.Max(EngineMath.Max(R, G), B);

    public static Color Lerp(Color a, Color b, float t)
    {
        t = EngineMath.Clamp01(t);
        return new Color(
            a.R + (b.R - a.R) * t,
            a.G + (b.G - a.G) * t,
            a.B + (b.B - a.B) * t,
            a.A + (b.A - a.A) * t
        );
    }

    public static Color LerpUnclamped(Color a, Color b, float t) =>
        new(
            a.R + (b.R - a.R) * t,
            a.G + (b.G - a.G) * t,
            a.B + (b.B - a.B) * t,
            a.A + (b.A - a.A) * t
        );

    /// <summary>Converts RGB to HSV.</summary>
    public static void RGBToHSV(Color rgbColor, out float h, out float s, out float v)
    {
        if (rgbColor.B > rgbColor.G && rgbColor.B > rgbColor.R)
            RGBToHSVHelper(4f, rgbColor.B, rgbColor.R, rgbColor.G, out h, out s, out v);
        else if (rgbColor.G > rgbColor.R)
            RGBToHSVHelper(2f, rgbColor.G, rgbColor.B, rgbColor.R, out h, out s, out v);
        else
            RGBToHSVHelper(0f, rgbColor.R, rgbColor.G, rgbColor.B, out h, out s, out v);
    }

    private static void RGBToHSVHelper(
        float offset,
        float dominantColor,
        float colorOne,
        float colorTwo,
        out float h,
        out float s,
        out float v
    )
    {
        v = dominantColor;
        if (v != 0f)
        {
            float small = colorOne > colorTwo ? colorTwo : colorOne;
            float diff = v - small;
            if (diff != 0f)
            {
                s = diff / v;
                h = offset + (colorOne - colorTwo) / diff;
            }
            else
            {
                s = 0f;
                h = offset + (colorOne - colorTwo);
            }
            h /= 6f;
            if (h < 0f)
                h += 1f;
        }
        else
        {
            s = 0f;
            h = 0f;
        }
    }

    public static Color HSVToRGB(float h, float s, float v) => HSVToRGB(h, s, v, true);

    public static Color HSVToRGB(float h, float s, float v, bool hdr)
    {
        Color retval = White;
        if (s == 0f)
        {
            retval.R = v;
            retval.G = v;
            retval.B = v;
        }
        else if (v == 0f)
        {
            retval.R = 0f;
            retval.G = 0f;
            retval.B = 0f;
        }
        else
        {
            retval.R = 0f;
            retval.G = 0f;
            retval.B = 0f;
            float tS = s,
                tV = v,
                tH = h * 6f;
            int hi = EngineMath.FloorToInt(tH);
            float t1 = tV * (1f - tS);
            float t2 = tV * (1f - tS * (tH - hi));
            float t3 = tV * (1f - tS * (1f - (tH - hi)));
            switch (hi)
            {
                case 0:
                    retval.R = tV;
                    retval.G = t3;
                    retval.B = t1;
                    break;
                case 1:
                    retval.R = t2;
                    retval.G = tV;
                    retval.B = t1;
                    break;
                case 2:
                    retval.R = t1;
                    retval.G = tV;
                    retval.B = t3;
                    break;
                case 3:
                    retval.R = t1;
                    retval.G = t2;
                    retval.B = tV;
                    break;
                case 4:
                    retval.R = t3;
                    retval.G = t1;
                    retval.B = tV;
                    break;
                case 5:
                    retval.R = tV;
                    retval.G = t1;
                    retval.B = t2;
                    break;
            }
        }
        if (!hdr)
        {
            retval.R = EngineMath.Clamp01(retval.R);
            retval.G = EngineMath.Clamp01(retval.G);
            retval.B = EngineMath.Clamp01(retval.B);
            retval.A = EngineMath.Clamp01(retval.A);
        }
        return retval;
    }

    public static implicit operator Vector4(Color c) => new(c.R, c.G, c.B, c.A);

    public static implicit operator Color(Vector4 v) => new(v.X, v.Y, v.Z, v.W);

    /// <summary>Component-wise addition (including alpha).</summary>
    public static Color operator +(Color a, Color b) =>
        new(a.R + b.R, a.G + b.G, a.B + b.B, a.A + b.A);

    /// <summary>Component-wise subtraction (including alpha).</summary>
    public static Color operator -(Color a, Color b) =>
        new(a.R - b.R, a.G - b.G, a.B - b.B, a.A - b.A);

    /// <summary>Component-wise product (including alpha).</summary>
    public static Color operator *(Color a, Color b) =>
        new(a.R * b.R, a.G * b.G, a.B * b.B, a.A * b.A);

    /// <summary>Scales all components including alpha.</summary>
    public static Color operator *(Color a, float d) => new(a.R * d, a.G * d, a.B * d, a.A * d);

    public static Color operator *(float d, Color a) => new(a.R * d, a.G * d, a.B * d, a.A * d);

    public static Color operator /(Color a, float d) => new(a.R / d, a.G / d, a.B / d, a.A / d);

    public static bool operator ==(Color lhs, Color rhs) =>
        lhs.R == rhs.R && lhs.G == rhs.G && lhs.B == rhs.B && lhs.A == rhs.A;

    public static bool operator !=(Color lhs, Color rhs) => !(lhs == rhs);

    public bool Equals(Color other) => R == other.R && G == other.G && B == other.B && A == other.A;

    public override bool Equals(object? obj) => obj is Color c && Equals(c);

    public override int GetHashCode() => HashUtil.Of(R, G, B, A);

    public override string ToString() => ToString(null, null);

    public string ToString(string format) => ToString(format, null);

    public string ToString(string? format, IFormatProvider? formatProvider) =>
        FormatUtil.Format("RGBA", "F3", format, formatProvider, R, G, B, A);
}
