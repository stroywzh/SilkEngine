using System;
using System.Linq;
using System.Reflection;
using SilkEngine.Assets;
using SilkEngine.Core;
using SilkEngine.Rendering;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.Backend;
using SilkEngine.Threading;

namespace SilkEngine.Tests.Assets;

/// <summary>
/// 渲染资源创建关联契约：创建批次只携带 RequestId（无资产身份），
/// Assets 侧经 AssetGpuResourceCache 按 RequestId 关联 (AssetId, Revision, Kind)，
/// 渲染线程创建完成后经结果批次在 Main 域发布句柄。
/// </summary>
[Collection("Assets")]
public class AssetGpuPublicationTests : IDisposable
{
    [Fact]
    public void CreateBatch_ContainsRequestIdButNoAssetIdentity()
    {
        var item = new RenderResourceCreateItem(
            new RenderResourceRequestId(1),
            new RenderTextureCreateRequest(
                new RenderTextureDescriptor(1, 1, 4),
                new byte[] { 255, 255, 255, 255 }));
        var batch = new RenderResourceCreateBatch(new[] { item });

        Assert.Equal(1UL, batch.Items[0].RequestId.Value);
        var propertyNames = typeof(RenderResourceCreateItem)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(x => x.Name)
            .ToArray();
        Assert.DoesNotContain("AssetId", propertyNames);
        Assert.DoesNotContain("AssetHandle", propertyNames);
    }

    [Fact]
    public void AssetGpuResourceCache_ResolvesRequestIdByAssetRevision()
    {
        var cache = new AssetGpuResourceCache();
        var assetId = new AssetId(Guid.NewGuid());
        cache.TrackRequest(new RenderResourceRequestId(9), assetId, 3, RenderResourceKind.Texture);

        Assert.True(cache.TryResolveRequest(new RenderResourceRequestId(9), out var tracked));
        Assert.Equal(assetId, tracked.AssetId);
        Assert.Equal(3UL, tracked.Revision);
        Assert.Equal(RenderResourceKind.Texture, tracked.Kind);
    }

    [Fact]
    public void AssetGpuResourceCache_RemoveRequest_DropsCorrelation()
    {
        var cache = new AssetGpuResourceCache();
        var id = new RenderResourceRequestId(7);
        cache.TrackRequest(id, new AssetId(Guid.NewGuid()), 1, RenderResourceKind.Mesh);

        Assert.True(cache.RemoveRequest(id));
        Assert.False(cache.TryResolveRequest(id, out _));
        Assert.False(cache.RemoveRequest(id));
    }

    [Fact]
    public void AssetGpuResourceCache_TryResolveRequest_MissReturnsFalse()
    {
        var cache = new AssetGpuResourceCache();

        Assert.False(cache.TryResolveRequest(new RenderResourceRequestId(42), out var tracked));
        Assert.Null(tracked);
    }

    [Fact]
    public void ReadyPayload_IsCreatedOnRenderThread_AndPublishedOnMainAfterHandshake()
    {
        using var runtime = TestRuntimeOnCurrentThread();
        using var backend = new RecordingBackend();
        using var host = CreateStartedHost(runtime, backend);
        var manager = CreateManager(runtime);
        var payload = new MeshAsset("cube", new float[] { 0, 1, 2 }, new[] { 3 }, null);
        var handle = manager.RegisterTransient(payload);

        manager.FlushPendingRenderCreates();
        host.SubmitFrame(CreateEmptySubmission(manager.DrainCreateBatch()));
        manager.ApplyCreateResults(host.LastCreateResults);

        Assert.NotEqual(0UL, manager.GetRenderHandleForTests(handle.Id, RenderResourceKind.Mesh));
        Assert.Equal(1, backend.MeshCreateCount);
        Assert.Equal(ThreadDomain.Main, manager.LastPublishDomainForTests);
    }

    [Fact]
    public void RegisterTransient_ResolvesPayloadWithoutVfs()
    {
        using var runtime = TestRuntimeOnCurrentThread();
        var manager = CreateManager(runtime);
        var payload = new MeshAsset("cube", new float[] { 0, 1, 2 }, new[] { 3 }, null);

        var handle = manager.RegisterTransient(payload);

        Assert.NotEqual(default, handle);
        Assert.True(manager.TryResolve(handle, out MeshAsset? resolved));
        Assert.Same(payload, resolved);
    }

    [Fact]
    public void ApplyCreateResults_StaleRevision_EnqueuesReleaseInsteadOfPublish()
    {
        using var runtime = TestRuntimeOnCurrentThread();
        using var backend = new RecordingBackend();
        using var host = CreateStartedHost(runtime, backend);
        var manager = CreateManager(runtime);
        var payload = new MeshAsset("cube", new float[] { 0, 1, 2 }, new[] { 3 }, null);
        var handle = manager.RegisterTransient(payload);

        manager.FlushPendingRenderCreates();
        var batch = manager.DrainCreateBatch();
        // 模拟结果回传前资产已被驱逐（缓存条目移除 → 修订视为 0 ≠ 登记 Revision）
        manager.Cache.Remove(handle.Id);
        host.SubmitFrame(CreateEmptySubmission(batch));
        manager.ApplyCreateResults(host.LastCreateResults);

        Assert.Equal(0UL, manager.GetRenderHandleForTests(handle.Id, RenderResourceKind.Mesh));
        // 过期句柄进入释放队列（渲染线程帧首消费）
        Assert.True(manager.TryDequeueRenderRelease(out var release));
        Assert.Equal(RenderResourceKind.Mesh, release.Kind);
        Assert.NotEqual(0UL, release.Handle);
    }

    public void Dispose() => Services.Unregister<AssetManager>();

    private static ThreadRuntime TestRuntimeOnCurrentThread()
    {
        var runtime = new ThreadRuntime();
        runtime.RegisterMainThread();
        return runtime;
    }

    private static RenderThreadHost CreateStartedHost(ThreadRuntime runtime, RecordingBackend backend)
    {
        var host = new RenderThreadHost(runtime, backend);
        runtime.RegisterManagedLoop(host);
        host.Start();
        return host;
    }

    private static AssetManager CreateManager(ThreadRuntime runtime)
        => new(new FakePipeline(), runtime.MainThread, runtime);

    private static RenderSubmission CreateEmptySubmission(RenderResourceCreateBatch creates)
        => new(FrameCameraBlock.Identity, [], creates);

    private sealed class RecordingBackend : IRenderBackend
    {
        public int MeshCreateCount;
        private ulong _nextHandle = 100;

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

        public RenderTextureHandle CreateTexture(RenderTextureCreateRequest request) => new(_nextHandle++);

        public RenderShaderHandle CreateShader(RenderShaderCreateRequest request) => new(_nextHandle++);

        public RenderMeshHandle CreateMesh(RenderMeshCreateRequest request)
        {
            MeshCreateCount++;
            return new RenderMeshHandle(_nextHandle++);
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakePipeline : IAssetPipeline, IAssetKeyResolver
    {
        public AssetOperation<T> Request<T>(AssetBuildKey key, CancellationToken cancellationToken = default)
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