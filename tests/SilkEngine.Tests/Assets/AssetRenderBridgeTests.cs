using SilkEngine.Assets;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Tests.Assets;

/// <summary>
/// 资产到渲染契约桥接测试：Payload → 无资产语义的 Rendering.Abstraction 创建请求；
/// GPU 句柄缓存只保存 (AssetId, Revision) → RenderHandle，驱逐生成不泄漏资产身份的释放请求。
/// </summary>
public class AssetRenderBridgeTests
{
    [Fact]
    public void TexturePayload_BecomesAssetFreeRenderRequest()
    {
        var bridge = new AssetRenderBridge(new FakeRenderRequestSink());
        var payload = new TextureAsset("white", new ImageData(1, 1, [255, 255, 255, 255]));

        var request = bridge.CreateTextureRequest(payload);

        Assert.Equal(1, request.Descriptor.Width);
        Assert.Equal(1, request.Descriptor.Height);
        Assert.Equal(4, request.PixelData.Length);
        Assert.DoesNotContain("Asset", request.GetType().AssemblyQualifiedName!);
    }

    [Fact]
    public void ShaderPayload_BecomesAssetFreeCompileRequest()
    {
        var bridge = new AssetRenderBridge(new FakeRenderRequestSink());
        // 任务 7：GLSL 双源码移除；桥接产出单 HLSL 源 + 入口/profile 的编译请求（backend="opengl"）
        var payload = new ShaderAsset("lit", "hlsl-source");

        var request = bridge.CreateShaderRequest(payload);

        Assert.Equal("hlsl-source", request.HlslSource);
        Assert.Equal("lit", request.SourcePath);
        Assert.Equal("vert", request.VertexEntryPoint);
        Assert.Equal("frag", request.FragmentEntryPoint);
        Assert.Equal("sm_6_0", request.Profile);
        Assert.Empty(request.Defines);
        Assert.Equal("opengl", request.Backend);
        Assert.Equal(RenderResourceKind.Shader, request.Kind);
        Assert.DoesNotContain("Asset", request.GetType().AssemblyQualifiedName!);
    }

    [Fact]
    public void MeshPayload_BecomesAssetFreeMeshRequest()
    {
        var bridge = new AssetRenderBridge(new FakeRenderRequestSink());
        var payload = new MeshAsset("quad", [0, 1, 2, 3], [2], [0, 1, 2]);

        var request = bridge.CreateMeshRequest(payload);

        Assert.Equal(4, request.Descriptor.VertexCount);
        Assert.Equal(3, request.Descriptor.IndexCount);
        Assert.Equal([2], request.Descriptor.Layout);
        Assert.DoesNotContain("Asset", request.GetType().AssemblyQualifiedName!);
    }

    [Fact]
    public void Submit_ForwardsCreateRequestsToSink()
    {
        var sink = new FakeRenderRequestSink();
        var bridge = new AssetRenderBridge(sink);
        var payload = new TextureAsset("white", new ImageData(1, 1, [255, 255, 255, 255]));

        bridge.SubmitTexture(payload);

        var request = Assert.IsType<RenderTextureCreateRequest>(Assert.Single(sink.Created));
        Assert.Equal(1, request.Descriptor.Width);
    }

    [Fact]
    public void Evict_MapsAssetIdentityToRenderReleaseButDoesNotLeakIdentity()
    {
        var cache = new AssetGpuResourceCache();
        var assetId = new AssetId(Guid.NewGuid());
        cache.Publish(assetId, revision: 2, new RenderTextureHandle(9));

        var request = cache.Evict(assetId, revision: 2);

        Assert.Equal(RenderResourceKind.Texture, request.Kind);
        Assert.Equal(9UL, request.Handle);
    }

    [Fact]
    public void Evict_UnpublishedMapping_ReturnsZeroHandleNoOp()
    {
        var cache = new AssetGpuResourceCache();

        var request = cache.Evict(new AssetId(Guid.NewGuid()), revision: 1);

        Assert.Equal(0UL, request.Handle);
    }

    /// <summary>渲染请求接收器桩：记录提交的创建/释放请求（测试夹具）</summary>
    internal sealed class FakeRenderRequestSink : IRenderRequestSink
    {
        public List<RenderResourceCreateRequest> Created { get; } = [];

        public List<RenderResourceReleaseRequest> Released { get; } = [];

        public void Submit(RenderResourceCreateRequest request) => Created.Add(request);

        public void Submit(RenderResourceReleaseRequest request) => Released.Add(request);
    }
}
