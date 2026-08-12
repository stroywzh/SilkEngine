using System.Collections.Generic;

namespace SilkEngine;

public sealed class GameObject : Object
{
    internal List<Component> _components = new();
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
        foreach (var c in _components)
            c.RecomputeActiveState();
        foreach (var child in Transform.Children)
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

    public T AddComponent<T>(ComponentRegistry? registry = null)
        where T : Component, new()
    {
        var c = new T();
        InitializeComponent(c, registry);
        return c;
    }

    /// <summary>组件工厂：挂载 → Awake → 活跃重算 → 注册。顺序 MUST 为挂载→Awake→Enable(条件)→注册。</summary>
    internal void InitializeComponent(Component c, ComponentRegistry? registry)
    {
        c.GameObject = this;
        _components.Add(c);

        if (c is MonoBehaviour mb && !mb.Awaked)
        {
            mb.Awaked = true;
            mb.OnAwake();
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
        if (c.Enabled && c.GameObject.IsActive)
            c.OnDisable();
        Object.Destroy(c); // 帧末由 CommitPending 执行 OnDestroy + Unregister
        return true;
    }
}
