using System;
using System.Threading;
using SilkEngine.Core.LogSystem;

namespace SilkEngine.Runtime.Host;

/// <summary>
/// TIP: 设计中MainLoop 会被Main启动，在Main中间被ThreadFractory创建
/// --启动线程(Main)-->MainLoop(Thread1)-->RenderLoop(Thread2)
/// 按照顺序创建，因此之后主线程一定是第一个加入RuntimeRegistry的。
/// </summary>
public sealed class MainLoop : RuntimeBase
{
    public override bool IsBackground => false;
    public override string ThreadName => "MainThread";
    internal override bool IsMainThread => true;

    //暂时在这里硬编码这个，后面做一个Dict的映射
    private RenderLoop _renderLoop;

    protected override void Initialize()
    {
        _thread = Thread.CurrentThread;
        ThreadFactory.RuntimeBuilder(out _renderLoop);
        Log.Info("MainLoop Init done...");
    }

    protected override void Run()
    {
        _renderLoop.Reset();
        Thread.Sleep(20);
        Log.Info("逻辑已完成，正在等待渲染");
        _renderLoop.WaitForFrameFinished();
    }

    protected override void DisposeCore()
    {
        _renderLoop.Dispose();
    }
}
