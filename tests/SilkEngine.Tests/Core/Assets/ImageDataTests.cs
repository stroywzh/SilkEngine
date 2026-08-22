using SilkEngine.Assets;

namespace SilkEngine.Tests.Core.Assets;

public class ImageDataTests
{
    [Fact]
    public void Constructor_PixelsShorterThanWidthTimesHeightTimesChannels_Throws()
    {
        // RGBA8：2x2 需要 16 字节
        Assert.Throws<ArgumentException>(() => new ImageData(2, 2, new byte[15]));
    }

    [Fact]
    public void Constructor_ExactPixelLength_Succeeds()
    {
        var data = new ImageData(2, 2, new byte[16]);
        Assert.Equal(16, data.Pixels.Length);
    }

    [Fact]
    public void Constructor_ZeroSized_Succeeds()
    {
        var data = new ImageData(0, 0, []);
        Assert.Empty(data.Pixels);
    }
}
