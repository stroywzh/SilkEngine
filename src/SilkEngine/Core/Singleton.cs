namespace SilkEngine;

public class Singleton<T>
    where T : Singleton<T>
{
    public static T Instance { get; protected set; }
    protected Singleton()
    {
        Instance = (T)this;
    }
}
