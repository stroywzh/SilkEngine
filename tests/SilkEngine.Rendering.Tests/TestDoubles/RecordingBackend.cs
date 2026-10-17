using SilkEngine.Rendering;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.Tests;

internal sealed class RecordingBackend : RenderBackendBase
{
    private int _executeCalls;

    public override string Name => "Recording";
    public List<RenderCommand[]> Frames { get; } = new();
    public bool ThrowOnInitialize { get; set; }
    public int ThrowOnExecuteCall { get; set; } = -1;
    public int DisposeCount { get; private set; }
    public int WaitForIdleCount { get; private set; }

    public Action? OnExecute { get; set; }

    protected override void OnInitialize(IGraphicsDevice device)
    {
        if (ThrowOnInitialize) throw new InvalidOperationException("initialize failed");
    }

    protected override void ExecuteCommands(ReadOnlySpan<RenderCommand> commands)
    {
        _executeCalls++;
        OnExecute?.Invoke();
        if (_executeCalls == ThrowOnExecuteCall)
            throw new InvalidOperationException("execute failed");

        Frames.Add(commands.ToArray());
    }

    protected override void OnWaitForIdle() => WaitForIdleCount++;
    protected override void OnDisposeCore() => DisposeCount++;
}
