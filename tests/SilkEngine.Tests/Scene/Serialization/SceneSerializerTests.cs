using System.Text.Json.Nodes;
using SilkEngine.Core;
using SilkEngine.Scene;
using SilkEngine.Scene.Serialization;
using SilkEngine.Math;

namespace SilkEngine.Tests.Scene.Serialization;
using Scene = SilkEngine.Scene.Scene;

[Collection("Serialization")]
public class SceneSerializerTests
{
    private class TestWriter : ILogWriter
    {
        public List<string> Messages = new();
        public void Write(string msg) => Messages.Add(msg);
    }

    private class TestSerializable : MonoBehaviour
    {
        public float Speed;
        public string? GuidRef;
        public Vector3 Offset;
        public Quaternion Rotation;
        public bool Lit = true;

        public override void ReadFrom(SerializedNode node)
        {
            Speed = node.GetFloat("Speed");
            GuidRef = node.GetString("GuidRef");
            Offset = node.GetVector3("Offset");
            Rotation = node.GetQuaternion("Rotation");
            Lit = node.GetBool("Lit");
        }

        public override void WriteTo(SerializedNode node)
        {
            node.SetFloat("Speed", Speed);
            node.SetString("GuidRef", GuidRef);
            node.SetVector3("Offset", Offset);
            node.SetQuaternion("Rotation", Rotation);
            node.SetBool("Lit", Lit);
        }
    }

    private const string GuidA = "1f2e3d4c-5b6a-7988-99aa-bbccddeeff00";

    private static Scene BuildHierarchyScene()
    {
        var scene = new Scene("Roundtrip");
        var root = new GameObject("Root");
        root.Transform.LocalPosition = new Vector3(1, 2, 3);
        root.Transform.LocalRotation = Quaternion.Euler(30, 45, 0);
        root.Transform.LocalScale = new Vector3(2, 2, 2);
        var c = root.AddComponent<TestSerializable>();
        c.Speed = 3.5f;
        c.GuidRef = GuidA;
        c.Offset = new Vector3(4, 5, 6);
        c.Rotation = Quaternion.Euler(0, 90, 0);
        c.Lit = false;
        scene.AddRootObject(root);

        var child = new GameObject("Child");
        child.Transform.LocalPosition = new Vector3(0, 1, 0);
        child.Transform.SetParent(root.Transform);
        var cc = child.AddComponent<TestSerializable>();
        cc.Speed = 7f;
        cc.GuidRef = null;
        return scene;
    }

    [Fact]
    public void Roundtrip_PreservesHierarchyComponentsAndGuidRefs()
    {
        ComponentTypeRegistry.Register(typeof(TestSerializable).FullName!, () => new TestSerializable());

        var scene2 = SceneSerializer.Deserialize(SceneSerializer.Serialize(BuildHierarchyScene()));

        var roots = scene2.GetRootGameObjects();
        Assert.Single(roots);
        var root2 = roots[0];
        Assert.Equal("Root", root2.Name);
        Assert.Equal(new Vector3(1, 2, 3), root2.Transform.LocalPosition);
        Assert.Equal(Quaternion.Euler(30, 45, 0), root2.Transform.LocalRotation);
        Assert.Equal(new Vector3(2, 2, 2), root2.Transform.LocalScale);

        Assert.Single(root2.Transform.Children);
        var child2 = root2.Transform.Children[0].GameObject!;
        Assert.Equal("Child", child2.Name);
        Assert.Equal(new Vector3(0, 1, 0), child2.Transform.LocalPosition);

        var c2 = root2.GetComponent<TestSerializable>()!;
        Assert.Equal(3.5f, c2.Speed);
        Assert.Equal(GuidA, c2.GuidRef);
        Assert.Equal(new Vector3(4, 5, 6), c2.Offset);
        Assert.Equal(Quaternion.Euler(0, 90, 0), c2.Rotation);
        Assert.False(c2.Lit);

        var cc2 = child2.GetComponent<TestSerializable>()!;
        Assert.Equal(7f, cc2.Speed);
        Assert.Null(cc2.GuidRef);
    }

    [Fact]
    public void Roundtrip_PreservesSceneName()
    {
        var scene = new Scene("ThirdPerson3D");
        scene.AddRootObject(new GameObject("GO"));
        var scene2 = SceneSerializer.Deserialize(SceneSerializer.Serialize(scene));
        Assert.Equal("ThirdPerson3D", scene2.Name);
    }

    [Fact]
    public void Roundtrip_PreservesTransformValues()
    {
        var scene = new Scene("T");
        var go = new GameObject("GO");
        go.Transform.LocalPosition = new Vector3(10, -5, 0.5f);
        go.Transform.LocalRotation = Quaternion.Euler(15, 90, 30);
        go.Transform.LocalScale = new Vector3(0.5f, 2, 3);
        scene.AddRootObject(go);

        var scene2 = SceneSerializer.Deserialize(SceneSerializer.Serialize(scene));
        var go2 = scene2.GetRootGameObjects()[0];
        Assert.Equal(go.Transform.LocalPosition, go2.Transform.LocalPosition);
        Assert.Equal(go.Transform.LocalRotation, go2.Transform.LocalRotation);
        Assert.Equal(go.Transform.LocalScale, go2.Transform.LocalScale);
    }

    [Fact]
    public void Serialize_OutputsCompleteSceneJson()
    {
        var scene = new Scene("SerializedScene");
        var ground = new GameObject("Ground");
        ground.Transform.LocalScale = new Vector3(20, 1, 20);
        var mr = ground.AddComponent<MeshRenderer>();   // 非托管资产 → 无 GUID 键
        scene.AddRootObject(ground);

        var json = SceneSerializer.Serialize(scene);

        Assert.Contains("\"Name\": \"SerializedScene\"", json);
        Assert.Contains("\"Ground\"", json);
        Assert.Contains("\"Transform\"", json);
        Assert.Contains("\"LocalScale\": [", json);
        Assert.Contains("\"SilkEngine.Scene.MeshRenderer\"", json);
        Assert.Contains("\"GameObjects\"", json);
    }

    [Fact]
    public void Deserialize_MissingFields_UseDefaults()
    {
        var json = """
        {
          "Name": "T",
          "GameObjects": [
            {
              "Name": "GO",
              "Components": {
                "Transform": { "LocalPosition": [1, 0, 0] }
              }
            }
          ]
        }
        """;

        var scene = SceneSerializer.Deserialize(json);
        var go = scene.GetRootGameObjects()[0];
        Assert.Equal("GO", go.Name);
        Assert.Equal(new Vector3(1, 0, 0), go.Transform.LocalPosition);
        Assert.Equal(Quaternion.Identity, go.Transform.LocalRotation);
        Assert.Equal(Vector3.One, go.Transform.LocalScale);
        Assert.Empty(go._components);
    }

    [Fact]
    public void Deserialize_UnknownComponentType_SkipsAndWarns()
    {
        var tw = new TestWriter();
        Log.AddWriter(tw);
        try
        {
            var json = """
            {
              "Name": "T",
              "GameObjects": [
                {
                  "Name": "GO",
                  "Components": {
                    "Transform": {},
                    "SilkEngine.Missing.Type": { "X": 1 }
                  }
                }
              ]
            }
            """;

            var scene = SceneSerializer.Deserialize(json);
            var go = scene.GetRootGameObjects()[0];
            Assert.Empty(go._components);
            Log.Flush();
            Assert.Contains(tw.Messages, m => m.Contains("SilkEngine.Missing.Type"));
        }
        finally
        {
            Log.RemoveWriter(tw);
        }
    }

    [Fact]
    public void Deserialize_InvalidJson_ThrowsJsonException()
    {
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => SceneSerializer.Deserialize("{ not json"));
    }

    [Fact]
    public void Serialize_PlainComponent_EmitsEmptyNodeWithTypeKey()
    {
        var scene = new Scene("T");
        var go = new GameObject("GO");
        go.AddComponent<PlainProbe>();
        scene.AddRootObject(go);

        var json = SceneSerializer.Serialize(scene);

        Assert.Contains(typeof(PlainProbe).FullName!, json);
    }

    [Fact]
    public void Serialize_EmptyContentComponent_WarnsOncePerType()
    {
        var tw = new TestWriter();
        Log.AddWriter(tw);
        try
        {
            var scene = new Scene("T");
            var a = new GameObject("A");
            a.AddComponent<PlainProbe>();
            var b = new GameObject("B");
            b.AddComponent<PlainProbe>();
            scene.AddRootObject(a);
            scene.AddRootObject(b);

            SceneSerializer.Serialize(scene);

            Log.Flush();
            var warns = tw.Messages.Where(m => m.Contains("PlainProbe")).ToList();
            Assert.Single(warns);
        }
        finally
        {
            Log.RemoveWriter(tw);
        }
    }
}

/// <summary>无字段组件探针：序列化内容为空，用于空节点 Warn 用例。</summary>
public partial class PlainProbe : Component { }
