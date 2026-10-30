namespace SilkEngine.Rendering.Abstraction;

/// <summary>The rendering device (RHI): owns the context, presents frames and executes recorded commands.</summary>
public interface IRenderDevice : IRenderWindowHost, IDisposable
{
    /// <summary>Makes this device's context current on the calling thread.</summary>
    void MakeCurrent();

    /// <summary>Presents the frame just executed (swap buffers).</summary>
    void Present();

    /// <summary>Blocks until the device has finished its queued work (best effort).</summary>
    void WaitIdle();

    /// <summary>Gets the capabilities actually implemented by this device.</summary>
    RenderCapabilities Capabilities { get; }

    // RenderTarget
    void BindRenderTarget(RenderTargetHandle target);

    void UnbindRenderTarget();

    // Viewport / scissor
    /// <summary>Sets the viewport rectangle in pixels.</summary>
    void SetViewport(int x, int y, int width, int height);

    void SetScissor(int x, int y, int w, int h);

    // Clear (semantic operation on the currently bound RT)
    void ClearColor(Color color);

    void ClearDepth(float depth);

    // Resources: buffer / texture / shader / single PSO
    /// <summary>Creates a buffer and returns its handle.</summary>
    BufferHandle CreateBuffer(in BufferDesc desc);

    /// <summary>Uploads bytes into an existing buffer.</summary>
    void UpdateBuffer(BufferHandle buffer, ReadOnlySpan<byte> data, int offsetBytes = 0);

    /// <summary>Releases a buffer; unknown handles are ignored.</summary>
    void DestroyBuffer(BufferHandle buffer);

    /// <summary>Binds a vertex buffer with the given stride.</summary>
    void BindVertexBuffer(BufferHandle buffer, uint stride);

    /// <summary>Binds an index buffer and remembers its element format.</summary>
    void BindIndexBuffer(BufferHandle buffer, IndexFormat format);

    /// <summary>Creates a 2D RGBA8 texture from 8-bit pixels.</summary>
    TextureHandle CreateTexture(int width, int height, ReadOnlySpan<byte> rgbaPixels);

    /// <summary>Binds a texture to a texture slot.</summary>
    void BindTexture(uint slot, TextureHandle texture);

    /// <summary>Compiles and links a shader program.</summary>
    /// <exception cref="RenderDeviceException">Compilation or linking failed.</exception>
    ShaderHandle CreateShader(in ShaderDesc desc);

    /// <summary>Creates an immutable pipeline state over an existing shader.</summary>
    PipelineStateHandle CreatePipelineState(in PipelineStateDesc desc);

    /// <summary>Applies a pipeline state (program, culling, depth, blending, topology).</summary>
    void SetPipelineState(PipelineStateHandle pipelineState);

    /// <summary>Releases a texture; unknown handles are ignored.</summary>
    void DestroyTexture(TextureHandle texture);

    /// <summary>Releases a shader; unknown handles are ignored.</summary>
    void DestroyShader(ShaderHandle shader);

    /// <summary>Releases a pipeline state; unknown handles are ignored.</summary>
    void DestroyPipelineState(PipelineStateHandle pipelineState);

    // Draw
    void DrawIndexed(int indexCount, int startIndex, int baseVertex);

    void Draw(int vertexCount, int startVertex);

    void Barrier();
}
