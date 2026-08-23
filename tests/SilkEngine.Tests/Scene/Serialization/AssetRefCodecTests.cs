using System.Text.Json.Nodes;
using SilkEngine.Assets;
using SilkEngine.Core;
using SilkEngine.Render;
using SilkEngine.Scene.Serialization;
using SilkEngine.Threading;
using Xunit;

namespace SilkEngine.Tests.Scene.Serialization;

// 契约 C3：自建 AssetManager 实例访问（AssetRefCodec 内部经 Services 解析，
// 本类自建实例，与本集合内其他测试串行，避免重复注册/注销竞争）
[Collection("Assets")]
public class AssetRefCodecTests : IDisposable
{
    private readonly AssetManager _am;

    public AssetRefCodecTests() => _am = new AssetManager(new ThreadPoolExecutor());

    public void Dispose() => Services.Unregister<AssetManager>();

    private Shader RegisterManaged(Guid guid)
    {
        var shader = new Shader { Name = "S" };
        var entry = _am.Cache.GetOrAdd(guid);
        entry.Data = shader;
        entry.State = AssetState.Ready;
        return shader;
    }

    [Fact]
    public void Write_ManagedAsset_WritesGuidString()
    {
        var guid = Guid.NewGuid();
        var shader = RegisterManaged(guid);
        var node = new SerializedNode(new JsonObject());

        AssetRefCodec.Write(node, "Shader", shader);

        Assert.Equal(guid.ToString(), node.GetString("Shader"));
    }

    [Fact]
    public void Write_NullOrUnmanaged_OmitsKey()
    {
        var node = new SerializedNode(new JsonObject());
        AssetRefCodec.Write(node, "A", null);
        AssetRefCodec.Write(node, "B", new Shader { Name = "unmanaged" });
        Assert.Equal("{}", node.AsJson());
    }

    [Fact]
    public void Read_CachedGuid_ReturnsSameAsset()
    {
        var guid = Guid.NewGuid();
        var shader = RegisterManaged(guid);
        var node = new SerializedNode(JsonNode.Parse($$""" { "Shader": "{{guid}}" } """)!.AsObject());

        Assert.Same(shader, AssetRefCodec.Read<Shader>(node, "Shader"));
    }

    [Fact]
    public void Read_InvalidOrUnknownGuid_ReturnsNull()
    {
        var node = new SerializedNode(JsonNode.Parse(
            """ { "A": "not-a-guid", "B": "11111111-1111-1111-1111-111111111111" } """)!.AsObject());
        Assert.Null(AssetRefCodec.Read<Shader>(node, "A"));
        Assert.Null(AssetRefCodec.Read<Shader>(node, "B"));
        Assert.Null(AssetRefCodec.Read<Shader>(node, "Missing"));
    }

    [Fact]
    public void ReadTracked_AssignsAndRefcounts()
    {
        var guid = Guid.NewGuid();
        var shader = RegisterManaged(guid);
        var node = new SerializedNode(JsonNode.Parse($$""" { "Shader": "{{guid}}" } """)!.AsObject());
        Shader? field = null;

        AssetRefCodec.ReadTracked(ref field, node, "Shader");

        Assert.Same(shader, field);
        Assert.Equal(1, _am.Cache.Find(guid)!.RefCount);
    }
}
