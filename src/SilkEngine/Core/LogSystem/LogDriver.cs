

using System.Runtime.CompilerServices;

namespace SilkEngine.Core.LogSystem;

/// <summary>
/// 临时负责驱动Log的Tick写入的类，后面改成转有线程
/// </summary>
internal class LogDriver
{
    private ILogWriter _writer;

    internal LogDriver()
    {
        var path = LogHelper.GetLogPath();
        if (OperatingSystem.IsWindows())
        {
            _writer = new WindowsLogWriter(path);
        }
        else if (OperatingSystem.IsLinux())
        {
            throw new NotImplementedException();
        }
        else
        {
            _writer = new WindowsLogWriter(path);
        }
    }

    //TODO: NotImplement
    public void Record(in string message)
    {
        Console.WriteLine(message);
        // _writer.Record(in message);
    }

    internal static void Tick()
    {
        throw new NotImplementedException();
    }


}