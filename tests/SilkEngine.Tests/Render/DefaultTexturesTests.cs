using SilkEngine.Assets;
using SilkEngine.Render;

namespace SilkEngine.Tests.Render;

public class DefaultTexturesTests
{
    [Fact]
    public void White_Is1x1()
    {
        var tex = DefaultTextures.White;

        Assert.Equal(1, tex.Data.Width);
        Assert.Equal(1, tex.Data.Height);
    }

    [Fact]
    public void White_IsOpaqueWhite()
    {
        var tex = DefaultTextures.White;

        Assert.Equal(new byte[] { 255, 255, 255, 255 }, tex.Data.RawBytes);
    }

    [Fact]
    public void White_IsNamedDefaultWhite()
    {
        Assert.Equal("DefaultWhite", DefaultTextures.White.Name);
    }
}
