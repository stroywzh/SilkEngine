using SilkEngine.Assets;
using SilkEngine.Core;
using SilkEngine.Math;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Scene;
using SilkEngine.Tests.Core.Assets;

namespace SilkEngine.Tests.Scene;

/// <summary>
/// RendererBase 新契约测试：内部经 AssetSlot 驻留资产（SetMesh/SetShader），
/// 经资产管理器 GPU 句柄缓存解析已发布 Render Handle；OnDestroy 释放驻留。
/// </summary>
[Collection("Assets")]
public class RendererBaseTests : IDisposable
{
    private readonly AssetManager _am;

    public RendererBaseTests() =>
        _am = TestAssetPipeline.CreateManager();

    public void Dispose() => Services.Unregister<AssetManager>();

    private AssetId RegisterReady(IAssetPayload payload)
    {
        var id = new AssetId(Guid.NewGuid());
        var entry = _am.Cache.GetOrAdd(id);
        entry.Payload = payload;
        entry.State = AssetState.Ready;
        return id;
    }

    [Fact]
    public void Defaults_AllHandlesDefault()
    {
        var ui = new GameObject().AddComponent<UIRenderer>();

        Assert.Equal(default, ui.MeshHandle);
        Assert.Equal(default, ui.ShaderHandle);
        Assert.Equal(default, ui.TextureHandle);
        Assert.Throws<KeyNotFoundException>(() => ui.MaterialParameters.GetFloat("Roughness"));
    }

    [Fact]
    public void SetMesh_CreatesSlot_AddsResidency()
    {
        var id = RegisterReady(new MeshAsset("M", [0, 0, 0], [3], null));
        var mr = NewRenderer();
        mr.SetMesh(new AssetHandle<MeshAsset>(id));

        _am.UnloadUnused(); // 驻留持有 → 不驱逐

        Assert.True(_am.TryResolve<MeshAsset>(id) is not null);
    }

    [Fact]
    public void SetShader_CreatesSlot_AddsResidency()
    {
        var id = RegisterReady(new ShaderAsset("S", "vs"));
        var mr = NewRenderer();
        mr.SetShader(new AssetHandle<ShaderAsset>(id));

        _am.UnloadUnused();

        Assert.True(_am.TryResolve<ShaderAsset>(id) is not null);
    }

    [Fact]
    public void OnDestroy_DisposesSlots_ReleasesResidency()
    {
        var meshId = RegisterReady(new MeshAsset("M", [0, 0, 0], [3], null));
        var shaderId = RegisterReady(new ShaderAsset("S", "vs"));
        var mr = NewRenderer();
        mr.SetMesh(new AssetHandle<MeshAsset>(meshId));
        mr.SetShader(new AssetHandle<ShaderAsset>(shaderId));

        mr.OnDestroy();
        _am.UnloadUnused();

        Assert.False(_am.TryResolve<MeshAsset>(meshId) is not null);
        Assert.False(_am.TryResolve<ShaderAsset>(shaderId) is not null);
    }

    [Fact]
    public void SetMesh_ResolvesPublishedHandle()
    {
        var id = RegisterReady(new MeshAsset("M", [0, 0, 0], [3], null));
        _am.PublishRenderMesh(id, new RenderMeshHandle(42));
        var mr = NewRenderer();
        mr.SetMesh(new AssetHandle<MeshAsset>(id));

        Assert.Equal(42UL, mr.MeshHandle.Value);
    }

    [Fact]
    public void SetShader_ResolvesPublishedHandle()
    {
        var id = RegisterReady(new ShaderAsset("S", "vs"));
        _am.PublishRenderShader(id, new RenderShaderHandle(9));
        var mr = NewRenderer();
        mr.SetShader(new AssetHandle<ShaderAsset>(id));

        Assert.Equal(9UL, mr.ShaderHandle.Value);
    }

    [Fact]
    public void UnpublishedSlot_ResolvesDefault()
    {
        var id = RegisterReady(new MeshAsset("M", [0, 0, 0], [3], null));
        var mr = NewRenderer();
        mr.SetMesh(new AssetHandle<MeshAsset>(id)); // 驻留但未发布 GPU 句柄

        Assert.Equal(default, mr.MeshHandle);
    }

    /// <summary>创建无场景上下文的渲染器并显式注入资产服务（构造器自注册已移除后的测试装配）。</summary>
    private MeshRenderer NewRenderer()
    {
        var mr = new GameObject().AddComponent<MeshRenderer>();
        mr.BindAssetService(_am);
        return mr;
    }

    [Fact]
    public void MaterialParameters_AssignmentFlowsThrough()
    {
        var mr = new GameObject().AddComponent<UIRenderer>();
        mr.MaterialParameters = new RenderMaterialParameters(
            [("Roughness", RenderParameterValue.Float(1f))]);

        Assert.Equal(1f, mr.MaterialParameters.GetFloat("Roughness"));
    }

    [Fact]
    public void TextureHandle_AssignmentFlowsThrough()
    {
        var ui = new GameObject().AddComponent<UIRenderer>();
        ui.TextureHandle = new RenderTextureHandle(5);

        Assert.Equal(5UL, ui.TextureHandle.Value);
    }

    [Fact]
    public void WorldMatrix_ComesFromTransform()
    {
        var go = new GameObject("Quad");
        go.Transform.LocalPosition = new Vector3(1, 2, 3);
        var ui = go.AddComponent<UIRenderer>();

        Assert.Equal(go.Transform.LocalToWorldMatrix, ui.WorldMatrix);
    }

    [Fact]
    public void MeshRenderer_SatisfiesIRenderableContract()
    {
        var mr = new GameObject("MR").AddComponent<MeshRenderer>();

        IRenderable r = mr;
        Assert.Equal(mr.ShaderHandle, r.ShaderHandle);
        Assert.Equal(mr.MeshHandle, r.MeshHandle);
        Assert.Equal(mr.TextureHandle, r.TextureHandle);
        Assert.Same(mr.MaterialParameters, r.MaterialParameters);
        Assert.Equal(mr.Enabled, r.Enabled);
        Assert.Equal(mr.WorldMatrix, r.WorldMatrix);
    }
}

