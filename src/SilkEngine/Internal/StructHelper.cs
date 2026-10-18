using System.Globalization;
using System.Text;

namespace SilkEngine;

/// <summary>
/// Shared string formatting for math value types. Keeps the "(a, b, c)" layout in one place.
/// </summary>
internal static class FormatUtil
{
    /// <summary>Formats values as "(v0, v1, ...)". <paramref name="format"/> null/empty falls back to <paramref name="defaultFormat"/>.</summary>
    public static string Format(
        string defaultFormat,
        string? format,
        IFormatProvider? provider,
        params float[] values
    ) => Format(string.Empty, defaultFormat, format, provider, values);

    /// <summary>Formats values as "prefix(v0, v1, ...)". <paramref name="format"/> null/empty falls back to <paramref name="defaultFormat"/>.</summary>
    public static string Format(
        string prefix,
        string defaultFormat,
        string? format,
        IFormatProvider? provider,
        params float[] values
    )
    {
        if (string.IsNullOrEmpty(format))
            format = defaultFormat;
        provider ??= CultureInfo.InvariantCulture.NumberFormat;

        var sb = new StringBuilder(prefix.Length + 2 + values.Length * (format!.Length + 8));
        sb.Append(prefix).Append('(');
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0)
                sb.Append(", ");
            sb.Append(values[i].ToString(format, provider));
        }
        sb.Append(')');
        return sb.ToString();
    }
}

/// <summary>
/// Shared hash code combination for math value types. Uses the classic "h * 397 ^ component" sequence,
/// so all structs stay hash-consistent with each other.
/// </summary>
internal static class HashUtil
{
    public static int Of(float a) => a.GetHashCode();

    public static int Of(float a, float b)
    {
        unchecked
        {
            return a.GetHashCode() * 397 ^ b.GetHashCode();
        }
    }

    public static int Of(float a, float b, float c)
    {
        unchecked
        {
            int h = a.GetHashCode();
            h = h * 397 ^ b.GetHashCode();
            h = h * 397 ^ c.GetHashCode();
            return h;
        }
    }

    public static int Of(float a, float b, float c, float d)
    {
        unchecked
        {
            int h = a.GetHashCode();
            h = h * 397 ^ b.GetHashCode();
            h = h * 397 ^ c.GetHashCode();
            h = h * 397 ^ d.GetHashCode();
            return h;
        }
    }
}

/// <summary>
/// Approximate equality helper used by <c>operator ==</c> on math structs.
/// Compares the squared Euclidean distance of all components against <see cref="EngineMath.kEpsilon"/>².
/// </summary>
internal static class ApproxEquals
{
    public static bool Equal(float a, float b) =>
        (a - b) * (a - b) < EngineMath.kEpsilon * EngineMath.kEpsilon;

    public static bool Equal(float a0, float a1, float b0, float b1)
    {
        float d0 = a0 - b0,
            d1 = a1 - b1;
        return d0 * d0 + d1 * d1 < EngineMath.kEpsilon * EngineMath.kEpsilon;
    }

    public static bool Equal(float a0, float a1, float a2, float b0, float b1, float b2)
    {
        float d0 = a0 - b0,
            d1 = a1 - b1,
            d2 = a2 - b2;
        return d0 * d0 + d1 * d1 + d2 * d2 < EngineMath.kEpsilon * EngineMath.kEpsilon;
    }

    public static bool Equal(
        float a0,
        float a1,
        float a2,
        float a3,
        float b0,
        float b1,
        float b2,
        float b3
    )
    {
        float d0 = a0 - b0,
            d1 = a1 - b1,
            d2 = a2 - b2,
            d3 = a3 - b3;
        return d0 * d0 + d1 * d1 + d2 * d2 + d3 * d3 < EngineMath.kEpsilon * EngineMath.kEpsilon;
    }
}
