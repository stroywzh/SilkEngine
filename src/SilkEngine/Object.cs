using System;

namespace SilkEngine.Core;

public abstract class Object
{
    private static int _nextID = 0;
    private readonly int _id = Interlocked.Increment(ref _nextID);
    public string Name { get; set; } = string.Empty;

    public int GetInstanceID() => _id;

    public static event Action<Object, float>? DestroyHandler;

    /// <summary>GameObject 专属销毁/实例化逻辑挂接点（Scene 层静态构造注册，单程序集内部委托，非反射）。</summary>
    internal static Action<Object>? GameObjectDestroyHook;
    internal static Func<Object, Object>? GameObjectInstantiateHook;

    internal bool _destroyPending;
    internal bool _destroyed;

    public static void Destroy(Object obj, float delay = 0f)
    {
        if (obj._destroyPending || obj._destroyed)
            return; // 幂等
        obj._destroyPending = true;
        GameObjectDestroyHook?.Invoke(obj); // 仅 GameObject 注册过；其他类型无操作
        DestroyHandler?.Invoke(obj, delay);
    }

    public static Object Instantiate(Object original)
    {
        // hook 仅在 GameObject 静态构造后注册：非 GameObject 类型走 NotSupportedException（原语义）
        if (GameObjectInstantiateHook is { } hook)
            return hook(original);
        throw new NotSupportedException($"Instantiate not supported for {original.GetType()}");
    }
}
