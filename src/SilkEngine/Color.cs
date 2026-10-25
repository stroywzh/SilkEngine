using SilkEngine.Mathematics;

namespace SilkEngine;

/// <summary>
/// Floating-point RGBA color (usually in [0,1], HDR values may exceed 1). Mirrors UnityEngine.Color.
/// </summary>
/// <remarks>
/// Pure value type: <c>readonly struct</c> with <c>public readonly</c> components, no in-place
/// mutators. <c>operator ==</c>, <see cref="Equals(Color)"/> and <see cref="GetHashCode"/> are exact
/// and mutually consistent (component-wise float bits).
/// </remarks>
[MathValue("RGBA", "F3", "R", "G", "B", "A")]
public readonly partial struct Color : IEquatable<Color>, IFormattable
{
    public readonly float R,
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
    /// <param name="rgbColor">Source color.</param>
    /// <param name="h">Hue in [0, 1].</param>
    /// <param name="s">Saturation in [0, 1].</param>
    /// <param name="v">Value, i.e. the largest component.</param>
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

    /// <summary>Converts HSV to RGB with HDR components allowed.</summary>
    public static Color HSVToRGB(float h, float s, float v) => HSVToRGB(h, s, v, true);

    /// <summary>Converts HSV to RGB, optionally clamping the result into [0, 1].</summary>
    /// <param name="h">Hue; any real value, wrapped into [0, 1) on entry (so 0 and 1 are the same hue).</param>
    /// <param name="s">Saturation; usually 0..1, not clamped.</param>
    /// <param name="v">Value; usually 0..1, not clamped.</param>
    /// <param name="hdr">
    /// When <c>false</c>, R/G/B are clamped to [0, 1]; when <c>true</c> the components are returned
    /// unclamped.
    /// </param>
    /// <returns>The color; alpha is always 1.</returns>
    /// <remarks>
    /// The hue is wrapped with <c>h -= floor(h)</c> before the sector switch, so wrapping and
    /// out-of-range hues map to a defined sector (e.g. <c>HSVToRGB(1, 1, 1)</c> is red, not black).
    /// REV-014.
    /// </remarks>
    public static Color HSVToRGB(float h, float s, float v, bool hdr)
    {
        // Wrap the hue into [0, 1) so the 6-way sector switch is total.
        h -= EngineMath.Floor(h);

        float r = 0f,
            g = 0f,
            b = 0f;

        if (s == 0f)
        {
            r = v;
            g = v;
            b = v;
        }
        else if (v == 0f)
        {
            r = 0f;
            g = 0f;
            b = 0f;
        }
        else
        {
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
                case 6: // h == 1 after wrapping keeps the same colour as h == 0
                    r = tV;
                    g = t3;
                    b = t1;
                    break;
                case 1:
                    r = t2;
                    g = tV;
                    b = t1;
                    break;
                case 2:
                    r = t1;
                    g = tV;
                    b = t3;
                    break;
                case 3:
                    r = t1;
                    g = t2;
                    b = tV;
                    break;
                case 4:
                    r = t3;
                    g = t1;
                    b = tV;
                    break;
                case 5:
                    r = tV;
                    g = t1;
                    b = t2;
                    break;
                default:
                    // Unreachable for finite h after wrapping; NaN/Infinity land on black.
                    break;
            }
        }

        if (!hdr)
        {
            r = EngineMath.Clamp01(r);
            g = EngineMath.Clamp01(g);
            b = EngineMath.Clamp01(b);
        }

        return new Color(r, g, b, 1f);
    }

    /// <summary>Implicit Color → Vector4 conversion (RGBA layout, no precision loss).</summary>
    /// <remarks>Declared on the root side: <see cref="Vector4"/> lives in <c>SilkEngine.Mathematics</c>.</remarks>
    public static implicit operator Vector4(Color c) => new(c.R, c.G, c.B, c.A);

    /// <summary>Explicit Vector4 → Color conversion (the reverse direction is narrowing: no alpha default).</summary>
    /// <remarks>Q9: the implicit reverse conversion was removed so overload resolution stays predictable.</remarks>
    public static explicit operator Color(Vector4 v) => new(v.X, v.Y, v.Z, v.W);

    /// <summary>Implicit Color32 → Color conversion (byte components widened to floats).</summary>
    /// <remarks>Declared on the root side: <see cref="Color32"/> lives in <c>SilkEngine.Mathematics</c>.</remarks>
    public static implicit operator Color(Color32 c) =>
        new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);

    /// <summary>Explicit Color → Color32 conversion (quantises to 8 bits per channel).</summary>
    /// <remarks>Q9: the quantisation point must be visible at the call site, so this is explicit.</remarks>
    public static explicit operator Color32(Color c) =>
        new(
            Color32.ToByte(c.R * 255f),
            Color32.ToByte(c.G * 255f),
            Color32.ToByte(c.B * 255f),
            Color32.ToByte(c.A * 255f)
        );

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

}
