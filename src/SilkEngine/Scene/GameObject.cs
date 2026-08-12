using System.Collections.Generic;

namespace SilkEngine;

public sealed class GameObject : Object
{
    internal List<Component> _components = new();
    public Transform Transform { get; }
    public bool IsActive { get; set; } = true;

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
        c.GameObject = this;
        _components.Add(c);
        (c as MonoBehaviour)?.OnEnable();
        (registry ?? SceneManager.ActiveRegistry)?.Register(c);
        return c;
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
        if (c != null)
        {
            _components.Remove(c);
            registry?.Unregister(c);
            return true;
        }
        return false;
    }
}
