using System.Collections.Generic;
using ProjectEngine.Math;

namespace ProjectEngine;

public sealed class Transform
{
    public GameObject? GameObject { get; internal set; }
    private Vector3 _localPosition,
        _localScale = Vector3.One;
    private Quaternion _localRotation = Quaternion.Identity;
    private Transform? _parent;
    private List<Transform> _children = new();

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

    public void SetParent(Transform? p)
    {
        _parent?._children.Remove(this);
        _parent = p;
        _parent?._children.Add(this);
    }

    internal void NotifyChildren()
    {
        foreach (var c in _children)
            c.NotifyChildren();
    }
}

