using System;
using System.Collections.Concurrent;
using System.Threading;
using SilkEngine.Runtime.Rendering;

namespace SilkEngine.Runtime.Host;

public sealed class RenderLoop : RuntimeBase
{
    // private static readonly RenderCommandQueue<> cmdQueue = new();
    protected override void Run(CancellationToken token)
    {
        throw new NotImplementedException();
    }
}
