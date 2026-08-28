using SilkEngine.Assets;
using SilkEngine.Host;
using SilkEngine.Math;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Scene;

namespace SilkEngine.Tests.Scene;

/// <summary>
/// Renderer 资产槽（阶段 3 任务 2）：Mesh/Texture 业务属性经 AssetSlot 驻留，
/// 替换属性时旧槽释放驻留；Texture/Material 属性解析为渲染契约（无资产语义）。
/// </summary>
[Collection("Assets")]
public sealed class RendererAssetSlotTests : IDisposable
{
    private readonly EngineHost _host;

    public RendererAssetSlotTests()
    {
        _host = EngineHost.Create(b => b.UseHeadlessForTests());
        _host.Initialize();
    }

    public void Dispose() => _host.Dispose();

    private static MeshAsset CreateMesh(string name) => new(name, [0, 1, 2], [3], null);

    [Fact]
    public void MeshProperty_ReplaceSlot_ReleasesOldResidency()
    {
        var scene = _host.SceneManager.Create("Main");
        _host.SceneManager.LoadScene(scene);
        var renderer = scene.CreateGameObject("Cube").AddComponent<MeshRenderer>();
        var first = _host.AssetManager.RegisterTransient(CreateMesh("first"));
        var second = _host.AssetManager.RegisterTransient(CreateMesh("second"));

        renderer.Mesh = first;
        renderer.Mesh = second;

        Assert.Equal(second, renderer.Mesh);
        Assert.Equal(0, _host.AssetManager.GetResidencyForTests(first.Id));
        Assert.Equal(1, _host.AssetManager.GetResidencyForTests(second.Id));
    }

    [Fact]
    public void TextureProperty_BindsSlot_AndResolvesPublishedHandle()
    {
        var scene = _host.SceneManager.Create("Main");
        _host.SceneManager.LoadScene(scene);
        var renderer = scene.CreateGameObject("Quad").AddComponent<UIRenderer>();
        var tex = _host.AssetManager.RegisterTransient(
            new TextureAsset("T", new ImageData(2, 2, [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1])));
        _host.AssetManager.PublishRenderTexture(tex.Id, new RenderTextureHandle(7));

        renderer.Texture = tex;

        Assert.Equal(tex, renderer.Texture);
        Assert.Equal(7UL, renderer.TextureHandle.Value);
        Assert.Equal(1, _host.AssetManager.GetResidencyForTests(tex.Id));
    }

    [Fact]
    public void MaterialProperty_ResolvesRenderParameters()
    {
        var scene = _host.SceneManager.Create("Main");
        _host.SceneManager.LoadScene(scene);
        var renderer = scene.CreateGameObject("Cube").AddComponent<MeshRenderer>();
        var material = new SilkEngine.Render.Material(
            new SilkEngine.Render.MaterialReference(new AssetId(Guid.NewGuid())));
        material.SetVector3("Tint", new Vector3(1, 0, 0));

        renderer.Material = material;

        Assert.Equal(new Vector3(1, 0, 0), renderer.MaterialParameters.GetVector3("Tint"));
    }

    [Fact]
    public void AssigningMaterialParameters_ClearsMaterialInstance()
    {
        var renderer = new GameObject().AddComponent<MeshRenderer>();
        renderer.Material = new SilkEngine.Render.Material(
            new SilkEngine.Render.MaterialReference(new AssetId(Guid.NewGuid())));

        renderer.MaterialParameters = new RenderMaterialParameters(
            [("Roughness", RenderParameterValue.Float(1f))]);

        Assert.Null(renderer.Material);
        Assert.Equal(1f, renderer.MaterialParameters.GetFloat("Roughness"));
    }
}
