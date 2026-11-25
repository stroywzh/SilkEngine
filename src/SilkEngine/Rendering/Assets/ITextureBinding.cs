using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.Assets;

/// <summary>Binding backed by a sampled texture: the device handle plus the source pixel size.</summary>
public interface ITextureBinding : IRuntimeBinding
{
    /// <summary>Device texture handle owned by this binding.</summary>
    TextureHandle Handle { get; }

    /// <summary>Source width in pixels; 1 for the shared fallback texture.</summary>
    int Width { get; }

    /// <summary>Source height in pixels; 1 for the shared fallback texture.</summary>
    int Height { get; }
}
