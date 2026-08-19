using System;
using System.Threading.Tasks;
using SilkEngine.Core;

namespace SilkEngine.Threading;

/// <summary>Task 包装完成句柄；Wait 吞异常并 Log.Error（与工作线程"错误不中断"语义一致）。</summary>
internal sealed class TaskJobHandle : IJobHandle
{
    private readonly Task _task;

    public TaskJobHandle(Task task) => _task = task;

    public bool IsCompleted => _task.IsCompleted;

    public void Wait()
    {
        try
        {
            _task.GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Error($"[Job] Task failed: {ex}");
        }
    }

    public ValueTask AsTask() => new(_task);
}
