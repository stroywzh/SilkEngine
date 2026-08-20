namespace SilkEngine.Scene;

/// <summary>
/// 组件生命周期基类（框架主要扩展点）。回调帧序：
/// OnAwake 于挂载时立即触发（AddComponent 内）→ OnStart 于首帧 Tick 前补发（仅一次）→
/// OnUpdate（每逻辑帧）/OnFixedUpdate（固定步长）/OnLateUpdate（Tick 后）/OnPostRender（渲染后）按序派发。
/// 仅活跃组件（Enabled 且宿主在层级中）收到 Update 系回调；OnAwake/OnStart 不受活跃性门控。
/// </summary>
public abstract class MonoBehaviour : Component
{
    /// <summary>挂载时立即调用一次（AddComponent 工厂内，ReadFrom 之后、Enable 之前）；不受活跃性门控。</summary>
    public virtual void OnAwake() { }

    /// <summary>首帧 Tick 派发前补发一次（仅活跃组件；已补发则不再触发，迟激活组件在激活后首帧补发）。</summary>
    public virtual void OnStart() { }

    /// <summary>每逻辑帧调用一次（SceneManager.Tick 派发，活跃组件）。</summary>
    /// <param name="deltaTime">本帧增量时间（秒，经 TimeScale 缩放）</param>
    public virtual void OnUpdate(float deltaTime) { }

    /// <summary>固定步长调用（SceneManager.FixedTick 派发，活跃组件；步长由 EngineLoop 固定步长累加器驱动）。</summary>
    /// <param name="deltaTime">固定步长（秒）</param>
    public virtual void OnFixedUpdate(float deltaTime) { }

    /// <summary>逻辑帧末调用（SceneManager.LateTick 派发，活跃组件；OnUpdate 之后）。</summary>
    public virtual void OnLateUpdate() { }

    /// <summary>渲染提交后调用（SceneManager.PostRender 派发，活跃组件）。</summary>
    public virtual void OnPostRender() { }
}
