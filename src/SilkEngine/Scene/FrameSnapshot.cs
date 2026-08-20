using System.Collections.Generic;
using SilkEngine.Core;

namespace SilkEngine.Scene;

public sealed class ComponentGroup
{
    public System.Type ComponentType { get; init; } = null!;
    public List<Component> Components { get; init; } = [];
}

public sealed class FrameSnapshot
{
    public long FrameCount { get; internal set; }
    public SilkEngine.Scene.Scene? ActiveScene { get; internal set; }
    internal List<ComponentGroup> Groups { get; } = [];

    /// <summary>MonoBehaviour 基类索引视图（按具体类型分组，派发遍历用；快照构建时复制）。</summary>
    internal List<List<MonoBehaviour>> MbGroups { get; } = [];

    /// <summary>类型化组件缓存：键=查询类型，值=所属 ComponentGroup 与缓存 List（组实例变化即失效，双缓冲重建安全）。</summary>
    private readonly Dictionary<System.Type, (ComponentGroup Group, object List)> _componentCache = new();

    /// <summary>
    /// 获取指定类型的组件列表（快照内缓存：同快照重复调用返回同实例 List；快照重建后自然失效）
    /// </summary>
    public IReadOnlyList<T> GetComponents<T>()
        where T : Component
    {
        foreach (var g in Groups)
        {
            if (g.ComponentType != typeof(T))
                continue;
            if (_componentCache.TryGetValue(typeof(T), out var entry) && entry.Group == g)
                return (IReadOnlyList<T>)entry.List;
            var list = new List<T>(g.Components.Count);
            foreach (var c in g.Components)
                list.Add((T)c);
            _componentCache[typeof(T)] = (g, list);
            return list;
        }
        return System.Array.Empty<T>();
    }
}

[Service(1)]
internal sealed class FrameSnapshotManager
{
    private FrameSnapshot _front = new();
    private FrameSnapshot _back = new();

    public FrameSnapshot Current { get; private set; }

    public FrameSnapshotManager() => Current = _front;

    internal void CommitPending(
        ComponentRegistry registry,
        List<SceneManager.DestroyEntry> destroys,
        SilkEngine.Scene.Scene? activeScene,
        float deltaTime
    )
    {
        for (int i = destroys.Count - 1; i >= 0; i--)
        {
            var e = destroys[i];
            e.Delay -= deltaTime;
            if (e.Delay > 0)
            {
                destroys[i] = e;
                continue;
            }

            if (e.Target is Component c && !c._destroyed)
            {
                c.OnDestroy();
                c._destroyed = true;
                registry.Unregister(c);
                c.GameObject._components.Remove(c);
            }
            else if (e.Target is GameObject go)
            {
                if (go._destroyed)
                {
                    destroys.RemoveAt(i); // 幂等命中：先摘除再跳过（否则条目滞留队列）
                    continue;
                }
                RemoveObjectRecursive(go, registry);
                go._destroyed = true;
                e.Origin?._rootObjects.Remove(go); // 原逻辑 activeScene 替换为条目记录的来源场景
            }
            if (LogConfig.Scene)
                Log.Info($"[Scene] Destroyed '{e.Target.Name}'");
            destroys.RemoveAt(i);
        }

        registry.ApplyPending();

        registry.BuildSnapshot(_back);

        _back.FrameCount++;
        _back.ActiveScene = activeScene;

        Current = _back;
        (_front, _back) = (_back, _front);
    }

    private static void RemoveObjectRecursive(GameObject go, ComponentRegistry registry)
    {
        if (go._destroyed)
            return;
        foreach (var c in go._components)
        {
            registry.Unregister(c);
            c.OnDestroy();
            c._destroyed = true;
        }
        go._components.Clear();
        foreach (var child in go.Transform.Children)
            if (child.GameObject is { } cgo)
                RemoveObjectRecursive(cgo, registry);
    }
}
