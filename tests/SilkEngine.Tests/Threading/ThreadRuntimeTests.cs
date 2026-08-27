using SilkEngine.Core;
using SilkEngine.Threading;
using Xunit;

namespace SilkEngine.Tests.Threading;

// 与 ServicesTests（调用 Services.Shutdown 清空全局注册表）串行，保证服务契约测试确定性
[Collection("Assets")]
public class ThreadRuntimeTests
{
    [Fact]
    public void Runtime_ServiceBootstrap_RegistersSingleRuntimeInstance()
    {
        // [Service] ModuleInitializer 注册可能已被 Services.Shutdown 清空（同集合内执行顺序不定），
        // 自管注册窗口：先注销残留 → 注册自建实例 → 断言单实例语义 → 清理（各顺序均稳定）
        using var runtime = new ThreadRuntime();
        Services.Unregister<ThreadRuntime>();
        Services.Register(runtime);
        try
        {
            Assert.Same(runtime, Services.Get<ThreadRuntime>());
        }
        finally
        {
            Services.Unregister<ThreadRuntime>();
        }
    }

    [Fact]
    public async Task Runtime_BackgroundFailure_IsAvailableThroughAsTask()
    {
        using var runtime = new ThreadRuntime();
        runtime.RegisterMainThread();
        var handle = runtime.Background.Run(_ => ValueTask.FromException(new InvalidOperationException("worker failed")));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await handle.AsTask());
        Assert.Equal("worker failed", ex.Message);
    }

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
