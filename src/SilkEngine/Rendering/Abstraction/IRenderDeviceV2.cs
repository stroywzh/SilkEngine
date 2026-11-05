namespace SilkEngine.Rendering.Abstraction;

/// <summary>
/// Rendering device with the RHI v2 capabilities; a backend may implement only the features it reports.
/// </summary>
/// <remarks>
/// A device that implements this interface also implements <see cref="IRenderDevice"/>; use
/// <see cref="IRenderDevice.Capabilities"/> to check which v2 feature is implemented before recording.
/// Every member follows the v1 thread contract: it may only be called from the render thread that
/// created the device.
/// </remarks>
public interface IRenderDeviceV2 : IRenderDevice
{
    /// <summary>Binds a byte range of a uniform buffer to a constant-binding point.</summary>
    /// <param name="binding">Constant-binding point used by the shader's uniform block.</param>
    /// <param name="buffer">Uniform buffer created with <see cref="BufferUsage.Uniform"/>.</param>
    /// <param name="offsetBytes">Byte offset of the range; must satisfy the device's uniform alignment.</param>
    /// <param name="sizeBytes">Size of the range in bytes.</param>
    /// <exception cref="ArgumentException">The buffer handle is unknown or not a uniform buffer.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The range is empty, exceeds the buffer, or the offset is misaligned.
    /// </exception>
    void BindConstantBuffer(ushort binding, BufferHandle buffer, uint offsetBytes, uint sizeBytes);
}
