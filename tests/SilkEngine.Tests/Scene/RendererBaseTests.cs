using SilkEngine.Assets;
using SilkEngine.Assets.Importer;
using SilkEngine.Assets.VirtualFileSystem;
using SilkEngine.Core;
using SilkEngine.Render;
using SilkEngine.Scene;
using SilkEngine.Tests.Core;
using SilkEngine.Tests.Render;

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
        var es = RegisterManaged(shader);
        var em = RegisterManaged(mesh);
        var ui = new GameObject().AddComponent<UIRenderer>();

        ui.Shader = shader;
        ui.Mesh = mesh;
        ui.Material = new Material(new MaterialReference(new AssetId(Guid.NewGuid())));

        Assert.Equal(1, es.RefCount);
        Assert.Equal(1, em.RefCount);
    }

    [Fact]
    public void ReplaceAsset_OldMinusOne_NewPlusOne()
    {
        var old = new Shader { Name = "Old" };
        var fresh = new Shader { Name = "Fresh" };
        var eOld = RegisterManaged(old);
        var eFresh = RegisterManaged(fresh);
        var ui = new GameObject().AddComponent<UIRenderer>();

        ui.Shader = old;
        ui.Shader = fresh;

        Assert.Equal(0, eOld.RefCount);
        Assert.Equal(1, eFresh.RefCount);
    }

    [Fact]
    public void OnDestroy_ReleasesAllTrackedAssets()
    {
        var shader = new Shader { Name = "S" };
        var mesh = new Mesh { Name = "M" };
        var es = RegisterManaged(shader);
        var em = RegisterManaged(mesh);
        var ui = new GameObject().AddComponent<UIRenderer>();
        ui.Shader = shader;
        ui.Mesh = mesh;
        ui.Material = new Material(new MaterialReference(new AssetId(Guid.NewGuid())));

        ui.OnDestroy();

        Assert.Equal(0, es.RefCount);
        Assert.Equal(0, em.RefCount);
    }

    [Fact]
    public void RendererBase_MaterialAssignmentDoesNotRegisterBusinessMaterialAsAsset()
    {
        var renderer = new GameObject().AddComponent<UIRenderer>();
        var material = new Material(new MaterialReference(new AssetId(Guid.NewGuid())));

        renderer.Material = material;

        Assert.Same(material, renderer.Material);
        Assert.DoesNotContain(_am.Cache.All(), e => ReferenceEquals(e.Data, material));
    }

    [Fact]
    public void RenderCollectionUsesBoundMaterialSnapshot()
    {
        var material = Fixtures.MaterialInstanceWithSource();
        var binding = Fixtures.ReadyBindingFor(material);
        var command = Fixtures.CollectSingleDraw(material, binding);

        Assert.Equal(binding.Resolve(material).Value!.Parameters, command.Material.Parameters);
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
