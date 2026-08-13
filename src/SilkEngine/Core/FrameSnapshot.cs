using System.Collections.Generic;
using System.Linq;
using SilkEngine.Scene;

namespace SilkEngine.Core;

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
                RemoveObjectRecursive(go, registry);
                go._destroyed = true;
                activeScene?._rootObjects.Remove(go);
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
