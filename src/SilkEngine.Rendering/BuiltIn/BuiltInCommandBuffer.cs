using SilkEngine.Rendering;

namespace SilkEngine.Rendering.Pipeline;

public class BuiltInCommandBuffer : CommandBufferBase
{
    public virtual void SetRenderTarget(in RenderTargetHandle target) =>
        AddCommand(new SetRenderTargetCommand(target));

    public virtual void DrawMesh(MeshHandle m, in Matrix4x4 mat, MaterialHandle matl) =>
        AddCommand(new DrawMeshCommand(m, mat, matl));
}
