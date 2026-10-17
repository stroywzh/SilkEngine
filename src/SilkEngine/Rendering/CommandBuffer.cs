using System.Numerics;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering;

internal enum LeaseState : byte
{
    Recording,
    Submitted,
    Executed,
    Faulted,
    Returned,
    Disposed,
}

internal sealed class CommandBufferStore
{
    internal List<RenderCommand> Commands { get; } = new();
    internal CommandBufferLease? Lease { get; set; }

    internal void Reset()
    {
        Commands.Clear();
        Lease = null;
    }
}

internal sealed class CommandBufferLease : ICommandBuffer
{
    internal RenderBackendBase Owner { get; }
    internal CommandBufferStore Store { get; }
    internal LeaseState State { get; set; } = LeaseState.Recording;

    internal CommandBufferLease(RenderBackendBase owner, CommandBufferStore store)
    {
        Owner = owner;
        Store = store;
    }

    public int CommandsCount => Owner.GetCommandCount(this);

    public void ClearColor(RenderTargetHandle target, Vector4 color) =>
        Owner.Record(this, RenderCommand.ClearColor(target, color));
}
