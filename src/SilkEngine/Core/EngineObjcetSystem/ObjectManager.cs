using System;
using System.Collections.Generic;
using SilkEngine;

namespace SilkEngine.Core.ObjcetSystem;

public enum ObjectFlag : byte
{
    Normal = 0,
    DontDestoryOnLoad = 1,
}

internal sealed class ObjectManager : ManagerBase<ObjectManager>
{
    private static ObjectContainer _normals = new();
    private static ObjectContainer _dontDestoryOnLoad = new();

    public void Destroy(GameObject go)
    {
        if (go.objectFlag == ObjectFlag.Normal)
        {
            _normals.Remove(go);
        }
        else if (go.objectFlag == ObjectFlag.DontDestoryOnLoad)
        {
            _dontDestoryOnLoad.Remove(go);
        }
    }

    public static void Register(GameObject go){}

    public void SetDontDestoryOnLoad(GameObject gameObject) { }
}
