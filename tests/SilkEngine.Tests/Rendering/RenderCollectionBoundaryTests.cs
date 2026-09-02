using SilkEngine.Assets;
using SilkEngine.Core;
using SilkEngine.Math;
using SilkEngine.Render;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.Pipeline;
using SilkEngine.Scene;
using SilkEngine.Tests.Core.Assets;

namespace SilkEngine.Tests.Rendering;

/// <summary>
/// 收集边界契约测试：RendererBase 内部经 AssetSlot 驻留资产（Mesh/材质/Texture，Assets/Scene 边界），
/// 着色器经材质绑定解析；对 Rendering collector 只暴露已解析的 Render Handle 与参数值；
/// ForwardPipeline 只复制 Render 值进入 RenderPacket，不解析任何资产。
/// </summary>
[Collection("Assets")]
public class RenderCollectionBoundaryTests : IDisposable
{
    private readonly AssetManager _am;
    private readonly RenderCollector _collector = new();
    private readonly ForwardPipeline _pipeline = new();

    public RenderCollectionBoundaryTests() => _am = TestAssetPipeline.CreateManager();

    public void Dispose() => Services.Unregister<AssetManager>();

    private AssetId RegisterReady(IAssetPayload payload, AssetId? id = null)
    {
        var assetId = id ?? new AssetId(Guid.NewGuid());
        var entry = _am.Cache.GetOrAdd(assetId);
        entry.Payload = payload;
        entry.State = AssetState.Ready;
        return assetId;
    }

    [Fact]
    public void Collector_EmitsRenderHandlesWithoutPayloadReferences()
    {
        var renderer = CreateRendererWithResolvedMaterialBinding();

        var packet = Assert.Single(Collect(renderer));

        Assert.NotEqual(default, packet.Mesh);
        Assert.NotEqual(default, packet.Shader);
        Assert.Equal(typeof(RenderMeshHandle), packet.Mesh.GetType());
    }

    [Fact]
    public void Collector_CopiesMaterialParametersIntoPacket()
    {
        var renderer = CreateRendererWithResolvedMaterialBinding();
        renderer.Material!.SetFloat("Roughness", 0.75f);

        var packet = Assert.Single(Collect(renderer));

        Assert.Equal(0.75f, packet.Material.GetFloat("Roughness"));
    }

    [Fact]
    public void Collector_CopiesWorldMatrixIntoPacket()
    {
        var renderer = CreateRendererWithResolvedMaterialBinding();
        renderer.Transform.LocalPosition = new Vector3(1, 2, 3);

        var packet = Assert.Single(Collect(renderer));

        Assert.Equal(renderer.WorldMatrix, packet.ModelMatrix);
    }

    [Fact]
    public void Collector_SkipsRendererWithUnresolvedHandles()
    {
        var renderer = new GameObject("Plain").AddComponent<MeshRenderer>(); // 无资产槽/材质 → default 句柄

        Assert.Empty(Collect(renderer));
    }

    [Fact]
    public void Renderer_ResolvesPublishedRenderHandlesFromMaterialBinding()
    {
        var renderer = CreateRendererWithResolvedMaterialBinding();

        Assert.Equal(42UL, renderer.MeshHandle.Value);
        Assert.Equal(7UL, renderer.ShaderHandle.Value);
    }

    [Fact]
    public void Renderer_Material_MeshTextureProperties_BindAndResolveHandles()
    {
        var meshHandle = _am.RegisterTransient(new MeshAsset("M", new float[] { 0, 0, 0, 1, 0, 0 }, new[] { 3 }, null));
        var textureHandle = _am.RegisterTransient(
            new TextureAsset("T", new ImageData(1, 1, [255, 255, 255, 255])));
        var shaderHandle = _am.RegisterTransient(new ShaderAsset("S", "vs"));
        var materialId = new AssetId(Guid.NewGuid());
        RegisterReady(new MaterialAsset(materialId, shaderHandle, null, new MaterialParameterSnapshot([])), materialId);

        var renderer = new GameObject("R").AddComponent<MeshRenderer>();
        renderer.BindAssetService(_am);
        var instance = new Material(new MaterialReference(materialId));
        renderer.Mesh = meshHandle;
        renderer.Material = instance;
        renderer.Texture = textureHandle;

        Assert.Equal(meshHandle, renderer.Mesh);
        Assert.Same(instance, renderer.Material);
        Assert.Equal(textureHandle, renderer.Texture);
    }

    private MeshRenderer CreateRendererWithResolvedMaterialBinding()
    {
        var meshId = RegisterReady(new MeshAsset("M", [0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0], [3], null));
        var shaderId = RegisterReady(new ShaderAsset("S", "vs"));
        _am.PublishRenderMesh(meshId, new RenderMeshHandle(42));
        _am.PublishRenderShader(shaderId, new RenderShaderHandle(7));
        var materialId = new AssetId(Guid.NewGuid());
        RegisterReady(
            new MaterialAsset(materialId, new AssetHandle<ShaderAsset>(shaderId), null, new MaterialParameterSnapshot([])),
            materialId);

        var renderer = new GameObject("R").AddComponent<MeshRenderer>();
        renderer.BindAssetService(_am);
        renderer.SetMesh(new AssetHandle<MeshAsset>(meshId));
        renderer.Material = new Material(new MaterialReference(materialId));
        return renderer;
    }

    private IReadOnlyList<RenderPacket> Collect(MeshRenderer renderer)
    {
        _collector.Gather([], [renderer], out _, out var batches);
        var cam = new GameObject("Cam").AddComponent<Camera>();
        cam.Orthographic = true;
        cam.UpdateMatrices(1f);
        return _pipeline.Build(cam, batches);
    }
}