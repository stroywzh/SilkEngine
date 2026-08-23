using System.Text.Json.Nodes;
using SilkEngine.Scene;
using SilkEngine.Scene.Serialization;

namespace SilkEngine.Tests.Scene;

[Collection("Serialization")]
public class GameObjectSerializationTests
{
    private class TestSerializable : MonoBehaviour
    {
        public float Speed;
        public float SeenAtAwake;
        public float SeenAtEnable;
        public bool ReadCalled;
        public override void OnAwake() => SeenAtAwake = Speed;
        public override void OnEnable() => SeenAtEnable = Speed;
        public override void ReadFrom(SerializedNode node)
        {
            ReadCalled = true;
            Speed = node.GetFloat("Speed");
        }
        public override void WriteTo(SerializedNode node) => node.SetFloat("Speed", Speed);
    }

    private static JsonObject DataWithComponent(float speed)
    {
        var key = typeof(TestSerializable).FullName!;
        return new JsonObject
        {
            ["Name"] = "GO",
            ["Components"] = new JsonObject
            {
                ["Transform"] = new JsonObject(),
                [key] = new JsonObject { ["Speed"] = speed },
            },
        };
    }

    /// <summary>模拟 SceneSerializer 反序列化窗口：挂载数据并让组件工厂经内部重载读取（context 局部持有，不污染全局 Services）。</summary>
    private static T WithAttachedData<T>(GameObject go, JsonObject data, Func<DeserializationContext, T> mount)
    {
        var ctx = new DeserializationContext();
        ctx.Attach(go, data);
        return mount(ctx);
    }

    [Fact]
    public void AddComponent_WithAttachedData_CallsReadFromBeforeAwakeBeforeEnable()
    {
        var go = new GameObject("GO");
        var c = WithAttachedData(go, DataWithComponent(3.5f), ctx => (TestSerializable)go.AddComponent(new TestSerializable(), null));

        Assert.True(c.ReadCalled);
        Assert.Equal(3.5f, c.Speed);            // 序列化值已生效
        Assert.Equal(3.5f, c.SeenAtAwake);      // ReadFrom 在 OnAwake 之前（Unity 语义）
        Assert.Equal(3.5f, c.SeenAtEnable);     // ReadFrom 在 Enable 之前
    }

    [Fact]
    public void AddComponent_WithoutAttachedData_DoesNotCallReadFrom()
    {
        var go = new GameObject("GO");
        var c = go.AddComponent<TestSerializable>();
        Assert.False(c.ReadCalled);
    }

    [Fact]
    public void AddComponent_NonSerializableComponent_IsIgnored()
    {
        var go = new GameObject("GO");
        var c = WithAttachedData(go, DataWithComponent(3.5f), ctx => (Tracker)go.AddComponent(new Tracker(), null));
        Assert.False(c.ReadCalled);   // Tracker 未覆写 WriteTo/ReadFrom（基类空默认）
    }

    [Fact]
    public void AddComponent_AfterDetach_DoesNotCallReadFrom()
    {
        var go = new GameObject("GO");
        var c = WithAttachedData(go, DataWithComponent(3.5f), ctx =>
        {
            ctx.Detach(go);   // 模拟组件均完成 ReadFrom 后的释放
            return (TestSerializable)go.AddComponent(new TestSerializable(), null);
        });
        Assert.False(c.ReadCalled);
    }

    [Fact]
    public void AddComponent_NonGeneric_GoesThroughSameFactory()
    {
        var go = new GameObject("GO");
        var c = WithAttachedData(go, DataWithComponent(2f), ctx => (TestSerializable)go.AddComponent(new TestSerializable(), null));
        Assert.True(c.ReadCalled);
        Assert.Equal(2f, c.Speed);
        Assert.Equal(2f, c.SeenAtEnable);
    }

    private class Tracker : MonoBehaviour
    {
        public bool ReadCalled;
    }
}
