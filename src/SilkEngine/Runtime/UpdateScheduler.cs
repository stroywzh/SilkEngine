namespace SilkEngine.Runtime;

/// <summary>
/// The single internal dispatcher for <see cref="IUpdatable"/> targets, called once per frame by
/// <see cref="Host.MainLoop"/>.
/// </summary>
/// <remarks>
/// Targets run in ascending registration order (lower <c>order</c> first; equal orders keep
/// registration order). The type is <c>internal</c> on purpose: driving the update loop is an engine
/// responsibility, not a public extension point. Business per-frame logic runs through the
/// <c>Component</c> hooks instead.
/// </remarks>
internal static class UpdateScheduler
{
    private static readonly List<IUpdatable> _snapShot = new();
    private static readonly Dictionary<IUpdatable, int> _orders = new();
    private static bool _dirty = false;

    /// <summary>Registers a target; registering twice logs and keeps the first registration.</summary>
    /// <param name="target">Target to drive.</param>
    /// <param name="order">Execution order; lower values run first. Defaults to 0.</param>
    internal static void Register(IUpdatable target, int order = 0)
    {
        if (target is null)
            return;

        if (_orders.ContainsKey(target))
        {
            Log.Debug($"重复注册{target.GetType().FullName}");
            return;
        }

        _orders.Add(target, order);
        _dirty = true;
    }

    /// <summary>Removes a registered target; unknown targets are ignored.</summary>
    /// <param name="target">Target to stop driving.</param>
    internal static void Unregister(IUpdatable target)
    {
        if (target is null)
            return;

        if (!_orders.Remove(target))
        {
            Log.Debug($"无效注销{target.GetType().FullName}");
        }

        _dirty = true;
    }

    /// <summary>Snapshots the registered targets, ordered by their registration order, before a frame.</summary>
    internal static void RefreshSnapshot()
    {
        if (_dirty)
        {
            _snapShot.Clear();
            _snapShot.AddRange(_orders.OrderBy(pair => pair.Value).Select(pair => pair.Key));
        }
    }

    /// <summary>Dispatches one update step to every snapshot entry.</summary>
    internal static void DispatchUpdate()
    {
        foreach (var u in _snapShot)
        {
            u.UpdateInternal();
        }
    }

    /// <summary>Dispatches one late-update step to every snapshot entry.</summary>
    internal static void DispatchLateUpdate()
    {
        foreach (var u in _snapShot)
        {
            u.LateUpdateInternal();
        }
    }

    /// <summary>Dispatches one fixed-step update to every snapshot entry.</summary>
    internal static void DispatchFixedUpdate()
    {
        foreach (var u in _snapShot)
        {
            u.FixedUpdateInternal();
        }
    }
}
