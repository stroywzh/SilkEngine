using SilkEngine.Assets;
using SilkEngine.Core;
using SilkEngine.Render;
using SilkEngine.Scene;
using SilkEngine.Tests.Core;
using SilkEngine.Tests.Core.Assets;

namespace SilkEngine.Tests.Scene;

[Collection("Assets")]
public class MeshRendererAssetTests : IDisposable
{
    private readonly AssetManager _am;

    public MeshRendererAssetTests() =>
        _am = TestAssetPipeline.CreateManager();

    public void Dispose() => Services.Unregister<AssetManager>();

    private AssetEntry RegisterManaged(IAsset asset)
    {
        var entry = _am.Cache.GetOrAdd(new AssetId(Guid.NewGuid()));
        entry.Payload = asset;
        entry.State = AssetState.Ready;
        return entry;
    }

    [Fact]
    public void Set_Shader_Managed_RefPlusOne()
    {
        var shader = new Shader { Name = "S" };
        var entry = RegisterManaged(shader);
        var mr = new GameObject().AddComponent<MeshRenderer>();
        mr.Shader = shader;
        Assert.Equal(1, entry.RefCount);
    }

    [Fact]
    public void Set_Mesh_Managed_RefPlusOne()
    {
        var mesh = new Mesh { Name = "M" };
        var entry = RegisterManaged(mesh);
        var mr = new GameObject().AddComponent<MeshRenderer>();
        mr.Mesh = mesh;
        Assert.Equal(1, entry.RefCount);
    }

    [Fact]
    public void Set_Material_NotTrackedAsAsset()
    {
        var mat = new Material(new MaterialReference(new AssetId(Guid.NewGuid())));
        var mr = new GameObject().AddComponent<MeshRenderer>();

        mr.Material = mat;

        Assert.Same(mat, mr.Material);
        Assert.DoesNotContain(_am.Cache.All(), e => ReferenceEquals(e.Payload, mat));
    }

    [Fact]
    public void Set_Unmanaged_ShaderMeshMaterial_NoOp()
    {
        var mr = new GameObject().AddComponent<MeshRenderer>();
        mr.Shader = new Shader { Name = "S" };      // 未注册 → no-op
        mr.Mesh = new Mesh { Name = "M" };
        var mat = new Material(new MaterialReference(new AssetId(Guid.NewGuid())));
        mr.Material = mat;
        Assert.Equal("S", mr.Shader!.Name);
        Assert.Equal("M", mr.Mesh!.Name);
        Assert.Same(mat, mr.Material);
    }

    [Fact]
    public void Replace_ManagedMesh_OldMinusOne_NewPlusOne()
    {
        var old = new Mesh { Name = "Old" };
        var fresh = new Mesh { Name = "Fresh" };
        var eOld = RegisterManaged(old);
        var eFresh = RegisterManaged(fresh);
        var mr = new GameObject().AddComponent<MeshRenderer>();
        mr.Mesh = old;
        mr.Mesh = fresh;
        Assert.Equal(0, eOld.RefCount);
        Assert.Equal(1, eFresh.RefCount);
    }

    [Fact]
    public void Set_SameMeshTwice_NoDoubleCount()
    {
        var mesh = new Mesh { Name = "M" };
        var entry = RegisterManaged(mesh);
        var mr = new GameObject().AddComponent<MeshRenderer>();
        mr.Mesh = mesh;
        mr.Mesh = mesh;
        Assert.Equal(1, entry.RefCount);
    }

    [Fact]
    public void OnDestroy_ReleasesAllTrackedAssets()
    {
        var shader = new Shader { Name = "S" };
        var mesh = new Mesh { Name = "M" };
        var es = RegisterManaged(shader);
        var em = RegisterManaged(mesh);
        var go = new GameObject();
        var mr = go.AddComponent<MeshRenderer>();
        mr.Shader = shader;
        mr.Mesh = mesh;
        mr.Material = new Material(new MaterialReference(new AssetId(Guid.NewGuid())));

        mr.OnDestroy();

        Assert.Equal(0, es.RefCount);
        Assert.Equal(0, em.RefCount);
    }

    [Fact]
    public void SharedMesh_OneRendererDestroyed_OthersKeepRef()
    {
        var mesh = new Mesh { Name = "M" };
        var entry = RegisterManaged(mesh);
        var go1 = new GameObject();
        var go2 = new GameObject();
        var mr1 = go1.AddComponent<MeshRenderer>();
        var mr2 = go2.AddComponent<MeshRenderer>();
        mr1.Mesh = mesh;
        mr2.Mesh = mesh;
        Assert.Equal(2, entry.RefCount);

        mr1.OnDestroy();

        Assert.Equal(1, entry.RefCount);
        Assert.Same(mesh, mr2.Mesh);
    }
}
