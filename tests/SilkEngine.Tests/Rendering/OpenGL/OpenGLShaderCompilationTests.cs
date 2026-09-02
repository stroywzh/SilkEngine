using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SilkEngine.Assets;
using SilkEngine.Rendering;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.Backend;
using SilkEngine.Rendering.OpenGL;
using SilkEngine.Threading;
using Xunit;

namespace SilkEngine.Tests.Rendering.OpenGL;

/// <summary>
/// 着色器编译契约测试：backend-neutral 编译请求的字段保留、负错误模型（错误消息含
/// source path/入口/profile/backend）、DXC 缺失时的 Unsupported 语义、失败阶段的
/// RequestId 关联与发布回退，以及 OpenGLReal 门控的真实 DXC 输出验证。
/// </summary>
public class OpenGLShaderCompilationTests
{
    private const string TestPath = "Shaders/Unlit.hlsl";

    [Fact]
    public async Task CompilerRequest_PreservesEntryProfileAndSource()
    {
        var compiler = new RecordingShaderCompiler();
        var request = new ShaderCompileRequest(
            TestPath, "source", "vert", "frag", "sm_6_0", [], ShaderBackends.OpenGl);

        var result = await compiler.CompileAsync(request, CancellationToken.None);

        Assert.Equal("vert", compiler.LastRequest!.VertexEntryPoint);
        Assert.Equal("frag", compiler.LastRequest.FragmentEntryPoint);
        Assert.Equal("sm_6_0", compiler.LastRequest.Profile);
        Assert.Equal(TestPath, compiler.LastRequest.SourcePath);
        Assert.Equal(ShaderCompileState.Succeeded, result.State);
    }

    [Fact]
    public async Task CompilerFailure_ContainsPathEntriesProfileAndBackend()
    {
        var compiler = new FailingShaderCompiler("syntax error");
        var result = await compiler.CompileAsync(
            new ShaderCompileRequest(
                TestPath, "bad", "vert", "frag", "sm_6_0", [], ShaderBackends.OpenGl),
            CancellationToken.None);

        Assert.Equal(ShaderCompileState.Failed, result.State);
        Assert.Contains(TestPath, result.Error!.Message);
        Assert.Contains("vert", result.Error.Message);
        Assert.Contains("frag", result.Error.Message);
        Assert.Contains(ShaderBackends.OpenGl, result.Error.Message);
    }

    [Fact]
    public async Task DxcMissing_ReturnsUnsupportedWithCompileContext()
    {
        var missingDxc = Path.Combine(
            Path.GetTempPath(), Guid.NewGuid().ToString("N"), "dxc.exe");
        var compiler = new DxcHlslCompiler(missingDxc);

        var result = await compiler.CompileAsync(
            new ShaderCompileRequest(
                TestPath, "src", "vert", "frag", "sm_6_0", [], ShaderBackends.OpenGl),
            CancellationToken.None);

        Assert.Equal(ShaderCompileState.Unsupported, result.State);
        Assert.Null(result.SpirV);
        Assert.Contains(TestPath, result.Error!.Message);
        Assert.Contains("vert", result.Error.Message);
        Assert.Contains("frag", result.Error.Message);
        Assert.Contains(ShaderBackends.OpenGl, result.Error.Message);
    }

    [Fact]
    public void AssetGpuResourceCache_RecordsCompileFailureStageForRequestId()
    {
        var cache = new AssetGpuResourceCache();
        var id = new RenderResourceRequestId(11);

        cache.RecordFailure(id, "gl-specialize", $"{TestPath} vert frag {ShaderBackends.OpenGl}: specialize failed");

        Assert.True(cache.TryGetFailure(id, out var stage, out var message));
        Assert.Equal("gl-specialize", stage);
        Assert.Contains(TestPath, message);
        Assert.Contains("vert", message);
        Assert.Contains(ShaderBackends.OpenGl, message);
        Assert.True(cache.RemoveFailure(id));
        Assert.False(cache.TryGetFailure(id, out _, out _));
    }

    [Fact]
    public void ShaderCompileFailure_StagesThroughRenderThread_AndSkipsPublish()
    {
        using var runtime = new ThreadRuntime();
        runtime.RegisterMainThread();
        var manager = new AssetManager(new StubPipeline(), runtime.MainThread, runtime);
        using var backend = new FailingBackend();
        using var host = CreateStartedHost(runtime, backend);
        var handle = manager.RegisterTransient(new ShaderAsset("lit", "hlsl"));

        manager.FlushPendingRenderCreates();
        var batch = manager.DrainCreateBatch();
        var item = Assert.Single(batch.Items);
        Assert.Equal(RenderResourceKind.Shader, item.Request.Kind);
        host.SubmitFrame(new RenderSubmission(FrameCameraBlock.Identity, [], batch));
        manager.ApplyCreateResults(host.LastCreateResults);

        Assert.Equal("gl-specialize", manager.LastFailureStageForTests);
        Assert.Contains(TestPath, manager.LastFailureMessageForTests);
        Assert.Contains(ShaderBackends.OpenGl, manager.LastFailureMessageForTests);
        Assert.Equal(0UL, manager.GetRenderHandleForTests(handle.Id, RenderResourceKind.Shader));
    }

    [OpenGLRealFact]
    public async Task DxcHlslCompiler_ProducesSpirVWithMagicAndEntryNames()
    {
        var compiler = new DxcHlslCompiler();
        var request = new ShaderCompileRequest(
            TestPath,
            TestHlslSource,
            "vert",
            "frag",
            "sm_6_0",
            [],
            ShaderBackends.OpenGl);

        var result = await compiler.CompileAsync(request, CancellationToken.None);

        Assert.Equal(ShaderCompileState.Succeeded, result.State);
        Assert.NotNull(result.SpirV);
        var data = result.SpirV!;
        Assert.True(data.Count >= 4, $"SPIR-V 输出过短: {data.Count} 字节");
        Assert.Equal(0x07230203u, (uint)(data[0] | (data[1] << 8) | (data[2] << 16) | (data[3] << 24)));
        Assert.True(ContainsAscii(data, "vert"), "SPIR-V 中应包含顶点入口名 'vert'");
        Assert.True(ContainsAscii(data, "frag"), "SPIR-V 中应包含片元入口名 'frag'");
    }

    /// <summary>真实 DXC 输入 HLSL：vert/frag 两个入口可独立按 vs/ps profile 编译。</summary>
    private const string TestHlslSource = """
        struct PsInput
        {
            float4 position : SV_Position;
        };

        PsInput vert(uint vertexId : SV_VertexID)
        {
            PsInput o;
            o.position = float4(float2(vertexId, vertexId) * 0.0f, 0.0f, 1.0f);
            return o;
        }

        float4 frag(PsInput input) : SV_Target
        {
            return float4(1.0f, 0.0f, 0.0f, 1.0f);
        }
        """;

    private static bool ContainsAscii(IReadOnlyList<byte> data, string text)
    {
        var needle = Encoding.ASCII.GetBytes(text);
        for (int i = 0; i + needle.Length <= data.Count; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (data[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
                return true;
        }
        return false;
    }

    private static RenderThreadHost CreateStartedHost(ThreadRuntime runtime, IRenderBackend backend)
    {
        var host = new RenderThreadHost(runtime, backend);
        runtime.RegisterManagedLoop(host);
        host.Start();
        return host;
    }

    /// <summary>契约假编译器：记录最后请求并返回固定成功结果（private fake）。</summary>
    private sealed class RecordingShaderCompiler : IShaderCompiler
    {
        public ShaderCompileRequest? LastRequest { get; private set; }

        public ValueTask<ShaderCompileResult> CompileAsync(
            ShaderCompileRequest request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return new ValueTask<ShaderCompileResult>(
                new ShaderCompileResult(
                    ShaderCompileState.Succeeded,
                    new byte[] { 0x03, 0x02, 0x23, 0x07 },
                    null));
        }
    }

    /// <summary>契约假编译器：返回携带请求上下文（路径/入口/profile/backend）的失败结果（private fake）。</summary>
    private sealed class FailingShaderCompiler(string reason) : IShaderCompiler
    {
        public ValueTask<ShaderCompileResult> CompileAsync(
            ShaderCompileRequest request,
            CancellationToken cancellationToken)
        {
            var message = $"{request.SourcePath} vert={request.VertexEntryPoint} frag={request.FragmentEntryPoint} profile={request.Profile} backend={request.Backend}: {reason}";
            return new ValueTask<ShaderCompileResult>(
                new ShaderCompileResult(
                    ShaderCompileState.Failed,
                    null,
                    new ShaderCompileError(message, request.SourcePath)));
        }
    }

    /// <summary>渲染线程失败注入后端：CreateShader 抛携带阶段与上下文的编译异常（private fake）。</summary>
    private sealed class FailingBackend : IRenderBackend
    {
        public void Initialize()
        {
        }

        public void Execute(RenderPacket packet)
        {
        }

        public void Present()
        {
        }

        public void Release(RenderResourceReleaseRequest request)
        {
        }

        public RenderTextureHandle CreateTexture(RenderTextureCreateRequest request) => new(1);

        public RenderShaderHandle CreateShader(RenderShaderCreateRequest request)
            => throw new ShaderCompilationException(
                "gl-specialize",
                $"{TestPath} vert frag {ShaderBackends.OpenGl}: specialize failed");

        public RenderMeshHandle CreateMesh(RenderMeshCreateRequest request) => new(1);

        public void Dispose()
        {
        }
    }

    /// <summary>管线桩：RegisterTransient 路径不触发管线请求。</summary>
    private sealed class StubPipeline : IAssetPipeline, IAssetKeyResolver
    {
        public AssetOperation<T> Request<T>(
            AssetBuildKey key,
            CancellationToken cancellationToken = default)
            where T : class, IAssetPayload
            => throw new NotSupportedException("测试管线不支持请求构建");

        public AssetBuildKey ResolveKey(string path) => throw new NotSupportedException("测试管线不支持路径解析");

        public ulong CurrentSourceRevision(AssetId assetId) => 0UL;

        public void Invalidate(AssetId assetId)
        {
        }

        public Action<AssetPipelineResult>? ResultSink { get; set; }
    }
}

/// <summary>
/// OpenGLReal 门控测试特性：发现期计算 Skip——仅当 SILKENGINE_OPENGL_REAL=1 且 DXC 可解析
/// 才运行；无编译器/无上下文时跳过而不是伪造通过。
/// </summary>
public sealed class OpenGLRealFactAttribute : FactAttribute
{
    private static readonly string? SkipReason = ComputeSkipReason();

    public OpenGLRealFactAttribute()
    {
        if (SkipReason is not null)
            Skip = SkipReason;
    }

    private static string? ComputeSkipReason()
    {
        if (Environment.GetEnvironmentVariable("SILKENGINE_OPENGL_REAL") != "1")
            return "跳过：SILKENGINE_OPENGL_REAL 未设为 1（真实 DXC/OpenGL 门控测试默认不运行）";
        if (string.IsNullOrWhiteSpace(DxcHlslCompiler.ResolveDxcPath()))
            return "跳过：未在配置路径或 PATH 中找到 dxc.exe";
        return null;
    }
}