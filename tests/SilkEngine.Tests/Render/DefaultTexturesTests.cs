using SilkEngine.Assets;
using SilkEngine.Render;

namespace SilkEngine.Tests.Render;

public class DefaultTexturesTests
{
    [Fact]
    public void White_Is1x1()
    {
        var tex = DefaultTextures.White;

        Assert.Equal(1, tex.ImageData.Width);
        Assert.Equal(1, tex.ImageData.Height);
    }

    [Fact]
    public void White_IsOpaqueWhite()
    {
        var tex = DefaultTextures.White;

        Assert.Equal(new byte[] { 255, 255, 255, 255 }, tex.ImageData.Pixels);
    }

    [Fact]
    public void White_IsNamedDefaultWhite()
    {
        Assert.Equal("DefaultWhite", DefaultTextures.White.Name);
    }
}
