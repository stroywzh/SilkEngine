using System.Collections.Generic;
using SilkEngine.Core;

namespace SilkEngine.Scene;

/// <summary>按具体类型分组的组件集合（快照构建时复制引用列表，与实时注册表物理隔离）。</summary>
public sealed class ComponentGroup
{
    /// <summary>组对应的具体组件类型。</summary>
    public System.Type ComponentType { get; init; } = null!;

    /// <summary>该类型的组件实例列表（快照构建时的引用副本）。</summary>
    public List<Component> Components { get; init; } = [];
}

/// <summary>
/// 双缓冲帧快照（帧原子性核心）：派发统一读当前快照 —— 帧内注册/销毁变更不即时可见，
/// 待帧末 CommitPending 销毁 + ApplyPending + 重建写侧并 swap 后，下一帧派发读取（B.1 后语义）。
/// </summary>
public sealed class FrameSnapshot
{
    /// <summary>快照对应帧计数（swap 时递增）。</summary>
    internal long FrameCount { get; set; }

    /// <summary>快照生成时的活动场景。</summary>
    internal SilkEngine.Scene.Scene? ActiveScene { get; set; }

    /// <summary>类型分组组件列表（快照构建时复制）。</summary>
    internal List<ComponentGroup> Groups { get; } = [];

    /// <summary>MonoBehaviour 基类索引视图（按具体类型分组，派发遍历用；快照构建时复制）。</summary>
    internal List<List<MonoBehaviour>> MbGroups { get; } = [];

    /// <summary>类型化组件缓存：键=查询类型，值=所属 ComponentGroup 与缓存 List（组实例变化即失效，双缓冲重建安全）。</summary>
    private readonly Dictionary<System.Type, (ComponentGroup Group, object List)> _componentCache = new();

    /// <summary>
    /// 获取指定类型的组件列表（快照内缓存：同快照重复调用返回同实例 List；快照重建后自然失效）。
    /// </summary>
    /// <returns>该类型的组件列表；无匹配类型时为空数组（非 null）</returns>
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

/// <summary>
/// 双缓冲快照管理器（[Service(1)] 自动注册）：帧末 CommitPending 统一执行
/// 销毁队列（OnDestroy + Unregister + 场景容器摘除）→ 注册 ApplyPending → 重建写侧快照 → swap（读侧原子切换）。
/// </summary>
[Service(1)]
internal sealed class FrameSnapshotManager
{
    private FrameSnapshot _front = new();
    private FrameSnapshot _back = new();

    /// <summary>当前读侧快照（派发消费；CommitPending swap 后更新）。</summary>
    public FrameSnapshot Current { get; private set; }

    public FrameSnapshotManager() => Current = _front;

    /// <summary>
    /// 帧末提交：按延迟到期顺序处理销毁队列（组件 OnDestroy + Unregister + 摘除；对象递归销毁 + 场景摘除，幂等），
    /// 随后 ApplyPending 注册、重建写侧快照并 swap。
    /// </summary>
    /// <param name="registry">组件注册表（注销与登记目标）</param>
    /// <param name="destroys">帧内累积的销毁队列（SceneManager._destroyQueue）</param>
    /// <param name="activeScene">当前活动场景（写入快照）</param>
    /// <param name="deltaTime">本帧增量时间（秒，用于销毁延迟倒计时）</param>
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
