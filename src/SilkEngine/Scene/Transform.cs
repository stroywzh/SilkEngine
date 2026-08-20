using System.Collections.Generic;
using SilkEngine.Math;

namespace SilkEngine.Scene;

/// <summary>
/// 对象层级变换：局部（Local*）相对父级，世界（Position/Rotation）为父链组合值（左手系，与引擎 LookAt/投影约定一致）。
/// Position = 父 Position + 父 Rotation × LocalPosition；Rotation setter 写回局部（含父时按父旋转逆变换）。
/// 注意：Scale 不组合父级（P1 已知限制），LocalToWorldMatrix 仍按组合值构造。
/// 局部值变更级联通知子树（NotifyChildren，供后端采集）。
/// </summary>
public sealed class Transform
{
    /// <summary>所属宿主 GameObject（构造时注入，恒非空）。</summary>
    public GameObject GameObject { get; private set; }
    private Vector3 _localPosition,
        _localScale = Vector3.One;
    private Quaternion _localRotation = Quaternion.Identity;
    private Transform? _parent;
    private List<Transform> _children = new();

    /// <summary>创建根级 Transform（无父级）。</summary>
    /// <param name="go">宿主 GameObject</param>
    public Transform(GameObject go)
    {
        GameObject = go;
    }

    /// <summary>创建挂载于指定父 Transform 下的子级 Transform。</summary>
    /// <param name="go">宿主 GameObject</param>
    /// <param name="parent">父级 Transform（构造即登记为子级）</param>
    public Transform(GameObject go, Transform parent)
    {
        GameObject = go;
        _parent = parent;
        parent._children.Add(this);
    }

    /// <summary>创建挂载于指定父 GameObject 下的子级 Transform。</summary>
    /// <param name="go">宿主 GameObject</param>
    /// <param name="parent">父级 GameObject（取其 Transform 建立父子关系）</param>
    public Transform(GameObject go, GameObject parent)
    {
        GameObject = go;
        _parent = parent.Transform;
        parent.Transform._children.Add(this);
    }

    /// <summary>相对父级的局部位置（左手系；变更级联通知子树）。</summary>
    public Vector3 LocalPosition
    {
        get => _localPosition;
        set
        {
            _localPosition = value;
            NotifyChildren();
        }
    }
    /// <summary>相对父级的局部旋转（左手系；变更级联通知子树）。</summary>
    public Quaternion LocalRotation
    {
        get => _localRotation;
        set
        {
            _localRotation = value;
            NotifyChildren();
        }
    }

    /// <summary>相对父级的局部缩放（左手系；变更级联通知子树）。</summary>
    public Vector3 LocalScale
    {
        get => _localScale;
        set
        {
            _localScale = value;
            NotifyChildren();
        }
    }

    /// <summary>世界位置：父链组合值（父 Position + 父 Rotation × LocalPosition）；无父时等于局部位置。</summary>
    public Vector3 Position =>
        _parent != null ? _parent.Position + _parent.Rotation * _localPosition : _localPosition;

    /// <summary>
    /// 世界旋转：无父时等于局部旋转；有父时 = 父 Rotation × LocalRotation（组合值）。
    /// setter 语义：按世界值写回局部 —— 无父直接赋值；有父按父旋转逆变换后赋值，并级联通知子树。
    /// </summary>
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
    /// <summary>局部缩放（不组合父级，P1 已知限制）。</summary>
    public Vector3 Scale => _localScale;

    /// <summary>父级 Transform；null 表示根对象。</summary>
    public Transform? Parent => _parent;

    /// <summary>子级 Transform 只读列表。</summary>
    public IReadOnlyList<Transform> Children => _children;

    /// <summary>世界前向（Rotation × Vector3.Forward，左手系）。</summary>
    public Vector3 Forward => Rotation * Vector3.Forward;

    /// <summary>局部 → 世界变换矩阵（CreateTRS(Position, Rotation, Scale)）。</summary>
    public Matrix4x4 LocalToWorldMatrix => Matrix4x4.CreateTRS(Position, Rotation, Scale);

    /// <summary>
    /// 重挂父级：从旧父级摘除并登记入新父级（null 表示成为根对象），随后级联重算激活状态
    /// （IsActiveInHierarchy 变化会传播 OnEnable/OnDisable）。
    /// </summary>
    /// <param name="p">新父级；null 表示脱离父级</param>
    /// <exception cref="InvalidOperationException">新父链上溯含自身或其子孙（成环）</exception>
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

    /// <summary>局部值变更级联：递归通知子树（供采集/渲染等消费方感知变更）。</summary>
    internal void NotifyChildren()
    {
        foreach (var c in _children)
            c.NotifyChildren();
    }
}
