using System.Collections.Generic;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.Pipeline;
using SilkEngine.Scene;

namespace SilkEngine.Tests.Rendering;

public class RenderCollectorTests
{
    [Fact]
    public void Gather_EmptyInputs_ReturnsNullCameraAndNoBatches()
    {
        var collector = new RenderCollector();
        collector.Gather([], [], out var camera, out var batches);
        Assert.Null(camera);
        Assert.Empty(batches);
    }

    [Fact]
    public void Gather_TakesFirstCamera()
    {
        var cam1 = new GameObject("A").AddComponent<Camera>();
        var cam2 = new GameObject("B").AddComponent<Camera>();
        var collector = new RenderCollector();
        collector.Gather([cam1, cam2], [], out var camera, out _);
        Assert.Same(cam1, camera);
    }

    [Fact]
    public void Gather_AssemblesSingleBatchWithAllRenderables()
    {
        var mr1 = new GameObject("R1").AddComponent<MeshRenderer>();
        var mr2 = new GameObject("R2").AddComponent<MeshRenderer>();
        var collector = new RenderCollector();
        collector.Gather([], [mr1, mr2], out _, out var batches);
        var batch = Assert.Single(batches);
        Assert.Equal(2, batch.Renderers.Count);
        Assert.Same(mr1, batch.Renderers[0]);
        Assert.Same(mr2, batch.Renderers[1]);
    }

    [Fact]
    public void Gather_SecondCall_ZeroAllocation()
    {
        var collector = new RenderCollector();
        var renderables = new List<IRenderable> { new GameObject("R").AddComponent<MeshRenderer>() };
        collector.Gather([], renderables, out _, out _); // 预热：JIT 编译 + 内部缓冲扩容

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 200; i++)
            collector.Gather([], renderables, out _, out _);
        var after = GC.GetAllocatedBytesForCurrentThread();

        Assert.True(after - before < 4096, $"Gather allocated {after - before} bytes over 200 calls");
    }

    [Fact]
    public void Gather_ReusesBatches_WithUpdatedContent()
    {
        var collector = new RenderCollector();
        var mr1 = new GameObject("R1").AddComponent<MeshRenderer>();
        var mr2 = new GameObject("R2").AddComponent<MeshRenderer>();
        var listA = new List<IRenderable> { mr1 };
        var listB = new List<IRenderable> { mr2 };

        collector.Gather([], listA, out _, out var batchesA);
        var batchA = Assert.Single(batchesA);
        Assert.Same(mr1, batchA.Renderers[0]); // 第一次内容

        collector.Gather([], listB, out _, out var batchesB);
        var batchB = Assert.Single(batchesB);
        Assert.Same(mr2, batchB.Renderers[0]); // 第二次内容
        Assert.Same(batchA, batchB);           // RenderBatch 实例复用
        Assert.Same(batchesA, batchesB);       // 批次列表实例复用
    }
}
