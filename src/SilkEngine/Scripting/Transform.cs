using System.Collections.ObjectModel;

namespace SilkEngine.Scripting;

/// <summary>
/// Spatial transform of an entity: local TRS, world space accessors and parent/child hierarchy.
/// </summary>
public sealed class Transform : Component
{
    private Transform? _parent;
    private readonly List<Transform> _children = new();
    private readonly ReadOnlyCollection<Transform> _childrenView;
    private Vector3 _localPosition;
    private Quaternion _localRotation = Quaternion.Identity;
    private Vector3 _localScale = Vector3.One;

    private Transform(GameObject go)
        : base(go)
    {
        _childrenView = _children.AsReadOnly();
    }

    /// <summary>Parent transform, or <c>null</c> when this transform sits at the root.</summary>
    public Transform? Parent => _parent;

    /// <summary>Direct child transforms in attachment order, as a read-only live view.</summary>
    /// <remarks>
    /// The view reflects later re-parenting; it cannot be modified by callers. Changing the parent goes
    /// through <see cref="SetParent"/>.
    /// </remarks>
    public IReadOnlyList<Transform> Children => _childrenView;

    /// <summary>Position relative to the parent. Equals the world position when the parent is <c>null</c>.</summary>
    public Vector3 LocalPosition
    {
        get => _localPosition;
        set => _localPosition = value;
    }

    /// <summary>Rotation relative to the parent. The assigned value is normalised.</summary>
    public Quaternion LocalRotation
    {
        get => _localRotation;
        set => _localRotation = value.normalized;
    }

    /// <summary>Scale relative to the parent.</summary>
    /// <remarks>The scale chain is multiplied component-wise into <see cref="LossyScale"/>.</remarks>
    public Vector3 LocalScale
    {
        get => _localScale;
        set => _localScale = value;
    }

    /// <summary>World space position.</summary>
    /// <remarks>
    /// Reading composes the parent chain as
    /// <c>parentPos + parentRot * (parentLossyScale ⊙ localPos)</c> in a single bottom-up pass: O(d).
    /// Writing solves for the local position using the parent's world pose.
    /// </remarks>
    public Vector3 Position
    {
        get => GetWorldPose().position;
        set
        {
            if (_parent is null)
            {
                _localPosition = value;
                return;
            }

            (Vector3 parentPosition, Quaternion parentRotation, Vector3 parentScale) =
                _parent.GetWorldPose();
            Vector3 p = Quaternion.Inverse(parentRotation) * (value - parentPosition);

            // Boundary: an axis whose parent scale is near zero cannot be inverted, so the previous
            // local component is kept instead of writing inf/NaN.
            _localPosition = new Vector3(
                EngineMath.Abs(parentScale.X) > EngineMath.kEpsilon
                    ? p.X / parentScale.X
                    : _localPosition.X,
                EngineMath.Abs(parentScale.Y) > EngineMath.kEpsilon
                    ? p.Y / parentScale.Y
                    : _localPosition.Y,
                EngineMath.Abs(parentScale.Z) > EngineMath.kEpsilon
                    ? p.Z / parentScale.Z
                    : _localPosition.Z
            );
        }
    }

    /// <summary>World space rotation.</summary>
    /// <remarks>
    /// Reading yields <c>parentRotation * localRotation</c>, matching the engine's UE-style axes;
    /// writing solves for the local rotation. O(d).
    /// </remarks>
    public Quaternion Rotation
    {
        get => GetWorldPose().rotation;
        set =>
            LocalRotation = _parent is null
                ? value
                : Quaternion.Inverse(_parent.GetWorldPose().rotation) * value;
    }

    /// <summary>World scale obtained by multiplying the local scale chain.</summary>
    /// <remarks>
    /// Boundary: exact only when no ancestor combines non-uniform scale with rotation. Such a
    /// combination has no valid pure-scale diagonal representation, so the approximation is
    /// accepted here as it is by other engines. O(d).
    /// </remarks>
    public Vector3 LossyScale => GetWorldPose().scale;

    /// <summary>Accumulates the world TRS from the local TRS in a single bottom-up pass.</summary>
    /// <returns>World position, rotation and lossy scale of this transform.</returns>
    /// <remarks>O(d) for depth d, no allocation, no cache.</remarks>
    private (Vector3 position, Quaternion rotation, Vector3 scale) GetWorldPose()
    {
        Vector3 position = _localPosition;
        Quaternion rotation = _localRotation;
        Vector3 scale = _localScale;

        for (Transform? parent = _parent; parent is not null; parent = parent._parent)
        {
            position =
                parent._localPosition
                + parent._localRotation * Vector3.Scale(parent._localScale, position);
            rotation = parent._localRotation * rotation;
            scale = Vector3.Scale(parent._localScale, scale);
        }

        return (position, rotation, scale);
    }

    /// <summary>World space forward direction (+X, Unreal-style axes).</summary>
    public Vector3 Forward => Rotation * Vector3.Forward;

    /// <summary>World space right direction (+Y, Unreal-style axes).</summary>
    public Vector3 Right => Rotation * Vector3.Right;

    /// <summary>World space up direction (+Z, Unreal-style axes).</summary>
    public Vector3 Up => Rotation * Vector3.Up;

    /// <summary>A transform can never be disabled; it stays active for the entity's lifetime.</summary>
    protected override bool IsDisableAllowed() => false;

    /// <summary>Changes the parent while optionally preserving the world position and rotation.</summary>
    /// <param name="newParent">New parent; <c>null</c> moves the transform to the root.</param>
    /// <param name="keepWorldPosition">
    /// <c>true</c> (default) preserves the world position and rotation by solving for the local pose;
    /// the world scale is <b>not</b> preserved and follows the new parent's chain.
    /// <c>false</c> preserves the current local TRS, so the world pose changes.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="newParent"/> is this transform or one of its descendants, which would create a cycle.
    /// </exception>
    public void SetParent(Transform? newParent, bool keepWorldPosition = true)
    {
        if (_parent == newParent)
            return;

        // Cycle check: walking up from the new parent must never reach this transform.
        for (Transform? ancestor = newParent; ancestor is not null; ancestor = ancestor._parent)
        {
            if (ReferenceEquals(ancestor, this))
            {
                throw new InvalidOperationException(
                    "Transform.SetParent: the attachment would create a cycle."
                );
            }
        }

        Vector3 oldPosition = default;
        Quaternion oldRotation = default;
        if (keepWorldPosition)
        {
            oldPosition = Position;
            oldRotation = Rotation;
        }

        var wasActive = GameObject.ActiveInHierarchy;

        _parent?._children.Remove(this);
        _parent = newParent;
        newParent?._children.Add(this);

        if (keepWorldPosition)
        {
            Position = oldPosition;
            Rotation = oldRotation;
        }

        var isActive = GameObject.ActiveInHierarchy;
        if (wasActive != isActive)
        {
            // Re-parenting changed the effective active state of the whole moved subtree.
            GameObject.ApplyActiveState(wasActive, isActive);
        }
    }

    /// <summary>Points the world forward direction at a world space position.</summary>
    /// <param name="worldPosition">Target position.</param>
    /// <param name="worldUp">
    /// Up reference used to remove the roll degree of freedom; +Z is the engine's up axis. When it is
    /// (anti)parallel to the view direction, a fallback axis is chosen instead of throwing.
    /// </param>
    /// <remarks>When the target is too close (near zero vector) the current rotation is kept.</remarks>
    public void LookAt(Vector3 worldPosition, Vector3 worldUp)
    {
        Vector3 dir = worldPosition - Position;

        // Compared against the squared length, so the threshold must be squared as well.
        if (dir.sqrMagnitude < EngineMath.kEpsilon * EngineMath.kEpsilon)
            return;

        Rotation = Quaternion.LookRotation(dir, worldUp);
    }

    /// <summary>Points the world forward direction at a world space position, using world +Z as up.</summary>
    /// <param name="worldPosition">Target position.</param>
    /// <remarks>When the target is too close the current rotation is kept.</remarks>
    public void LookAt(Vector3 worldPosition) => LookAt(worldPosition, Vector3.Up);

    internal static Transform Create(GameObject go)
    {
        return new Transform(go);
    }
}
