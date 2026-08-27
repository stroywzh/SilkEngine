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
        var ui = new GameObject().AddComponent<UIRenderer>();

        ui.Shader = shader;
        ui.Mesh = MeshFactory.CreateQuad(1f, 1f);
        ui.Material = new MaterialLegacy { Name = "PngMat" };

        Assert.Same(shader, ui.Shader);
        Assert.Equal("Quad", ui.Mesh!.Name);
        Assert.Equal("PngMat", ui.Material!.Name);
        Assert.True(ui.Enabled);
    }
}
