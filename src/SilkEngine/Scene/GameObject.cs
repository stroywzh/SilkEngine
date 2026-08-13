using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using SilkEngine.Core.Assets.Serialization;

namespace SilkEngine;

public sealed class GameObject : Object
{
    internal List<Component> _components = new();
    internal JsonObject? _serializedData;

    /// <summary>反序列化管道：挂载序列化数据并恢复 Transform 值（仅覆盖存在键的字段）。</summary>
    internal void AttachSerializedData(JsonObject node)
    {
        _serializedData = node;
        if (node["Components"] is JsonObject comps && comps["Transform"] is JsonObject t)
        {
            var sn = new SerializedNode(t);
            if (t.ContainsKey("LocalPosition"))
                Transform.LocalPosition = sn.GetVector3("LocalPosition");
            if (t.ContainsKey("LocalRotation"))
                Transform.LocalRotation = sn.GetQuaternion("LocalRotation");
            if (t.ContainsKey("LocalScale"))
                Transform.LocalScale = sn.GetVector3("LocalScale");
        }
    }

    /// <summary>反序列化管道：清理挂载数据（组件均已完成 ReadFrom 后调用）。</summary>
    internal void ClearSerializedData() => _serializedData = null;
    public Transform Transform { get; }
    private bool _isActive = true;
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value)
                return;
            _isActive = value;
            NotifyActivationChanged();
        }
    }

    /// <summary>沿父链的层级活跃状态。</summary>
    public bool IsActiveInHierarchy
        => _isActive && (Transform.Parent?.GameObject?.IsActiveInHierarchy ?? true);

    internal void NotifyActivationChanged()
    {
        foreach (var c in _components.ToArray())
            c.RecomputeActiveState();
        foreach (var child in Transform.Children.ToArray())
            child.GameObject?.NotifyActivationChanged();
    }

    public GameObject(string name = "GameObject")
    {
        Name = name;
        Transform = new Transform((GameObject)this);
    }

    public GameObject(Transform parent, string name = "GameObject")
    {
        Name = name;
        Transform = new Transform((GameObject)this, parent);
    }

    public Component AddComponent(Component c, ComponentRegistry? registry = null)
    {
        InitializeComponent(c, registry);
        return c;
    }

    public T AddComponent<T>(ComponentRegistry? registry = null)
        where T : Component, new()
    {
        return (T)AddComponent(new T(), registry);
    }

    /// <summary>组件工厂：挂载 → Awake → 反序列化(ReadFrom) → 活跃重算 → 注册。顺序 MUST 为挂载→Awake→ReadFrom→Enable(条件)→注册。</summary>
    internal void InitializeComponent(Component c, ComponentRegistry? registry)
    {
        c.GameObject = this;
        _components.Add(c);

        if (c is MonoBehaviour mb && !mb.Awaked)
        {
            mb.Awaked = true;
            mb.OnAwake();
        }

        if (
            _serializedData != null
            && c is ISerializableComponent s
            && _serializedData["Components"] is JsonObject comps
            && comps[c.GetType().FullName!] is JsonObject compNode
        )
        {
            s.ReadFrom(new SerializedNode(compNode));
        }

        c.RecomputeActiveState();

        (registry ?? SceneManager.ActiveRegistry)?.Register(c);
    }

    public T? GetComponent<T>()
        where T : Component
    {
        foreach (var c in _components)
            if (c is T t)
                return t;

        return null;
    }

    public bool RemoveComponent<T>(ComponentRegistry? registry = null)
        where T : Component
    {
        var c = GetComponent<T>();
        if (c == null)
            return false;

        _components.Remove(c);
        c.MarkRemoved();
        Object.Destroy(c); // 帧末由 CommitPending 执行 OnDestroy + Unregister
        return true;
    }
}
