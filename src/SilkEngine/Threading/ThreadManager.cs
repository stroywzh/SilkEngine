using System;
using System.Threading;

namespace SilkEngine.Threading;

//TODO: 线程管理器，负责管理ThreadController
public class ThreadManager
{
    private uint count = 0;
    private SortedDictionary<uint, ThreadController> controllers = new();
    private bool mainThreadRegisterd = false;

    public void RegisterMainThread(Thread mainThread)
    {
        if (count > 0)
        {
            throw new InvalidOperationException("主线程必须最先注册且只能注册一次");
        }

        if (mainThreadRegisterd)
        {
            throw new InvalidOperationException("禁止重复注册主线程|");
        }

        Register(mainThread);
        mainThreadRegisterd = true;
    }

    public ThreadController CreateThreadController(
        Action entry,
        string name,
        bool isBackground = true,
        ThreadPriority priority = ThreadPriority.Normal
    )
    {
        var thread = ThreadFactory.CreateThread(entry, name, isBackground, priority);
        return Register(thread);
    }

    public bool TryGetController(uint internalId, out ThreadController controller)
    {
        controller = null;
        if (controllers.TryGetValue(internalId, out var i) && i is not null)
        {
            controller = i;
            return true;
        }

        return false;
    }

    //TODO:未完成
    public bool TryGetController(string name, out ThreadController controller)
    {
        controller = null;
        foreach (var i in controllers.Values)
        {
            if (name == i.Context.Name)
            {
                controller = i;
                return true;
            }
        }

        return false;
    }

    private ThreadController Register(Thread thread)
    {
        var context = new ThreadContext(thread, count);
        var main = new ThreadController(context);
        var controller = controllers[count] = main;
        count += 1;
        return controller;
    }
}
