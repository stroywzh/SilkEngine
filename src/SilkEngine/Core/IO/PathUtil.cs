namespace SilkEngine.Core.IO;

/// <summary>Path comparison rules shared by every file-backed subsystem.</summary>
internal static class PathUtil
{
    /// <summary>Comparer matching the platform's file-system case rules.</summary>
    internal static StringComparer Comparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>Comparison matching the platform's file-system case rules.</summary>
    internal static StringComparison Comparison { get; } =
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
}
