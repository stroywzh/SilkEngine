using SilkEngine.Assets;
using SilkEngine.Core;
using SilkEngine.Render;
using SilkEngine.Threading;

namespace SilkEngine.Tests.Render;

// 与 Part 2 资产测试同集合：本类自建实例缓存（Services.TryGet ambient 解析）
[Collection("Assets")]
public class MaterialMainTextureTests : IDisposable
{
    private readonly AssetManager _am;

    public MaterialMainTextureTests() => _am = new AssetManager(new ThreadPoolExecutor());

    public void Dispose() => Services.Unregister<AssetManager>();

    private static Texture2D MakeTex(string name) =>
        new() { Name = name, Data = new ImageData(1, 1, [255, 255, 255, 255]) };

    // Part 2 测试惯例：先登记缓存条目（托管化），再经 entry.RefCount 断言
    private AssetEntry RegisterManaged(IAsset asset)
    {
        var entry = _am.Cache.GetOrAdd(Guid.NewGuid());
        entry.Data = asset;
        entry.State = AssetState.Ready;
        return entry;
    }

    [Fact]
    public void MainTexture_Set_AddsRefToNewValue()
    {
        var mat = new Material();
        var tex = MakeTex("T");
        var entry = RegisterManaged(tex);

        mat.MainTexture = tex;

        Assert.Equal(1, entry.RefCount);
    }

    [Fact]
    public void MainTexture_Replace_ReleasesOldValue()
    {
        var mat = new Material();
        var oldTex = MakeTex("Old");
        var newTex = MakeTex("New");
        var oldEntry = RegisterManaged(oldTex);
        var newEntry = RegisterManaged(newTex);
        mat.MainTexture = oldTex;

        mat.MainTexture = newTex;

        Assert.Equal(0, oldEntry.RefCount);
        Assert.Equal(1, newEntry.RefCount);
    }

    [Fact]
    public void MainTexture_Clear_ReleasesValue()
    {
        var mat = new Material();
        var tex = MakeTex("T");
        var entry = RegisterManaged(tex);
        mat.MainTexture = tex;

        mat.MainTexture = null;

        Assert.Equal(0, entry.RefCount);
    }

    [Fact]
    public void MaterialDisposed_CascadesReleaseToMainTexture()
    {
        var mat = new Material();
        var matEntry = RegisterManaged(mat);
        var tex = MakeTex("T");
        var texEntry = RegisterManaged(tex);
        mat.MainTexture = tex; // tex 0 → 1

        _am.TryAddRef(mat);  // mat 0 → 1
        _am.TryRelease(mat); // mat 1 → 0 → 同步触发 MaterialDisposed → 级联 TryRelease(tex)

        Assert.Equal(0, matEntry.RefCount);
        Assert.Equal(0, texEntry.RefCount);
    }
}
