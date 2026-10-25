using SilkEngine.Runtime;

namespace SilkEngine.Core;

/// <summary>
/// Base class for engine managers that are driven every frame.
/// <para>
/// Owns the process-wide instance: creation is a factory-based <see cref="Lazy{T}"/>, so the
/// constructor only initialises fields and never registers with the update driver or calls
/// virtual hooks.
/// <br/>Registration calls: Factory <see cref="Lazy{T}"/> constructor -&gt; <see cref="Bootstrap()"/>
/// -&gt; <see cref="Initialize"/> after the constructor has returned.
/// </para>
/// <para>
/// The <c>new()</c> constraint will makes a missing parameterless error.
/// </para>
/// </summary>
/// <typeparam name="TSelf">The concrete manager type (CRTP self reference).</typeparam>
internal abstract class ManagerBase<TSelf> : IUpdatable
    where TSelf : ManagerBase<TSelf>, new()
{
    private static readonly Lazy<TSelf> s_instance = new(static () =>
    {
        var instance = new TSelf();
        instance.Bootstrap();
        return instance;
    });

    /// <summary>The process-wide instance, created on first access.</summary>
    internal static TSelf Instance => s_instance.Value;

    void IUpdatable.UpdateInternal() => OnUpdate();

    void IUpdatable.LateUpdateInternal() => OnLateUpdate();

    void IUpdatable.FixedUpdateInternal() => OnFixedUpdate();

    /// <summary>
    /// Runs exactly once per instance, immediately after the constructor returns: registers the
    /// manager with the <see cref="UpdateScheduler"/>, then calls <see cref="Initialize"/>.
    /// </summary>
    /// <remarks>Called by the <see cref="Instance"/> factory only; never call it directly.</remarks>
    private void Bootstrap()
    {
        UpdateScheduler.Register(this);
        Initialize();
    }

    /// <summary>Initialises the manager state. Runs after the constructor, before the first update.</summary>
    protected virtual void Initialize() { }

    /// <summary>Called once per fixed-step tick.</summary>
    protected virtual void OnFixedUpdate() { }

    /// <summary>Called once per frame, after all updates.</summary>
    protected virtual void OnLateUpdate() { }

    /// <summary>Called once per frame.</summary>
    protected virtual void OnUpdate() { }
}
