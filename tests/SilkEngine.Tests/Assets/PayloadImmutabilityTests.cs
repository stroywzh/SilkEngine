using SilkEngine.Assets;
using SilkEngine.Render;

namespace SilkEngine.Tests.Assets;

/// <summary>Payload 不可变性测试：构造时复制可变数组、材质实例覆盖相互独立</summary>
public class PayloadImmutabilityTests
{
    [Fact]
    public void PayloadConstructors_CopyMutableArrays()
    {
        var pixels = new byte[] { 1, 2, 3, 4 };
        var vertices = new float[] { 0, 1, 2 };
        var layout = new[] { 3 };
        var mesh = new MeshAsset("mesh", vertices, layout, null);
        var image = new ImageData(1, 1, pixels);

        pixels[0] = 9;
        vertices[0] = 9;
        layout[0] = 9;

        Assert.Equal(1, image.RawBytes[0]);
        Assert.Equal(0, mesh.Vertices[0]);
        Assert.Equal(3, mesh.Layout[0]);
    }

    [Fact]
    public void MaterialAsset_ToInstance_CreatesIndependentOverrides()
    {
        var asset = new MaterialAsset(
            new AssetId(Guid.NewGuid()),
            new AssetHandle<ShaderAsset>(new AssetId(Guid.NewGuid())),
            null,
            new MaterialParameterSnapshot([]),
            revision: 1);

        var first = asset.ToInstance();
        var second = asset.ToInstance();
        first.SetFloat("Roughness", 0.2f);

        Assert.NotEqual(first.Overrides.Version, second.Overrides.Version);
        Assert.False(second.Overrides.TryGetFloat("Roughness", out _));
    }
}
