namespace SilkEngine.Rendering;

/// <summary>Identifies the semantic operation stored in a recorded command payload.</summary>
public enum RenderCommandKind : byte
{
    // Pipeline and resource binding
    SetPipeline = 10,
    SetBindGroup = 11,
    SetPushConstants = 12,
    SetShaderKeyword = 13,
    SetConstantBuffer = 14,

    // Render passes
    BeginRenderPass = 20,
    EndRenderPass = 21,
    NextSubpass = 22,
    SetRenderTargets = 23, // for APIs without a RenderPass style

    // Viewport and scissor
    SetViewport = 30,
    SetScissorRect = 31,

    // Geometry binding
    BindVertexBuffer = 40,
    BindIndexBuffer = 41,
    BindUnorderedAccessView = 42,
    BindTexture = 43,

    // Draw (rasterization)
    Draw = 50,
    DrawIndexed = 51,
    DrawInstanced = 52,
    DrawIndexedInstanced = 53,
    DrawIndexedIndirect = 54,
    DrawIndexedIndirectCount = 55,

    // Compute
    Dispatch = 60,
    DispatchIndirect = 61,

    // Transfer and clear
    CopyBuffer = 70,
    CopyBufferToTexture = 71,
    CopyTextureToBuffer = 72,
    CopyTexture = 73,
    ClearColor = 74,
    ClearDepthStencil = 75,
    ClearUnorderedAccessView = 76,

    // Synchronization and barriers
    ResourceBarrier = 80,
    UAVBarrier = 81,
    GPUFence = 82,
    WaitOnGPUFence = 83,

}
