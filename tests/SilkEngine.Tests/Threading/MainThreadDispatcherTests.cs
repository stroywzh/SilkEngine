using SilkEngine.Threading;
using Xunit;

namespace SilkEngine.Tests.Threading;

public class MainThreadDispatcherTests
{
    [Fact]
    public void Drain_ExecutesOnlySelectedPhaseAndUsesBatchBoundary()
    {
        var dispatcher = new MainThreadDispatcher(new TestGuard(ThreadDomain.Main));
        var calls = new List<int>();

        dispatcher.Post(MainThreadPhase.FrameCommit, () => calls.Add(1));
        dispatcher.Post(MainThreadPhase.PreRender, () =>
        {
            calls.Add(2);
            dispatcher.Post(MainThreadPhase.PreRender, () => calls.Add(3));
        });

        dispatcher.Drain(MainThreadPhase.PreRender);
        Assert.Equal([2], calls);
        dispatcher.Drain(MainThreadPhase.PreRender);
        Assert.Equal([2, 3], calls);
        dispatcher.Drain(MainThreadPhase.FrameCommit);
        Assert.Equal([2, 3, 1], calls);
    }

    [Fact]
    public async Task InvokeAsync_CancelledBeforeDrain_DoesNotInvokeCallback()
    {
        var dispatcher = new MainThreadDispatcher(new TestGuard(ThreadDomain.Main));
        using var cancellation = new CancellationTokenSource();
        var invoked = false;
        var operation = dispatcher.InvokeAsync(MainThreadPhase.FrameCommit, () => invoked = true, cancellation.Token);

        cancellation.Cancel();
        dispatcher.Drain(MainThreadPhase.FrameCommit);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await operation);
        Assert.False(invoked);
    }

    private sealed class TestGuard : IThreadGuard
    {
        private readonly ThreadDomain _domain;
        public TestGuard(ThreadDomain domain) => _domain = domain;
        public ThreadDomain Current => _domain;
        public bool IsCurrent(ThreadDomain domain) => domain == _domain;
        public void Assert(ThreadDomain expected, string operation)
        {
            if (_domain != expected) throw new ThreadDomainException(operation, expected, _domain);
        }
    }
}
