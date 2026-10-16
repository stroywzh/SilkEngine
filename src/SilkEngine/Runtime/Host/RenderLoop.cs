using System;
using System.Collections.Concurrent;
using System.Threading;
using SilkEngine.Rendering;

namespace SilkEngine.Runtime.Host;

internal sealed class RenderLoop : RuntimeBase
{
    // 标志位，0未完成，1已完成
    private byte _completed = 0;

    public bool IsComplete => Volatile.Read(ref _completed) == 1;
    public override string ThreadName => "RenderThread";

    protected override void Run()
    {
        Thread.Sleep(160);
        Log.Info("RenderFinished");
        Signal();
    }

    internal void Signal() => Volatile.Write(ref _completed, 1);

    public void Reset() => Volatile.Write(ref _completed, 0);

    internal void WaitForFrameFinished()
    {
        var spinner = new SpinWait();
        while (Volatile.Read(ref _completed) == 0)
        {
            spinner.SpinOnce();
        }
    }
}
