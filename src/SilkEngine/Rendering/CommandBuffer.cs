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

/// <summary>One recorded command inside a store's arena.</summary>
internal readonly struct CommandEntry
{
    internal readonly byte Id;
    internal readonly int Offset;
    internal readonly int Length;

    internal CommandEntry(byte id, int offset, int length)
    {
        Id = id;
        Offset = offset;
        Length = length;
    }
}

internal sealed class CommandBufferStore
{
    internal byte[] Arena = Array.Empty<byte>();
    internal int ArenaLength;
    internal List<CommandEntry> Entries { get; } = new();
    internal CommandBufferLease? Lease { get; set; }

    /// <summary>Copies one payload into the arena and records its slice.</summary>
    internal void Append(byte id, ReadOnlySpan<byte> payload)
    {
        var required = ArenaLength + payload.Length;
        if (required > Arena.Length)
        {
            Array.Resize(ref Arena, Math.Max(required, Math.Max(256, Arena.Length * 2)));
        }

        payload.CopyTo(Arena.AsSpan(ArenaLength));
        Entries.Add(new CommandEntry(id, ArenaLength, payload.Length));
        ArenaLength = required;
    }

    internal void Reset()
    {
        Entries.Clear();
        ArenaLength = 0;
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

    public void Record(int commandId, ReadOnlySpan<byte> payload) =>
        Owner.Record(this, commandId, payload);
}
