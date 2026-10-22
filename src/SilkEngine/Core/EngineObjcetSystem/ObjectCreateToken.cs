using SilkEngine.Runtime;

namespace SilkEngine.Core.EngineObjcetSystem;

internal readonly struct ComponentCreateToken
{
    internal readonly GameObject GameObject;

    public ComponentCreateToken(GameObject gameObject)
    {
        GameObject = gameObject;
    }
}

internal readonly struct ObjectCreateToken
{
    internal readonly MainThreadContext MainThreadContext;
    internal readonly ObjectId ObjectId;

    public ObjectCreateToken(MainThreadContext cxt, ObjectId id)
    {
        MainThreadContext = cxt;
        ObjectId = id;
    }
}
