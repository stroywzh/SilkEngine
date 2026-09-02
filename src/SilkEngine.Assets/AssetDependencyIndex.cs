namespace SilkEngine.Assets;

/// <summary>
/// 资产依赖索引：正向（资产 → 其依赖）与反向（依赖 → 依赖方）内存索引。
/// <see cref="ReplaceDependencies"/> 幂等替换某资产的依赖边（先清旧边再建新边）；
/// <see cref="InvalidateCascade"/> 沿反向边 BFS 返回受级联失效影响的资产集合（去重、不含种子自身）。
/// 由 Pipeline 在 Main/FrameCommit 阶段写入，查询方多为 Main 域（内部锁保护，兼容测试多线程）。
/// </summary>
public sealed class AssetDependencyIndex
{
    private readonly object _gate = new();
    private readonly Dictionary<AssetId, List<AssetId>> _forward = [];
    private readonly Dictionary<AssetId, HashSet<AssetId>> _reverse = [];

    /// <summary>幂等替换指定资产的依赖边：移除旧边并重建正向/反向索引；dependencies 为空时仅清除旧边。</summary>
    /// <param name="assetId">依赖方资产标识</param>
    /// <param name="dependencies">新的依赖资产标识列表</param>
    public void ReplaceDependencies(AssetId assetId, IReadOnlyList<AssetId> dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        lock (_gate)
        {
            if (_forward.Remove(assetId, out var previous))
            {
                foreach (var dependency in previous)
                    RemoveReverse(dependency, assetId);
            }
            if (dependencies.Count == 0)
                return;
            var list = new List<AssetId>(dependencies);
            _forward[assetId] = list;
            foreach (var dependency in list)
            {
                if (!_reverse.TryGetValue(dependency, out var dependents))
                    _reverse[dependency] = dependents = [];
                dependents.Add(assetId);
            }
        }
    }

    /// <summary>查询指定资产的直接依赖列表（未登记返回空列表）。</summary>
    /// <param name="assetId">资产标识</param>
    /// <returns>直接依赖的资产标识列表</returns>
    public IReadOnlyList<AssetId> GetDependencies(AssetId assetId)
    {
        lock (_gate)
            return _forward.TryGetValue(assetId, out var dependencies) ? [.. dependencies] : [];
    }

    /// <summary>查询直接依赖指定资产的资产列表（反向边；未登记返回空列表）。</summary>
    /// <param name="dependencyId">被依赖的资产标识</param>
    /// <returns>直接依赖方的资产标识列表</returns>
    public IReadOnlyList<AssetId> GetDependents(AssetId dependencyId)
    {
        lock (_gate)
            return _reverse.TryGetValue(dependencyId, out var dependents) ? [.. dependents] : [];
    }

    /// <summary>
    /// 级联失效查询：沿反向边 BFS 收集因指定依赖资产失效而受影响的所有资产（传递闭包，去重）。
    /// 返回集合不含种子资产自身——种子资产的失效由调用方显式处理。
    /// </summary>
    /// <param name="dependencyId">被失效的依赖资产标识</param>
    /// <returns>受级联影响的资产标识列表（BFS 顺序）</returns>
    public IReadOnlyList<AssetId> InvalidateCascade(AssetId dependencyId)
    {
        lock (_gate)
        {
            var affected = new List<AssetId>();
            var visited = new HashSet<AssetId> { dependencyId };
            var queue = new Queue<AssetId>();
            if (_reverse.TryGetValue(dependencyId, out var direct))
            {
                foreach (var dependent in direct)
                    queue.Enqueue(dependent);
            }
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!visited.Add(current))
                    continue;
                affected.Add(current);
                if (_reverse.TryGetValue(current, out var next))
                {
                    foreach (var dependent in next)
                    {
                        if (!visited.Contains(dependent))
                            queue.Enqueue(dependent);
                    }
                }
            }
            return affected;
        }
    }

    private void RemoveReverse(AssetId dependencyId, AssetId assetId)
    {
        if (!_reverse.TryGetValue(dependencyId, out var dependents))
            return;
        dependents.Remove(assetId);
        if (dependents.Count == 0)
            _reverse.Remove(dependencyId);
    }
}