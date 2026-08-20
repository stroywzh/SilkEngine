using System;
using System.Threading;
using SilkEngine.Core;

namespace SilkEngine.Threading;

/// <summary>线程创建工厂：线程统一创建入口（禁止直接 new Thread()），命名/后台/优先级一次配置。</summary>
public static class ThreadFactory
{
    /// <summary>创建并配置线程（未启动；DEBUG 下记录创建日志）。</summary>
    /// <param name="entry">线程入口委托</param>
    /// <param name="name">线程名</param>
    /// <param name="isBackground">是否后台线程（默认 true）</param>
    /// <param name="priority">线程优先级（默认 Normal）</param>
    /// <returns>已配置未启动的 Thread 实例</returns>
    public static Thread CreateThread(
        Action entry,
        string name,
        bool isBackground = true,
        ThreadPriority priority = ThreadPriority.Normal
    )
    {
#if DEBUG
        Log.Info(
            $"[ThreadFactory] Creating New Thread:[{name}]|Background:{isBackground}|Priority:{priority}"
        );
#endif
        return new Thread(() => entry())
        {
            Name = name,
            IsBackground = isBackground,
            Priority = priority,
        };
    }
}
