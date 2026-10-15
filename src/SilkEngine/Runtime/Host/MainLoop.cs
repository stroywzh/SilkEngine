using System;
using System.Threading;
using SilkEngine.Core.LogSystem;

namespace SilkEngine.Runtime.Host;

public sealed class MainLoop : RuntimeBase
{
    public override bool IsBackground => false;
    public override string ThreadName => "MainThread";
    internal override bool IsMainThread => true;

    protected override void Initialize()
    {
        _thread = Thread.CurrentThread;
    }

    protected override void Run(CancellationToken token)
    {
        while (!token.IsCancellationRequested) { }
    }
}
