using System.Numerics;

namespace SilkEngine.Rendering.Abstraction;

/// <summary>
/// A writable lease over backend-owned command storage. It becomes permanently non-writable after
/// submission, return, execution, fault, or backend disposal.
/// </summary>
public interface ICommandBuffer
{
    /// <summary>Gets the command count while this buffer is writable.</summary>
    /// <exception cref="InvalidOperationException">The buffer is no longer writable, or the backend is faulted.</exception>
    /// <exception cref="ObjectDisposedException">The owning backend was disposed.</exception>
    int CommandsCount { get; }

    /// <summary>Records a clear-color operation.</summary>
    /// <param name="target">Render target to clear.</param>
    /// <param name="color">Color written to the target.</param>
    /// <exception cref="InvalidOperationException">The buffer is no longer writable, or the backend is faulted.</exception>
    /// <exception cref="ObjectDisposedException">The owning backend was disposed.</exception>
    void ClearColor(RenderTargetHandle target, Vector4 color);
}

