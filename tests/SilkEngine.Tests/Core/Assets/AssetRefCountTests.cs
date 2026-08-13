using SilkEngine.Core.Assets;
using SilkEngine.Render;

namespace SilkEngine.Tests.Core.Assets;

[Collection("Assets")]
public class AssetRefCountTests
{
    private static AssetEntry RegisterManaged(AssetManager am, IAsset asset)
    {
        var entry = am.Cache.GetOrAdd(Guid.NewGuid());
        entry.Data = asset;
        entry.State = AssetState.Ready;
        return entry;
    }

    [Fact]
    public void TryAddRef_ManagedAsset_Increments()
    {
        var am = new AssetManager(new RecordingScheduler());
        var tex = new Texture2D { Name = "T" };
        var entry = RegisterManaged(am, tex);
        Assert.True(am.TryAddRef(tex));
        Assert.Equal(1, entry.RefCount);
    }

    [Fact]
    public void TryAddRef_UnmanagedAsset_IsNoOp()
    {
        var am = new AssetManager(new RecordingScheduler());
        var tex = new Texture2D { Name = "U" };
        Assert.False(am.TryAddRef(tex));
    }

    [Fact]
    public void TryRelease_Decrements_AndClampsAtZero()
    {
        var am = new AssetManager(new RecordingScheduler());
        var tex = new Texture2D { Name = "T" };
        var entry = RegisterManaged(am, tex);
        am.TryAddRef(tex);
        Assert.True(am.TryRelease(tex));
        Assert.Equal(0, entry.RefCount);
        Assert.False(am.TryRelease(tex)); // 重复释放 → false，不抛异常
        Assert.Equal(0, entry.RefCount);            // 下限钳制，不为负
    }

    [Fact]
    public void Release_UserApi_Decrements()
    {
        var am = new AssetManager(new RecordingScheduler());
        var tex = new Texture2D { Name = "T" };
        var entry = RegisterManaged(am, tex);
        am.TryAddRef(tex);
        am.Release(tex);
        Assert.Equal(0, entry.RefCount);
    }

    [Fact]
    public void SetTracked_NewValuePlusOne_OldValueMinusOne()
    {
        var am = new AssetManager(new RecordingScheduler());
        var a = new Texture2D { Name = "A" };
        var b = new Texture2D { Name = "B" };
        var ea = RegisterManaged(am, a);
        var eb = RegisterManaged(am, b);
        Texture2D? field = null;

        am.SetTracked(ref field, a);
        Assert.Equal(1, ea.RefCount);
        Assert.Equal(0, eb.RefCount);

        am.SetTracked(ref field, b);
        Assert.Equal(0, ea.RefCount);
        Assert.Equal(1, eb.RefCount);
        Assert.Same(b, field);
    }

    [Fact]
    public void SetTracked_SameInstance_NoDoubleCount()
    {
        var am = new AssetManager(new RecordingScheduler());
        var a = new Texture2D { Name = "A" };
        var entry = RegisterManaged(am, a);
        Texture2D? field = null;
        am.SetTracked(ref field, a);
        am.SetTracked(ref field, a);
        Assert.Equal(1, entry.RefCount);
    }

    [Fact]
    public void SetTracked_Null_ReleasesOldValue()
    {
        var am = new AssetManager(new RecordingScheduler());
        var a = new Texture2D { Name = "A" };
        var entry = RegisterManaged(am, a);
        Texture2D? field = a;
        am.SetTracked(ref field, null);
        Assert.Equal(0, entry.RefCount);
        Assert.Null(field);
    }

    [Fact]
    public void SetTracked_UnmanagedOldAndNew_NoOp()
    {
        var am = new AssetManager(new RecordingScheduler());
        var a = new Texture2D { Name = "A" };
        Texture2D? field = null;
        am.SetTracked(ref field, a); // 非托管 → 无条目 → no-op
        Assert.Same(a, field);
        am.SetTracked(ref field, null);
    }

    [Fact]
    public void SetTracked_ShaderMeshMaterial_AreTrackableAssets()
    {
        var am = new AssetManager(new RecordingScheduler());
        var shader = new Shader { Name = "S" };
        var mesh = new Mesh { Name = "M" };
        var material = new Material { Name = "Mat" };
        Assert.IsAssignableFrom<IAsset>(shader);
        Assert.IsAssignableFrom<IAsset>(mesh);
        Assert.IsAssignableFrom<IAsset>(material);
    }
}
