using System;
using System.Threading.Tasks;

namespace SilkEngine.Threading;

/// <summary>
/// 工作完成句柄
/// </summary>
public interface IJobHandle
{
    /// <summary>
    /// 是否完成
    /// <br/>完成后返回true
    /// </summary>
    bool IsCompleted { get; }

    /// <summary>
    /// 阻塞等待完成
    /// <br/>传播原始异常（不包装为 AggregateException）；取消以 OperationCanceledException 结束
    /// </summary>
    void Wait();

    /// <summary>
    /// 转换为ValueTask
    /// </summary>
    /// <returns>转换为ValueTask类型</returns>
    ValueTask AsTask();
}
