using SilkEngine.Core;
using SilkEngine.Scene;
using SilkEngine.Scene.Serialization;

namespace SilkEngine.Tests.Scene.Serialization;

[Collection("Serialization")]
public class ComponentTypeRegistryTests
{
    private class TestWriter : ILogWriter
    {
        public List<string> Messages = new();
        public void Write(string msg) => Messages.Add(msg);
    }

    [Fact]
    public void Resolve_MeshRenderer_ReturnsFactory()
    {
        var factory = ComponentTypeRegistry.Resolve("SilkEngine.Scene.MeshRenderer");
        Assert.NotNull(factory);
        Assert.IsType<MeshRenderer>(factory!());
    }

    [Fact]
    public void Resolve_Camera_ReturnsFactory()
    {
        var factory = ComponentTypeRegistry.Resolve("SilkEngine.Scene.Camera");
        Assert.NotNull(factory);
        Assert.IsType<Camera>(factory!());
    }

    [Fact]
    public void Resolve_Unknown_ReturnsNullAndWarns()
    {
        var tw = new TestWriter();
        Log.AddWriter(tw);
        try
        {
            Assert.Null(ComponentTypeRegistry.Resolve("SilkEngine.Missing.Type"));
            Assert.Contains(tw.Messages, m => m.Contains("SilkEngine.Missing.Type"));
        }
        finally
        {
            Log.RemoveWriter(tw);
        }
    }

    [Fact]
    public void Register_CustomType_ThenResolve()
    {
        ComponentTypeRegistry.Register("SilkEngine.Tests.CustomComp", () => new Camera());
        var factory = ComponentTypeRegistry.Resolve("SilkEngine.Tests.CustomComp");
        Assert.NotNull(factory);
        Assert.IsType<Camera>(factory!());
    }

    [Fact]
    public void Register_Generic_ThenResolve()
    {
        ComponentTypeRegistry.Register<GenCameraProbe>();
        var factory = ComponentTypeRegistry.Resolve(typeof(GenCameraProbe).FullName!);
        Assert.NotNull(factory);
        Assert.IsType<GenCameraProbe>(factory!());
    }
}

public partial class GenCameraProbe : Component { }
