namespace SilkEngine.Rendering.Abstraction;

// Typed rendering handles (C-01/C-03): they stay in the engine assembly because they appear in
// public cross-assembly signatures (IRenderDevice, ClearColorCommand, RenderItem users), and only
// their namespace moves under Rendering.Abstraction. The generic resource wrapper stays in Handlers.cs.

/// <summary>Opaque 32-bit mesh handle.</summary>
public readonly record struct MeshHandle(uint Id);

/// <summary>Opaque 32-bit material handle.</summary>
public readonly record struct MaterialHandle(uint Id);

/// <summary>Opaque 32-bit texture handle.</summary>
public readonly record struct TextureHandle(uint Id);

/// <summary>Opaque 32-bit render-target handle; 0 is the prototype window target.</summary>
public readonly record struct RenderTargetHandle(uint Id);

/// <summary>Opaque 32-bit shader handle.</summary>
public readonly record struct ShaderHandle(uint Id);

/// <summary>Opaque 32-bit compute-shader handle.</summary>
/// <remarks>预留：未实现/未接线；计划批次见 proposal-04 D-A1（RHI 资源 API）。</remarks>
public readonly record struct ComputeShaderHandle(uint Id);

/// <summary>Opaque 32-bit buffer handle (vertex, index or uniform buffer).</summary>
public readonly record struct BufferHandle(uint Id);

/// <summary>Opaque 32-bit pipeline-state handle.</summary>
public readonly record struct PipelineStateHandle(uint Id);
