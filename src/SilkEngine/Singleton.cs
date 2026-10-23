using System;
using System.Threading;

namespace SilkEngine;

public abstract class Singleton<T>
    where T : class
{
    private static readonly Lazy<T> _instance = new Lazy<T>();

    protected Singleton() { }

    public static T Instance => _instance.Value;
}

public class SingletonInstance<T> : Singleton<T>
    where T : class
{
    protected SingletonInstance()
    {
        _ = Instance;
    }
}
