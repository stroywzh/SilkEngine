using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SilkEngine.Core;
using SilkEngine.Threading;

namespace SilkEngine.Assets;

/// <summary>
/// 业务安全资产操作：默认 await 在 Main 安全阶段恢复（Continuation 阶段）；
/// <see cref="AsTask"/> 显式逃逸到标准 Task（不再承诺 continuation 线程）；
/// <see cref="Cancel"/> 只取消当前调用方；共享 Pipeline Job 不受单方取消影响。
/// 内部 Pipeline 不使用本业务操作递归依赖。
/// </summary>
/// <typeparam name="T">资产载荷类型</typeparam>
public sealed class AssetOperation<T>
    where T : class, IAssetPayload
{
    private readonly Task<T> _task;
    private readonly Action _cancel;
    private readonly IMainThreadDispatcher _dispatcher;
    private readonly ThreadRuntime _runtime;
    private readonly TaskCompletionSource<T> _completion;

    /// <summary>创建安全操作：底层任务完成后完成状态经 Main 安全阶段发布。</summary>
    /// <param name="assetId">资产标识（外部任务包装时可为 default）</param>
    /// <param name="task">底层任务（共享 Job 或外部 Task）</param>
    /// <param name="cancel">取消回调；null 时默认只取消本操作</param>
    /// <param name="dispatcher">主线程派发器（Main 阶段发布用）</param>
    /// <param name="runtime">线程运行时（域判定用）</param>
    internal AssetOperation(
        AssetId assetId,
        Task<T> task,
        Action? cancel,
        IMainThreadDispatcher dispatcher,
        ThreadRuntime runtime)
    {
        AssetId = assetId;
        _task = task;
        _dispatcher = dispatcher;
        _runtime = runtime;
        _completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _cancel = cancel is null ? () => _completion.TrySetCanceled() : cancel;
        WireCompletion(task);
    }

    /// <summary>资产标识（FromTask 包装外部任务时为 default）</summary>
    public AssetId AssetId { get; }

    /// <summary>本操作是否已完成（成功/失败/取消）</summary>
    public bool IsCompleted => _completion.Task.IsCompleted;

    /// <summary>显式逃逸到标准 Task：返回缓存的底层任务（传播成功/异常/取消，不承诺 Main continuation）</summary>
    public Task<T> AsTask() => _task;

    /// <summary>取消当前调用方的操作；共享底层任务与其它消费者不受影响</summary>
    public void Cancel() => _cancel();

    /// <summary>安全 await 入口</summary>
    public AssetOperationAwaiter<T> GetAwaiter() => new(this);

    /// <summary>
    /// 从外部 Task 创建安全操作：不搬移外部 Task 的执行线程，
    /// 只把其结果/异常/取消重新发布到 Main 安全阶段。经 Services 中的 AssetManager 包装。
    /// </summary>
    /// <param name="task">外部任务</param>
    /// <returns>安全操作</returns>
    public static AssetOperation<T> FromTask(Task<T> task)
        => Services.Get<AssetManager>().WrapExternalTask(task);

    /// <summary>完成状态任务（每操作独立；取消/异常/结果均落在其上）</summary>
    internal Task<T> Completion => _completion.Task;

    /// <summary>await 续延登记：完成状态确定后经 Main 安全阶段执行调用方续延</summary>
    internal void OnCompleted(Action continuation)
    {
        var completion = _completion.Task;
        if (completion.IsCompleted)
        {
            MarshalContinuation(continuation);
            return;
        }
        completion.ContinueWith(
            _ => MarshalContinuation(continuation),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void MarshalContinuation(Action continuation)
    {
        if (_runtime.CurrentDomain == ThreadDomain.Main)
            continuation();
        else
            _dispatcher.Post(MainThreadPhase.Continuation, continuation);
    }

    private void WireCompletion(Task<T> task)
    {
        task.ContinueWith(
            static (completed, state) => ((AssetOperation<T>)state!).Publish(completed),
            this,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void Publish(Task<T> completed)
    {
        switch (completed.Status)
        {
            case TaskStatus.RanToCompletion:
                _completion.TrySetResult(completed.Result);
                break;
            case TaskStatus.Canceled:
                _completion.TrySetCanceled();
                break;
            default:
                _completion.TrySetException(completed.Exception!);
                break;
        }
    }
}

/// <summary>AssetOperation 的安全 await 支持：完成状态确定后经 Main 安全阶段执行续延</summary>
/// <typeparam name="T">资产载荷类型</typeparam>
public readonly struct AssetOperationAwaiter<T> : INotifyCompletion, ICriticalNotifyCompletion
    where T : class, IAssetPayload
{
    private readonly AssetOperation<T> _operation;

    internal AssetOperationAwaiter(AssetOperation<T> operation) => _operation = operation;

    /// <summary>是否无需挂起</summary>
    public bool IsCompleted => _operation.IsCompleted;

    /// <summary>登记续延（完成状态确定后经 Main 安全阶段执行）</summary>
    public void OnCompleted(Action continuation) => _operation.OnCompleted(continuation);

    /// <summary>登记续延（unsafe 变体；与 OnCompleted 相同语义）</summary>
    public void UnsafeOnCompleted(Action continuation) => OnCompleted(continuation);

    /// <summary>await 结果；失败时抛出底层异常，取消时抛 OperationCanceledException</summary>
    /// <returns>资产载荷</returns>
    public T GetResult() => _operation.Completion.GetAwaiter().GetResult();
}
