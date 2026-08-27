using SilkEngine.Assets;
using SilkEngine.Render;
using SilkEngine.Render.OpenGL;

namespace SilkEngine.Tests.Render;

public class OpenGLMaterialTests
{
    [Fact]
    public void ResolveTexture_NoMainTexture_ReturnsWhitePlaceholder()
    {
        var bound = BoundValue(mainTexture: null);

        Assert.Same(DefaultTextures.White, OpenGLMaterial.ResolveTexture(bound, null));
    }

    [Fact]
    public void ResolveTexture_HasMainTexture_ResolverResolves_ReturnsIt()
    {
        var tex = new Texture2D
        {
            Name = "T",
            Data = new ImageData(1, 1, [1, 2, 3, 4]),
        };
        var bound = BoundValue(mainTexture: new AssetHandle<TextureAsset>(new AssetId(Guid.NewGuid())));

        Assert.Same(tex, OpenGLMaterial.ResolveTexture(bound, _ => tex));
    }

    [Fact]
    public void ResolveTexture_ResolverReturnsNull_FallsBackToWhitePlaceholder()
    {
        var bound = BoundValue(mainTexture: new AssetHandle<TextureAsset>(new AssetId(Guid.NewGuid())));

        Assert.Same(DefaultTextures.White, OpenGLMaterial.ResolveTexture(bound, _ => null));
    }

    private static BoundMaterialValue BoundValue(AssetHandle<TextureAsset>? mainTexture) =>
        new(
            new MaterialParameterSnapshot([]),
            new AssetHandle<ShaderAsset>(new AssetId(Guid.NewGuid())),
            mainTexture,
            0,
            0);
}
