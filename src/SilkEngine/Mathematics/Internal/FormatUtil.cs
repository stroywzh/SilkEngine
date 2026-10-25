using System.Globalization;
using System.Text;

namespace SilkEngine.Mathematics.Internal;

/// <summary>
/// Shared string formatting for math value types. Keeps the "prefix(a, b, c)" layout in one place.
/// </summary>
/// <remarks>
/// Lives in the lowest layer (<c>SilkEngine.Mathematics.Internal</c>) because both the root math
/// types and the <c>SilkEngine.Mathematics</c> types use it.
/// </remarks>
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
