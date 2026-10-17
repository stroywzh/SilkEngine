using SilkEngine.Rendering;
using SilkEngine.Rendering.Abstraction;
using Xunit;

namespace SilkEngine.Rendering.Tests;

public sealed class RenderingFailureTests
{
    private sealed class BrokenPipeline : RenderPipelineBase
    {
        public bool FailInitialization { get; init; }
        public bool FailCleanup { get; init; }
        public readonly Exception InitError = new InvalidOperationException("pipeline init");
        public readonly Exception CleanupError = new InvalidOperationException("pipeline cleanup");
        protected override void OnInitialize()
        {
            if (FailInitialization) throw InitError;
        }
        protected override void RecordCommands(ICommandBuffer commands, RenderContext context) { }
        protected override void OnResize(int width, int height) => throw new InvalidOperationException("resize");
        protected override void DisposeCore()
        {
            if (FailCleanup) throw CleanupError;
        }
    }

    private sealed class BrokenBackend : RenderBackendBase
    {
        public override string Name => "Broken";
        public readonly Exception InitError = new InvalidOperationException("backend init");
        public readonly Exception CleanupError = new InvalidOperationException("backend cleanup");
        protected override void OnInitialize(IGraphicsDevice device) => throw InitError;
        protected override void ExecuteCommands(ReadOnlySpan<RenderCommand> commands) { }
        protected override void OnDisposeCore() => throw CleanupError;
    }

    [Fact]
    public void Pipeline_InitializationAndCleanupErrors_AreBothPreserved()
    {
        using var backend = new SpyBackend();
        using var pipeline = new BrokenPipeline { FailInitialization = true, FailCleanup = true };
        var error = Assert.Throws<AggregateException>(() => pipeline.Initialize(backend));
        Assert.Contains(pipeline.InitError, error.InnerExceptions);
        Assert.Contains(pipeline.CleanupError, error.InnerExceptions);
    }

    [Fact]
    public void Backend_InitializationAndCleanupErrors_AreBothPreserved()
    {
        using var backend = new BrokenBackend();
        var error = Assert.Throws<AggregateException>(() => backend.Initialize(new NullGraphicsDevice()));
        Assert.Contains(backend.InitError, error.InnerExceptions);
        Assert.Contains(backend.CleanupError, error.InnerExceptions);
    }

    [Fact]
    public void ResizeExecutionFailure_FaultsPipeline_WhileNegativeArgumentDoesNot()
    {
        using var backend = new SpyBackend();
        using var pipeline = new BrokenPipeline();
        pipeline.Initialize(backend);
        Assert.Throws<ArgumentOutOfRangeException>(() => pipeline.Resize(-1, 100));
        pipeline.RenderFrame(default);
        Assert.Throws<InvalidOperationException>(() => pipeline.Resize(100, 100));
        var error = Assert.Throws<InvalidOperationException>(() => pipeline.RenderFrame(default));
        Assert.Contains("faulted", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
