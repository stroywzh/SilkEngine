namespace SilkEngine.Rendering.Abstraction;

/// <summary>A render-thread pipeline that records and submits work without executing the backend.</summary>
/// <remarks>The host owns the backend and calls all lifecycle operations on its render thread.</remarks>
public interface IRenderPipeline : IDisposable
{
    /// <summary>Gets the pipeline display name.</summary>
    string Name { get; }

    /// <summary>Initializes once using an already initialized backend owned by the host.</summary>
    /// <exception cref="ArgumentNullException">The backend is null.</exception>
    /// <exception cref="InvalidOperationException">Initialization already began or failed.</exception>
    /// <exception cref="ObjectDisposedException">The pipeline was disposed.</exception>
    /// <remarks>Hook failures propagate; partial resources are cleaned once and initialization cannot be retried.</remarks>
    void Initialize(IRenderBackend backend);

    /// <summary>Updates a non-negative drawable size; a zero dimension skips the resize hook.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is negative.</exception>
    /// <exception cref="InvalidOperationException">The pipeline is unavailable or a callback is active.</exception>
    /// <exception cref="ObjectDisposedException">The pipeline was disposed.</exception>
    void Resize(int width, int height);

    /// <summary>Records and submits one frame; empty frames return their buffer without submitting.</summary>
    /// <exception cref="InvalidOperationException">The pipeline is unavailable or a callback is active.</exception>
    /// <exception cref="ObjectDisposedException">The pipeline was disposed.</exception>
    /// <remarks>Recording/backend failures propagate. Does not call ExecuteRender or wait for the GPU.
    /// Dispose is idempotent but must not be called from an active lifecycle hook.</remarks>
    void RenderFrame(RenderContext ctx);
}

