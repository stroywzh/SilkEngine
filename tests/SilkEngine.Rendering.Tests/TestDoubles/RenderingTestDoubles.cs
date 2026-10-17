using System.Numerics;
using SilkEngine.Rendering;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.Tests;

internal sealed class NullGraphicsDevice : IGraphicsDevice { }

internal sealed class StubCommandBuffer : ICommandBuffer
{
    public int CommandsCount => 0;
    public void ClearColor(RenderTargetHandle target, Vector4 color) { }
}

internal sealed class SpyCommandBuffer : ICommandBuffer
{
    private int _count;
    public bool IsWritable { get; set; } = true;
    public int CommandsCount => _count;

    public void ClearColor(RenderTargetHandle target, Vector4 color)
    {
        if (!IsWritable) throw new InvalidOperationException("Buffer is not writable.");
        _count++;
    }
}

internal sealed class SpyBackend : IRenderBackend
{
    public string Name => "Spy";
    public SpyCommandBuffer? LastRented { get; private set; }
    public int RentCount { get; private set; }
    public int ReturnCount { get; private set; }
    public int SubmitCount { get; private set; }
    public int ExecuteRenderCount { get; private set; }
    public int WaitForIdleCount { get; private set; }
    public bool ThrowOnSubmit { get; set; }
    public bool Initialized { get; private set; }
    public bool Disposed { get; private set; }
    public Action? OnExecute { get; set; }

    public void Initialize(IGraphicsDevice device) => Initialized = true;

    public ICommandBuffer RentCommandBuffer()
    {
        RentCount++;
        LastRented = new SpyCommandBuffer();
        return LastRented;
    }

    public void RecycleCommandBuffer(ICommandBuffer buffer)
    {
        ReturnCount++;
        ((SpyCommandBuffer)buffer).IsWritable = false;
    }

    public void Submit(ICommandBuffer buffer)
    {
        if (ThrowOnSubmit) throw new InvalidOperationException("submit failed");
        SubmitCount++;
        ((SpyCommandBuffer)buffer).IsWritable = false;
    }

    public void ExecuteRender()
    {
        ExecuteRenderCount++;
        OnExecute?.Invoke();
    }

    public void WaitForIdle() => WaitForIdleCount++;
    public void Dispose() => Disposed = true;
}

internal sealed class TestPipeline : RenderPipelineBase
{
    public override string Name => "Test Pipeline";
    public Action<ICommandBuffer>? OnRecord { get; set; }
    public bool ThrowOnRecord { get; set; }
    public bool ThrowOnInitialize { get; set; }
    public int InitializeCount { get; private set; }
    public int ResizeCount { get; private set; }
    public (int Width, int Height) LastResize { get; private set; }
    public int DisposeCount { get; private set; }

    protected override void OnInitialize()
    {
        InitializeCount++;
        if (ThrowOnInitialize) throw new InvalidOperationException("initialize failed");
    }

    protected override void RecordCommands(ICommandBuffer commands, RenderContext ctx)
    {
        if (ThrowOnRecord) throw new InvalidOperationException("record failed");
        OnRecord?.Invoke(commands);
    }

    protected override void OnResize(int width, int height)
    {
        ResizeCount++;
        LastResize = (width, height);
    }

    protected override void DisposeCore() => DisposeCount++;
}
