using SilkEngine.Assets;
using SilkEngine.Render;
using SilkEngine.Scene;

namespace SilkEngine.Tests.Scene;

public class UIRendererTests
{
    [Fact]
    public void InheritsRendererBase_AndImplementsIRenderable()
    {
        var ui = new GameObject().AddComponent<UIRenderer>();

        Assert.IsAssignableFrom<RendererBase>(ui);
        Assert.IsAssignableFrom<IRenderable>(ui);
    }

    [Fact]
    public void AssembleAssets_AllReadableBack()
    {
        var shader = new Shader { Name = "PngShader" };
        var material = new Material(new MaterialReference(new AssetId(Guid.NewGuid())));
        var ui = new GameObject().AddComponent<UIRenderer>();

        ui.Shader = shader;
        ui.Mesh = MeshFactory.CreateQuad(1f, 1f);
        ui.Material = material;

        Assert.Same(shader, ui.Shader);
        Assert.Equal("Quad", ui.Mesh!.Name);
        Assert.Same(material, ui.Material);
        Assert.True(ui.Enabled);
    }
}
