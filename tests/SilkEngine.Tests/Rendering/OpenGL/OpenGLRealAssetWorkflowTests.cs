using System;
using System.IO;
using Silk.NET.OpenGL;
using SilkEngine.Assets;
using SilkEngine.Assets.Importer;
using SilkEngine.Math;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.Backend;
using SilkEngine.Rendering.OpenGL;
using Xunit;

namespace SilkEngine.Tests.Rendering.OpenGL;

/// <summary>
/// 任务 12：真实 OpenGL 资产工作流门控测试。
/// 仅在 <see cref="OpenGLRealFactAttribute"/> 门控（SILKENGINE_OPENGL_REAL=1 且 DXC 可解析）下创建窗口
/// GL 上下文，走生产 <see cref="OpenGLRenderBackend"/> 端点验证 纹理上传 / OBJ 网格上传 /
/// 着色器 SPIR-V 特化（glShaderBinary/glSpecializeShader）/ 材质绘制与释放的闭环。
/// 条件满足时上下文创建或任何足步骤失败按诚实原则显式失败；
/// 无环境变量时必须整类 skipped（接受验收命令 --filter Category=OpenGLReal 输出全 skipped），
/// 本机无 GPU/无桌面会话不影响 Headless 功能测试。
/// </summary>
[Trait("Category", "OpenGLReal")]
public class OpenGLRealAssetWorkflowTests
{
    private const string ObjLogicalPath = "Meshes/Cube.obj";

    [OpenGLRealFact]
    public void ShaderBinaryAndSpecialize_CreateDrawableLinkedProgram()
    {
        using var backend = new OpenGLRenderBackend();
        backend.Initialize(); // 真实窗口 GL 上下文；条件满足时失败即诚实失败
        var gl = backend.GL;

        var shader = backend.CreateShader(new RenderShaderCreateRequest(
            "Shaders/Unlit.hlsl",
            TestHlslSource,
            "vert",
            "frag",
            "sm_6_0",
            [],
            ShaderBackends.OpenGl));

        Assert.NotEqual(0UL, shader.Value);
        Assert.Equal(GLEnum.NoError, gl.GetError());
    }

    [OpenGLRealFact]
    public void TextureUpload_CreatesRgbaTextureWithoutError()
    {
        using var backend = new OpenGLRenderBackend();
        backend.Initialize();
        var gl = backend.GL;

        var texture = backend.CreateTexture(new RenderTextureCreateRequest(
            new RenderTextureDescriptor(2, 2, 4),
            new byte[] { 255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 255, 255 }));

        Assert.NotEqual(0UL, texture.Value);
        Assert.Equal(GLEnum.NoError, gl.GetError());
    }

    [OpenGLRealFact]
    public void ObjMeshUpload_ImportsDiskAssetAndCreatesIndexedMesh()
    {
        using var backend = new OpenGLRenderBackend();
        backend.Initialize();
        var gl = backend.GL;

        var bytes = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Assets", "Meshes", "Cube.obj"));
        var result = new ObjMeshImporter().Import(
            bytes, new AssetImportContext(ObjLogicalPath, null));
        var meshAsset = Assert.IsType<MeshAsset>(result.Payload);
        Assert.NotNull(meshAsset.Indices);
        Assert.True(meshAsset.Indices!.Length > 0, "OBJ 导入应产出索引网格");

        var mesh = backend.CreateMesh(new RenderMeshCreateRequest(
            new RenderMeshDescriptor(
                meshAsset.Vertices.Length,
                meshAsset.Indices.Length,
                meshAsset.Layout),
            meshAsset.Vertices,
            meshAsset.Indices));

        Assert.NotEqual(0UL, mesh.Value);
        Assert.Equal(GLEnum.NoError, gl.GetError());
    }

    [OpenGLRealFact]
    public void MaterialDraw_ExecutesFrameWithShaderTextureAndMesh()
    {
        using var backend = new OpenGLRenderBackend();
        backend.Initialize();
        var gl = backend.GL;

        var shader = backend.CreateShader(new RenderShaderCreateRequest(
            "Shaders/Unlit.hlsl",
            TestHlslSource,
            "vert",
            "frag",
            "sm_6_0",
            [],
            ShaderBackends.OpenGl));
        var texture = backend.CreateTexture(new RenderTextureCreateRequest(
            new RenderTextureDescriptor(1, 1, 4),
            new byte[] { 255, 255, 255, 255 }));
        var mesh = CreateMeshFromImportedCube(backend);

        var packet = new RenderPacket(
            shader,
            mesh,
            texture,
            new RenderMaterialParameters([("Roughness", RenderParameterValue.Float(0.5f))]),
            Matrix4x4.Identity);
        backend.ExecuteFrame(new RenderSubmission(
            new FrameCameraBlock(Matrix4x4.Identity, Matrix4x4.Identity),
            [packet],
            RenderResourceCreateBatch.Empty));

        Assert.Equal(GLEnum.NoError, gl.GetError());
    }

    [OpenGLRealFact]
    public void Release_ReclaimsTextureMeshAndShaderResources()
    {
        using var backend = new OpenGLRenderBackend();
        backend.Initialize();
        var gl = backend.GL;

        var shader = backend.CreateShader(new RenderShaderCreateRequest(
            "Shaders/Unlit.hlsl",
            TestHlslSource,
            "vert",
            "frag",
            "sm_6_0",
            [],
            ShaderBackends.OpenGl));
        var texture = backend.CreateTexture(new RenderTextureCreateRequest(
            new RenderTextureDescriptor(1, 1, 4),
            new byte[] { 64, 128, 255, 255 }));
        var mesh = CreateMeshFromImportedCube(backend);

        backend.Release(new RenderResourceReleaseRequest(RenderResourceKind.Shader, shader.Value));
        backend.Release(new RenderResourceReleaseRequest(RenderResourceKind.Texture, texture.Value));
        backend.Release(new RenderResourceReleaseRequest(RenderResourceKind.Mesh, mesh.Value));

        Assert.Equal(GLEnum.NoError, gl.GetError());
    }

    /// <summary>经 OBJ 导入器从磁盘资产构建网格创建请求并上传（共享路径：OBJ mesh upload + material draw + release 共用）。</summary>
    private static RenderMeshHandle CreateMeshFromImportedCube(OpenGLRenderBackend backend)
    {
        var bytes = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Assets", "Meshes", "Cube.obj"));
        var result = new ObjMeshImporter().Import(
            bytes, new AssetImportContext(ObjLogicalPath, null));
        var meshAsset = Assert.IsType<MeshAsset>(result.Payload);
        return backend.CreateMesh(new RenderMeshCreateRequest(
            new RenderMeshDescriptor(
                meshAsset.Vertices.Length,
                meshAsset.Indices?.Length ?? 0,
                meshAsset.Layout),
            meshAsset.Vertices,
            meshAsset.Indices ?? []));
    }

    /// <summary>真实 GL 加载输入的 HLSL：属性布局（position/normal/uv）与 OBJ 网格布局一致，采样 uMainTex。</summary>
    private const string TestHlslSource = """
        struct PsInput
        {
            float4 position : SV_Position;
            float2 uv : TEXCOORD0;
        };

        Texture2D uMainTex;
        SamplerState uMainTex_sampler;

        PsInput vert(uint vertexId : SV_VertexID,
                     float4 position : POSITION,
                     float3 normal : NORMAL,
                     float2 uv : TEXCOORD0)
        {
            PsInput o;
            o.position = float4(position.xyz + normal.xyz * 0.01f, 1.0f);
            o.uv = uv;
            return o;
        }

        float4 frag(PsInput input) : SV_Target
        {
            return uMainTex.Sample(uMainTex_sampler, input.uv);
        }
        """;
}