using System;
using System.Collections.Concurrent;
using System.Threading;
using SilkEngine.Rendering;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Runtime.Host;

internal sealed class RenderLoop : RuntimeBase
{
    // 标志位，0未完成，1已完成
    private byte _completed = 0;

    private static RenderFrameDriver _renderFrameDriver;
    public bool IsComplete => Volatile.Read(ref _completed) == 1;
    public override string ThreadName => "RenderThread";

    protected override void Initialize()
    {
        //TIP:暂时使用Application作为注册
        _renderFrameDriver = new(Application.RenderPipeline, Application.RenderBackend);
        _renderFrameDriver.Initialize(Application.GraphicsDevice);
    }

    protected override void Run()
    {
        Thread.Sleep(1000);
        // TIP:临时使用default的RenderContext
        _renderFrameDriver.RenderFrame(default);
        Log.Info("RenderFinished");
        Signal();
    }

    private void Signal() => Volatile.Write(ref _completed, 1);

    public void Reset() => Volatile.Write(ref _completed, 0);

    internal void WaitForFrameFinished()
    {
        var spinner = new SpinWait();
        while (Volatile.Read(ref _completed) == 0)
        {
            spinner.SpinOnce();
        }
    }

    protected override void DisposeCore() { }
}
