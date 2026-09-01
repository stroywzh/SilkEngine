using System;
using System.Threading;
using System.Threading.Tasks;

namespace SilkEngine.Threading;

/// <summary>
/// 主线程帧阶段：PreRender 在本帧 Render 收集前生效；FrameCommit 在帧末快照 swap 后、资产结果应用前；
/// Continuation 供生命周期安全回调等帧末收尾使用。
/// </summary>
public enum MainThreadPhase
{
    PreRender,
    FrameCommit,
    Continuation,
}

/// <summary>
/// 主线程投递接口：Worker 域可 Post/InvokeAsync 投递，Main 域在对应帧阶段排空执行。
/// 业务层只经此接口使用主线程阶段能力。
/// </summary>
public interface IMainThreadDispatcher
{
    /// <summary>投递回调到指定阶段（Main 域排空执行）；关闭后抛 InvalidOperationException。</summary>
    /// <param name="phase">目标帧阶段</param>
    /// <param name="action">回调委托</param>
    void Post(MainThreadPhase phase, Action action);

    /// <summary>投递回调并返回完成句柄；关闭或取消时以取消结束，不执行回调。</summary>
    /// <param name="phase">目标帧阶段</param>
    /// <param name="action">回调委托</param>
    /// <param name="cancellationToken">取消令牌（取消后回调不再执行）</param>
    /// <returns>等待 Main 域执行完成的句柄</returns>
    ValueTask InvokeAsync(MainThreadPhase phase, Action action, CancellationToken cancellationToken = default);
}
