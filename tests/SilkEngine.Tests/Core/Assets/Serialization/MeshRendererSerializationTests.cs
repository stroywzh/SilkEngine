using System.Text.Json.Nodes;
using SilkEngine;
using SilkEngine.Core.Assets;
using SilkEngine.Scene.Serialization;
using SilkEngine.Render;
using SilkEngine.Tests.Core.Assets;

namespace SilkEngine.Tests.Core.Assets.Serialization;

// 反序列化经 Services.TryGet 解析资产管理器（WriteGuid/Resolve ambient），须与注册者同集合串行
[Collection("Assets")]
public class MeshRendererSerializationTests : IClassFixture<AssetsFixture>
{
    private readonly AssetManager _am;

    public MeshRendererSerializationTests(AssetsFixture fixture) => _am = fixture.Manager;

    private const string GuidShader = "1f2e3d4c-5b6a-7988-99aa-bbccddeeff00";
    private const string GuidMesh = "2a3b4c5d-6e7f-80a1-b2c3-d4e5f6071829";
    private const string GuidMaterial = "9f8e7d6c-5b4a-3928-1706-f5e4d3c2b1a0";
    private const string GuidShaderCached = "0e1f2a3b-4c5d-6e7f-8091-a2b3c4d5e6f7";

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
        var entry = _am.Cache.GetOrAdd(Guid.Parse(GuidShader));
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
    public void ReadFrom_CachedGuid_RestoresSameInstance()
    {
        var guid = Guid.Parse(GuidShaderCached);
        var shader = new Shader { Name = "Lit" };
        var entry = _am.Cache.GetOrAdd(guid);
        entry.Data = shader;
        entry.State = AssetState.Ready;

        var mr = new MeshRenderer();
        mr.ReadFrom(new SerializedNode(
            JsonNode.Parse($$""" { "Shader": "{{GuidShaderCached}}" } """)!.AsObject()));

        Assert.Same(shader, mr.Shader);
    }

    [Fact]
    public void ReadFrom_UnknownGuids_DoesNotThrow_AndLeavesNull()
    {
        var mr = new MeshRenderer();
        var node = new SerializedNode(
            JsonNode.Parse(
                $$"""
                {
                  "Shader": "{{Guid.NewGuid()}}",
                  "Mesh": "{{Guid.NewGuid()}}",
                  "Material": "{{Guid.NewGuid()}}"
                }
                """
            )!.AsObject()
        );

        mr.ReadFrom(node);   // 未命中缓存 → 返回 null 占位，不抛异常

        Assert.Null(mr.Shader);
        Assert.Null(mr.Mesh);
        Assert.Null(mr.Material);
    }
}
