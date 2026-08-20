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
    /// 进行等待
    /// <br/>线程阻塞等待
    /// </summary>
    void Wait();

    /// <summary>
    /// 转换为ValueTask
    /// </summary>
    /// <returns>转换为ValueTask类型</returns>
    ValueTask AsTask();
}
