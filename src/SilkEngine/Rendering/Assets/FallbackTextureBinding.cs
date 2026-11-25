using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.Assets;

/// <summary>Shared 1x1 white texture drawn in place of a texture that failed to upload.</summary>
internal sealed class FallbackTextureBinding : ITextureBinding
{
    internal FallbackTextureBinding(TextureHandle handle) => Handle = handle;

    public TextureHandle Handle { get; }

    public int Width => 1;

    public int Height => 1;

    public Type BindingType => typeof(FallbackTextureBinding);

    public void Destroy(IRenderDevice device) => device.DestroyTexture(Handle);
}
