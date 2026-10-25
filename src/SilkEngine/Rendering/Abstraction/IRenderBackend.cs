namespace SilkEngine.Rendering.Abstraction;

/// <summary>
/// Owns the command-buffer queue, executes a frame on the device, and creates that device.
/// </summary>
/// <remarks>
/// Thread affinity: <see cref="Initialize"/> captures the calling thread as the owner. Every other
/// member must be called from that thread; there is no locking on the render path. The window is not
/// owned here: the backend creates the device, and the device owns context, window events and presentation.
/// </remarks>
public interface IRenderBackend : IDisposable
{
    /// <summary>Gets the backend display name.</summary>
    string Name { get; }

    /// <summary>Gets the device created by <see cref="Initialize"/>, or <c>null</c> before it.</summary>
    /// <remarks>The device also carries the window-host capability of the backend (C-06).</remarks>
    IRenderDevice? Device { get; }

    /// <summary>Initializes the backend exactly once and creates its device.</summary>
    /// <exception cref="InvalidOperationException">The backend was already initialized or is faulted.</exception>
    /// <exception cref="ObjectDisposedException">The backend was disposed.</exception>
    void Initialize();

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

    /// <summary>Submits a recording buffer; normal return transfers ownership.</summary>
    /// <exception cref="ArgumentException">The buffer is empty or foreign.</exception>
    /// <exception cref="InvalidOperationException">The buffer is not recording, or the backend is faulted.</exception>
    /// <exception cref="ObjectDisposedException">The backend was disposed.</exception>
    /// <remarks>Submitting an empty buffer is a caller contract violation, not a no-op.</remarks>
    void Submit(ICommandBuffer buffer);

    /// <summary>Executes every pending submission and presents the frame.</summary>
    /// <param name="context">Read-only snapshot of the frame being executed.</param>
    /// <exception cref="RenderDeviceException">A recorded command id has no executor in this assembly.</exception>
    /// <exception cref="InvalidOperationException">The backend is not initialized or is faulted.</exception>
    /// <exception cref="ObjectDisposedException">The backend was disposed.</exception>
    /// <remarks>
    /// Fixed order (single-threaded, render thread only): <c>MakeCurrent</c> → dispatch every pending
    /// command through the per-assembly table → <c>Present</c>. An empty queue still makes the context
    /// current and still presents.
    /// <br/>The snapshot belongs to the backend: it validates that a frame was published and uses
    /// the frame index in its diagnostics. Executors receive only the device through
    /// <c>CommandContext</c>, so the snapshot is either genuinely consumed or not carried at all.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// <see cref="FrameContext.FrameIndex"/> is zero (no frame published) in addition to the state checks.
    /// </exception>
    void ExecuteFrame(in FrameContext context);
}
