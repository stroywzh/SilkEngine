using SilkEngine.Math;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.Pipeline;

namespace SilkEngine.Tests.Rendering;

/// <summary>
/// RenderCollector 扩展点（阶段 3 任务 4）：新增渲染器类型经 IRendererProvider 注册接入，
/// 不修改 EngineLoop；Collect 统一收集全部 provider 输出并组装批次。
/// </summary>
public class RenderCollectorExtensibilityTests
{
    private sealed class FakeRenderer : IRenderable
    {
        public RenderShaderHandle ShaderHandle => default;
        public RenderMeshHandle MeshHandle => default;
        public RenderTextureHandle TextureHandle => default;
        public RenderMaterialParameters MaterialParameters => new([]);
        public bool Enabled => true;
        public Matrix4x4 WorldMatrix => Matrix4x4.Identity;
    }

    private sealed class FakeRendererProvider : IRendererProvider
    {
        public IEnumerable<IRenderable> Collect() => [new FakeRenderer()];
    }

    [Fact]
    public void AddingRendererProvider_ProducesBatchWithoutEngineLoopChange()
    {
        var collector = new RenderCollector();
        collector.AddProvider(new FakeRendererProvider());

        collector.Collect([], out _, out var batches);

        Assert.Single(batches);
        Assert.Single(batches[0].Renderers);
    }

    [Fact]
    public void NoProviders_ProducesEmptyBatchesAndNullCamera()
    {
        var collector = new RenderCollector();

        collector.Collect([], out var camera, out var batches);

        Assert.Null(camera);
        Assert.Empty(batches);
    }

    [Fact]
    public void MultipleProviders_AreAggregatedIntoSingleBatch()
    {
        var collector = new RenderCollector();
        collector.AddProvider(new FakeRendererProvider());
        collector.AddProvider(new FakeRendererProvider());

        collector.Collect([], out _, out var batches);

        Assert.Single(batches);
        Assert.Equal(2, batches[0].Renderers.Count);
    }
}
