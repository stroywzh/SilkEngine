using SilkEngine.Math;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Tests.Rendering;

public class RenderContractTests
{
    [Fact]
    public void RenderContracts_CarryOnlyRenderValues()
    {
        var textureRequest = new RenderTextureCreateRequest(
            new RenderTextureDescriptor(1, 1, 4),
            new byte[] { 255, 255, 255, 255 });
        var packet = new RenderPacket(
            new RenderShaderHandle(1),
            new RenderMeshHandle(2),
            new RenderTextureHandle(3),
            new RenderMaterialParameters([("Roughness", RenderParameterValue.Float(0.5f))]),
            Matrix4x4.Identity);

        Assert.Equal(4, textureRequest.PixelData.Length);
        Assert.Equal(3UL, packet.Texture.Value);
        Assert.Equal(0.5f, packet.Material.GetFloat("Roughness"));
    }

    [Fact]
    public void ReleaseRequest_UsesRenderHandleWithoutAssetIdentity()
    {
        var request = new RenderResourceReleaseRequest(RenderResourceKind.Texture, 7);

        Assert.Equal(RenderResourceKind.Texture, request.Kind);
        Assert.Equal(7UL, request.Handle);
    }

    [Fact]
    public void CreateRequests_CopyInputArraysAndMemory()
    {
        var pixels = new byte[] { 1, 2, 3, 4 };
        var textureRequest = new RenderTextureCreateRequest(
            new RenderTextureDescriptor(1, 1, 4), pixels);
        pixels[0] = 99;
        Assert.Equal(1, textureRequest.PixelData.Span[0]);

        var vertices = new float[] { 0f, 0f, 1f, 1f };
        var indices = new int[] { 0, 1, 2 };
        var layout = new int[] { 3 };
        var meshRequest = new RenderMeshCreateRequest(
            new RenderMeshDescriptor(4, 3, layout), vertices, indices);
        vertices[0] = 42f;
        indices[0] = 7;
        layout[0] = 9;
        Assert.Equal(0f, meshRequest.Vertices.Span[0]);
        Assert.Equal(0, meshRequest.Indices.Span[0]);
        Assert.Equal(3, meshRequest.Descriptor.Layout[0]);
    }
}
