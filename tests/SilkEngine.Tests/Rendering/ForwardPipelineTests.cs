using SilkEngine.Math;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.Pipeline;
using SilkEngine.Scene;

namespace SilkEngine.Tests.Rendering;

/// <summary>
/// 新 ForwardPipeline 契约测试：只复制已解析的 Render Handle 与参数值进入 RenderPacket，
/// 不解析资产；未解析句柄的渲染器跳过。
/// </summary>
public class ForwardPipelineTests
{
    private sealed class StubRenderable : IRenderable
    {
        public RenderShaderHandle ShaderHandle { get; init; }

        public RenderMeshHandle MeshHandle { get; init; }

        public RenderTextureHandle TextureHandle { get; init; }

        public RenderMaterialParameters MaterialParameters { get; init; } = new([]);

        public bool Enabled => true;

        public Matrix4x4 WorldMatrix { get; init; } = Matrix4x4.Identity;
    }

    private static IReadOnlyList<RenderPacket> Build(IRenderable renderable)
    {
        var pipeline = new ForwardPipeline();
        var cam = new GameObject().AddComponent<Camera>();
        cam.Orthographic = true;
        cam.UpdateMatrices(1f);
        var batches = new List<RenderBatch> { new() { Renderers = [renderable] } };
        return pipeline.Build(cam, batches);
    }

    [Fact]
    public void Build_CopiesResolvedRenderValuesIntoPacket()
    {
        var renderable = new StubRenderable
        {
            ShaderHandle = new RenderShaderHandle(1),
            MeshHandle = new RenderMeshHandle(2),
            TextureHandle = new RenderTextureHandle(3),
            MaterialParameters = new RenderMaterialParameters(
                [("Roughness", RenderParameterValue.Float(0.5f))]),
            WorldMatrix = Matrix4x4.Identity,
        };

        var packet = Assert.Single(Build(renderable));

        Assert.Equal(1UL, packet.Shader.Value);
        Assert.Equal(2UL, packet.Mesh.Value);
        Assert.Equal(3UL, packet.Texture.Value);
        Assert.Equal(0.5f, packet.Material.GetFloat("Roughness"));
        Assert.Equal(Matrix4x4.Identity, packet.ModelMatrix);
    }

    [Fact]
    public void Build_SkipsRendererWithDefaultHandles()
    {
        var renderable = new StubRenderable(); // 全部 default 句柄

        Assert.Empty(Build(renderable));
    }

    [Fact]
    public void Build_SkipsRendererMissingShaderOrMesh()
    {
        var noShader = new StubRenderable { MeshHandle = new RenderMeshHandle(2) };
        var noMesh = new StubRenderable { ShaderHandle = new RenderShaderHandle(1) };

        Assert.Empty(Build(noShader));
        Assert.Empty(Build(noMesh));
    }
}
