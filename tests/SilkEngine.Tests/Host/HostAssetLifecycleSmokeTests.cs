using SilkEngine.Assets;
using SilkEngine.Host;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Scene;
using SilkEngine.Tests.Assets;
using SilkEngine.Tests.Core;

namespace SilkEngine.Tests.Host;

// 别名须置于文件范围命名空间声明之后（外层命名空间成员（SilkEngine.Tests.Assets）优先于编译单元级别名）
using Assets = SilkEngine.Assets.Assets;

using Scene = SilkEngine.Scene.Scene;

/// <summary>
/// 宿主级资产生命周期冒烟（任务 9）：帧末驱逐接线（StepFrame 帧末 UnloadUnused）、
/// GPU release 于渲染帧首排空、场景卸载 Slot 驻留释放、关闭顺序（停止新请求 → 最后解静态门面）。
/// </summary>
[Collection("Assets")]
public sealed class HostAssetLifecycleSmokeTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly EngineHost _host;

    public HostAssetLifecycleSmokeTests()
    {
        _tempRoot = TestTempDirectory.Create();
        File.WriteAllBytes(Path.Combine(_tempRoot, "T.png"), PngFixtures.RedPng);
        _host = EngineHost.Create(builder =>
        {
            builder.UseHeadlessForTests();
            builder.UseAssetRoot(_tempRoot);
            builder.UseLibraryRoot(Path.Combine(_tempRoot, "Library"));
        });
        _host.Initialize();
    }

    public void Dispose()
    {
        _host.Dispose();
        // 与 TestAssetPipelineFixture.Dispose 同款防御：SQLite 可能仍持有文件句柄，清理失败静默容忍
        try
        {
            TestTempDirectory.Delete(_tempRoot);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void FrameEnd_UnloadsUnheldPayload_AndDrainsQueuedGpuReleaseOnRenderFrameStart()
    {
        var manager = _host.AssetManager;
        var handle = manager.GetHandle<TextureAsset>("T.png");
        var payload = manager.LoadAsync<TextureAsset>("T.png").AsTask().GetAwaiter().GetResult();
        using var slot = manager.CreateSlot(handle); // 驻留保护 → GPU 创建完成并发布

        for (var i = 0; i < 3; i++)
            _host.Loop.StepFrame();
        Assert.True(manager.TryResolve(handle, out TextureAsset? cached));
        Assert.Same(payload, cached);
        Assert.NotEqual(0UL, manager.GetRenderHandleForTests(handle.Id, RenderResourceKind.Texture));

        slot.Dispose(); // 解除驻留 → 帧末驱逐把 GPU 释放请求入队
        _host.Loop.StepFrame();
        Assert.False(manager.TryResolve(handle, out _));
        Assert.True(manager.DrainReleaseRequestsForTests().Count > 0);

        _host.Loop.StepFrame(); // 渲染帧首排空释放队列
        Assert.Empty(manager.DrainReleaseRequestsForTests());
    }

    [Fact]
    public void SceneUnload_DisposesRendererSlots_ReleasingResidency()
    {
        var manager = _host.AssetManager;
        var scene = _host.SceneManager.Create("Main");
        _host.SceneManager.LoadScene(scene);
        var renderer = scene.CreateGameObject("Cube").AddComponent<MeshRenderer>();
        var mesh = manager.RegisterTransient(new MeshAsset("cube", [0, 1, 2], [3], null));
        renderer.Mesh = mesh;
        Assert.Equal(1, manager.GetResidencyForTests(mesh.Id));

        var next = _host.SceneManager.Create("Next");
        _host.SceneManager.LoadScene(next);
        _host.Loop.StepFrame();

        Assert.Equal(0, manager.GetResidencyForTests(mesh.Id));
    }

    [Fact]
    public void Dispose_StopsNewRequests_AndUnbindsStaticFacadeLast()
    {
        var manager = _host.AssetManager; // Dispose 前捕获引用

        _host.Dispose();

        Assert.True(_host.IsDisposed);
        Assert.Throws<InvalidOperationException>(() => Assets.Load<TextureAsset>("T.png"));
        Assert.Throws<ObjectDisposedException>(() => manager.LoadAsync<TextureAsset>("T.png"));
    }
}