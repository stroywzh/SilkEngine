using SilkEngine.Math;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.OpenGL;

namespace SilkEngine.Tests.Rendering.OpenGL;

/// <summary>
/// OpenGL 三矩阵上传契约：帧首上传 uView/uProjection（相机块），逐包上传 uModel；
/// 全部 transpose=true（行主序约定）；location == -1 的 uniform 跳过。
/// 经 internal 命令接缝（IOpenGlFrameCalls）与资源接缝注入录制桩，不依赖真实 GL 上下文。
/// </summary>
public class OpenGLMatrixUploadTests
{
    private sealed class RecordingFrameCalls : IOpenGlFrameCalls
    {
        public List<string> MatrixNames { get; } = [];
        public List<bool> MatrixTransposes { get; } = [];
        public List<string> SetupNames { get; } = [];
        private readonly Dictionary<int, string> _uniformNames = [];
        private int _loc = 100;

        public void SetupFrame(float r, float g, float b, float a, int width, int height)
            => SetupNames.Add("SetupFrame");

        public void UseProgram(uint program) { }

        public int GetUniformLocation(uint program, string name)
        {
            var loc = _loc++;
            _uniformNames[loc] = name;
            return loc;
        }

        public void Uniform1(int location, float value) { }

        public void Uniform3(int location, Vector3 value) { }

        public void UniformMatrix4(int location, bool transpose, Matrix4x4 matrix)
        {
            MatrixNames.Add(_uniformNames.GetValueOrDefault(location, location.ToString()));
            MatrixTransposes.Add(transpose);
        }

        public void Dispose() { }
    }

    private sealed class FakeShader : IOpenGlShaderResource, IDisposable
    {
        public uint Program => 7;
        public void Dispose() { }
    }

    private sealed class FakeMesh : IOpenGlMeshResource, IDisposable
    {
        public int DrawCount;
        public void Draw() => DrawCount++;
        public void Dispose() { }
    }

    private sealed class FakeTexture : IOpenGlTextureResource, IDisposable
    {
        public int BindCount;
        public void Bind(uint unit) => BindCount++;
        public void Dispose() { }
    }

    private static RenderPacket SamplePacket(ulong shader = 1, ulong mesh = 2, ulong texture = 0) => new(
        new RenderShaderHandle(shader),
        new RenderMeshHandle(mesh),
        new RenderTextureHandle(texture),
        new RenderMaterialParameters([("Roughness", RenderParameterValue.Float(0.5f))]),
        Matrix4x4.Identity);

    [Fact]
    public void OpenGlBackend_UploadsViewProjectionPerFrameAndModelPerPacket()
    {
        var backend = new OpenGLRenderBackend();
        var calls = new RecordingFrameCalls();
        backend.SetFrameCallsForTests(calls);
        backend.RegisterResourceForTests(1, new FakeShader());
        backend.RegisterResourceForTests(2, new FakeMesh());
        var submission = new RenderSubmission(
            new FrameCameraBlock(Matrix4x4.Identity, Matrix4x4.Identity),
            [SamplePacket()],
            RenderResourceCreateBatch.Empty);

        backend.ExecuteFrame(submission);

        Assert.Equal(["uView", "uProjection", "uModel"], calls.MatrixNames);
        Assert.All(calls.MatrixTransposes, t => Assert.True(t));
        Assert.Equal(1, calls.SetupNames.Count);
    }

    [Fact]
    public void OpenGlBackend_MultiplePackets_ReuploadCameraPerPacket()
    {
        var backend = new OpenGLRenderBackend();
        var calls = new RecordingFrameCalls();
        backend.SetFrameCallsForTests(calls);
        backend.RegisterResourceForTests(1, new FakeShader());
        backend.RegisterResourceForTests(2, new FakeMesh());
        var submission = new RenderSubmission(
            new FrameCameraBlock(Matrix4x4.Identity, Matrix4x4.Identity),
            [SamplePacket(), SamplePacket()],
            RenderResourceCreateBatch.Empty);

        backend.ExecuteFrame(submission);

        Assert.Equal(6, calls.MatrixNames.Count); // 3 矩阵 × 2 包
        Assert.Equal("uView", calls.MatrixNames[3]);
        Assert.Equal("uProjection", calls.MatrixNames[4]);
        Assert.Equal("uModel", calls.MatrixNames[5]);
    }

    [Fact]
    public void OpenGlBackend_MissingUniform_SkipsUpload()
    {
        var backend = new OpenGLRenderBackend();
        var calls = new MissingUniformFrameCalls();
        backend.SetFrameCallsForTests(calls);
        backend.RegisterResourceForTests(1, new FakeShader());
        backend.RegisterResourceForTests(2, new FakeMesh());
        var submission = new RenderSubmission(
            new FrameCameraBlock(Matrix4x4.Identity, Matrix4x4.Identity),
            [SamplePacket()],
            RenderResourceCreateBatch.Empty);

        backend.ExecuteFrame(submission);

        Assert.Empty(calls.MatrixNames);
    }

    [Fact]
    public void OpenGlBackend_BindsTextureSamplerForPacketsWithTexture()
    {
        var backend = new OpenGLRenderBackend();
        var calls = new RecordingFrameCalls();
        backend.SetFrameCallsForTests(calls);
        backend.RegisterResourceForTests(1, new FakeShader());
        backend.RegisterResourceForTests(2, new FakeMesh());
        var texture = new FakeTexture();
        backend.RegisterResourceForTests(3, texture);
        var submission = new RenderSubmission(
            new FrameCameraBlock(Matrix4x4.Identity, Matrix4x4.Identity),
            [SamplePacket(texture: 3)],
            RenderResourceCreateBatch.Empty);

        backend.ExecuteFrame(submission);

        Assert.Equal(1, texture.BindCount);
    }

    private sealed class MissingUniformFrameCalls : IOpenGlFrameCalls
    {
        public List<string> MatrixNames { get; } = [];

        public void SetupFrame(float r, float g, float b, float a, int width, int height) { }

        public void UseProgram(uint program) { }

        public int GetUniformLocation(uint program, string name) => -1; // 全部未命中

        public void Uniform1(int location, float value) { }

        public void Uniform3(int location, Vector3 value) { }

        public void UniformMatrix4(int location, bool transpose, Matrix4x4 matrix)
            => MatrixNames.Add(location.ToString());

        public void Dispose() { }
    }
}
