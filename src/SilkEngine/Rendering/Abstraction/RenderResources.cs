namespace SilkEngine.Rendering.Abstraction;

// TODO: route these through AssetSystem handles as real resources.


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
public readonly struct ShaderDesc
{
    /// <summary>Creates a shader description.</summary>
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
/// Capabilities actually implemented by this device.
/// </summary>
public readonly struct RenderCapabilities
{
    /// <summary>Creates a capability set.</summary>
    public RenderCapabilities(
        bool buffers,
        bool textures,
        bool shaders,
        bool pipelineState,
        bool barrier = false,
        bool constantBuffers = false
    )
    {
        SupportsBuffers = buffers;
        SupportsTextures = textures;
        SupportsShaders = shaders;
        SupportsPipelineState = pipelineState;
        SupportsBarrier = barrier;
        SupportsConstantBuffers = constantBuffers;
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

    /// <summary>Whether uniform-buffer binding through <c>IRenderDeviceV2.BindConstantBuffer</c> is implemented.</summary>
    public bool SupportsConstantBuffers { get; }
}
