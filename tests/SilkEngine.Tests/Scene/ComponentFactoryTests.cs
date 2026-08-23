using SilkEngine.Scene;

namespace SilkEngine.Tests.Scene;

public class ComponentFactoryTests
{
    private class Dummy : Component { }

    [Fact]
    public void Resolve_RegisteredGenericType_ReturnsFactory()
    {
        ComponentFactory.Register<Dummy>();

        var factory = ComponentFactory.Resolve(typeof(Dummy).FullName!);

        Assert.NotNull(factory);
        var instance = factory!();
        Assert.IsType<Dummy>(instance);
    }

    [Fact]
    public void Resolve_RegisteredCustomFactory_ReturnsNewInstance()
    {
        ComponentFactory.Register(typeof(Dummy).FullName!, () => new Dummy());

        var factory = ComponentFactory.Resolve(typeof(Dummy).FullName!);

        Assert.NotNull(factory);
        Assert.NotSame(factory!(), factory!());   // 每次调用都新建实例
    }

    [Fact]
    public void Resolve_UnknownType_ReturnsNull()
    {
        Assert.Null(ComponentFactory.Resolve("SilkEngine.Tests.NoSuchType"));
    }

    [Fact]
    public void StaticCtor_RegistersMeshRendererAndCamera()
    {
        Assert.NotNull(ComponentFactory.Resolve(typeof(MeshRenderer).FullName!));
        Assert.NotNull(ComponentFactory.Resolve(typeof(Camera).FullName!));
    }
}
