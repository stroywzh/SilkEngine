using SilkEngine.Threading;
using Xunit;

namespace SilkEngine.Tests.Threading;

public class ThreadRuntimeTests
{
    [Fact]
    public async Task BackgroundRun_SetsWorkerDomainForEntireAsyncOperation()
    {
        using var runtime = new ThreadRuntime();
        runtime.RegisterMainThread();
        var observed = new List<ThreadDomain>();

        var handle = runtime.Background.Run(async token =>
        {
            observed.Add(runtime.CurrentDomainForTests);
            await Task.Yield();
            observed.Add(runtime.CurrentDomainForTests);
        });

        await handle.AsTask();
        Assert.Equal([ThreadDomain.Worker, ThreadDomain.Worker], observed);
    }

    [Fact]
    public void Dispose_RejectsNewWorkAndIsIdempotent()
    {
        using var runtime = new ThreadRuntime();
        runtime.RegisterMainThread();
        runtime.Dispose();

        Assert.Throws<InvalidOperationException>(() => runtime.Background.Run(_ => ValueTask.CompletedTask));
        Assert.Throws<InvalidOperationException>(() => runtime.MainThread.Post(MainThreadPhase.PreRender, () => { }));
        runtime.Dispose();
    }
}
