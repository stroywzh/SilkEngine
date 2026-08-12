using System.Collections.Generic;
using System.Linq;

namespace SilkEngine;

public sealed class ComponentGroup
{
    public System.Type ComponentType { get; init; } = null!;
    public List<Component> Components { get; init; } = [];
}

public sealed class FrameSnapshot
{
    public long FrameCount { get; internal set; }
    public Scene? ActiveScene { get; internal set; }
    internal List<ComponentGroup> Groups { get; } = [];

    public IReadOnlyList<T> GetComponents<T>() where T : Component
    {
        var group = Groups.Find(g => g.ComponentType == typeof(T));
        return group == null
            ? System.Array.Empty<T>()
            : group.Components.Cast<T>().ToList().AsReadOnly();
    }
}

public sealed class FrameSnapshotManager
{
    private FrameSnapshot _front = new();
    private FrameSnapshot _back = new();

    public FrameSnapshot Current { get; private set; }

    public FrameSnapshotManager() => Current = _front;

    internal void CommitPending(
        ComponentRegistry registry,
        List<SceneManager.DestroyEntry> destroys,
        Scene? activeScene)
    {
        foreach (var e in destroys)
        {
            if (e.Target is MonoBehaviour mb)
                mb.OnDestroy();
            if (e.Target is GameObject go)
                activeScene?._rootObjects.Remove(go);
        }
        destroys.Clear();

        registry.ApplyPending();

        registry.RefreshSnapshot(_back);

        _back.FrameCount++;
        _back.ActiveScene = activeScene;

        Current = _back;
        (_front, _back) = (_back, _front);
    }
}
