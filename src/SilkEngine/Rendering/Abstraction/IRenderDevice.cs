namespace SilkEngine.Rendering.Abstraction;

/// <summary>
/// The rendering device (RHI): owns the graphics context, presents frames, and executes the
/// uploaded command stream on the caller's render thread.
/// </summary>
/// <remarks>
/// <para>
/// It owns the context and presents, and it is not a hardware abstraction layer. 
/// It also carries the window-host capability so the window/context/present owner is a single object;
/// </para>
/// <para>
/// Lifecycle per frame: <see cref="MakeCurrent"/> -&gt; execute commands -&gt; <see cref="Present"/>. All
/// members must be called from the render thread that created the device; there is no locking.
/// </para>
/// </remarks>
public interface IRenderDevice : IRenderWindowHost, IDisposable
{
    /// <summary>Makes this device's context current on the calling thread.</summary>
    void MakeCurrent();

    /// <summary>Presents the frame just executed (swap buffers).</summary>
    /// <remarks>Called exactly once per executed frame, including frames with an empty command queue.</remarks>
    void Present();

    /// <summary>Blocks until the device has finished its queued work (best effort).</summary>
    void WaitIdle();

    /// <summary>Gets the capabilities actually implemented by this device.</summary>
    /// <remarks>
    /// Operations outside the set throw <see cref="System.NotSupportedException"/>.
    /// </remarks>
    RenderCapabilities Capabilities { get; }

    // RenderTarget
    void BindRenderTarget(RenderTargetHandle target);

    void UnbindRenderTarget();

    // 视口 / 裁剪
    /// <summary>Sets the viewport rectangle in pixels.</summary>
    /// <param name="x">Left edge.</param>
    /// <param name="y">Bottom edge.</param>
    /// <param name="width">Viewport width in pixels.</param>
    /// <param name="height">Viewport height in pixels.</param>
    void SetViewport(int x, int y, int width, int height);

    void SetScissor(int x, int y, int w, int h);

    // 清屏（语义操作，对当前绑定的 RT 生效）
    void ClearColor(Color color);

    void ClearDepth(float depth);

    // 资源：buffer / texture / shader / 单 PSO
    /// <summary>Creates a buffer and returns its handle.</summary>
    /// <param name="desc">Allocation parameters.</param>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="BufferDesc.SizeBytes"/> is not positive.</exception>
    /// <exception cref="NotSupportedException">The device does not support buffers.</exception>
    BufferHandle CreateBuffer(in BufferDesc desc);

    /// <summary>Uploads bytes into an existing buffer.</summary>
    /// <param name="buffer">Target buffer.</param>
    /// <param name="data">Bytes to copy.</param>
    /// <param name="offsetBytes">Byte offset inside the buffer.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="offsetBytes"/> is negative, or <paramref name="data"/> does not fit into the
    /// allocation starting at that offset (<c>offsetBytes + data.Length &gt; SizeBytes</c>).
    /// </exception>
    /// <exception cref="ArgumentException">The handle is unknown.</exception>
    void UpdateBuffer(BufferHandle buffer, ReadOnlySpan<byte> data, int offsetBytes = 0);

    /// <summary>Releases a buffer; unknown handles are ignored.</summary>
    /// <param name="buffer">Buffer to release.</param>
    void DestroyBuffer(BufferHandle buffer);

    /// <summary>Binds a vertex buffer with the given stride.</summary>
    /// <param name="buffer">Vertex buffer.</param>
    /// <param name="stride">Bytes between consecutive vertices.</param>
    /// <remarks>
    /// Prototype convention: attribute location 0 is the position, two floats at offset 0, matching
    /// the built-in shader of the prototype pipeline.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="stride"/> is zero.</exception>
    void BindVertexBuffer(BufferHandle buffer, uint stride);

    /// <summary>Binds an index buffer and remembers its element format.</summary>
    /// <param name="buffer">Index buffer.</param>
    /// <param name="format">Element format used by later indexed draws.</param>
    void BindIndexBuffer(BufferHandle buffer, IndexFormat format);

    /// <summary>Creates a 2D RGBA8 texture from 8-bit pixels.</summary>
    /// <param name="width">Texture width in pixels.</param>
    /// <param name="height">Texture height in pixels.</param>
    /// <param name="rgbaPixels">Row-major RGBA pixels; length must be width × height × 4.</param>
    /// <exception cref="ArgumentException">The pixel count does not match the size.</exception>
    /// <exception cref="NotSupportedException">The device does not support textures.</exception>
    TextureHandle CreateTexture(int width, int height, ReadOnlySpan<byte> rgbaPixels);

    /// <summary>Binds a texture to a texture slot.</summary>
    /// <param name="slot">Zero-based texture slot.</param>
    /// <param name="texture">Texture to bind.</param>
    void BindTexture(uint slot, TextureHandle texture);

    /// <summary>Compiles and links a shader program.</summary>
    /// <param name="desc">Shader sources.</param>
    /// <exception cref="ArgumentException">A source is empty.</exception>
    /// <exception cref="RenderDeviceException">Compilation or linking failed.</exception>
    /// <exception cref="NotSupportedException">The device does not support shaders.</exception>
    ShaderHandle CreateShader(in ShaderDesc desc);

    /// <summary>Creates an immutable pipeline state over an existing shader.</summary>
    /// <param name="desc">Pipeline-state parameters.</param>
    /// <exception cref="ArgumentException">The shader handle is unknown.</exception>
    /// <exception cref="NotSupportedException">The device does not support pipeline states.</exception>
    PipelineStateHandle CreatePipelineState(in PipelineStateDesc desc);

    /// <summary>Applies a pipeline state (program, culling, depth, blending, topology).</summary>
    /// <param name="pipelineState">Pipeline state to apply.</param>
    /// <exception cref="ArgumentException">The handle is unknown.</exception>
    void SetPipelineState(PipelineStateHandle pipelineState);

    /// <summary>Releases a texture; unknown handles are ignored.</summary>
    /// <param name="texture">Texture to release.</param>
    /// <remarks>Completes the resource lifetime (REN-N-08): without it a texture could only be released
    /// by disposing the whole device.</remarks>
    void DestroyTexture(TextureHandle texture);

    /// <summary>Releases a linked shader program; unknown handles are ignored.</summary>
    /// <param name="shader">Shader to release.</param>
    /// <remarks>
    /// Pipeline states created over the shader become invalid and are dropped as well, so a later
    /// <see cref="SetPipelineState"/> reports an unknown handle instead of switching to a deleted program.
    /// </remarks>
    void DestroyShader(ShaderHandle shader);

    /// <summary>Releases a pipeline state; unknown handles are ignored.</summary>
    /// <param name="pipelineState">Pipeline state to release.</param>
    void DestroyPipelineState(PipelineStateHandle pipelineState);

    // 绘制
    void DrawIndexed(int indexCount, int startIndex, int baseVertex);

    void Draw(int vertexCount, int startVertex);

    void Barrier();
}
