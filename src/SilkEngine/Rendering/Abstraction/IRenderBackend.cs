namespace SilkEngine.Rendering.Abstraction;

/// <summary>Owns the command-buffer queue, executes a frame on the device and creates that device.</summary>
public interface IRenderBackend : IDisposable
{
    /// <summary>Gets the backend display name.</summary>
    string Name { get; }

    /// <summary>Gets the device created by <see cref="Initialize"/>, or <c>null</c> before it.</summary>
    IRenderDevice? Device { get; }

    /// <summary>Initializes the backend and creates its device.</summary>
    void Initialize();

    /// <summary>Rents a writable command buffer.</summary>
    ICommandBuffer RentCommandBuffer();

    /// <summary>Returns a command buffer without submitting it.</summary>
    void RecycleCommandBuffer(ICommandBuffer buffer);

    /// <summary>Submits a recording command buffer.</summary>
    void Submit(ICommandBuffer buffer);

    /// <summary>Executes one frame on the device.</summary>
    void ExecuteFrame(in FrameContext context);
}
