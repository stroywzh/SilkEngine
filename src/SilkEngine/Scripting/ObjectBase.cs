using SilkEngine.Core.EngineObjcetSystem;

namespace SilkEngine;

public abstract class ObjectBase
{
    public ObjectId InstanceId { get; internal set; }

    protected ObjectBase()
    {
        EngineObjectManager.Instance.Register(this);
    }

    protected internal virtual void OnCreate() { }

    protected internal virtual void OnDestroyCore() { }
}
