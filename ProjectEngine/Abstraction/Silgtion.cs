namespace ProjectEngine.Abstraction;

/// <summary>
/// 单例基类
/// <br/>不会延迟初始化
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class Singleton<T>
    where T : Singleton<T>, new()
{
    private static T _instance;

    protected Singleton()
    {
        _instance = Activator.CreateInstance<T>();
    }

    public static T Instance
    {
        get => _instance;
    }
}
