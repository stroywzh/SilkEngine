using System;

namespace SilkEngine.Threading;

/// <summary>
/// 线程执行域：Main（引擎主线程帧序）、Worker（后台 CPU 工作）、Render（GPU 专用线程）；
/// 未登记线程标识为 Unknown。
/// </summary>
public enum ThreadDomain
{
    Unknown,
    Main,
    Worker,
    Render,
}

/// <summary>线程域守卫（internal 运行时协议）：业务层不得直接接触 Guard 实现。</summary>
public interface IThreadGuard
{
    /// <summary>当前线程所处域。</summary>
    ThreadDomain Current { get; }

    /// <summary>当前线程是否处于指定域。</summary>
    bool IsCurrent(ThreadDomain domain);

    /// <summary>断言当前域为 expected；不匹配抛 ThreadDomainException。</summary>
    void Assert(ThreadDomain expected, string operation);
}

/// <summary>
/// 线程域违规异常：携带操作名、期望域与实际域，供错误上报与诊断定位。
/// </summary>
/// <param name="operation">违规操作名</param>
/// <param name="expected">期望线程域</param>
/// <param name="actual">实际线程域</param>
public sealed class ThreadDomainException(
    string operation,
    ThreadDomain expected,
    ThreadDomain actual) : InvalidOperationException(
        $"{operation} must run in {expected} domain, but current domain is {actual}.")
{
    /// <summary>违规操作名。</summary>
    public string Operation { get; } = operation;

    /// <summary>期望线程域。</summary>
    public ThreadDomain Expected { get; } = expected;

    /// <summary>实际线程域。</summary>
    public ThreadDomain Actual { get; } = actual;
}
