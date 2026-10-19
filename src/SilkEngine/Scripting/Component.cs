namespace SilkEngine;

/// <summary>
/// Base class for a behaviour carrier attached to an entity.
/// </summary>
/// <remarks>
/// <para>Every hook is <c>protected internal virtual</c>. Override only what is needed; hooks that
/// are not overridden are still invoked by the <see cref="GameObject"/> dispatch loop, which costs
/// a negligible empty virtual call.</para>
/// <para>All base implementations are empty, so overrides must not call <c>base.*</c>.</para>
/// </remarks>
public abstract class Component
{
    private GameObject _gameObject;
    private bool _enabled = true;

    [Obsolete("Do NOT use this")]
    private Component()
    {
        // Reserved for the future component construction flow; an instance built here would be
        // unbound and unusable, which the flow is expected to prevent.
        _gameObject = null!;
    }

    /// <summary>Creates a component pre-bound to the entity that will own it.</summary>
    /// <param name="gameObject">Entity that will own the component.</param>
    /// <exception cref="ArgumentNullException"><paramref name="gameObject"/> is <c>null</c>.</exception>
    /// <remarks>
    /// This is the constructor the construction flow behind <see cref="GameObject.AddComponent{T}()"/>
    /// calls: the component is bound to its owning entity from construction. Constructing a component
    /// directly does not attach it; <see cref="GameObject.AddComponent{T}()"/> is the only attachment path.
    /// </remarks>
    protected Component(GameObject gameObject)
    {
        ArgumentNullException.ThrowIfNull(gameObject);
        _gameObject = gameObject;
    }

    /// <summary>Entity that owns this component. Guaranteed non-null once the component is attached.</summary>
    /// <returns>The owning <see cref="GameObject"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// The component has not been attached through <see cref="GameObject.AddComponent{T}()"/>, or it
    /// was removed and is now unbound.
    /// </exception>
    public GameObject gameObject =>
        _gameObject
        ?? throw new InvalidOperationException(
            $"Component {GetType().Name} is not attached to an entity yet; create it through GameObject.AddComponent."
        );

    internal GameObject? gameObjectOrNull => _gameObject;

    /// <summary>Binds the owning entity. Called only by <see cref="GameObject.AddComponent{T}()"/>.</summary>
    internal void Bind(GameObject owner) => _gameObject = owner;

    /// <summary>Clears the binding after the component was removed from its entity.</summary>
    internal void Unbind() => _gameObject = null!;

    /// <summary>Enable flag of this component.</summary>
    /// <remarks>
    /// <see cref="OnEnable"/> and <see cref="OnDisable"/> fire only when the value actually changes,
    /// the component is attached, and the owning entity is active in the hierarchy. Setting the flag
    /// before attachment (for example inside the constructor) is a no-op.
    /// </remarks>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
                return;

            _enabled = value;

            // Unbound components have no effective state to switch; only attached ones dispatch.
            if (_gameObject is null || !_gameObject.ActiveInHierarchy)
                return;

            if (value)
            {
                OnEnable();
            }
            else
            {
                OnDisable();
            }
        }
    }

    /// <summary>Whether this component is enabled and its owner is active in the hierarchy.</summary>
    /// <remarks>Returns <c>false</c> for an unbound component instead of throwing.</remarks>
    public bool ActiveInHierarchy => _enabled && _gameObject is not null && _gameObject.ActiveInHierarchy;

    #region Hooks

    /// <summary>Called once when the component is attached.</summary>
    /// <remarks>Attaching to an already created entity invokes it immediately.</remarks>
    protected internal virtual void Awake() { }

    /// <summary>Called once before the first dispatch of the owning entity.</summary>
    protected internal virtual void Start() { }

    /// <summary>Called once per frame.</summary>
    /// <remarks>Use <see cref="FixedUpdate"/> for logic that must run on a fixed step.</remarks>
    protected internal virtual void Update() { }

    /// <summary>Called once per frame, after <see cref="Update"/>.</summary>
    /// <remarks>Intended for logic that depends on final positions, such as camera follow.</remarks>
    protected internal virtual void LateUpdate() { }

    /// <summary>Called on a fixed step; may run zero or several times per frame.</summary>
    protected internal virtual void FixedUpdate() { }

    /// <summary>Called when the effective active state changes from <c>false</c> to <c>true</c>.</summary>
    protected internal virtual void OnEnable() { }

    /// <summary>Called when the effective active state changes from <c>true</c> to <c>false</c>.</summary>
    protected internal virtual void OnDisable() { }

    /// <summary>Called when the component is detached or its owning entity is destroyed.</summary>
    /// <remarks><see cref="gameObject"/> is still usable here.</remarks>
    protected internal virtual void OnDestroy() { }

    #endregion
}
