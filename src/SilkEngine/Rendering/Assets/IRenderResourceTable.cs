using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.Assets;

/// <summary>Bindings currently available to the render side; the pipeline never sees asset identities.</summary>
/// <remarks>Handles are issued on the render thread. The added/removed queues are safe to consume from another
/// thread so the scene can follow binding lifetimes.</remarks>
public interface IRenderResourceTable
{
    /// <summary>Gets the binding behind a live handle.</summary>
    /// <typeparam name="T">Binding type to retrieve.</typeparam>
    /// <param name="handle">Handle previously published through <see cref="TryTakeAdded"/>.</param>
    /// <param name="binding">The binding when the handle is live and has the requested type.</param>
    /// <returns><c>false</c> when the handle is stale or holds another binding type.</returns>
    bool TryGetBinding<T>(RenderHandle handle, out T binding)
        where T : class, IRuntimeBinding;

    /// <summary>True while the handle refers to a live binding; safe to call from any thread.</summary>
    bool IsValid(RenderHandle handle);

    /// <summary>Number of handles currently bound.</summary>
    int BoundCount { get; }

    /// <summary>Number of assets waiting for an upload attempt.</summary>
    int PendingCount { get; }

    /// <summary>Number of assets whose last upload attempt failed.</summary>
    int FailedCount { get; }

    /// <summary>Takes one newly bound handle; thread-safe.</summary>
    bool TryTakeAdded(out RenderHandle handle);

    /// <summary>Takes one retired handle; thread-safe.</summary>
    bool TryTakeRemoved(out RenderHandle handle);

    /// <summary>Appends every live binding of the requested type to a reusable list.</summary>
    /// <param name="destination">List that receives the bindings; it is not cleared.</param>
    /// <returns>Number of appended bindings.</returns>
    /// <remarks>Render thread only.</remarks>
    int CollectBindings<T>(List<T> destination)
        where T : class, IRuntimeBinding;
}
