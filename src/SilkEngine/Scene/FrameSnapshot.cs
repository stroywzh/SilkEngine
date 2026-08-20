using System.Collections.Generic;
using System.Linq;
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

    public IReadOnlyList<T> GetComponents<T>()
        where T : Component
    {
        foreach (var g in Groups)
            if (g.ComponentType == typeof(T))
                return g.Components as IReadOnlyList<T> ?? g.Components.Cast<T>().ToList();
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

        registry.RefreshSnapshot(_back);

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
