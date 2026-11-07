using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.Assets;

/// <summary>Device resource bound to one loaded asset; owned by the runtime cache and released on the render thread.</summary>
public interface IRuntimeBinding
{
    /// <summary>Destroys the device resource; called on the render thread when the asset is released.</summary>
    void Destroy(IRenderDevice device);
}
