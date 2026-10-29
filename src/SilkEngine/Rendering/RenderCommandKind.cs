namespace SilkEngine.Rendering;

/// <summary>Identifies the semantic operation stored in a recorded command payload.</summary>
public enum RenderCommandKind : byte
{
    // —— 管线与资源绑定 ——
    SetPipeline = 10,
    SetBindGroup = 11,
    SetPushConstants = 12,
    SetShaderKeyword = 13,

    // —— 渲染通道 ——
    BeginRenderPass = 20,
    EndRenderPass = 21,
    NextSubpass = 22,
    SetRenderTargets = 23, // 用于非 RenderPass 风格的 API

    // —— 视口与裁剪 ——
    SetViewport = 30,
    SetScissorRect = 31,

    // —— 几何绑定 ——
    BindVertexBuffer = 40,
    BindIndexBuffer = 41,
    BindUnorderedAccessView = 42,
    BindTexture = 43,

    // —— 绘制（光栅化）——
    Draw = 50,
    DrawIndexed = 51,
    DrawInstanced = 52,
    DrawIndexedInstanced = 53,
    DrawIndexedIndirect = 54,
    DrawIndexedIndirectCount = 55,

    // —— 计算 ——
    Dispatch = 60,
    DispatchIndirect = 61,

    // —— 传输与清除 ——
    CopyBuffer = 70,
    CopyBufferToTexture = 71,
    CopyTextureToBuffer = 72,
    CopyTexture = 73,
    ClearColor = 74,
    ClearDepthStencil = 75,
    ClearUnorderedAccessView = 76,

    // —— 同步与屏障 ——
    ResourceBarrier = 80,
    UAVBarrier = 81,
    GPUFence = 82,
    WaitOnGPUFence = 83,

}
