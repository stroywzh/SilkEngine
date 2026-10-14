using System;
using System.Threading;

namespace SilkEngine;

public abstract class Singleton<T>
    where T : Singleton<T>
{
    private static readonly Lazy<T> _instance = new Lazy<T>();

    protected Singleton() { }

    public static T Instance => _instance.Value;
}

public class SingletonInstance<T> : Singleton<T>
    where T : SingletonInstance<T> { }
