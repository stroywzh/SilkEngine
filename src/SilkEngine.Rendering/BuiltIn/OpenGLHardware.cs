using Silk.NET.OpenGL;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.BuiltIn;

class OpenGLHardware : IRenderHardware
{
    private readonly GL _gl;

    public OpenGLHardware(GL gl)
    {
        _gl = gl;
    }

    public void BeginFrame() { }

    public void EndFrame() { }

    public void BindRenderTarget(RenderTargetHandle target) =>
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, target.Id);

    public void ClearColor(Color color)
    {
        _gl.ClearColor(color.ToSystemColor());
        _gl.Clear(ClearBufferMask.ColorBufferBit);
    }

    public void ClearDepth(float depth)
    {
        _gl.ClearDepth(depth);
        _gl.Clear(ClearBufferMask.DepthBufferBit);
    }

    public void Draw(int vertexCount, int startVertex) =>
        _gl.DrawArrays(PrimitiveType.Triangles, startVertex, (uint)vertexCount);

    public void DrawIndexed(int indexCount, int startIndex, int baseVertex)
    {
        var offset = (nint)(startIndex * sizeof(uint));
        _gl.DrawElementsBaseVertex(
            PrimitiveType.Triangles,
            (uint)indexCount,
            DrawElementsType.UnsignedInt,
            in offset,
            baseVertex
        );
    }

    public void SetIndexBuffer()
    {
        throw new NotImplementedException();
    }

    public void SetScissor(int x, int y, int w, int h) => _gl.Scissor(x, y, (uint)w, (uint)h);

    public void SetTexture(int slot, TextureHandle tex)
    {
        throw new NotImplementedException();
    }

    public void SetVertexBuffer()
    {
        throw new NotImplementedException();
    }

    public void SetViewport(int x, int y, int w, int h) => _gl.Viewport(x, y, (uint)w, (uint)h);

    public void UnbindRenderTarget()
    {
        throw new NotImplementedException();
    }

    public void Barrier()
    {
        throw new NotImplementedException();
    }
}
