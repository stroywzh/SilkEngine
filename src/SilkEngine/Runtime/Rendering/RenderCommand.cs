namespace SilkEngine.Runtime.Rendering;

public interface IRenderCommand
{
    void Execute(RenderContext ctx);
}

public struct SetRenderTargetCommand : IRenderCommand
{
    //TODO: 后面做资源管理之后把这个Item换成Tex+Mesh
    public RenderTargetHandle Target;

    public SetRenderTargetCommand(RenderTargetHandle rt)
    {
        Target = rt;
    }

    public void Execute(RenderContext ctx)
    {
        throw new NotImplementedException();
    }
}

public struct DrawMeshCommand : IRenderCommand
{
    public MeshHandle Mesh;
    public Matrix4x4 Matrix;
    public MaterialHandle Material;

    public DrawMeshCommand(MeshHandle m,in Matrix4x4 mat, MaterialHandle matl)
    {
        Mesh = m;
        Matrix = mat;
        Material = matl;
    }

    public void Execute(RenderContext ctx)
    {
        throw new NotImplementedException();
    }
}
