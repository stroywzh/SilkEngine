using SilkEngine.Scene.Serialization;

namespace SilkEngine;

public abstract class Component : Object
{
    public GameObject GameObject { get; internal set; } = null!;
    public Transform Transform => GameObject.Transform;

    internal bool Awaked;   // OnAwake 已触发
    internal bool Started;  // OnStart 已触发

    private bool _enabled = true;
    private bool _enableFired;

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

    public void OnValidate() { }

    /// <summary>组件从宿主移除时的收尾：若已启用则触发 OnDisable 并清除状态。</summary>
    internal void MarkRemoved()
    {
        if (_enableFired)
        {
            _enableFired = false;
            OnDisable();
        }
    }

    public virtual void OnEnable() { }

    public virtual void OnDisable() { }

    public virtual void OnDestroy() { }

    /// <summary>序列化出口：将组件字段写入节点。基类空默认 no-op；SceneSerializer.Serialize 对全部组件调用。</summary>
    public virtual void WriteTo(SerializedNode node) { }

    /// <summary>序列化入口：从节点恢复字段。基类空默认 no-op；组件工厂在反序列化管道内调用。</summary>
    public virtual void ReadFrom(SerializedNode node) { }
}
