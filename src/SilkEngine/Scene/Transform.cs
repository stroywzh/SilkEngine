using System.Collections.Generic;
using SilkEngine.Math;

namespace SilkEngine.Scene;

public sealed class Transform
{
    public GameObject GameObject { get; private set; }
    private Vector3 _localPosition,
        _localScale = Vector3.One;
    private Quaternion _localRotation = Quaternion.Identity;
    private Transform? _parent;
    private List<Transform> _children = new();

    public Transform(GameObject go)
    {
        GameObject = go;
    }

    public Transform(GameObject go, Transform parent)
    {
        GameObject = go;
        _parent = parent;
        parent._children.Add(this);
    }

    public Transform(GameObject go, GameObject parent)
    {
        GameObject = go;
        _parent = parent.Transform;
        parent.Transform._children.Add(this);
    }

    public Vector3 LocalPosition
    {
        get => _localPosition;
        set
        {
            _localPosition = value;
            NotifyChildren();
        }
    }
    public Quaternion LocalRotation
    {
        get => _localRotation;
        set
        {
            _localRotation = value;
            NotifyChildren();
        }
    }
    public Vector3 LocalScale
    {
        get => _localScale;
        set
        {
            _localScale = value;
            NotifyChildren();
        }
    }
    public Vector3 Position =>
        _parent != null ? _parent.Position + _parent.Rotation * _localPosition : _localPosition;
    public Quaternion Rotation
    {
        get => _parent != null ? _parent.Rotation * _localRotation : _localRotation;
        set
        {
            _localRotation =
                _parent != null
                    ? new Quaternion(
                        -_parent.Rotation.X,
                        -_parent.Rotation.Y,
                        -_parent.Rotation.Z,
                        _parent.Rotation.W
                    ) * value
                    : value;
            NotifyChildren();
        }
    }
    public Vector3 Scale => _localScale;
    public Transform? Parent => _parent;
    public IReadOnlyList<Transform> Children => _children;
    public Vector3 Forward => Rotation * Vector3.Forward;
    public Matrix4x4 LocalToWorldMatrix => Matrix4x4.CreateTRS(Position, Rotation, Scale);

    /// <summary>重挂父级；上溯新父链查环（含自身），成环抛 InvalidOperationException。</summary>
    public void SetParent(Transform? p)
    {
        for (var cur = p; cur != null; cur = cur.Parent)
            if (cur == this)
                throw new InvalidOperationException(
                    $"Cannot parent '{GameObject.Name}' to itself or a descendant (cycle)"
                );
        _parent?._children.Remove(this);
        _parent = p;
        _parent?._children.Add(this);
        GameObject?.NotifyActivationChanged();
    }

    internal void NotifyChildren()
    {
        foreach (var c in _children)
            c.NotifyChildren();
    }
}
