using SilkEngine.Assets;
using SilkEngine.Rendering.Abstraction;
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
    public void AssembleHandles_AllReadableBack()
    {
        var ui = new GameObject().AddComponent<UIRenderer>();

        ui.SetMesh(new AssetHandle<MeshAsset>(new AssetId(Guid.NewGuid())));
        ui.TextureHandle = new RenderTextureHandle(3);
        ui.MaterialParameters = new RenderMaterialParameters(
            [("Roughness", RenderParameterValue.Float(0.5f))]);

        Assert.Equal(3UL, ui.TextureHandle.Value);
        Assert.Equal(0.5f, ui.MaterialParameters.GetFloat("Roughness"));
        Assert.True(ui.Enabled);
    }
}
