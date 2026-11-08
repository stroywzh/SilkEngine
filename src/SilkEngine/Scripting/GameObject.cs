using SilkEngine.Core.ObjectSystem;
using SilkEngine.Internal;

namespace SilkEngine.Scripting;

/// <summary>
/// Entity: the host of components and of exactly one <see cref="Transform"/>.
/// </summary>
/// <remarks>
/// Instances are created by the internal factory only (<see cref="Create"/>); the public
/// parameterless constructor does not exist, so an entity always owns a valid
/// <see cref="ObjectId"/> or is explicitly uninitialised.
/// </remarks>
public sealed class GameObject
{
    /// <summary>Identity of this entity; fixed at construction, with no external <c>init</c>.</summary>
    public ObjectId Id { get; }

    /// <summary>Lifetime scope fixed at allocation time; never changes after registration.</summary>
    internal ObjectScope Scope { get; }
    private Transform _transform;
    private readonly List<Component> _components = new();
    internal bool Initialized { get; init; }
    private bool _isActive = true;
    private bool _firstUpdate = true;

    // Dispatch snapshot: rebuilt only when the component list changes, so a steady frame allocates
    // nothing. Attach/remove bumps the version, which also gives the snapshot semantics documented on
    // ForEachEnabledComponent (a component added mid-dispatch runs from the next frame on).
    private Component[] _dispatchSnapshot = Array.Empty<Component>();
    private bool _snapDirty = false;

    /// <summary>Whether this entity and every ancestor are active.</summary>
    public bool ActiveInHierarchy
    {
        get
        {
            var parent = _transform.Parent is not null
                ? _transform.Parent.IsActiveAndEnabled
                : true;
            var activeState = _isActive;
            return activeState && parent;
        }
    }

    /// <summary>Parent transform, or <c>null</c> when this entity is at the hierarchy root.</summary>
    public Transform? Parent => _transform.Parent ?? null;

    /// <summary>The transform owned by this entity.</summary>
    public Transform Transform => _transform;

    /// <summary>Internal construction entry: only the <see cref="ObjectManager"/> factory calls it.</summary>
    internal GameObject(ObjectCreateToken token)
    {
        Id = token.Id;
        Scope = token.Scope;

        if (!Id.IsNone)
        {
            _transform = Transform.Create(this);
            Initialized = true;
        }
        else
        {
            _transform = default!;
            Initialized = false;
        }
    }

    /// <summary>Activates or deactivates this entity (the parent chain is not changed).</summary>
    /// <param name="value">New local active state.</param>
    /// <remarks>
    /// The whole subtree is diffed against the previous effective state: every enabled component that
    /// crosses a <c>false</c>→<c>true</c> boundary receives <c>OnEnable</c>, and every enabled component
    /// that crosses <c>true</c>→<c>false</c> receives <c>OnDisable</c>. Toggling an entity that is
    /// already inactive in the hierarchy dispatches nothing, because its effective state does not change.
    /// </remarks>
    public void SetActive(bool value)
    {
        if (value == _isActive)
            return;

        var wasActive = ActiveInHierarchy;
        _isActive = value;
        var isActive = ActiveInHierarchy;

        if (wasActive != isActive)
        {
            ApplyActiveState(wasActive, isActive);
        }
    }

    /// <summary>Creates an entity, optionally copying the local pose of an existing one.</summary>
    /// <param name="origin">Optional entity used as the construction source.</param>
    /// <returns>The new entity; it is already registered with the object manager.</returns>
    /// <remarks>
    /// TODO: Copy scope: all components include <see cref="Transform"/> will be copied.
    /// <br/>Now only copy <see cref="Transform"/>.
    /// </remarks>
    public static GameObject Create(GameObject? origin = null)
    {
        var entity = ObjectManager.Instance.Create();

        if (origin is not null)
        {
            entity.Transform.LocalPosition = origin.Transform.LocalPosition;
            entity.Transform.LocalRotation = origin.Transform.LocalRotation;
            entity.Transform.LocalScale = origin.Transform.LocalScale;
        }

        return entity;
    }

    /// <summary>Schedules the entity for destruction at the end of the frame.</summary>
    public static void Destroy(GameObject gameObject) => ObjectManager.Instance.Destroy(gameObject);

    /// <summary>Destroys the entity immediately, without waiting for the frame end.</summary>
    public static void DestroyImmediate(GameObject gameObject) =>
        ObjectManager.Instance.DestroyImmediate(gameObject);

    /// <summary>Looks up a registered entity by id.</summary>
    /// <returns>The entity, or <c>null</c> when the id is unknown or already released.</returns>
    public static GameObject? Find(ObjectId id) =>
        id.IsNone ? null
        : ObjectManager.Instance.TryGet(id, out var go) ? go
        : null;

    /// <summary>Creates a component and attaches it to this entity.</summary>
    /// <typeparam name="T">Component type with a public <c>(GameObject)</c> constructor.</typeparam>
    /// <returns>The attached component.</returns>
    /// <remarks>
    /// Call order: construct → attach → <c>AwakeInternal</c> → (when the entity is active)
    /// <c>OnEnableInternal</c> and other frame loop method.
    /// <br/>This is the only entry that creates components, so a component will never exists without an owner.
    /// </remarks>
    public T AddComponent<T>(T? origin = null)
        where T : Component
    {
        T component = ComponentFactory<T>.Create(this);
        if (origin is not null)
        {
            component.Enabled = origin.Enabled;
            //TODO: serialization copy; Enabled is a placeholder for now.
        }

        _components.Add(component);
        _snapDirty = true;

        component.AwakeInternal();
        if (ActiveInHierarchy)
        {
            component.OnEnableInternal();
        }

        return component;
    }

    /// <summary>Gets the first attached component of type <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">Component type to look for.</typeparam>
    /// <returns>The component, or <c>null</c> when none is attached.</returns>
    public T? GetComponent<T>()
        where T : Component
    {
        foreach (var component in _components)
        {
            if (component is T match)
                return match;
        }

        return null;
    }

    /// <summary>Removes an attached component and destroys it.</summary>
    /// <param name="component">Component to remove; a component of another entity is ignored.</param>
    /// <returns><c>true</c> when the component was attached and is now destroyed.</returns>
    /// <remarks>Removal runs <c>OnDestroyInternal</c> once; the component keeps a valid owner reference.</remarks>
    public bool RemoveComponent(Component component)
    {
        if (component is null)
            return false;
        if (!_components.Remove(component))
            return false;

        _snapDirty = true;
        component.OnDestroyInternal();
        return true;
    }

    #region UpdateHooks

    internal void UpdateInternal()
    {
        if (!ActiveInHierarchy)
            return;

        if (_firstUpdate)
        {
            _firstUpdate = false;
            DispatchStart();
        }

        ForEachEnabledComponent(static component => component.UpdateInternal());
    }

    internal void FixedUpdateInternal()
    {
        if (!ActiveInHierarchy)
            return;

        if (_firstUpdate)
            return;

        ForEachEnabledComponent(static component => component.FixedUpdateInternal());
    }

    internal void LateUpdateInternal()
    {
        if (!ActiveInHierarchy)
            return;

        if (_firstUpdate)
            return;

        ForEachEnabledComponent(static component => component.LateUpdateInternal());
    }

    /// <summary>Runs <paramref name="step"/> for every enabled component of the snapshot taken at dispatch start.</summary>
    /// <remarks>
    /// <para>
    /// A component destroyed during the dispatch (by <see cref="RemoveComponent"/> or entity teardown)
    /// is skipped: <see cref="Component.IsDestroyed"/> is checked per entry. The copy is cached and only
    /// rebuilt when the list changes, so a steady frame performs no allocation.
    /// </para>
    /// </remarks>
    internal void ForEachEnabledComponent(Action<Component> step)
    {
        ArgumentNullException.ThrowIfNull(step);

        var snapshot = DispatchSnapshot();
        for (var i = 0; i < snapshot.Length; i++)
        {
            var component = snapshot[i];
            if (component.IsDestroyed)
                continue;
            if (component.IsActiveAndEnabled)
            {
                step(component);
            }
        }
    }

    /// <summary>Gets the dispatch snapshot, rebuilding it only when the component list changed.</summary>
    private Component[] DispatchSnapshot()
    {
        if (_snapDirty)
        {
            _dispatchSnapshot = _components.ToArray();
            _snapDirty = false;
        }

        return _dispatchSnapshot;
    }

    /// <summary>First-frame dispatch: every enabled component receives its one-time start notification.</summary>
    private void DispatchStart() =>
        ForEachEnabledComponent(static component => component.StartInternal());

    #endregion

    #region LifeCycle

    /// <summary>
    /// Engine-only activation propagation: dispatches enable/disable where the effective state of this
    /// entity or one of its descendants crosses a boundary.
    /// </summary>
    /// <param name="wasActive">Effective state of this entity before the change.</param>
    /// <param name="isActive">Effective state of this entity after the change.</param>
    internal void ApplyActiveState(bool wasActive, bool isActive)
    {
        if (wasActive != isActive)
        {
            var snapshot = DispatchSnapshot();
            for (var i = 0; i < snapshot.Length; i++)
            {
                var component = snapshot[i];
                if (component.IsDestroyed)
                    continue;
                if (isActive)
                {
                    component.ActivateInternal();
                }
                else
                {
                    component.DeactivateInternal();
                }
            }
        }

        var children = _transform.Children;
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            var childSelfActive = child.GameObject._isActive;
            child.GameObject.ApplyActiveState(
                wasActive && childSelfActive,
                isActive && childSelfActive
            );
        }
    }

    /// <summary>Engine-only teardown: notifies every attached component once, before the entity leaves the store.</summary>
    /// <remarks>
    /// Called by <c>ObjectManager.RemoveGameObject</c> for both the deferred and the immediate path, so a
    /// destroyed entity reaches <c>OnDestroy</c> exactly like <see cref="RemoveComponent"/> does. The
    /// notification is idempotent per component (<see cref="Component.IsDestroyed"/>).
    /// </remarks>
    internal void DestroyComponents()
    {
        var snapshot = DispatchSnapshot();
        for (var i = 0; i < snapshot.Length; i++)
        {
            snapshot[i].OnDestroyInternal();
        }

        _components.Clear();
        _snapDirty = true;
    }

    #endregion
}
