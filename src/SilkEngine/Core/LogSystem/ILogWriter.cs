namespace SilkEngine.Core.LogSystem;
public interface ILogWriter
{
    string LogFile{get;}
    void Record(in string message);
}

internal static class LogHelper
{
    public static string GetLogPath()
    {
        var dir = Path.Combine(AppContext.BaseDirectory,"Log");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return dir;
    } 
}