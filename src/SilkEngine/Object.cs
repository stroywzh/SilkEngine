using System;

namespace SilkEngine.Core;

/// <summary>
/// 引擎对象基类（GameObject 与 Component 的共同基类）。
/// 销毁经 <see cref="Destroy"/> 标记并由 SceneManager 于帧末统一提交；实例 ID 进程内单调递增。
/// </summary>
public abstract class Object
{
    private static int _nextID = 0;
    private readonly int _id = Interlocked.Increment(ref _nextID);

    /// <summary>对象名称（默认空字符串）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>返回该对象进程内全局唯一的实例 ID（从 1 起单调递增）。</summary>
    /// <returns>实例 ID。</returns>
    public int GetInstanceID() => _id;

    /// <summary>
    /// 销毁广播事件：参数为（对象, 延迟秒数）。SceneManager 订阅后入销毁队列，
    /// 帧末 CommitPending 统一提交（延迟销毁按延迟秒数生效）。
    /// </summary>
    public static event Action<Object, float>? DestroyHandler;

    /// <summary>GameObject 专属销毁/实例化逻辑挂接点（Scene 层静态构造注册，单程序集内部委托，非反射）。</summary>
    internal static Action<Object>? GameObjectDestroyHook;
    internal static Func<Object, Object>? GameObjectInstantiateHook;

    internal bool _destroyPending;
    internal bool _destroyed;

    /// <summary>
    /// 请求销毁对象。幂等：对象已标记销毁或已销毁时调用无操作；
    /// 仅标记 _destroyPending 并广播 DestroyHandler，实际销毁由 SceneManager 帧末提交执行。
    /// </summary>
    /// <param name="obj">要销毁的对象；null 调用将抛出空引用异常。</param>
    /// <param name="delay">延迟销毁秒数（≥ 0，默认 0 表示本帧末提交）。</param>
    public static void Destroy(Object obj, float delay = 0f)
    {
        if (obj._destroyPending || obj._destroyed)
            return; // 幂等
        obj._destroyPending = true;
        GameObjectDestroyHook?.Invoke(obj); // 仅 GameObject 注册过；其他类型无操作
        DestroyHandler?.Invoke(obj, delay);
    }

    /// <summary>
    /// 克隆 original（仅 GameObject 支持；经 GameObjectInstantiateHook 委托）。
    /// </summary>
    /// <param name="original">要克隆的对象。</param>
    /// <returns>克隆实例。</returns>
    /// <exception cref="NotSupportedException">original 非 GameObject，或 GameObject 静态构造未执行（hook 未注册）。</exception>
    public static Object Instantiate(Object original)
    {
        // hook 仅在 GameObject 静态构造后注册：非 GameObject 类型走 NotSupportedException（原语义）
        if (GameObjectInstantiateHook is { } hook)
            return hook(original);
        throw new NotSupportedException($"Instantiate not supported for {original.GetType()}");
    }
}
