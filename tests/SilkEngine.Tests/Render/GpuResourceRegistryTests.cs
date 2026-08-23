using SilkEngine.Assets;
using SilkEngine.Render.OpenGL;

namespace SilkEngine.Tests.Render;

public class GpuResourceRegistryTests
{
    private sealed class Asset : IAsset { public string Name { get; init; } = ""; }
    private sealed class Gpu : IDisposable
    {
        public bool Disposed;
        public void Dispose() => Disposed = true;
    }

    [Fact]
    public void GetOrCreate_SameInstance_Reuses()
    {
        var reg = new GpuResourceRegistry();
        var asset = new Asset();
        var factoryCalls = 0;
        var g1 = reg.GetOrCreate(asset, a => { factoryCalls++; return new Gpu(); });
        var g2 = reg.GetOrCreate(asset, a => { factoryCalls++; return new Gpu(); });
        Assert.Same(g1, g2);
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public void GetOrCreate_DifferentInstances_NoShare()
    {
        var reg = new GpuResourceRegistry();
        var g1 = reg.GetOrCreate(new Asset(), _ => new Gpu());
        var g2 = reg.GetOrCreate(new Asset(), _ => new Gpu());
        Assert.NotSame(g1, g2);
    }

    [Fact]
    public void Evict_DisposesAndRemoves()
    {
        var reg = new GpuResourceRegistry();
        var asset = new Asset();
        var gpu = reg.GetOrCreate(asset, _ => new Gpu());
        reg.Evict(asset);
        Assert.True(gpu.Disposed);
        var g2 = reg.GetOrCreate(asset, _ => new Gpu());
        Assert.NotSame(gpu, g2); // 驱逐后可重建
    }

    [Fact]
    public void ReleaseAll_IsIdempotent()
    {
        var reg = new GpuResourceRegistry();
        reg.GetOrCreate(new Asset(), _ => new Gpu());
        reg.GetOrCreate(new Asset(), _ => new Gpu());
        reg.ReleaseAll();
        reg.ReleaseAll(); // 不抛
    }
}
