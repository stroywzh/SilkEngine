namespace SilkEngine.Rendering.Abstraction;

// Minimum viable resource set : buffers, textures, shaders and one pipeline
// state. Samplers, MRT, compute and indirect remain out of scope.
// TODO : 把这些东西作为实际资源，通过AssetSystem传递句柄


/// <summary>Intended usage of a buffer allocation.</summary>
public enum BufferUsage : byte
{
    /// <summary>Vertex data bound through <c>BindVertexBuffer</c>.</summary>
    Vertex = 0,

    /// <summary>Index data bound through <c>BindIndexBuffer</c>.</summary>
    Index = 1,

    /// <summary>Uniform (constant) data.</summary>
    Uniform = 2,
}

/// <summary>Element format of an index buffer.</summary>
public enum IndexFormat : byte
{
    /// <summary>16-bit unsigned indices.</summary>
    UInt16 = 0,

    /// <summary>32-bit unsigned indices.</summary>
    UInt32 = 1,
}

/// <summary>Primitive topology of a pipeline state.</summary>
public enum PrimitiveTopology : byte
{
    /// <summary>Independent triangles.</summary>
    TriangleList = 0,

    /// <summary>Independent lines.</summary>
    LineList = 1,
}

/// <summary>Face-culling mode of a pipeline state.</summary>
public enum CullMode : byte
{
    /// <summary>Draw both faces.</summary>
    None = 0,

    /// <summary>Cull back faces.</summary>
    Back = 1,

    /// <summary>Cull front faces.</summary>
    Front = 2,
}

/// <summary>Colour blending mode of a pipeline state.</summary>
public enum BlendMode : byte
{
    /// <summary>Blending disabled.</summary>
    None = 0,

    /// <summary>Standard alpha blending.</summary>
    Alpha = 1,

    /// <summary>Additive blending.</summary>
    Additive = 2,
}

/// <summary>Allocation parameters of <see cref="IRenderDevice.CreateBuffer"/>.</summary>
public readonly struct BufferDesc
{
    /// <summary>Creates a buffer description.</summary>
    /// <param name="sizeBytes">Allocation size in bytes; must be positive.</param>
    /// <param name="usage">Intended usage, which selects the binding target.</param>
    public BufferDesc(int sizeBytes, BufferUsage usage = BufferUsage.Vertex)
    {
        SizeBytes = sizeBytes;
        Usage = usage;
    }

    /// <summary>Allocation size in bytes.</summary>
    public int SizeBytes { get; }

    /// <summary>Intended usage of the buffer.</summary>
    public BufferUsage Usage { get; }
}

/// <summary>Creation parameters of <see cref="IRenderDevice.CreateShader"/>.</summary>
/// <remarks>
/// Prototype scope: both stages are GLSL 330 sources, because the built-in backend is the only
/// implementation. A cross-backend shader language stays out of scope (P3+).
/// </remarks>
public readonly struct ShaderDesc
{
    /// <summary>Creates a shader description.</summary>
    /// <param name="vertexSource">Vertex-stage source.</param>
    /// <param name="fragmentSource">Fragment-stage source.</param>
    public ShaderDesc(string vertexSource, string fragmentSource)
    {
        VertexSource = vertexSource;
        FragmentSource = fragmentSource;
    }

    /// <summary>Vertex-stage source.</summary>
    public string VertexSource { get; }

    /// <summary>Fragment-stage source.</summary>
    public string FragmentSource { get; }
}

/// <summary>Creation parameters of <see cref="IRenderDevice.CreatePipelineState"/>.</summary>
public readonly struct PipelineStateDesc
{
    /// <summary>Creates a pipeline-state description.</summary>
    /// <param name="shader">Linked shader program to use.</param>
    /// <param name="topology">Primitive topology.</param>
    /// <param name="cull">Face-culling mode.</param>
    /// <param name="depthTest">Whether depth testing is enabled.</param>
    /// <param name="depthWrite">Whether depth writes are enabled.</param>
    /// <param name="blend">Colour blending mode.</param>
    public PipelineStateDesc(
        ShaderHandle shader,
        PrimitiveTopology topology = PrimitiveTopology.TriangleList,
        CullMode cull = CullMode.None,
        bool depthTest = false,
        bool depthWrite = false,
        BlendMode blend = BlendMode.None
    )
    {
        Shader = shader;
        Topology = topology;
        Cull = cull;
        DepthTest = depthTest;
        DepthWrite = depthWrite;
        Blend = blend;
    }

    /// <summary>Linked shader program to use.</summary>
    public ShaderHandle Shader { get; }

    /// <summary>Primitive topology.</summary>
    public PrimitiveTopology Topology { get; }

    /// <summary>Face-culling mode.</summary>
    public CullMode Cull { get; }

    /// <summary>Whether depth testing is enabled.</summary>
    public bool DepthTest { get; }

    /// <summary>Whether depth writes are enabled.</summary>
    public bool DepthWrite { get; }

    /// <summary>Colour blending mode.</summary>
    public BlendMode Blend { get; }
}

/// <summary>
/// What the device can actually do. Resource operations outside this set will throw <see cref="System.NotSupportedException"/>.
/// </summary>
public readonly struct RenderCapabilities
{
    /// <summary>Creates a capability set.</summary>
    /// <param name="buffers">Whether buffer creation/binding is implemented.</param>
    /// <param name="textures">Whether texture creation/binding is implemented.</param>
    /// <param name="shaders">Whether shader compilation is implemented.</param>
    /// <param name="pipelineState">Whether pipeline-state objects are implemented.</param>
    /// <param name="barrier">Whether <see cref="IRenderDevice.Barrier"/> is implemented.</param>
    public RenderCapabilities(
        bool buffers,
        bool textures,
        bool shaders,
        bool pipelineState,
        bool barrier = false
    )
    {
        SupportsBuffers = buffers;
        SupportsTextures = textures;
        SupportsShaders = shaders;
        SupportsPipelineState = pipelineState;
        SupportsBarrier = barrier;
    }

    /// <summary>Whether buffer creation/binding is implemented.</summary>
    public bool SupportsBuffers { get; }

    /// <summary>Whether texture creation/binding is implemented.</summary>
    public bool SupportsTextures { get; }

    /// <summary>Whether shader compilation is implemented.</summary>
    public bool SupportsShaders { get; }

    /// <summary>Whether pipeline-state objects are implemented.</summary>
    public bool SupportsPipelineState { get; }

    /// <summary>Whether <see cref="IRenderDevice.Barrier"/> is implemented (not implemented by GL).</summary>
    public bool SupportsBarrier { get; }
}
