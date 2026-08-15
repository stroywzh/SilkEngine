using System;
using System.Threading;

namespace SilkEngine.Threading;

// TODO: 线程控制器，负责具体线程的管理，负责提供封装，启动，提交任务，执行人物，返回结果，提供WorkloadRequest返回WorkloadResult
public class ThreadController
{
    private readonly ThreadContext _context;
    public uint InternalId => _context.InternalManagedId;
    public ThreadContext Context => _context;

    public ThreadController(ThreadContext context)
    {
        _context = context;
    }
}
