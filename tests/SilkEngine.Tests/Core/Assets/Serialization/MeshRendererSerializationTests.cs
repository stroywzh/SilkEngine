using System.Text.Json.Nodes;
using SilkEngine;
using SilkEngine.Core.Assets;
using SilkEngine.Core.Assets.Serialization;
using SilkEngine.Render;

namespace SilkEngine.Tests.Core.Assets.Serialization;

[Collection("Serialization")]
public class MeshRendererSerializationTests
{
    private const string GuidShader = "1f2e3d4c-5b6a-7988-99aa-bbccddeeff00";
    private const string GuidMesh = "2a3b4c5d-6e7f-80a1-b2c3-d4e5f6071829";
    private const string GuidMaterial = "9f8e7d6c-5b4a-3928-1706-f5e4d3c2b1a0";

    [Fact]
    public void WriteTo_NullAssets_OmitsAllKeys()
    {
        var mr = new MeshRenderer();
        var obj = new JsonObject();
        mr.WriteTo(new SerializedNode(obj));
        Assert.Equal("{}", obj.ToJsonString());
    }

    [Fact]
    public void WriteTo_UnmanagedAssets_OmitsAllKeys()
    {
        var mr = new MeshRenderer
        {
            Shader = new Shader { Name = "Lit" },          // 非托管：缓存无条目
            Mesh = MeshFactory.CreateCube(1f),
            Material = new Material { Name = "M" },
        };
        var obj = new JsonObject();
        mr.WriteTo(new SerializedNode(obj));
        Assert.Equal("{}", obj.ToJsonString());
    }

    [Fact]
    public void WriteTo_ManagedAsset_WritesItsGuid()
    {
        var shader = new Shader { Name = "Lit" };
        var entry = AssetManager.Cache.GetOrAdd(Guid.Parse(GuidShader));
        entry.Data = shader;
        entry.State = AssetState.Ready;
        var mr = new MeshRenderer { Shader = shader };
        var obj = new JsonObject();
        mr.WriteTo(new SerializedNode(obj));
        Assert.Equal(GuidShader, obj.ToJsonString().Contains(GuidShader)
            ? obj["Shader"]!.GetValue<string>()
            : null);
    }

    [Fact]
    public void ReadFrom_NullGuidFields_LeavesNull()
    {
        var mr = new MeshRenderer();
        var node = new SerializedNode(new JsonObject());
        mr.ReadFrom(node);
        Assert.Null(mr.Shader);
        Assert.Null(mr.Mesh);
        Assert.Null(mr.Material);
    }

    [Fact]
    public void ReadFrom_UnknownGuids_DoesNotThrow_AndLeavesNull()
    {
        var mr = new MeshRenderer();
        var node = new SerializedNode(
            JsonNode.Parse(
                $$"""
                {
                  "Shader": "{{GuidShader}}",
                  "Mesh": "{{GuidMesh}}",
                  "Material": "{{GuidMaterial}}"
                }
                """
            )!.AsObject()
        );

        mr.ReadFrom(node);   // 未命中缓存 → LazyAsync 触发（对伪造路径失败）→ Asset null，不抛异常

        Assert.Null(mr.Shader);
        Assert.Null(mr.Mesh);
        Assert.Null(mr.Material);
    }
}
