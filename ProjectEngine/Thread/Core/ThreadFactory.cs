using System;
using System.Threading;

namespace ProjectEngine.Threading;

public static class ThreadFactory
{
    public static Thread CreateThread(Action loop, string name,
        bool isBackground = true,
        ThreadPriority priority = ThreadPriority.Normal)
    {
        return new Thread(() => loop())
        {
            Name = name,
            IsBackground = isBackground,
            Priority = priority
        };
    }
}
