using SilkEngine.Assets;
using SilkEngine.Render.OpenGL;

namespace SilkEngine.Tests.Render;

public class OpenGLTextureRegistryTests
{
    private static Texture2D MakeTex(string name) =>
        new() { Name = name, ImageData = new ImageData(1, 1, [255, 255, 255, 255]) };

    [Fact]
    public void GetOrCreate_Hit_ReturnsSameInstance_WithoutNewFactoryCall()
    {
        var tex = MakeTex("T");
        int created = 0;
        var reg = new OpenGLTextureRegistry(t =>
        {
            created++;
            return new OpenGLTexture(t);
        });

        var a = reg.GetOrCreate(tex);
        var b = reg.GetOrCreate(tex);

        Assert.Same(a, b);
        Assert.Equal(1, created);
        Assert.Equal(1, reg.Count);
    }

    [Fact]
    public void GetOrCreate_DistinctTextures_ProducesDistinctEntries()
    {
        var reg = new OpenGLTextureRegistry(t => new OpenGLTexture(t));

        var a = reg.GetOrCreate(MakeTex("T1"));
        var b = reg.GetOrCreate(MakeTex("T2"));

        Assert.NotSame(a, b);
        Assert.Equal(2, reg.Count);
    }

    [Fact]
    public void TryRemove_ExistingEntry_RemovesAndReturnsIt()
    {
        var tex = MakeTex("T");
        var reg = new OpenGLTextureRegistry(t => new OpenGLTexture(t));
        reg.GetOrCreate(tex);

        Assert.True(reg.TryRemove(tex, out var removed));
        Assert.NotNull(removed);
        Assert.Equal(0, reg.Count);
    }

    [Fact]
    public void TryRemove_MissingEntry_ReturnsFalse()
    {
        var reg = new OpenGLTextureRegistry(t => new OpenGLTexture(t));

        Assert.False(reg.TryRemove(MakeTex("T"), out _));
    }

    [Fact]
    public void TryRemove_ThenGetOrCreate_Recreates()
    {
        var tex = MakeTex("T");
        int created = 0;
        var reg = new OpenGLTextureRegistry(t =>
        {
            created++;
            return new OpenGLTexture(t);
        });
        reg.GetOrCreate(tex);
        reg.TryRemove(tex, out _);

        var recreated = reg.GetOrCreate(tex);

        Assert.Equal(2, created);
        Assert.Same(tex, recreated.Data);
    }
}
