using SilkEngine.Threading;
using Xunit;

namespace SilkEngine.Tests.Threading;

public class ThreadRuntimeTests
{
    [Fact]
    public async Task BackgroundRun_SetsWorkerDomainForEntireAsyncOperation()
    {
        using var runtime = new ThreadRuntime();
        // 主线程用 ThreadFactory 专用线程登记：xUnit 测试线程是线程池线程，
        // await 后可能被 Task.Run 直接复用（线程身份误判 Main），与生产形态（专用主线程）不符
        var main = ThreadFactory.CreateThread(runtime.RegisterMainThread, "TestMain");
        main.Start();
        main.Join();
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
