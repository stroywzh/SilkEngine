using System.Collections.Generic;
using SilkEngine.Core;
using Object = SilkEngine.Core.Object;

namespace SilkEngine.Scene;

/// <summary>
/// 场景对象工厂：承载 Transform 与组件集合的树节点。
/// 组件经 AddComponent 挂载（顺序：挂载 → OnAwake → RecomputeActiveState(Enable) → 注册），
/// 销毁走帧末队列（Object.Destroy → SceneManager 帧末统一 OnDestroy + Unregister）。
/// </summary>
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
        foreach (var c in go._components)
        {
            var factory = ComponentFactory.Resolve(c.GetType().FullName!);
            if (factory == null)
                continue;
            clone.AddComponent(factory());   // 默认值重建：挂载→OnAwake→Enable→注册（不复刻状态）
        }
        foreach (var child in go.Transform.Children)
        {
            var cgo = InstantiateGameObject(child.GameObject!);
            cgo.Transform.SetParent(clone.Transform);
        }
        return clone;
    }

    internal List<Component> _components = new();

    /// <summary>所属场景上下文（Scene.CreateGameObject 装配；独立创建的对象为 null）。</summary>
    internal SceneContext? Context { get; set; }

    /// <summary>所属场景（经上下文解析；未绑定上下文为 null；测试与业务归属查询用）。</summary>
    internal Scene? SceneForTests => Context?.Scene;

    /// <summary>本对象固有 Transform（构造时创建，恒非空）。</summary>
    public Transform Transform { get; }
    private bool _isActive = true;

    /// <summary>
    /// 自身活跃开关；置 false 级联通知自身组件与全部子树（RecomputeActiveState → OnEnable/OnDisable）。
    /// </summary>
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

    /// <summary>
    /// 级联活跃通知：重算自身组件活跃态并递归子树（IsActive/SetParent 变更入口）。
    /// </summary>
    internal void NotifyActivationChanged()
    {
        foreach (var c in _components.ToArray())
            c.RecomputeActiveState();
        foreach (var child in Transform.Children.ToArray())
            child.GameObject?.NotifyActivationChanged();
    }

    /// <summary>创建根对象（无父级，位于世界原点）。</summary>
    /// <param name="name">对象名称</param>
    public GameObject(string name = "GameObject")
    {
        Name = name;
        Transform = new Transform((GameObject)this);
    }

    /// <summary>创建绑定场景上下文的根对象（Scene.CreateGameObject 内部使用）。</summary>
    /// <param name="name">对象名称</param>
    /// <param name="context">场景上下文（可为 null）</param>
    internal GameObject(string name, SceneContext? context)
    {
        Name = name;
        Context = context;
        Transform = new Transform((GameObject)this);
    }

    /// <summary>创建挂载于指定父 Transform 下的子对象。</summary>
    /// <param name="parent">父级 Transform（构造即建立父子关系）</param>
    /// <param name="name">对象名称</param>
    public GameObject(Transform parent, string name = "GameObject")
    {
        Name = name;
        Transform = new Transform((GameObject)this, parent);
    }

    /// <summary>
    /// 挂载组件并返回其实例。
    /// </summary>
    /// <param name="c">组件实例（同实例重复添加或已挂载于其他宿主时抛异常）</param>
    /// <param name="registry">组件注册表；null 时走 Services 回退链（SceneManager 注册表）</param>
    /// <returns>已挂载的组件实例</returns>
    /// <exception cref="InvalidOperationException">同实例重复添加，或组件已挂载于其他 GameObject</exception>
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

    /// <summary>创建并挂载指定类型的组件（AddComponent(new T()) 的便捷形式）。</summary>
    /// <param name="registry">组件注册表；null 时走 Services 回退链（SceneManager 注册表）</param>
    /// <returns>已挂载的新建组件实例</returns>
    public T AddComponent<T>(ComponentRegistry? registry = null)
        where T : Component, new()
    {
        return (T)AddComponent(new T(), registry);
    }

    /// <summary>
    /// 组件工厂：挂载 → OnAwake → RecomputeActiveState(Enable) → 注册。
    /// </summary>
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

        // 注册目标优先级：显式 registry → 场景上下文注册表 → Services 回退链（协调裁决 C1，
        // 兼容旧测试语义；阶段 4 移除回退后仅显式/上下文路径生效）
        var target = registry
            ?? Context?.Registry
            ?? (Services.TryGet<SceneManager>(out var sm) ? sm?.Registry : null);
        target?.Register(c);

        if (LogConfig.Lifecycle)
            Log.Info($"[Lifecycle] Added component {c.GetType().Name} to '{Name}'");
    }

    /// <summary>按类型查找首个已挂载组件（线性扫描）。</summary>
    /// <returns>匹配的组件实例；未找到返回 null</returns>
    public T? GetComponent<T>()
        where T : Component
    {
        foreach (var c in _components)
            if (c is T t)
                return t;

        return null;
    }

    /// <summary>
    /// 移除首个匹配类型的组件：立即从组件集合摘除并触发 OnDisable（若启用），
    /// OnDestroy 与注销经帧末销毁队列（CommitPending）执行。
    /// </summary>
    /// <param name="registry">组件注册表；null 时走 Services 回退链</param>
    /// <returns>是否成功移除（类型未挂载返回 false）</returns>
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
}
