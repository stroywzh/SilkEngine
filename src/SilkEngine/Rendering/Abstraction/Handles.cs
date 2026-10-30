namespace SilkEngine.Rendering.Abstraction;

// Typed rendering handles: they stay in the engine assembly because they appear in public
// cross-assembly signatures (IRenderDevice, ClearColorCommand, RenderItem users). They are
// device-owned GPU resource handles and are deliberately separate from SilkEngine.Asset.AssetId,
// which is the asset-table identity.

/// <summary>Opaque 32-bit mesh handle.</summary>
/// <remarks>Device-owned handle; valid only on its creating thread.</remarks>
public readonly record struct MeshHandle(uint Id);

/// <summary>Opaque 32-bit material handle.</summary>
/// <remarks>Device-owned GPU resource handle: valid only on the thread that created it.</remarks>
public readonly record struct MaterialHandle(uint Id);

/// <summary>Opaque 32-bit texture handle.</summary>
/// <remarks>Device-owned GPU resource handle: valid only on the thread that created it.</remarks>
public readonly record struct TextureHandle(uint Id);

/// <summary>Opaque 32-bit render-target handle; 0 is the prototype window target.</summary>
/// <remarks>Device-owned GPU resource handle: valid only on the thread that created it.</remarks>
public readonly record struct RenderTargetHandle(uint Id);

/// <summary>Opaque 32-bit shader handle.</summary>
/// <remarks>Device-owned GPU resource handle: valid only on the thread that created it.</remarks>
public readonly record struct ShaderHandle(uint Id);

/// <summary>Opaque 32-bit buffer handle (vertex, index or uniform buffer).</summary>
/// <remarks>Device-owned GPU resource handle: valid only on the thread that created it.</remarks>
public readonly record struct BufferHandle(uint Id);

/// <summary>Opaque 32-bit pipeline-state handle.</summary>
/// <remarks>Device-owned GPU resource object handle: valid only on the thread that created it.</remarks>
public readonly record struct PipelineStateHandle(uint Id);
