using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.BuiltIn;

/// <summary>
/// Textured quad drawn through the recorded command stream; the per-draw transform, tint and alpha
/// come from a uniform block.
/// </summary>
public static class TexturedQuad
{
    /// <summary>Bytes between consecutive vertices (two floats).</summary>
    public const uint StrideBytes = 2 * sizeof(float);

    /// <summary>Number of vertices in the quad (two triangles).</summary>
    public const int VertexCount = 6;

    /// <summary>Texture slot the fragment stage samples.</summary>
    public const uint TextureSlot = 0;

    /// <summary>Vertex positions in normalized device coordinates, two floats per vertex.</summary>
    public static ReadOnlySpan<float> Vertices =>
        [
            -1f, -1f,
             1f, -1f,
             1f,  1f,
            -1f, -1f,
             1f,  1f,
            -1f,  1f,
        ];

    private const string VertexShaderSource = """
        #version 330
        layout (location = 0) in vec2 aPosition;
        layout(std140) uniform DrawConstants
        {
            vec4 uTransform;
            vec4 uTint;
        };
        out vec2 vUV;
        void main()
        {
            vUV = aPosition * 0.5 + 0.5;
            gl_Position = vec4(aPosition * uTransform.xy + uTransform.zw, 0.0, 1.0);
        }
        """;

    private const string FragmentShaderSource = """
        #version 330
        in vec2 vUV;
        uniform sampler2D uTexture;
        layout(std140) uniform DrawConstants
        {
            vec4 uTransform;
            vec4 uTint;
        };
        out vec4 FragColor;
        void main()
        {
            vec2 uv = vUV;
            uv.y = 1.0 - uv.y;
            FragColor = texture(uTexture, uv) * uTint;
        }
        """;

    private const string LegacyVertexShaderSource = """
        #version 330
        layout (location = 0) in vec2 aPosition;
        out vec2 vUV;
        void main()
        {
            vUV = aPosition * 0.5 + 0.5;
            gl_Position = vec4(aPosition, 0.0, 1.0);
        }
        """;

    private const string LegacyFragmentShaderSource = """
        #version 330
        in vec2 vUV;
        uniform sampler2D uTexture;
        out vec4 FragColor;
        void main()
        {
            vec2 uv = vUV;
            uv.y = 1.0 - uv.y;
            FragColor = texture(uTexture, uv);
        }
        """;

    /// <summary>GPU resources of the quad; release them with <see cref="Destroy"/>.</summary>
    public readonly struct Resources
    {
        /// <summary>Vertex buffer holding <see cref="Vertices"/>.</summary>
        public BufferHandle VertexBuffer { get; }

        /// <summary>Pipeline state of the textured shader.</summary>
        public PipelineStateHandle PipelineState { get; }

        /// <summary>Linked shader program the pipeline state was created over.</summary>
        public ShaderHandle Shader { get; }

        internal Resources(
            BufferHandle vertexBuffer,
            PipelineStateHandle pipelineState,
            ShaderHandle shader
        )
        {
            VertexBuffer = vertexBuffer;
            PipelineState = pipelineState;
            Shader = shader;
        }
    }

    /// <summary>Creates the quad resources with per-draw constants and alpha blending.</summary>
    /// <param name="device">Device that owns the resources.</param>
    /// <returns>Handles to release with <see cref="Destroy"/>.</returns>
    /// <exception cref="RenderDeviceException">
    /// The CPU constant layout does not match the std140 block, or shader compilation/linking failed.
    /// </exception>
    public static Resources Create(IRenderDevice device)
    {
        if (Unsafe.SizeOf<DrawConstants2D>() != DrawConstants2D.SizeBytes)
        {
            throw new RenderDeviceException(
                $"DrawConstants2D is {Unsafe.SizeOf<DrawConstants2D>()} B; expected {DrawConstants2D.SizeBytes} B."
            );
        }

        return CreateResources(
            device,
            VertexShaderSource,
            FragmentShaderSource,
            BlendMode.Alpha
        );
    }

    /// <summary>Creates the quad resources without constants, for devices that only implement RHI v1.</summary>
    /// <param name="device">Device that owns the resources.</param>
    /// <returns>Handles to release with <see cref="Destroy"/>.</returns>
    /// <exception cref="RenderDeviceException">Shader compilation or linking failed.</exception>
    public static Resources CreateLegacy(IRenderDevice device) =>
        CreateResources(device, LegacyVertexShaderSource, LegacyFragmentShaderSource, BlendMode.None);

    /// <summary>Binds the quad's pipeline state and vertex buffer for a batch of draws.</summary>
    /// <param name="commands">Writable command buffer of the current frame.</param>
    /// <param name="resources">Resources returned by <see cref="Create"/> or <see cref="CreateLegacy"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="commands"/> is null.</exception>
    public static void BindState(ICommandBuffer commands, in Resources resources)
    {
        ArgumentNullException.ThrowIfNull(commands);

        var setPipeline = new SetPipelineStateCommand(resources.PipelineState);
        commands.Record(in setPipeline);

        var bindVertexBuffer = new BindVertexBufferCommand(resources.VertexBuffer, StrideBytes);
        commands.Record(in bindVertexBuffer);
    }

    /// <summary>Records one quad draw using a per-draw constant range bound by the caller.</summary>
    /// <param name="commands">Writable command buffer of the current frame.</param>
    /// <param name="texture">Texture sampled by the fragment stage.</param>
    /// <param name="constantBuffer">Uniform buffer that owns the draw-constant range.</param>
    /// <param name="constantOffset">Byte offset of the 32 B constant range.</param>
    /// <exception cref="ArgumentNullException"><paramref name="commands"/> is null.</exception>
    /// <remarks>Call <see cref="BindState"/> once before the first draw of a batch.</remarks>
    public static void RecordDraw(
        ICommandBuffer commands,
        TextureHandle texture,
        BufferHandle constantBuffer,
        uint constantOffset
    )
    {
        ArgumentNullException.ThrowIfNull(commands);

        var setConstants = new SetConstantBufferCommand(
            constantBuffer,
            constantOffset,
            DrawConstants2D.SizeBytes,
            ShaderConstantBindings.Draw
        );
        commands.Record(in setConstants);

        var bindTexture = new BindTextureCommand(texture, TextureSlot);
        commands.Record(in bindTexture);

        var draw = new DrawCommand(VertexCount, 0);
        commands.Record(in draw);
    }

    /// <summary>Records the full sequence for one quad draw.</summary>
    /// <param name="commands">Writable command buffer of the current frame.</param>
    /// <param name="resources">Resources returned by <see cref="Create"/>.</param>
    /// <param name="texture">Texture sampled by the fragment stage.</param>
    /// <param name="constantBuffer">Uniform buffer that owns the draw-constant range.</param>
    /// <param name="constantOffset">Byte offset of the 32 B constant range.</param>
    /// <exception cref="ArgumentNullException"><paramref name="commands"/> is null.</exception>
    public static void Record(
        ICommandBuffer commands,
        in Resources resources,
        TextureHandle texture,
        BufferHandle constantBuffer,
        uint constantOffset
    )
    {
        BindState(commands, in resources);
        RecordDraw(commands, texture, constantBuffer, constantOffset);
    }

    /// <summary>Records one full-screen draw with the constant-free legacy shader.</summary>
    /// <param name="commands">Writable command buffer of the current frame.</param>
    /// <param name="resources">Resources returned by <see cref="CreateLegacy"/>.</param>
    /// <param name="texture">Texture sampled by the fragment stage.</param>
    /// <exception cref="ArgumentNullException"><paramref name="commands"/> is null.</exception>
    public static void RecordLegacy(
        ICommandBuffer commands,
        in Resources resources,
        TextureHandle texture
    )
    {
        BindState(commands, in resources);

        var bindTexture = new BindTextureCommand(texture, TextureSlot);
        commands.Record(in bindTexture);

        var draw = new DrawCommand(VertexCount, 0);
        commands.Record(in draw);
    }

    /// <summary>Releases the quad resources.</summary>
    /// <param name="device">Device that owns the resources.</param>
    /// <param name="resources">Resources to release; unknown handles are ignored by the device.</param>
    /// <exception cref="ArgumentNullException"><paramref name="device"/> is null.</exception>
    public static void Destroy(IRenderDevice device, in Resources resources)
    {
        ArgumentNullException.ThrowIfNull(device);

        device.DestroyPipelineState(resources.PipelineState);
        device.DestroyShader(resources.Shader);
        device.DestroyBuffer(resources.VertexBuffer);
    }

    private static Resources CreateResources(
        IRenderDevice device,
        string vertexSource,
        string fragmentSource,
        BlendMode blend
    )
    {
        ArgumentNullException.ThrowIfNull(device);

        var vertices = MemoryMarshal.AsBytes(Vertices);
        var vertexBuffer = device.CreateBuffer(new BufferDesc(vertices.Length, BufferUsage.Vertex));
        try
        {
            device.UpdateBuffer(vertexBuffer, vertices);

            var shader = device.CreateShader(new ShaderDesc(vertexSource, fragmentSource));
            try
            {
                var pipelineState = device.CreatePipelineState(
                    new PipelineStateDesc(
                        shader,
                        PrimitiveTopology.TriangleList,
                        CullMode.None,
                        depthTest: false,
                        depthWrite: false,
                        blend: blend
                    )
                );
                return new Resources(vertexBuffer, pipelineState, shader);
            }
            catch
            {
                device.DestroyShader(shader);
                throw;
            }
        }
        catch
        {
            device.DestroyBuffer(vertexBuffer);
            throw;
        }
    }
}
