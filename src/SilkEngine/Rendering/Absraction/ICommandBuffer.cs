namespace SilkEngine.Rendering.Abstraction;

/// <summary>
/// A writable lease over backend-owned command storage. It becomes permanently non-writable after
/// submission, return, execution, fault, or backend disposal.
/// </summary>
/// <remarks>
/// Recording is command-agnostic: one command is an id plus the raw bytes of its struct. The buffer
/// copies the payload, so the caller's span may die immediately after this call returns.
/// </remarks>
public interface ICommandBuffer
{
    /// <summary>Gets the command count while this buffer is writable.</summary>
    /// <exception cref="InvalidOperationException">The buffer is no longer writable, or the backend is faulted.</exception>
    /// <exception cref="ObjectDisposedException">The owning backend was disposed.</exception>
    int CommandsCount { get; }

    /// <summary>Copies one command payload into this buffer.</summary>
    /// <param name="commandId">Registered command id in 0–255; ids come from the command attribute.</param>
    /// <param name="payload">Raw bytes of the command struct; copied before this method returns.</param>
    /// <exception cref="ArgumentOutOfRangeException">The id is outside 0–255.</exception>
    /// <exception cref="InvalidOperationException">The buffer is no longer writable, or the backend is faulted.</exception>
    /// <exception cref="ObjectDisposedException">The owning backend was disposed.</exception>
    void Record(int commandId, ReadOnlySpan<byte> payload);
}
