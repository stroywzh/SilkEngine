using System.Text.Json.Nodes;
using SilkEngine.Scene.Serialization;
using SilkEngine.Math;

namespace SilkEngine.Tests.Core.Assets.Serialization;

[CollectionDefinition("Serialization")]
public class SerializationTestsCollection { }

[Collection("Serialization")]
public class SerializedNodeTests
{
    [Fact]
    public void GetString_ReturnsValue_WhenPresent()
    {
        var node = new SerializedNode(JsonNode.Parse("""{ "Shader": "guid-1" }""")!.AsObject());
        Assert.Equal("guid-1", node.GetString("Shader"));
    }

    [Fact]
    public void GetString_ReturnsNull_WhenMissing()
    {
        var node = new SerializedNode(new JsonObject());
        Assert.Null(node.GetString("Shader"));
    }

    [Fact]
    public void GetInt_ReturnsValue_WhenPresent()
    {
        var node = new SerializedNode(JsonNode.Parse("""{ "Count": 42 }""")!.AsObject());
        Assert.Equal(42, node.GetInt("Count"));
    }

    [Fact]
    public void GetInt_ReturnsZero_WhenMissing()
    {
        var node = new SerializedNode(new JsonObject());
        Assert.Equal(0, node.GetInt("Count"));
    }

    [Fact]
    public void GetFloat_ReturnsValue_WhenPresent()
    {
        var node = new SerializedNode(JsonNode.Parse("""{ "Speed": 3.5 }""")!.AsObject());
        Assert.Equal(3.5f, node.GetFloat("Speed"));
    }

    [Fact]
    public void GetFloat_ReturnsZero_WhenMissing()
    {
        var node = new SerializedNode(new JsonObject());
        Assert.Equal(0f, node.GetFloat("Speed"));
    }

    [Fact]
    public void GetBool_ReturnsValue_WhenPresent()
    {
        var node = new SerializedNode(JsonNode.Parse("""{ "Lit": true }""")!.AsObject());
        Assert.True(node.GetBool("Lit"));
    }

    [Fact]
    public void GetBool_ReturnsFalse_WhenMissing()
    {
        var node = new SerializedNode(new JsonObject());
        Assert.False(node.GetBool("Lit"));
    }

    [Fact]
    public void GetVector3_ReturnsValue_WhenPresent()
    {
        var node = new SerializedNode(JsonNode.Parse("""{ "LocalPosition": [1, 2, 3] }""")!.AsObject());
        Assert.Equal(new Vector3(1, 2, 3), node.GetVector3("LocalPosition"));
    }

    [Fact]
    public void GetVector3_ReturnsZero_WhenMissing()
    {
        var node = new SerializedNode(new JsonObject());
        Assert.Equal(Vector3.Zero, node.GetVector3("LocalPosition"));
    }

    [Fact]
    public void GetQuaternion_ReturnsValue_WhenPresent()
    {
        var node = new SerializedNode(JsonNode.Parse("""{ "LocalRotation": [0, 0.5, 0, 0.866] }""")!.AsObject());
        Assert.Equal(new Quaternion(0, 0.5f, 0, 0.866f), node.GetQuaternion("LocalRotation"));
    }

    [Fact]
    public void GetQuaternion_ReturnsIdentity_WhenMissing()
    {
        var node = new SerializedNode(new JsonObject());
        Assert.Equal(Quaternion.Identity, node.GetQuaternion("LocalRotation"));
    }

    [Fact]
    public void SetThenGet_RoundtripsAllTypes()
    {
        var node = new SerializedNode(new JsonObject());
        node.SetString("S", "abc");
        node.SetInt("I", 42);
        node.SetFloat("F", 1.5f);
        node.SetBool("B", true);
        node.SetVector3("V", new Vector3(1, 2, 3));
        node.SetQuaternion("Q", new Quaternion(0, 0.7071f, 0, 0.7071f));

        Assert.Equal("abc", node.GetString("S"));
        Assert.Equal(42, node.GetInt("I"));
        Assert.Equal(1.5f, node.GetFloat("F"));
        Assert.True(node.GetBool("B"));
        Assert.Equal(new Vector3(1, 2, 3), node.GetVector3("V"));
        Assert.Equal(new Quaternion(0, 0.7071f, 0, 0.7071f), node.GetQuaternion("Q"));
    }

    [Fact]
    public void SetString_Null_RemovesKey()
    {
        var node = new SerializedNode(new JsonObject());
        node.SetString("S", "abc");
        node.SetString("S", null);
        Assert.Null(node.GetString("S"));
        Assert.Equal("{}", node.AsJson());
    }

    [Fact]
    public void ContainsKey_TrueWhenPresent_FalseWhenMissing()
    {
        var node = new SerializedNode(JsonNode.Parse("""{ "Speed": 3.5 }""")!.AsObject());
        Assert.True(node.ContainsKey("Speed"));
        Assert.False(node.ContainsKey("Missing"));
    }

    [Fact]
    public void SetRawThenGetRaw_RoundtripsJsonNode()
    {
        var node = new SerializedNode(new JsonObject());
        node.SetRaw("K", JsonNode.Parse("""[1, 2]"""));
        Assert.Equal("[1,2]", node.GetRaw("K")!.ToJsonString());
    }

    [Fact]
    public void SetRaw_Null_RemovesKey()
    {
        var node = new SerializedNode(new JsonObject());
        node.SetRaw("K", JsonNode.Parse("1"));
        node.SetRaw("K", null);
        Assert.Null(node.GetRaw("K"));
        Assert.Equal("{}", node.AsJson());
    }
}
