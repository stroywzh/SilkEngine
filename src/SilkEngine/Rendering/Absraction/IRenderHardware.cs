namespace SilkEngine.Rendering.Abstraction;

/// <summary>
/// A RHI for all rendering.
/// </summary>
public interface IRenderHardware
{
    // 帧管理
    void BeginFrame();
    void EndFrame();

    // RenderTarget
    void BindRenderTarget(RenderTargetHandle target);
    void UnbindRenderTarget();

    // 视口 / 裁剪
    void SetViewport(int x, int y, int w, int h);
    void SetScissor(int x, int y, int w, int h);

    // 清屏（语义操作，对当前绑定的 RT 生效）
    void ClearColor(Color color);
    void ClearDepth(float depth);

    // 资源绑定, 现在是占位
    void SetVertexBuffer();
    void SetIndexBuffer();
    void SetTexture(int slot, TextureHandle tex);

    // 绘制
    void DrawIndexed(int indexCount, int startIndex, int baseVertex);
    void Draw(int vertexCount, int startVertex);

    // 同步,占位
    void Barrier();
}
