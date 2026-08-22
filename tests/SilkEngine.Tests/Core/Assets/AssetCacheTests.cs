using SilkEngine.Assets;

namespace SilkEngine.Tests.Core.Assets;

[Collection("Assets")]
public class AssetCacheTests
{
    [Fact]
    public void Find_Missing_ReturnsNull()
    {
        var cache = new AssetCache();
        Assert.Null(cache.Find(Guid.NewGuid()));
    }

    [Fact]
    public void GetOrAdd_CreatesEntry_InLoadingState()
    {
        var cache = new AssetCache();
        var guid = Guid.NewGuid();
        var entry = cache.GetOrAdd(guid);
        Assert.Equal(guid, entry.Guid);
        Assert.Equal(AssetState.Loading, entry.State);
        Assert.Null(entry.Data);
        Assert.Null(entry.Pending);
        Assert.Empty(entry.Awaiters);
        Assert.Equal(0, entry.RefCount);
    }

    [Fact]
    public void GetOrAdd_ReturnsSameInstance_ForSameGuid()
    {
        var cache = new AssetCache();
        var guid = Guid.NewGuid();
        Assert.Same(cache.GetOrAdd(guid), cache.GetOrAdd(guid));
    }

    [Fact]
    public void GetOrAdd_DifferentGuids_CreateDistinctEntries()
    {
        var cache = new AssetCache();
        Assert.NotSame(cache.GetOrAdd(Guid.NewGuid()), cache.GetOrAdd(Guid.NewGuid()));
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void Remove_ExistingGuid_RemovesEntry_AndCountDecrements()
    {
        var cache = new AssetCache();
        var guid = Guid.NewGuid();
        cache.GetOrAdd(guid);
        Assert.True(cache.Remove(guid));
        Assert.Null(cache.Find(guid));
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void Remove_MissingGuid_ReturnsFalse()
    {
        var cache = new AssetCache();
        Assert.False(cache.Remove(Guid.NewGuid()));
    }

    private sealed class TestAsset : IAsset { }

    [Fact]
    public void FindByAsset_AfterDataSet_HitsDirect()
    {
        var cache = new AssetCache();
        var entry = cache.GetOrAdd(Guid.NewGuid());
        var asset = new TestAsset();
        cache.SetData(entry, asset);

        Assert.Same(entry, cache.FindByAsset(asset));
    }

    [Fact]
    public void FindByAsset_AfterRemove_ReturnsNull()
    {
        var cache = new AssetCache();
        var guid = Guid.NewGuid();
        var entry = cache.GetOrAdd(guid);
        var asset = new TestAsset();
        cache.SetData(entry, asset);
        Assert.Same(entry, cache.FindByAsset(asset));

        Assert.True(cache.Remove(guid));
        Assert.Null(cache.FindByAsset(asset));
    }

    [Fact]
    public void FindByAsset_DataReplaced_IndexUpdated()
    {
        var cache = new AssetCache();
        var entry = cache.GetOrAdd(Guid.NewGuid());
        var a = new TestAsset();
        var b = new TestAsset();
        cache.SetData(entry, a);
        Assert.Same(entry, cache.FindByAsset(a));

        cache.SetData(entry, b);
        Assert.Null(cache.FindByAsset(a));
        Assert.Same(entry, cache.FindByAsset(b));
    }

    [Fact]
    public void FindByAsset_Null_ReturnsNull()
    {
        var cache = new AssetCache();
        Assert.Null(cache.FindByAsset(null!));
    }

    [Fact]
    public void FindByAsset_DirectDataAssignment_StillResolves()
    {
        var cache = new AssetCache();
        var entry = cache.GetOrAdd(Guid.NewGuid());
        var asset = new TestAsset();
        entry.Data = asset;

        Assert.Same(entry, cache.FindByAsset(asset));
        Assert.Same(entry, cache.FindByAsset(asset));
    }
}
