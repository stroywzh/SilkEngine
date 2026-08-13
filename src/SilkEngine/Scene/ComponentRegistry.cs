using System;
using System.Collections.Generic;
using System.Linq;
using SilkEngine.Core;

namespace SilkEngine.Scene;

public sealed class ComponentRegistry
{
    private readonly Dictionary<Type, ComponentGroup> _groups = new();
    private readonly List<Component> _pendingAdds = [];

    public void Register(Component c)
    {
        if (_pendingAdds.Contains(c))
            return;
        var t = c.GetType();
        if (_groups.TryGetValue(t, out var g) && g.Components.Contains(c))
            return;
        _pendingAdds.Add(c);
    }

    public void Unregister(Component c)
    {
        _pendingAdds.Remove(c);
        if (_groups.TryGetValue(c.GetType(), out var g))
            g.Components.Remove(c);
    }

    public void ApplyPending()
    {
        foreach (var c in _pendingAdds)
        {
            var t = c.GetType();
            if (!_groups.TryGetValue(t, out var g))
            {
                g = new ComponentGroup { ComponentType = t };
                _groups[t] = g;
            }
            g.Components.Add(c);
        }
        _pendingAdds.Clear();
    }

    public void RefreshSnapshot(FrameSnapshot snapshot)
    {
        snapshot.Groups.Clear();
        snapshot.Groups.AddRange(_groups.Values); // 引用既有分组，零分配
    }

    public IReadOnlyList<T> GetOfType<T>() where T : Component
    {
        if (_groups.TryGetValue(typeof(T), out var g))
            return g.Components as IReadOnlyList<T> ?? g.Components.Cast<T>().ToList();
        return System.Array.Empty<T>();
    }
}
