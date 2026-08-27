using System;
using System.Threading;
using System.Threading.Tasks;

namespace SilkEngine.Threading;

/// <summary>
/// Worker 后台调度接口：业务层与 AssetPipeline 经此提交纯 CPU 工作；
/// 隐藏 CLR ThreadPool、Worker 数量与停止细节。
/// </summary>
public interface IBackgroundScheduler
{
    /// <summary>提交异步工作到 Worker 域执行（整个异步生命周期内保持 Worker 域）。</summary>
    /// <param name="work">异步工作委托（接收取消令牌）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>工作完成句柄（传播成功、异常与取消）</returns>
    IJobHandle Run(Func<CancellationToken, ValueTask> work, CancellationToken cancellationToken = default);
}
