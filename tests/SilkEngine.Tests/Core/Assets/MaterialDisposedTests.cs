using SilkEngine.Assets;
using SilkEngine.Render;

namespace SilkEngine.Tests.Core.Assets;

[Collection("Assets")]
public class MaterialDisposedTests : IClassFixture<AssetsFixture>
{
    private readonly AssetManager _am;

    public MaterialDisposedTests(AssetsFixture fixture) => _am = fixture.Manager;

    private AssetEntry RegisterManaged(IAsset asset)
    {
        var entry = _am.Cache.GetOrAdd(Guid.NewGuid());
        entry.Data = asset;
        entry.State = AssetState.Ready;
        return entry;
    }

    [Fact]
    public void Release_ToZero_FiresMaterialDisposed()
    {
        var mat = new Material { Name = "M" };
        var entry = RegisterManaged(mat);
        _am.TryAddRef(mat);
        var fired = 0;
        mat.MaterialDisposed += _ => fired++;
        _am.Release(mat);
        Assert.Equal(1, fired);
        Assert.Equal(0, entry.RefCount);
    }

    [Fact]
    public void Release_NotToZero_DoesNotFire()
    {
        var mat = new Material { Name = "M" };
        RegisterManaged(mat);
        _am.TryAddRef(mat);
        _am.TryAddRef(mat);
        var fired = 0;
        mat.MaterialDisposed += _ => fired++;
        _am.Release(mat); // 2 → 1，未归零
        Assert.Equal(0, fired);
    }

    [Fact]
    public void UnmanagedMaterial_Release_DoesNotFire()
    {
        var mat = new Material { Name = "M" }; // 非托管：无缓存条目
        var fired = 0;
        mat.MaterialDisposed += _ => fired++;
        _am.Release(mat);
        Assert.Equal(0, fired);
    }

    [Fact]
    public void TextureRelease_DoesNotFireMaterialDisposed()
    {
        var tex = new Texture2D { Name = "T" };
        var entry = RegisterManaged(tex);
        _am.TryAddRef(tex);
        var fired = 0;
        var mat = new Material { Name = "M" };
        mat.MaterialDisposed += _ => fired++;
        _am.Release(tex);
        Assert.Equal(0, fired);
        Assert.Equal(0, entry.RefCount);
    }
}
