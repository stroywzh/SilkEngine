using System.Text.Json.Nodes;
using SilkEngine;
using SilkEngine.Scene.Serialization;
using SilkEngine.Math;

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

    [Fact]
    public void AddComponent_WithAttachedData_CallsReadFromAfterAwakeBeforeEnable()
    {
        var go = new GameObject("GO");
        go.AttachSerializedData(DataWithComponent(3.5f));

        var c = go.AddComponent<TestSerializable>();

        Assert.True(c.ReadCalled);
        Assert.Equal(3.5f, c.Speed);            // 序列化值已生效
        Assert.Equal(0f, c.SeenAtAwake);        // ReadFrom 在 OnAwake 之后（Awake 时字段仍是默认值）
        Assert.Equal(3.5f, c.SeenAtEnable);     // ReadFrom 在 RecomputeActiveState(Enable) 之前
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
        go.AttachSerializedData(DataWithComponent(3.5f));
        var c = go.AddComponent<Tracker>();
        Assert.False(c.ReadCalled);   // Tracker 未实现 ISerializableComponent
    }

    [Fact]
    public void AddComponent_AfterClearSerializedData_DoesNotCallReadFrom()
    {
        var go = new GameObject("GO");
        go.AttachSerializedData(DataWithComponent(3.5f));
        go.ClearSerializedData();
        var c = go.AddComponent<TestSerializable>();
        Assert.False(c.ReadCalled);
    }

    [Fact]
    public void AddComponent_NonGeneric_GoesThroughSameFactory()
    {
        var go = new GameObject("GO");
        go.AttachSerializedData(DataWithComponent(2f));
        var c = (TestSerializable)go.AddComponent(new TestSerializable());
        Assert.True(c.ReadCalled);
        Assert.Equal(2f, c.Speed);
        Assert.Equal(2f, c.SeenAtEnable);
    }

    [Fact]
    public void AttachSerializedData_RestoresTransformValues()
    {
        var go = new GameObject("GO");
        var data = JsonNode.Parse(
            """{ "Name":"GO", "Components": { "Transform": { "LocalPosition": [1,2,3], "LocalRotation": [0,0,0.5,0.866], "LocalScale": [4,5,6] } } }"""
        )!.AsObject();

        go.AttachSerializedData(data);

        Assert.Equal(new Vector3(1, 2, 3), go.Transform.LocalPosition);
        Assert.Equal(new Quaternion(0, 0, 0.5f, 0.866f), go.Transform.LocalRotation);
        Assert.Equal(new Vector3(4, 5, 6), go.Transform.LocalScale);
    }

    [Fact]
    public void AttachSerializedData_MissingTransformKeys_KeepCurrentValues()
    {
        var go = new GameObject("GO");
        go.Transform.LocalPosition = new Vector3(9, 9, 9);
        var data = JsonNode.Parse("""{ "Name":"GO", "Components": {} }""")!.AsObject();

        go.AttachSerializedData(data);

        Assert.Equal(new Vector3(9, 9, 9), go.Transform.LocalPosition);  // 无 Transform 节点 → 不覆盖
        Assert.Equal(Vector3.One, go.Transform.LocalScale);              // 缺键 → 保留默认 One
        Assert.Equal(Quaternion.Identity, go.Transform.LocalRotation);   // 缺键 → 保留默认 Identity
    }

    private class Tracker : MonoBehaviour
    {
        public bool ReadCalled;
    }
}
