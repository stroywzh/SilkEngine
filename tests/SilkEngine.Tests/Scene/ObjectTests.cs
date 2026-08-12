using SilkEngine;

namespace SilkEngine.Tests.Scene;

[Collection("SceneManager")]
public class ObjectTests
{
    private class TestObject : Object { }

    [Fact] public void Name_DefaultsToEmpty() => Assert.Equal("", new TestObject().Name);
    [Fact] public void Name_CanBeSet() => Assert.Equal("Test", new TestObject { Name = "Test" }.Name);
    [Fact] public void InstanceID_IsUnique() => Assert.NotEqual(new TestObject().GetInstanceID(), new TestObject().GetInstanceID());
    [Fact] public void InstanceID_IsSequential() => Assert.True(new TestObject().GetInstanceID() < new TestObject().GetInstanceID());

    [Fact]
    public void Destroy_InvokesHandler()
    {
        Object? d = null; float dl = -1;
        Object.DestroyHandler += (o, t) => { d = o; dl = t; };
        var obj = new TestObject();
        Object.Destroy(obj, 0.5f);
        Assert.Same(obj, d); Assert.Equal(0.5f, dl);
    }
}
