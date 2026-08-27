using SilkEngine.Assets;
using SilkEngine.Assets.Importer;
using SilkEngine.Assets.VirtualFileSystem;
using SilkEngine.Core;
using SilkEngine.Render;
using SilkEngine.Scene;
using SilkEngine.Tests.Core;

namespace SilkEngine.Tests.Scene;

[Collection("Assets")]
public class RendererBaseTests : IDisposable
{
    private readonly AssetManager _am;

    public RendererBaseTests() =>
        _am = new AssetManager(new InMemoryAssetFileSystem("Assets"), new AssetImporterRegistry(), new RecordingScheduler());

    public void Dispose() => Services.Unregister<AssetManager>();

    private AssetEntry RegisterManaged(IAsset asset)
    {
        var entry = _am.Cache.GetOrAdd(new AssetId(Guid.NewGuid()));
        entry.Data = asset;
        entry.State = AssetState.Ready;
        return entry;
    }

    [Fact]
    public void Defaults_AllAssetsNull()
    {
        var ui = new GameObject().AddComponent<UIRenderer>();

        Assert.Null(ui.Shader);
        Assert.Null(ui.Mesh);
        Assert.Null(ui.Material);
    }

    [Fact]
    public void SetAssets_TrackedRefPlusOne()
    {
        var shader = new Shader { Name = "S" };
        var mesh = new Mesh { Name = "M" };
        var mat = new Material { Name = "Mat" };
        var es = RegisterManaged(shader);
        var em = RegisterManaged(mesh);
        var emat = RegisterManaged(mat);
        var ui = new GameObject().AddComponent<UIRenderer>();

        ui.Shader = shader;
        ui.Mesh = mesh;
        ui.Material = mat;

        Assert.Equal(1, es.RefCount);
        Assert.Equal(1, em.RefCount);
        Assert.Equal(1, emat.RefCount);
    }

    [Fact]
    public void ReplaceAsset_OldMinusOne_NewPlusOne()
    {
        var old = new Material { Name = "Old" };
        var fresh = new Material { Name = "Fresh" };
        var eOld = RegisterManaged(old);
        var eFresh = RegisterManaged(fresh);
        var ui = new GameObject().AddComponent<UIRenderer>();

        ui.Material = old;
        ui.Material = fresh;

        Assert.Equal(0, eOld.RefCount);
        Assert.Equal(1, eFresh.RefCount);
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
        var ui = new GameObject().AddComponent<UIRenderer>();
        ui.Shader = shader;
        ui.Mesh = mesh;
        ui.Material = mat;

        ui.OnDestroy();

        Assert.Equal(0, es.RefCount);
        Assert.Equal(0, em.RefCount);
        Assert.Equal(0, emat.RefCount);
    }

    [Fact]
    public void WorldMatrix_ComesFromTransform()
    {
        var go = new GameObject("Quad");
        go.Transform.LocalPosition = new SilkEngine.Math.Vector3(1, 2, 3);
        var ui = go.AddComponent<UIRenderer>();

        Assert.Equal(go.Transform.LocalToWorldMatrix, ui.WorldMatrix);
    }
}
