using SilkEngine.Scene.Serialization;
using Object = SilkEngine.Core.Object;

namespace SilkEngine.Scene;

/// <summary>
/// 组件基类：挂载于 GameObject 的行为单元。
/// 活跃状态机以 RecomputeActiveState 为单一真理源（Enabled ∧ GameObject.IsActiveInHierarchy 决定活跃性）；
/// 生命周期 OnEnable/OnDisable/OnDestroy 由状态机与帧末销毁队列驱动。
/// </summary>
public abstract class Component : Object
{
    /// <summary>所属宿主 GameObject（AddComponent 挂载时赋值）。</summary>
    public GameObject GameObject { get; internal set; } = null!;

    /// <summary>宿主 Transform（等同 GameObject.Transform）。</summary>
    public Transform Transform => GameObject.Transform;

    internal bool Awaked;   // OnAwake 已触发
    internal bool Started;  // OnStart 已触发

    private bool _enabled = true;
    private bool _enableFired;

    /// <summary>
    /// 组件自身启用开关；置位即触发 RecomputeActiveState —— 与宿主层级活跃性共同决定 OnEnable/OnDisable 是否派发。
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
                return;
            _enabled = value;
            RecomputeActiveState();
        }
    }

    /// <summary>
    /// 活跃状态机单一真理源：按 Enabled ∧ GameObject.IsActiveInHierarchy 重算，
    /// 状态翻转时派发 OnEnable/OnDisable（幂等：仅在翻转沿触发）。
    /// </summary>
    internal void RecomputeActiveState()
    {
        bool shouldBeActive = _enabled && GameObject.IsActiveInHierarchy;
        if (shouldBeActive && !_enableFired)
        {
            _enableFired = true;
            OnEnable();
        }
        else if (!shouldBeActive && _enableFired)
        {
            _enableFired = false;
            OnDisable();
        }
    }


    /// <summary>组件从宿主移除时的收尾：若已启用则触发 OnDisable 并清除状态。</summary>
    internal void MarkRemoved()
    {
        if (_enableFired)
        {
            _enableFired = false;
            OnDisable();
        }
    }

    /// <summary>组件启用时调用：挂载后经 RecomputeActiveState 触发，或失活后恢复活跃时再次触发。</summary>
    public virtual void OnEnable() { }

    /// <summary>组件停用时调用：Enabled 置 false、宿主失活、移除或帧末销毁时经状态机触发。</summary>
    public virtual void OnDisable() { }

    /// <summary>组件销毁时调用：帧末销毁队列（FrameSnapshotManager.CommitPending）统一执行；仅调用一次。</summary>
    public virtual void OnDestroy() { }

    /// <summary>序列化出口：将组件字段写入节点。基类空默认 no-op；SceneSerializer.Serialize 对全部组件调用。</summary>
    public virtual void WriteTo(SerializedNode node) { }

    /// <summary>序列化入口：从节点恢复字段。基类空默认 no-op；组件工厂在反序列化管道内调用。</summary>
    public virtual void ReadFrom(SerializedNode node) { }
}
