using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using SilkEngine.Core;
using SilkEngine.Scene.Serialization;
using Object = SilkEngine.Core.Object;

namespace SilkEngine.Scene;

public sealed class GameObject : Object
{
    static GameObject()
    {
        Object.GameObjectDestroyHook = obj =>
        {
            if (obj is GameObject go) // 类型守卫：组件销毁（Object.Destroy(c)）不崩溃
                DestroyRecursive(go);
        };
        Object.GameObjectInstantiateHook = obj =>
        {
            if (obj is not GameObject go) // 类型守卫：保持 Object.Instantiate(非GameObject) 抛 NotSupportedException
                throw new NotSupportedException($"Instantiate not supported for {obj.GetType()}");
            return InstantiateGameObject(go);
        };
    }

    private static void DestroyRecursive(GameObject go)
    {
        go._destroyPending = true;
        foreach (var child in go.Transform.Children.ToArray())
            DestroyRecursive(child.GameObject!);
        go.IsActive = false;
        foreach (var c in go._components)
            c.Enabled = false;
    }

    private static GameObject InstantiateGameObject(GameObject go)
    {
        var clone = new GameObject(go.Name + "(Clone)") { IsActive = go.IsActive };
        clone.Transform.LocalPosition = go.Transform.LocalPosition;
        clone.Transform.LocalRotation = go.Transform.LocalRotation;
        clone.Transform.LocalScale = go.Transform.LocalScale;
        foreach (var child in go.Transform.Children)
        {
            var cgo = InstantiateGameObject(child.GameObject!);
            cgo.Transform.SetParent(clone.Transform);
        }
        return clone;
    }

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
    public bool IsActiveInHierarchy =>
        _isActive && (Transform.Parent?.GameObject?.IsActiveInHierarchy ?? true);

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

    /// <summary>挂载组件；同实例重复添加或跨宿主重挂抛 InvalidOperationException。</summary>
    public Component AddComponent(Component c, ComponentRegistry? registry = null)
    {
        if (_components.Contains(c))
            throw new InvalidOperationException(
                $"Component '{c.GetType().Name}' is already attached to GameObject '{Name}'"
            );
        if (c.GameObject is { } host && host != this)
            throw new InvalidOperationException(
                $"Component '{c.GetType().Name}' is already attached to GameObject '{host.Name}'"
            );
        InitializeComponent(c, registry);
        return c;
    }

    public T AddComponent<T>(ComponentRegistry? registry = null)
        where T : Component, new()
    {
        return (T)AddComponent(new T(), registry);
    }

    /// <summary>
    /// 组件工厂：挂载 → ReadFrom(序列化数据) → OnAwake → RecomputeActiveState(Enable) → 注册。
    /// 顺序遵循 Unity 语义：OnAwake 中看到的字段即为序列化恢复后的值；
    /// 无挂载数据时 ReadFrom 不调用（基类空默认）。
    /// </summary>
    internal void InitializeComponent(Component c, ComponentRegistry? registry)
    {
        c.GameObject = this;
        _components.Add(c);

        if (
            _serializedData != null
            && _serializedData["Components"] is JsonObject comps
            && comps[c.GetType().FullName!] is JsonObject compNode
        )
        {
            c.ReadFrom(new SerializedNode(compNode));
        }

        if (c is MonoBehaviour mb && !mb.Awaked)
        {
            mb.Awaked = true;
            mb.OnAwake();
        }

        c.RecomputeActiveState();

        // 回退链（协调裁决 C1）：Services 未注册时 TryGet 静默不注册（保留旧测试语义）
        (registry ?? (Services.TryGet<SceneManager>(out var sm) ? sm?.Registry : null))?.Register(c);

        if (LogConfig.Lifecycle)
            Log.Info($"[Lifecycle] Added component {c.GetType().Name} to '{Name}'");
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
        if (LogConfig.Lifecycle)
            Log.Info($"[Lifecycle] Removed component {typeof(T).Name} from '{Name}'");
        return true;
    }

#if DEBUG
    public string GetAllComponentName()
    {
        System.Text.StringBuilder sb = new(64);
        sb.Append("AllComponents:\n");
        foreach (var i in _components)
        {
            sb.Append(_components.IndexOf(i));
            sb.Append("Name|");
            sb.Append(i.Name);
            sb.Append("|Type|");
            sb.Append(i.GetType().Name);
            sb.Append("\n");
        }
        return sb.ToString();
    }
#endif
}
