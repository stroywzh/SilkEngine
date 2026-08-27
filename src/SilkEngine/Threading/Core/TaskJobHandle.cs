using System;
using System.Threading.Tasks;

namespace SilkEngine.Threading;

/// <summary>Task 包装完成句柄；Wait 经 GetResult 传播原始异常与取消。</summary>
internal sealed class TaskJobHandle : IJobHandle
{
    private readonly Task _task;

    public TaskJobHandle(Task task) => _task = task;

    public bool IsCompleted => _task.IsCompleted;

    public void Wait() => _task.GetAwaiter().GetResult();

    public ValueTask AsTask() => new(_task);
}
