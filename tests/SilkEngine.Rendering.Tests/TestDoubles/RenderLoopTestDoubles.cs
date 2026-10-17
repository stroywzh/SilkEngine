using System.Numerics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using SilkEngine.Rendering;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Runtime;

namespace SilkEngine.Rendering.Tests;

internal sealed class LoopBackend : RenderBackendBase, IRenderWindowHost
{
    private int _pumps,
        _outputs,
        _executes,
        _idleCalls;

    public override string Name => "CPU loop backend";
    public bool IsClosing => false;
    public int SkipAttempts { get; init; }
    public int Pumps => Volatile.Read(ref _pumps);
    public int Outputs => Volatile.Read(ref _outputs);
    public int Executes => Volatile.Read(ref _executes);
    public int IdleCalls => Volatile.Read(ref _idleCalls);
    public Exception? InitializeError { get; init; }
    public Action? BeforeOutput { get; set; }
    public Action? OnExecute { get; init; }
    public int OwnerThread { get; private set; }
    public int DisposeThread { get; private set; }

    protected override void OnInitialize(IGraphicsDevice device)
    {
        OwnerThread = Environment.CurrentManagedThreadId;
        if (InitializeError is not null)
            throw InitializeError;
    }

    public void PumpEvents() => Interlocked.Increment(ref _pumps);

    public bool TryRenderFrame(Action executeCommands)
    {
        if (Interlocked.Increment(ref _outputs) <= SkipAttempts)
            return false;
        executeCommands();
        BeforeOutput?.Invoke();
        return true;
    }

    protected override void ExecuteCommands(ReadOnlySpan<RenderCommand> commands)
    {
        Interlocked.Increment(ref _executes);
        OnExecute?.Invoke();
    }

    protected override void OnWaitForIdle() => Interlocked.Increment(ref _idleCalls);

    protected override void OnDisposeCore() => DisposeThread = Environment.CurrentManagedThreadId;
}

internal sealed class LoopPipeline : RenderPipelineBase
{
    public Action<RenderContext>? Record { get; set; }

    protected override void OnInitialize() { }

    protected override void RecordCommands(ICommandBuffer commands, RenderContext context)
    {
        Record?.Invoke(context);
        commands.ClearColor(new RenderTargetHandle(1), Vector4.One);
    }
}

// Reflection is limited to test-side construction/invocation, never the production render path.
internal sealed class LoopHarness : IDisposable
{
    private static readonly MethodInfo BeginFrame =
        typeof(Time).GetMethod(
            "BeginFrame",
            BindingFlags.Static | BindingFlags.NonPublic
        )
        ?? throw new MissingMethodException("Time.BeginFrame is missing.");

    private readonly RuntimeBase _loop;
    private readonly MethodInfo _ready,
        _publish,
        _wait;

    public LoopHarness(LoopPipeline pipeline, LoopBackend backend, ulong initialFrameId = 0)
    {
        var type = typeof(RuntimeBase).Assembly.GetType("SilkEngine.Runtime.Host.RenderLoop", true)!;
        _loop = (RuntimeBase)
            Activator.CreateInstance(
                type,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new object[] { pipeline, backend, new NullGraphicsDevice() },
                null
            )!;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        _ready = type.GetMethod("WaitUntilReady", flags)!;
        _publish = type.GetMethod("PublishFrame", flags)!;
        _wait = type.GetMethod("WaitForFrameFinished", flags)!;
        if (_ready is null || _publish is null || _wait is null)
            throw new MissingMethodException("RenderLoop frame protocol is missing.");
        // Seed before starting only for the near-overflow test; never mutate live counters.
        type.GetField("_publishedFrameId", flags)!.SetValue(_loop, initialFrameId);
        type.GetField("_completedFrameId", flags)!.SetValue(_loop, initialFrameId);
        _loop.Start();
    }

    private object? Invoke(MethodInfo method, params object[] args)
    {
        try
        {
            return method.Invoke(_loop, args);
        }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }

    public void Ready(CancellationToken token) => Invoke(_ready, token);

    public void Publish(ulong id)
    {
        // Production announces the frame through the Time facade; mirror that on the test thread.
        BeginFrame.Invoke(null, new object[] { id, 0.0 });
        Invoke(_publish);
    }

    public void Wait(CancellationToken token) => Invoke(_wait, token);

    public void Stop() => _loop.RequestStop();

    public void Dispose() => _loop.Dispose();
}
