using System;
using System.Runtime.InteropServices;

namespace SilkEngine.Threading;

/// <summary>
/// 线程上下文，用于记录具体线程的信息
/// </summary>
public record ThreadContext
{
    /// <summary>线程句柄（执行者线程本体）</summary>
    public Thread Thread { get; init; }

    /// <summary>
    /// 线程名称
    /// </summary>
    public string Name => Thread.Name ?? $"UnNamed-ManagedThread-{InternalManagedId}";

    /// <summary>
    /// OS 提供的线程 ID（Windows：GetCurrentThreadId；非 Windows 回退 Environment.CurrentManagedThreadId）
    /// </summary>
    public int NativeThreadId => Native.GetCurrentThreadId();

    /// <summary>
    /// 内部管理ID
    /// </summary>
    public uint InternalManagedId { get; internal set; }

    /// <summary>是否后台线程（透传 Thread.IsBackground）</summary>
    public bool IsBackground => Thread.IsBackground;

    /// <summary>线程优先级（透传 Thread.Priority）</summary>
    public ThreadPriority Priority => Thread.Priority;

    /// <summary>创建线程上下文。</summary>
    /// <param name="thread">线程句柄</param>
    /// <param name="internalId">内部管理 ID（执行者自增分配）</param>
    public ThreadContext(Thread thread, uint internalId)
    {
        this.Thread = thread;
        this.InternalManagedId = internalId;
    }

    public override string ToString()
    {
        return $"-ThreadContext:Name{Name}\n |NativeThreadId{NativeThreadId}-InternalId{InternalManagedId}\n |IsBackGround{IsBackground}-Priority{Priority}";
    }
}

/// <summary>
/// 原生线程 ID 提供者：Windows 经 P/Invoke GetCurrentThreadId 获取 OS 线程 ID，
/// 非 Windows 回退托管线程 ID（Environment.CurrentManagedThreadId）。
/// </summary>
internal static class Native
{
    private static readonly bool _isWindows = OperatingSystem.IsWindows();

    [DllImport("kernel32.dll", EntryPoint = "GetCurrentThreadId")]
    private static extern uint GetCurrentThreadIdNative();

    public static int GetCurrentThreadId() =>
        _isWindows
            ? unchecked((int)GetCurrentThreadIdNative())
            : Environment.CurrentManagedThreadId;
}
