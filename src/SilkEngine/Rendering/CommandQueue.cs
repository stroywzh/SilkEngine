using System.Runtime.InteropServices;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering;

/// <summary>
/// Owns the command-buffer pool, the pending submissions and the issued leases, i.e. the whole
/// record -&gt; submit -&gt; execute protocol.
/// </summary>
/// <remarks>
/// <see cref="RenderBackendBase"/> keeps the public contract (thread affinity, state machine,
/// diagnostics) and delegates the buffers here; the queue itself performs no thread checking, so the
/// backend remains the single place that decides which thread may record.
/// </remarks>
internal sealed class CommandQueue
{
    private readonly RenderBackendBase _owner;
    private readonly Stack<CommandBufferStore> _freeStores = new();
    private readonly Queue<CommandBufferStore> _pending = new();
    private readonly HashSet<CommandBufferLease> _issued = new();

    /// <summary>Creates the queue of one backend.</summary>
    /// <param name="owner">Backend that owns the leases and dispatches the commands.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    internal CommandQueue(RenderBackendBase owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <summary>Number of submitted frames waiting to be executed.</summary>
    internal int PendingCount => _pending.Count;

    /// <summary>Rents a writable buffer, reusing a pooled store when possible.</summary>
    internal CommandBufferLease Rent()
    {
        var store = _freeStores.Count > 0 ? _freeStores.Pop() : new CommandBufferStore();
        var lease = new CommandBufferLease(_owner, store);
        store.Lease = lease;
        _issued.Add(lease);
        return lease;
    }

    /// <summary>Returns a recording buffer without submitting it.</summary>
    internal void Recycle(ICommandBuffer? buffer)
    {
        var lease = RequireOwnedLease(buffer);
        if (lease.State != LeaseState.Recording)
            throw new InvalidOperationException("Only a recording buffer can be returned.");

        lease.State = LeaseState.Returned;
        _issued.Remove(lease);
        var store = lease.Store;
        store.Reset();
        _freeStores.Push(store);
    }

    /// <summary>Submits a recording buffer; normal return transfers ownership.</summary>
    internal void Submit(ICommandBuffer? buffer)
    {
        var lease = RequireOwnedLease(buffer);
        if (lease.State != LeaseState.Recording)
            throw new InvalidOperationException("Only a recording buffer can be submitted.");
        if (lease.Store.Entries.Count == 0)
            throw new ArgumentException(
                "The command buffer is empty; record at least one command or recycle it.",
                nameof(buffer)
            );

        // Validation complete; enqueue first, then make the transfer visible.
        _pending.Enqueue(lease.Store);
        lease.State = LeaseState.Submitted;
    }

    /// <summary>Executes every pending submission and presents the frame.</summary>
    /// <param name="device">Device the commands run on.</param>
    /// <param name="frameContext">Snapshot of the frame being executed.</param>
    /// <remarks>
    /// Order: <c>MakeCurrent</c> -&gt; dispatch every pending command -&gt; <c>Present</c>. 
    /// <br/>An empty queue still makes the context current and still presents. A failing frame drops everything still
    /// queued and keeps the backend usable.
    /// </remarks>
    internal void Execute(IRenderDevice device, in FrameContext frameContext)
    {
        ArgumentNullException.ThrowIfNull(device);

        try
        {
            var context = new CommandContext(device);

            device.MakeCurrent();

            while (_pending.Count > 0)
            {
                var store = _pending.Peek();

                foreach (ref readonly var entry in CollectionsMarshal.AsSpan(store.Entries))
                {
                    var payload = new ReadOnlySpan<byte>(store.Arena, entry.Offset, entry.Length);
                    if (!_owner.DispatchCommand(entry.Id, payload, ref context))
                        throw new RenderDeviceException(
                            $"Unknown render command id 0x{entry.Id:X2} in frame "
                                + $"{frameContext.FrameIndex}; no executor is registered in this assembly."
                        );
                }

                _pending.Dequeue();
                var lease = store.Lease!;
                lease.State = LeaseState.Executed;
                _issued.Remove(lease);
                store.Reset();
                _freeStores.Push(store);
            }

            // Presentation belongs to the frame, not to the commands.
            device.Present();
        }
        catch
        {
            // One frame failed: drop everything still queued, keep the backend usable.
            // Do not replay executed buffers; do not leave pending buffers writable.
            while (_pending.Count > 0)
            {
                var store = _pending.Dequeue();
                var lease = store.Lease!;
                lease.State = LeaseState.Faulted;
                _issued.Remove(lease);
                store.Reset();
                _freeStores.Push(store);
            }

            throw;
        }
    }

    /// <summary>Drops every lease and pooled buffer; called once from the backend's Dispose.</summary>
    internal void Clear()
    {
        foreach (var lease in _issued)
        {
            lease.State = LeaseState.Disposed;
            lease.Store.Reset();
        }
        _issued.Clear();

        while (_pending.Count > 0)
            _pending.Dequeue().Reset();

        while (_freeStores.Count > 0)
            _freeStores.Pop().Reset();
    }

    private CommandBufferLease RequireOwnedLease(ICommandBuffer? buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (buffer is not CommandBufferLease lease || !ReferenceEquals(lease.Owner, _owner))
            throw new ArgumentException(
                "The command buffer was not rented from this backend.",
                nameof(buffer)
            );

        return lease;
    }
}
