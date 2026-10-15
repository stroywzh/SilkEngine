using System;
using System.IO;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace SilkEngine.Core.LogSystem;

internal class WindowsLogWriter : ILogWriter
{
    private static readonly ConcurrentQueue<string> _queue = new();
    private static readonly AutoResetEvent _signal = new(false);

    private string _logFile;
    public string LogFile
    {
        get=>_logFile;
    }

    public WindowsLogWriter(string logPath)
    {
        string now = DateTime.Now.Date.Day.ToString();
        _logFile = Path.Combine(logPath,"Log"+ now +".log");
    }

    public void Record(in string message)
    {
        _queue.Enqueue(message);
        _signal.Set();
    }

    
    private void ProcessQueue()
    {
        throw new NotImplementedException();
        // while (true)
        // {
        //     _signal.WaitOne();
        //     while (_queue.TryDequeue(out var msg))
        //     {
        //         // 写入文件
        //     }
        // }
    }
}