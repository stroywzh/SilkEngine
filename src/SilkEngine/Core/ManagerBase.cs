using SilkEngine.Runtime;

namespace SilkEngine.Core;

internal abstract class ManagerBase<TManager> : SingletonInstance<TManager>, IUpdateable
    where TManager : class
{
    protected ManagerBase()
        : base()
    {
        UpdateDriver.Register(this);
        Initialize();
    }

    void IUpdateable.UpdateInternal()
    {
        OnUpdate();
    }

    void IUpdateable.LateUpdateInternal()
    {
        OnLateUpdate();
    }

    void IUpdateable.FixedUpdateInternal()
    {
        OnFixedUpdate();
    }

    protected virtual void Initialize() { }

    protected virtual void OnFixedUpdate() { }

    protected virtual void OnLateUpdate() { }

    protected virtual void OnUpdate() { }
}
