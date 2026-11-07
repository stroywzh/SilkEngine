using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.Assets;

/// <summary>Runtime binding of one uploaded texture: the texture asset and its device handle.</summary>
public sealed class TextureBinding : IRuntimeBinding
{
    /// <summary>The uploaded texture asset.</summary>
    public required Texture Texture { get; init; }

    /// <summary>Device texture handle owned by this binding.</summary>
    public required TextureHandle Handle { get; init; }

    /// <summary>Destroys the device texture; called on the render thread.</summary>
    public void Destroy(IRenderDevice device) => device.DestroyTexture(Handle);
}

