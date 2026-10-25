namespace SilkEngine.Scripting;

/// <summary>
/// Base class for a behaviour carrier attached to an entity.
/// </summary>
/// <remarks>
/// <para>Two layers. Layer A is the business surface: the eight Unity-like hooks
/// (<see cref="Awake"/>, <see cref="Start"/>, <see cref="Update"/>, <see cref="LateUpdate"/>,
/// <see cref="FixedUpdate"/>, <see cref="OnEnable"/>, <see cref="OnDisable"/>, <see cref="OnDestroy"/>),
/// <c>protected virtual</c>, never called by user code.</para>
/// <para>Only the engine calls method named as <c> bussiness name + "Internal"</c>, they migrate state first and 
/// then invoke the business hook.</para>
/// <para>All base implementations are empty, so overrides could not call <c>base.*</c>.</para>
/// </remarks>
public abstract class Component
{
    private GameObject _gameObject;
    private bool _enabled = true;
    private bool _destroyed;

    /// <summary>Creates a component bound to <paramref name="owner"/>.</summary>
    /// <param name="owner">Entity that owns the component; must not be null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    protected Component(GameObject owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _gameObject = owner;
    }

    /// <summary>The entity that owns this component.</summary>
    public GameObject GameObject => _gameObject;

    /// <summary>Enable flag of this component.</summary>
    /// <remarks>
    /// <see cref="OnEnable"/> and <see cref="OnDisable"/> fire only when the value actually changes,
    /// the component is attached, and the owning entity is active in the hierarchy. Setting the flag
    /// on an inactive entity only changes the state (no callback). Components whose
    /// <see cref="IsDisableAllowed"/> returns <c>false"/> keep this flag set.
    /// </remarks>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
                return;

            if (!IsDisableAllowed() && !value)
            {
                Log.Debug("这个组件不允许被禁用");
                return;
            }

            // Inactive hierarchy: the state still changes, but no lifecycle callback fires.
            if (!_gameObject.ActiveInHierarchy)
            {
                _enabled = value;
                return;
            }

            if (value)
            {
                OnEnableInternal();
            }
            else
            {
                OnDisableInternal();
            }
        }
    }

    /// <summary>Whether this component has already received its destroy notification.</summary>
    /// <remarks>Once destroyed a component receives no further dispatch; the flag also makes
    /// <see cref="OnDestroyInternal"/> idempotent when both detach and entity teardown reach it.</remarks>
    internal bool IsDestroyed => _destroyed;

    /// <summary>Whether this component is enabled and its owner is active in the hierarchy.</summary>
    /// <remarks>
    /// The name is deliberately different from the hierarchy flag of <see cref="GameObject"/>: the
    /// two members used to share one name while meaning different things. The owner is always present
    /// (the constructor rejects <c>null</c>), so no unbound state exists to report (CORE-N-10).
    /// </remarks>
    public bool IsActiveAndEnabled => _enabled && _gameObject.ActiveInHierarchy;

    #region Hooks

    /// <summary>Called once when the component is attached if GameObject is active.</summary>
    /// <remarks>Attaching to an already created entity invokes it immediately.</remarks>
    protected virtual void Awake() { }

    /// <summary>Called once before the first dispatch of the owning entity.</summary>
    protected virtual void Start() { }

    /// <summary>Called once per frame.</summary>
    /// <remarks>Use <see cref="FixedUpdate"/> for logic that must run on a fixed step.</remarks>
    protected virtual void Update() { }

    /// <summary>Called once per frame, after <see cref="Update"/> and after this frame's rendering.</summary>
    /// <remarks>Intended for logic that depends on final positions, such as camera follow.</remarks>
    protected virtual void LateUpdate() { }

    /// <summary>Called on a fixed step; may run zero or several times per frame.</summary>
    protected virtual void FixedUpdate() { }

    /// <summary>Called when the effective active state changes from <c>false</c> to <c>true</c>.</summary>
    protected virtual void OnEnable() { }

    /// <summary>Called when the effective active state changes from <c>true</c> to <c>false</c>.</summary>
    protected virtual void OnDisable() { }

    /// <summary>Called when the component is detached or its owning entity is destroyed.</summary>
    /// <remarks><see cref="GameObject"/> is still usable here.</remarks>
    protected internal virtual void OnDestroy() { }

    /// <summary>
    /// Whether <see cref="Enabled"/> may be set to <c>false</c>. Components that must stay enabled
    /// (for example <see cref="Transform"/>) override this and return <c>false</c>.
    /// </summary>
    protected virtual bool IsDisableAllowed() => true;

    #endregion

    #region Internal notifications

    /// <summary>Engine-only mount notification; runs <see cref="Awake"/>.</summary>
    /// <remarks>Called by <see cref="GameObject.AddComponent{T}"/> right after attachment.</remarks>
    internal void AwakeInternal() => Awake();

    /// <summary>Engine-only first-dispatch notification; gated by <see cref="IsActiveAndEnabled"/>.</summary>
    internal void StartInternal()
    {
        if (!IsActiveAndEnabled)
            return;

        Start();
    }

    /// <summary>Engine-only per-frame notification; gated by <see cref="IsActiveAndEnabled"/>.</summary>
    internal void UpdateInternal()
    {
        if (!IsActiveAndEnabled)
            return;

        Update();
    }

    /// <summary>Engine-only late-frame notification; gated by <see cref="IsActiveAndEnabled"/>.</summary>
    internal void LateUpdateInternal()
    {
        if (!IsActiveAndEnabled)
            return;

        LateUpdate();
    }

    /// <summary>Engine-only fixed-step notification; gated by <see cref="IsActiveAndEnabled"/>.</summary>
    internal void FixedUpdateInternal()
    {
        if (!IsActiveAndEnabled)
            return;

        FixedUpdate();
    }

    /// <summary>Engine-only enable notification: migrates <see cref="Enabled"/> first, then runs the hook.</summary>
    internal void OnEnableInternal()
    {
        _enabled = true;
        OnEnable();
    }

    /// <summary>Engine-only disable notification: migrates <see cref="Enabled"/> first, then runs the hook.</summary>
    internal void OnDisableInternal()
    {
        _enabled = false;
        OnDisable();
    }

    /// <summary>Engine-only detach/destroy notification; runs <see cref="OnDestroy"/> once.</summary>
    internal void OnDestroyInternal()
    {
        if (_destroyed)
            return;

        _destroyed = true;
        OnDestroy();
    }

    /// <summary>
    /// Engine-only hierarchy activation notification: the owner (or an ancestor) became active, so an
    /// enabled component enters its enable state. <see cref="Enabled"/> is not modified, because the
    /// component's own flag is independent of the hierarchy.
    /// </summary>
    internal void ActivateInternal()
    {
        if (!_enabled || _destroyed)
            return;

        OnEnable();
    }

    /// <summary>
    /// Engine-only hierarchy deactivation notification: the owner (or an ancestor) became inactive.
    /// <see cref="Enabled"/> is not modified; <see cref="OnDisable"/> fires only for enabled components,
    /// mirroring the enable path.
    /// </summary>
    internal void DeactivateInternal()
    {
        if (!_enabled || _destroyed)
            return;

        OnDisable();
    }

    #endregion
}
