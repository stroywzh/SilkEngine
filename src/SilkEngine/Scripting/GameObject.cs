using System.Diagnostics.CodeAnalysis;
using SilkEngine.Core.EngineObjcetSystem;
using SilkEngine.Internal;

namespace SilkEngine;

/// <summary>
/// Entity: the only object in the engine that owns an identity. It owns one <see cref="Transform"/>
/// and every attached <see cref="Component"/>.
/// </summary>
/// <remarks>
/// <para>The constructor is private; instances are created through
/// <see cref="Instantiate(GameObject)"/> </para>
/// <para>Index 0 of <see cref="Components"/> is always the <see cref="Transform"/>. It is owned by
/// the entity itself, is never cloned as a component and cannot be removed.</para>
/// </remarks>
public sealed class GameObject : ObjectBase
{
    private readonly List<Component> _components = new(4);
    private readonly List<Component> _pendingRemoval = new(1);
    private readonly Transform _transform;

    private bool _activeSelf = true;
    private bool _activeInHierarchy = true;
    private bool _awake;
    private bool _started;
    private bool _dispatching;

    private GameObject(Component[]? components = null)
        : base()
    {
        _transform = Transform.Create(this);
        AttachInternal(_transform); // index 0 is always the transform

        if (components is not null && components.Length > 0)
        {
            foreach (var component in components)
            {
                AttachInternal(component);
            }
        }
    }

    /// <summary>
    /// Transform of this GameObject
    /// </summary>
    public Transform Transform => _transform;

    /// <summary>All components in attachment order. Index 0 is always the <see cref="Transform"/>.</summary>
    public IReadOnlyList<Component> Components => _components;

    /// <summary>Own active flag, independent of the parent chain.</summary>
    public bool ActiveSelf => _activeSelf;

    /// <summary>Whether this entity and every ancestor entity are active. O(1), maintained by propagation.</summary>
    public bool ActiveInHierarchy => _activeInHierarchy;

    #region Activation

    /// <summary>Sets the own active flag and propagates the change to all components and child entities.</summary>
    /// <param name="value">New own active flag.</param>
    /// <remarks>
    /// Heavyweight operation: it walks the hierarchy and dispatches component callbacks, which is why
    /// it is a method rather than a property setter.
    /// </remarks>
    public void SetActive(bool value)
    {
        if (_activeSelf == value)
            return;

        _activeSelf = value;
        RefreshActivation(_transform.Parent?.gameObject?.ActiveInHierarchy ?? true);
    }

    internal void RefreshActivation(bool parentActive)
    {
        bool now = _activeSelf && parentActive;
        if (now == _activeInHierarchy)
            return;

        _activeInHierarchy = now;

        if (!_awake)
            return;

        for (int i = 0, count = _components.Count; i < count; i++)
        {
            var component = _components[i];
            if (!component.Enabled)
                continue;

            if (now)
            {
                component.OnEnable();
            }
            else
            {
                component.OnDisable();
            }
        }

        foreach (var child in _transform.Children)
        {
            child.gameObject?.RefreshActivation(now);
        }
    }

    #endregion

    #region Component

    /// <summary>Creates and attaches a component.</summary>
    /// <typeparam name="T">
    /// Component type with a public constructor taking a <see cref="GameObject"/>.
    /// </typeparam>
    /// <returns>The newly created component instance, already attached to this entity.</returns>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="T"/> is <see cref="Transform"/>, has no public constructor taking a
    /// <see cref="GameObject"/>, or this entity already has a component of that concrete type.
    /// </exception>
    /// <remarks>
    /// The component is constructed with this entity as its owner, so it is bound before
    /// <see cref="Component.Awake"/> runs. Each entity holds at most one component per concrete
    /// type; use <see cref="GetComponent{T}"/> to reach it again. Constructing a component directly
    /// does not attach it; this method is the only attachment path.
    /// </remarks>
    public T AddComponent<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T
    >()
        where T : Component
    {
        if (typeof(T) == typeof(Transform))
        {
            throw new InvalidOperationException(
                "The entity transform is owned by the entity and cannot be attached."
            );
        }

        var component = ComponentFactory<T>.Create(this);
        AttachInternal(component);
        return component;
    }

    /// <summary>Removes a component and immediately dispatches its OnDisable and OnDestroy.</summary>
    /// <param name="component">Component to remove.</param>
    /// <returns>
    /// <c>true</c> when the component belongs to this entity and was removed; always <c>false</c> for
    /// the <see cref="Transform"/>.
    /// </returns>
    /// <remarks>Safe to call from inside a dispatch: the list is compacted after the dispatch ends.</remarks>
    public bool RemoveComponent(Component component)
    {
        if (component is null || component is Transform)
            return false;

        if (!ReferenceEquals(component.gameObjectOrNull, this))
            return false;

        if (!_components.Contains(component))
            return false;

        DetachInternal(component);

        if (_dispatching)
        {
            _pendingRemoval.Add(component);
        }
        else
        {
            _components.Remove(component);
        }

        return true;
    }

    /// <summary>Returns the first component of type <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">Component type; <see cref="Transform"/> is included.</typeparam>
    /// <returns>The matching component, or <c>null</c> when absent. Linear scan, typically 3-10 components.</returns>
    public T? GetComponent<T>()
        where T : Component
    {
        for (int i = 0, count = _components.Count; i < count; i++)
        {
            if (_components[i] is T match)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>Returns the first component of type <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">Component type.</typeparam>
    /// <param name="component">The matching component, or <c>null</c> when absent.</param>
    /// <returns><c>true</c> when a component was found.</returns>
    public bool TryGetComponent<T>(out T? component)
        where T : Component
    {
        component = GetComponent<T>();
        return component is not null;
    }

    /// <summary>Destroys the entity and all of its components.</summary>
    /// <remarks>Component callbacks run in reverse attachment order.</remarks>
    public static void Destroy(GameObject gameObject)
    {
        EngineObjectManager.Instance.Destroy(gameObject);
    }

    #endregion

    #region Lifecycle

    /// <inheritdoc/>
    protected internal override void OnCreate()
    {
        base.OnCreate();
        _awake = true;

        int count = _components.Count;
        for (int i = 0; i < count; i++)
        {
            _components[i].Awake();
        }

        if (!_activeInHierarchy)
            return;

        for (int i = 0; i < count; i++)
        {
            if (_components[i].Enabled)
            {
                _components[i].OnEnable();
            }
        }
    }

    /// <inheritdoc/>
    protected internal override void OnDestroyCore()
    {
        for (int i = _components.Count - 1; i >= 0; i--)
        {
            var component = _components[i];

            if (_activeInHierarchy && component.Enabled)
            {
                component.OnDisable();
            }

            component.OnDestroy();
            // component.gameObject = null;
        }

        _components.Clear();
        _pendingRemoval.Clear();

        base.OnDestroyCore();
    }

    #endregion

    #region Dispatch
    // TODO(runtime hook-up): called by MainLoop once the design is frozen. Intentionally not wired yet.
    internal void DispatchUpdate()
    {
        if (!_activeInHierarchy)
            return;

        DispatchStart();
        DispatchAll(static component => component.Update());
    }

    internal void DispatchLateUpdate()
    {
        if (!_activeInHierarchy)
            return;

        DispatchStart();
        DispatchAll(static component => component.LateUpdate());
    }

    internal void DispatchFixedUpdate()
    {
        if (!_activeInHierarchy)
            return;

        DispatchStart();
        DispatchAll(static component => component.FixedUpdate());
    }

    private void DispatchStart()
    {
        if (_started)
            return;

        _started = true;

        for (int i = 0, count = _components.Count; i < count; i++)
        {
            var component = _components[i];
            if (ReferenceEquals(component.gameObjectOrNull, this))
            {
                component.Start();
            }
        }
    }

    private void DispatchAll(Action<Component> hook)
    {
        bool previous = _dispatching;
        _dispatching = true;

        try
        {
            for (int i = 0, count = _components.Count; i < count; i++)
            {
                var component = _components[i];

                if (!ReferenceEquals(component.gameObjectOrNull, this))
                    continue;

                if (component.Enabled)
                {
                    hook(component);
                }
            }
        }
        finally
        {
            _dispatching = previous;

            if (!_dispatching)
            {
                CompactRemoved();
            }
        }
    }

    private void CompactRemoved()
    {
        for (int i = _pendingRemoval.Count - 1; i >= 0; i--)
        {
            _components.Remove(_pendingRemoval[i]);
        }

        _pendingRemoval.Clear();
    }

    #endregion

    #region Internals
    private void AttachInternal(Component component)
    {
        // The construction flow (AddComponent<T>) creates the component already bound to this
        // entity; a cloned component arrives unbound. Any other binding is rejected: components
        // cannot be moved between entities.
        var bound = component.gameObjectOrNull;
        if (bound is not null && !ReferenceEquals(bound, this))
        {
            throw new InvalidOperationException(
                $"The component:{component.GetType().Name} is already attached to another entity; remove it first."
            );
        }

        // 反正Transform也是Component，就直接在这里特判了。
        if (component is Transform transform)
        {
            if (ReferenceEquals(bound, this) && _components.Contains(transform))
                return;

            transform.Bind(this);
            _components.Insert(0, transform);
            return;
        }

        // Attaching the same instance twice is a no-op; hooks must not run twice.
        if (_components.Contains(component))
            return;

        // One component per concrete type per entity. Without this, a second instance would make
        // GetComponent<T>() return whichever instance happens to come first.
        for (int i = 0, count = _components.Count; i < count; i++)
        {
            if (_components[i].GetType() == component.GetType())
            {
                throw new InvalidOperationException(
                    $"The entity already has a component of type {component.GetType().Name}; "
                        + "a component type can be attached to an entity only once."
                );
            }
        }

        component.Bind(this);
        _components.Add(component);

        if (!_awake)
            return;

        component.Awake();

        if (_activeInHierarchy && component.Enabled)
        {
            component.OnEnable();
        }
    }

    private void DetachInternal(Component component)
    {
        if (_activeInHierarchy && component.Enabled)
        {
            component.OnDisable();
        }

        component.OnDestroy();
        component.Unbind();
    }

    #endregion

    #region Cloning

    /// <summary>Clones an entity to the root level.</summary>
    /// <param name="gameObject">Source entity.</param>
    /// <returns>The cloned entity.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="gameObject"/> is <c>null</c>.</exception>
    public static GameObject Instantiate(GameObject gameObject) => Instantiate(gameObject, null);

    /// <summary>Clones an entity: new instance, copied active state and local transform, components in source order.</summary>
    /// <param name="gameObject">Source entity.</param>
    /// <param name="parent">Parent transform of the clone; <c>null</c> places it at the root.</param>
    /// <returns>The cloned entity, with a new <see cref="ObjectBase.InstanceId"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="gameObject"/> is <c>null</c>.</exception>
    /// <exception cref="NotImplementedException">Component cloning semantics are not defined yet.</exception>
    public static GameObject Instantiate(GameObject gameObject, Transform? parent)
    {
        ArgumentNullException.ThrowIfNull(gameObject);

        // Index 0 is the transform, which the clone creates for itself instead of cloning.
        var components = new Component[gameObject._components.Count - 1];
        for (int i = 1; i < gameObject._components.Count; i++)
        {
            components[i - 1] = CloneComponent(gameObject._components[i]);
        }

        var clone = Create(components);
        clone._activeSelf = gameObject._activeSelf;

        Transform cloneTransform = clone._transform;
        cloneTransform.SetParent(parent, keepWorldPosition: false);
        cloneTransform.LocalPosition = gameObject._transform.LocalPosition;
        cloneTransform.LocalRotation = gameObject._transform.LocalRotation;
        cloneTransform.LocalScale = gameObject._transform.LocalScale;

        clone.RefreshActivation(parent?.gameObject?.ActiveInHierarchy ?? true);

        return clone;
    }

    //TODO：Component克隆逻辑未明了
    // The clone must be returned unbound; AttachInternal is what binds it to the new entity.
    private static Component CloneComponent(Component source)
    {
        throw new NotImplementedException();
    }

    // internal method to create a gameobject
    // Registration is owned exclusively by ObjectBase's constructor; registering here would
    // allocate a second slot and enqueue the entity twice.
    private static GameObject Create(Component[]? components = null) => new(components);

    #endregion
}
