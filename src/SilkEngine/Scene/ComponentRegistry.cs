using System;
using System.Collections.Generic;
using System.Linq;
using SilkEngine.Core;

namespace SilkEngine.Scene;

/// <summary>
/// 类型索引组件注册表（[Service(1)] 自动注册）：Register/Unregister 为延迟语义 ——
/// 变更先进 pending 队列，ApplyPending 与快照重建（BuildSnapshot）统一在帧末提交时生效，
/// 保证帧内派发读到的始终是稳定的双缓冲快照。
/// 组件 → 分组反向索引（引用相等）提供 O(1) 反查：Register 去重、Unregister 摘组、Contains
/// 均直查索引，消除批量场景下分组列表线性扫描导致的 O(n²) 开销。
/// </summary>
[Service(1)]
public sealed class ComponentRegistry
{
    private readonly Dictionary<Type, ComponentGroup> _groups = new();
    private readonly Dictionary<Type, List<MonoBehaviour>> _mbIndex = new();
    private readonly Dictionary<Component, ComponentGroup> _reverse = new(ReferenceEqualityComparer.Instance);
    private readonly List<Component> _pendingAdds = [];

    /// <summary>登记组件（延迟：加入 pending 队列，ApplyPending 时生效；重复登记幂等）。</summary>
    /// <param name="c">要登记的组件</param>
    public void Register(Component c)
    {
        if (_pendingAdds.Contains(c) || _reverse.ContainsKey(c))
            return;
        _pendingAdds.Add(c);
    }

    /// <summary>立即注销组件：经反向索引 O(1) 摘除分组（空组/空索引同步移除）并清除索引项。</summary>
    /// <param name="c">要注销的组件</param>
    public void Unregister(Component c)
    {
        _pendingAdds.Remove(c);
        if (_reverse.Remove(c, out var g))
        {
            g.Components.Remove(c);
            if (g.Components.Count == 0)
                _groups.Remove(c.GetType());
        }
        if (c is MonoBehaviour mb && _mbIndex.TryGetValue(c.GetType(), out var mbList))
        {
            mbList.Remove(mb);
            if (mbList.Count == 0)
                _mbIndex.Remove(c.GetType());
        }
    }

    /// <summary>提交 pending 队列：全部待登记组件落入类型组、反向索引与基类索引（帧末由 CommitPending 调用）。</summary>
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
            _reverse[c] = g;
            if (c is MonoBehaviour mb)
            {
                if (!_mbIndex.TryGetValue(t, out var list))
                    _mbIndex[t] = list = new List<MonoBehaviour>();
                list.Add(mb);
            }
        }
        _pendingAdds.Clear();
    }

    /// <summary>构建复制型快照：组件/基类索引均复制引用列表，快照与实时注册表物理隔离（帧原子性）。</summary>
    internal void BuildSnapshot(FrameSnapshot snapshot)
    {
        snapshot.Groups.Clear();
        foreach (var g in _groups.Values)
            snapshot.Groups.Add(new ComponentGroup
            {
                ComponentType = g.ComponentType,
                Components = [.. g.Components],
            });
        snapshot.MbGroups.Clear();
        foreach (var list in _mbIndex.Values)
            snapshot.MbGroups.Add([.. list]);
    }

    /// <summary>按具体类型查询已注册组件列表（类型组直查；未注册返回空数组）。</summary>
    /// <returns>该类型的组件列表；无匹配类型时为空数组（非 null）</returns>
    public IReadOnlyList<T> GetOfType<T>() where T : Component
    {
        if (_groups.TryGetValue(typeof(T), out var g))
            return g.Components as IReadOnlyList<T> ?? g.Components.Cast<T>().ToList();
        return System.Array.Empty<T>();
    }

    /// <summary>
    /// 反查：组件是否已登记（pending 未应用或已生效均返回 true；引用相等）。
    /// 反向索引 O(1) 直查，供测试与未来消费；批量注册/注销场景避免分组列表线性扫描。
    /// </summary>
    /// <param name="c">要查询的组件</param>
    /// <returns>已登记返回 true；未登记或已注销返回 false</returns>
    internal bool Contains(Component c) => _reverse.ContainsKey(c) || _pendingAdds.Contains(c);

    /// <summary>MonoBehaviour 基类索引：按具体类型归类的列表视图（SceneManager 派发消费，零分配）。</summary>
    internal IEnumerable<List<MonoBehaviour>> MonoBehaviourGroups => _mbIndex.Values;
}
