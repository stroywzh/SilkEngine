using SilkEngine.Math;
using SilkEngine.Rendering;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.Backend;
using SilkEngine.Threading;

namespace SilkEngine.Tests.Rendering;

/// <summary>
/// RenderThreadHost 生命周期与帧握手契约测试：线程经 ThreadFactory 创建、
/// backend 在渲染线程 finally 释放、ThreadRuntime 关闭时统一 RequestStop + Join。
/// </summary>
public class RenderThreadHostTests
{
    private sealed class RecordingBackend : IRenderBackend
    {
        public List<RenderPacket> Packets = [];
        public int PresentCount;
        public int InitializeCount;
        public int ReleaseCount;
        public ulong ReleasedHandle;
        public bool Disposed;
        public int DisposeCount;
        public int DisposeThreadId;

        public void Initialize() => InitializeCount++;

        public void Execute(RenderPacket packet) => Packets.Add(packet);

        public void Present() => PresentCount++;

        public void Release(RenderResourceReleaseRequest request)
        {
            ReleaseCount++;
            ReleasedHandle = request.Handle;
        }

        public void Dispose()
        {
            Disposed = true;
            DisposeCount++;
            DisposeThreadId = Environment.CurrentManagedThreadId;
        }
    }

    private static RenderPacket SamplePacket(ulong shader = 1, ulong mesh = 2) => new(
        new RenderShaderHandle(shader),
        new RenderMeshHandle(mesh),
        new RenderTextureHandle(3),
        new RenderMaterialParameters([("Roughness", RenderParameterValue.Float(0.5f))]),
        Matrix4x4.Identity);

    [Fact]
    public void RuntimeDispose_StopsHostAndDisposesBackendOnRenderThread()
    {
        using var runtime = new ThreadRuntime();
        runtime.RegisterMainThread();
        var backend = new RecordingBackend();
        using var host = new RenderThreadHost(runtime, backend);
        runtime.RegisterManagedLoop(host);
        host.Start();

        runtime.Dispose();

        Assert.True(backend.Disposed);
        Assert.NotEqual(Thread.CurrentThread.ManagedThreadId, backend.DisposeThreadId);
        Assert.Equal(1, backend.DisposeCount);
    }

    [Fact]
    public void Start_InitializesBackendOnceOnRenderThread()
    {
        using var runtime = new ThreadRuntime();
        runtime.RegisterMainThread();
        var backend = new RecordingBackend();
        using var host = new RenderThreadHost(runtime, backend);
        runtime.RegisterManagedLoop(host);
        host.Start();

        runtime.Dispose();

        Assert.Equal(1, backend.InitializeCount);
        Assert.NotEqual(Thread.CurrentThread.ManagedThreadId, backend.DisposeThreadId);
    }

    [Fact]
    public void SubmitFrame_ExecutesFrozenPacketsAndPresents()
    {
        using var runtime = new ThreadRuntime();
        runtime.RegisterMainThread();
        var backend = new RecordingBackend();
        using var host = new RenderThreadHost(runtime, backend);
        runtime.RegisterManagedLoop(host);
        host.Start();

        host.SubmitFrame([SamplePacket(), SamplePacket(4, 5)]);

        Assert.Equal(2, backend.Packets.Count);
        Assert.Equal(1UL, backend.Packets[0].Shader.Value);
        Assert.Equal(5UL, backend.Packets[1].Mesh.Value);
        Assert.Equal(1, backend.PresentCount);
    }

    [Fact]
    public void SubmitFrame_EmptyFrame_StillPresents()
    {
        using var runtime = new ThreadRuntime();
        runtime.RegisterMainThread();
        var backend = new RecordingBackend();
        using var host = new RenderThreadHost(runtime, backend);
        runtime.RegisterManagedLoop(host);
        host.Start();

        host.SubmitFrame([]);

        Assert.Empty(backend.Packets);
        Assert.Equal(1, backend.PresentCount);
    }

    [Fact]
    public void FrameStart_DrainsQueuedReleasesIntoBackendRelease()
    {
        using var runtime = new ThreadRuntime();
        runtime.RegisterMainThread();
        var backend = new RecordingBackend();
        using var host = new RenderThreadHost(runtime, backend);
        runtime.RegisterManagedLoop(host);
        host.DrainUnloadQueue =
            consume => consume(new RenderResourceReleaseRequest(RenderResourceKind.Texture, 7));
        host.Start();

        host.SubmitFrame([]);

        Assert.Equal(1, backend.ReleaseCount);
        Assert.Equal(7UL, backend.ReleasedHandle);
    }

    [Fact]
    public void Dispose_IsIdempotent_BackendDisposedExactlyOnce()
    {
        using var runtime = new ThreadRuntime();
        runtime.RegisterMainThread();
        var backend = new RecordingBackend();
        var host = new RenderThreadHost(runtime, backend);
        runtime.RegisterManagedLoop(host);
        host.Start();

        host.Dispose();
        host.Dispose();
        runtime.Dispose();

        Assert.Equal(1, backend.DisposeCount);
        Assert.True(backend.Disposed);
    }
}
