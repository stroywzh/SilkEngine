using SilkEngine.Core.Assets;
using SilkEngine.Render;
using SilkEngine.Tests.Core.Assets;

namespace SilkEngine.Tests.Scene;

[Collection("Assets")]
public class MeshRendererAssetTests : IClassFixture<AssetsFixture>
{
    private readonly AssetManager _am;

    public MeshRendererAssetTests(AssetsFixture fixture) => _am = fixture.Manager;

    private AssetEntry RegisterManaged(IAsset asset)
    {
        var entry = _am.Cache.GetOrAdd(Guid.NewGuid());
        entry.Data = asset;
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
    public void Set_Material_Managed_RefPlusOne()
    {
        var mat = new Material { Name = "Mat" };
        var entry = RegisterManaged(mat);
        var mr = new GameObject().AddComponent<MeshRenderer>();
        mr.Material = mat;
        Assert.Equal(1, entry.RefCount);
    }

    [Fact]
    public void Set_Unmanaged_ShaderMeshMaterial_NoOp()
    {
        var mr = new GameObject().AddComponent<MeshRenderer>();
        mr.Shader = new Shader { Name = "S" };      // 未注册 → no-op
        mr.Mesh = new Mesh { Name = "M" };
        mr.Material = new Material { Name = "Mat" };
        Assert.Equal("S", mr.Shader!.Name);
        Assert.Equal("M", mr.Mesh!.Name);
        Assert.Equal("Mat", mr.Material!.Name);
    }

    [Fact]
    public void Replace_ManagedMaterial_OldMinusOne_NewPlusOne()
    {
        var old = new Material { Name = "Old" };
        var fresh = new Material { Name = "Fresh" };
        var eOld = RegisterManaged(old);
        var eFresh = RegisterManaged(fresh);
        var mr = new GameObject().AddComponent<MeshRenderer>();
        mr.Material = old;
        mr.Material = fresh;
        Assert.Equal(0, eOld.RefCount);
        Assert.Equal(1, eFresh.RefCount);
    }

    [Fact]
    public void Set_SameMaterialTwice_NoDoubleCount()
    {
        var mat = new Material { Name = "M" };
        var entry = RegisterManaged(mat);
        var mr = new GameObject().AddComponent<MeshRenderer>();
        mr.Material = mat;
        mr.Material = mat;
        Assert.Equal(1, entry.RefCount);
    }

    [Fact]
    public void OnDestroy_ReleasesAllTrackedAssets()
    {
        var shader = new Shader { Name = "S" };
        var mesh = new Mesh { Name = "M" };
        var mat = new Material { Name = "Mat" };
        var es = RegisterManaged(shader);
        var em = RegisterManaged(mesh);
        var emat = RegisterManaged(mat);
        var go = new GameObject();
        var mr = go.AddComponent<MeshRenderer>();
        mr.Shader = shader;
        mr.Mesh = mesh;
        mr.Material = mat;

        mr.OnDestroy();

        Assert.Equal(0, es.RefCount);
        Assert.Equal(0, em.RefCount);
        Assert.Equal(0, emat.RefCount);
    }

    [Fact]
    public void OnDestroy_ZeroMaterialRef_FiresMaterialDisposed()
    {
        var mat = new Material { Name = "M" };
        RegisterManaged(mat);
        var go = new GameObject();
        var mr = go.AddComponent<MeshRenderer>();
        mr.Material = mat; // RefCount 0 → 1

        var fired = 0;
        mat.MaterialDisposed += _ => fired++;
        mr.OnDestroy();    // SetTracked(ref _material, null) → Release → 归零
        Assert.Equal(1, fired);
    }

    [Fact]
    public void SharedMaterial_OneRendererDestroyed_OthersKeepRef()
    {
        var mat = new Material { Name = "M" };
        var entry = RegisterManaged(mat);
        var go1 = new GameObject();
        var go2 = new GameObject();
        var mr1 = go1.AddComponent<MeshRenderer>();
        var mr2 = go2.AddComponent<MeshRenderer>();
        mr1.Material = mat;
        mr2.Material = mat;
        Assert.Equal(2, entry.RefCount);

        mr1.OnDestroy();

        Assert.Equal(1, entry.RefCount);
        Assert.Same(mat, mr2.Material);
    }
}
