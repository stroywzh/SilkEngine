namespace SilkEngine.Rendering.Abstraction;

/// <summary>Owns command storage and consumes submitted command buffers.</summary>
public interface IRenderBackend : IDisposable
{
    /// <summary>Gets the backend display name.</summary>
    string Name { get; }

    /// <summary>Initializes the backend exactly once.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="device"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The backend was already initialized or is faulted.</exception>
    /// <exception cref="ObjectDisposedException">The backend was disposed.</exception>
    void Initialize(IGraphicsDevice device);

    /// <summary>Rents a writable buffer; it must be submitted or returned.</summary>
    /// <returns>A new, exclusive writable lease with no recorded commands.</returns>
    /// <exception cref="InvalidOperationException">The backend is not initialized or is faulted.</exception>
    /// <exception cref="ObjectDisposedException">The backend was disposed.</exception>
    ICommandBuffer RentCommandBuffer();

    /// <summary>Returns a recording buffer without submitting it.</summary>
    /// <exception cref="ArgumentException">The buffer was not rented from this backend.</exception>
    /// <exception cref="InvalidOperationException">The buffer is not recording, or the backend is faulted.</exception>
    /// <exception cref="ObjectDisposedException">The backend was disposed.</exception>
    void RecycleCommandBuffer(ICommandBuffer buffer);

    /// <summary>Submits a non-empty recording buffer; normal return transfers ownership.</summary>
    /// <exception cref="ArgumentException">The buffer is empty or foreign.</exception>
    /// <exception cref="InvalidOperationException">The buffer is not recording, or the backend is faulted.</exception>
    /// <exception cref="ObjectDisposedException">The backend was disposed.</exception>
    /// <remarks>If this method throws, the caller retains ownership and may return the buffer.</remarks>
    void Submit(ICommandBuffer buffer);

    /// <summary>Consumes all pending submissions in order; an empty queue is a no-op.</summary>
    /// <exception cref="InvalidOperationException">The backend is not initialized or is faulted.</exception>
    /// <exception cref="ObjectDisposedException">The backend was disposed.</exception>
    void ExecuteRender();

    /// <summary>Waits for already-issued GPU work; does not execute pending CPU submissions.</summary>
    /// <exception cref="InvalidOperationException">The backend is unavailable or a callback is active.</exception>
    /// <exception cref="ObjectDisposedException">The backend was disposed.</exception>
    void WaitForIdle();
}

/// <summary>Marker interface for a graphics device supplied by the host.</summary>
public interface IGraphicsDevice { }
