using System.Diagnostics.CodeAnalysis;
using SilkEngine.Core;
using SilkEngine.Core.ObjcetSystem;
using SilkEngine.Internal;
using SilkEngine.Runtime;

namespace SilkEngine;

public sealed class GameObject
{
    public ObjectId InstanceId { get; init; }
    internal ObjectFlag objectFlag = ObjectFlag.Normal;
    private Transform _tranform;
    private Component[] components = Array.Empty<Component>();
    internal bool _initialized { get; init; }
    private bool _isActive = true;
    private bool _frisrtUpdate = true;
    public bool ActiveInHierarchy
    {
        get
        {
            var parent = _tranform.Parent is not null ? _tranform.Parent.ActiveInHierarchy : true;
            var activeState = _isActive;
            return activeState && parent;
        }
    }
    public Transform? Parent => _tranform.Parent ?? null;
    public Transform Transform => _tranform;

    public GameObject()
    {
        throw new InvalidOperationException("不允许直接 new 一个 GameObject");
    }

    private GameObject(ObjectCreateToken token)
    {
        if (!token.ObjectId.IsNull)
        {
            InstanceId = token.ObjectId;

            _tranform = Transform.Create(this);
            _initialized = true;
        }
        else
        {
            _tranform = default!;
            _initialized = false;
        }
    }

    public void SetActive(bool value)
    {
        if (value != _isActive)
        {
            _isActive = value;
        }
    }

    /// <summary>内部构造入口：仅 ObjectManager 工厂调用。</summary>
    internal static GameObject CreateNew(ObjectCreateToken token)
    {
        return new(token);
    }

    public static GameObject Instantiate(GameObject gameObject) =>
        GameObjectHelper.Instantiate(gameObject);

    public static void Destroy(GameObject gameObject) => GameObjectHelper.Destroy(gameObject);

    #region UpdateHooks
    internal void UpdateInternal()
    {
        if (!ActiveInHierarchy)
            return;

        if (_frisrtUpdate)
        {
            _frisrtUpdate = false;
            foreach (var c in components)
            {
                c.Start();
            }
        }

        foreach (var c in components)
        {
            if (c.Enabled)
            {
                c.Update();
            }
        }
    }

    internal void FixedUpdateInternal()
    {
        if (!ActiveInHierarchy)
            return;

        if (_frisrtUpdate)
            return;

        foreach (var c in components)
        {
            if (c.Enabled)
            {
                c.FixedUpdate();
            }
        }
    }

    internal void LateUpdateInternal()
    {
        if (!ActiveInHierarchy)
            return;

        if (_frisrtUpdate)
            return;

        foreach (var c in components)
        {
            if (c.Enabled)
            {
                c.LateUpdate();
            }
        }
    }
    #endregion
}
