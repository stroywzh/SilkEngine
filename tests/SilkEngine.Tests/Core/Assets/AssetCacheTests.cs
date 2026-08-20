using SilkEngine.Core.Assets;

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
}
