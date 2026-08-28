using System;
using System.Linq;
using System.Reflection;
using SilkEngine.Assets;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Tests.Assets;

/// <summary>
/// 渲染资源创建关联契约：创建批次只携带 RequestId（无资产身份），
/// Assets 侧经 AssetGpuResourceCache 按 RequestId 关联 (AssetId, Revision, Kind)。
/// </summary>
public class AssetGpuPublicationTests
{
    [Fact]
    public void CreateBatch_ContainsRequestIdButNoAssetIdentity()
    {
        var item = new RenderResourceCreateItem(
            new RenderResourceRequestId(1),
            new RenderTextureCreateRequest(
                new RenderTextureDescriptor(1, 1, 4),
                new byte[] { 255, 255, 255, 255 }));
        var batch = new RenderResourceCreateBatch(new[] { item });

        Assert.Equal(1UL, batch.Items[0].RequestId.Value);
        var propertyNames = typeof(RenderResourceCreateItem)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(x => x.Name)
            .ToArray();
        Assert.DoesNotContain("AssetId", propertyNames);
        Assert.DoesNotContain("AssetHandle", propertyNames);
    }

    [Fact]
    public void AssetGpuResourceCache_ResolvesRequestIdByAssetRevision()
    {
        var cache = new AssetGpuResourceCache();
        var assetId = new AssetId(Guid.NewGuid());
        cache.TrackRequest(new RenderResourceRequestId(9), assetId, 3, RenderResourceKind.Texture);

        Assert.True(cache.TryResolveRequest(new RenderResourceRequestId(9), out var tracked));
        Assert.Equal(assetId, tracked.AssetId);
        Assert.Equal(3UL, tracked.Revision);
        Assert.Equal(RenderResourceKind.Texture, tracked.Kind);
    }

    [Fact]
    public void AssetGpuResourceCache_RemoveRequest_DropsCorrelation()
    {
        var cache = new AssetGpuResourceCache();
        var id = new RenderResourceRequestId(7);
        cache.TrackRequest(id, new AssetId(Guid.NewGuid()), 1, RenderResourceKind.Mesh);

        Assert.True(cache.RemoveRequest(id));
        Assert.False(cache.TryResolveRequest(id, out _));
        Assert.False(cache.RemoveRequest(id));
    }

    [Fact]
    public void AssetGpuResourceCache_TryResolveRequest_MissReturnsFalse()
    {
        var cache = new AssetGpuResourceCache();

        Assert.False(cache.TryResolveRequest(new RenderResourceRequestId(42), out var tracked));
        Assert.Null(tracked);
    }
}