using SilkEngine.Core.Assets;

namespace SilkEngine.Tests.Core.Assets;

public class AssetTypesTests
{
    [Fact]
    public void ImageData_ExposesSizeAndPixels()
    {
        byte[] pixels = [255, 0, 0, 255];
        var data = new ImageData(1, 1, pixels);
        Assert.Equal(1, data.Width);
        Assert.Equal(1, data.Height);
        Assert.Same(pixels, data.Pixels);
    }

    [Fact]
    public void Texture2D_IsAsset_WithNameAndImageData()
    {
        var tex = new Texture2D { Name = "Red", ImageData = new ImageData(1, 1, [255, 0, 0, 255]) };
        Assert.IsAssignableFrom<IAsset>(tex);
        Assert.Equal("Red", tex.Name);
        Assert.Equal(1, tex.ImageData.Width);
    }

    [Fact]
    public void Texture2D_DefaultImageData_IsEmpty()
    {
        var tex = new Texture2D();
        Assert.Equal(0, tex.ImageData.Width);
        Assert.Empty(tex.ImageData.Pixels);
    }
}
