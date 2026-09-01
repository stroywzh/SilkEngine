using System;
using System.Threading;
using System.Threading.Tasks;

namespace SilkEngine.Threading;

/// <summary>
/// 受运行时控制的 Worker 调度实现：委托在 CoreCLR ThreadPool 执行，
/// 整个异步生命周期内进入 Worker 域（含 await 后续），链接运行时停止令牌，
/// 返回传播异常/取消的 TaskJobHandle。关闭后提交快速失败。
/// </summary>
internal sealed class BackgroundScheduler : IBackgroundScheduler
{
    private readonly ThreadRuntime _runtime;

    internal BackgroundScheduler(ThreadRuntime runtime) => _runtime = runtime;

    /// <summary>提交异步工作；ThreadRuntime 关闭后抛 InvalidOperationException。</summary>
    public IJobHandle Run(Func<CancellationToken, ValueTask> work, CancellationToken cancellationToken = default)
    {
        if (_runtime.IsDisposed)
            throw new InvalidOperationException("ThreadRuntime 已关闭");
        var linked = CancellationTokenSource.CreateLinkedTokenSource(_runtime.StoppingToken, cancellationToken);
        var task = Task.Run(async () =>
        {
            using var scope = _runtime.Enter(ThreadDomain.Worker);
            await work(linked.Token).ConfigureAwait(false);
        }, linked.Token);
        return new TaskJobHandle(task);
    }
}
