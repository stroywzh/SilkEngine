using SilkEngine.Assets;
using SilkEngine.Render;
using SilkEngine.Render.OpenGL;

namespace SilkEngine.Tests.Render;

public class OpenGLMaterialTests
{
    [Fact]
    public void ResolveTexture_NoMainTexture_ReturnsWhitePlaceholder()
    {
        var mat = new Material();

        Assert.Same(DefaultTextures.White, OpenGLMaterial.ResolveTexture(mat));
    }

    [Fact]
    public void ResolveTexture_HasMainTexture_ReturnsIt()
    {
        var tex = new Texture2D
        {
            Name = "T",
            ImageData = new ImageData(1, 1, [1, 2, 3, 4]),
        };
        var mat = new Material { MainTexture = tex };

        Assert.Same(tex, OpenGLMaterial.ResolveTexture(mat));
    }
}
