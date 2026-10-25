namespace SilkEngine.Core.LogSystem;

/// <summary>
/// Filesystem locations used by the file sink.
/// </summary>
internal static class LogPaths
{
    /// <summary>Returns the log directory next to the application, creating it when missing.</summary>
    /// <returns>Absolute path of the log directory.</returns>
    public static string GetLogPath()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Log");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return dir;
    }
}
