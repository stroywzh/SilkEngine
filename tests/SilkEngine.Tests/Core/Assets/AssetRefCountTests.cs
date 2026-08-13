using SilkEngine.Core.Assets;

namespace SilkEngine.Tests.Core.Assets;

[Collection("Assets")]
public class AssetRefCountTests
{
    private static AssetEntry RegisterManaged(IAsset asset)
    {
        var entry = AssetManager.Cache.GetOrAdd(Guid.NewGuid());
        entry.Data = asset;
        entry.State = AssetState.Ready;
        return entry;
    }

    [Fact]
    public void TryAddRef_ManagedAsset_Increments()
    {
        var tex = new Texture2D { Name = "T" };
        var entry = RegisterManaged(tex);
        Assert.True(AssetManager.TryAddRef(tex));
        Assert.Equal(1, entry.RefCount);
    }

    [Fact]
    public void TryAddRef_UnmanagedAsset_IsNoOp()
    {
        var tex = new Texture2D { Name = "U" };
        Assert.False(AssetManager.TryAddRef(tex));
    }

    [Fact]
    public void TryRelease_Decrements_AndClampsAtZero()
    {
        var tex = new Texture2D { Name = "T" };
        var entry = RegisterManaged(tex);
        AssetManager.TryAddRef(tex);
        Assert.True(AssetManager.TryRelease(tex));
        Assert.Equal(0, entry.RefCount);
        Assert.False(AssetManager.TryRelease(tex)); // 重复释放 → false，不抛异常
        Assert.Equal(0, entry.RefCount);            // 下限钳制，不为负
    }

    [Fact]
    public void Release_UserApi_Decrements()
    {
        var tex = new Texture2D { Name = "T" };
        var entry = RegisterManaged(tex);
        AssetManager.TryAddRef(tex);
        AssetManager.Release(tex);
        Assert.Equal(0, entry.RefCount);
    }

    [Fact]
    public void SetTracked_NewValuePlusOne_OldValueMinusOne()
    {
        var a = new Texture2D { Name = "A" };
        var b = new Texture2D { Name = "B" };
        var ea = RegisterManaged(a);
        var eb = RegisterManaged(b);
        Texture2D? field = null;

        AssetManager.SetTracked(ref field, a);
        Assert.Equal(1, ea.RefCount);
        Assert.Equal(0, eb.RefCount);

        AssetManager.SetTracked(ref field, b);
        Assert.Equal(0, ea.RefCount);
        Assert.Equal(1, eb.RefCount);
        Assert.Same(b, field);
    }

    [Fact]
    public void SetTracked_SameInstance_NoDoubleCount()
    {
        var a = new Texture2D { Name = "A" };
        var entry = RegisterManaged(a);
        Texture2D? field = null;
        AssetManager.SetTracked(ref field, a);
        AssetManager.SetTracked(ref field, a);
        Assert.Equal(1, entry.RefCount);
    }

    [Fact]
    public void SetTracked_Null_ReleasesOldValue()
    {
        var a = new Texture2D { Name = "A" };
        var entry = RegisterManaged(a);
        Texture2D? field = a;
        AssetManager.SetTracked(ref field, null);
        Assert.Equal(0, entry.RefCount);
        Assert.Null(field);
    }

    [Fact]
    public void SetTracked_UnmanagedOldAndNew_NoOp()
    {
        var a = new Texture2D { Name = "A" };
        Texture2D? field = null;
        AssetManager.SetTracked(ref field, a); // 非托管 → 无条目 → no-op
        Assert.Same(a, field);
        AssetManager.SetTracked(ref field, null);
    }
}
