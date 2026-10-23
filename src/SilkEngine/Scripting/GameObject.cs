using System.Diagnostics.CodeAnalysis;
using SilkEngine.Core.ObjcetSystem;
using SilkEngine.Internal;
using SilkEngine.Runtime;

namespace SilkEngine;

public sealed class GameObject : IUpdateable
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
        throw new InvalidOperationException("不允许直接new一个GameObject");
    }

    private GameObject(ObjectCreateToken token)
    {
        if (!token.MainThreadContext.IsMainThread)
        {
            Log.Warning("你正在尝试通过同步方式从非主线程实例化GameObject");
        }

        if (!token.ObjectId.IsNull)
        {
            InstanceId = token.ObjectId;

            _tranform = Transform.Create(this);
            _initialized = true;
            ObjectManager.Register(this);
        }
        else
        {
            throw new InvalidOperationException("不允许直接new一个GameObject");
        }
    }

    public void SetActive(bool value)
    {
        if (value != _isActive)
        {
            _isActive = value;
        }
    }

    public static GameObject Instantiate(GameObject gameObject, Transform? parent = null)
    {
        //BUG: 临时验证路径
        var id = ObjectId.Null;
        // ObjectCreateToken token = new(MainThreadContext,id,gameObject)
        throw new NotImplementedException();
    }

    private static GameObject CreateNew(ObjectCreateToken token)
    {
        return new(token);
    }

    #region  UpdateHooks
    public void UpdateInternal()
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

    public void FixedUpdateInternal()
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

    public void LateUpdateInternal()
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
