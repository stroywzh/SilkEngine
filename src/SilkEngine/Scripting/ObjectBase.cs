using SilkEngine.Core.EngineObjcetSystem;

namespace SilkEngine;

public abstract class ObjectBase
{
    public ObjectId InstanceId { get; internal set; }

    protected ObjectBase()
    {
        //TODO: 我现在感觉这个反而有点过度抽象了（）
        // EngineObjectManager.Instance.Register(this);
    }

    protected internal virtual void OnCreate() { }

    protected internal virtual void OnDestroyCore() { }
}
