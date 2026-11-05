using System.Runtime.InteropServices;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.BuiltIn;

//DEBUG_Only
internal static class DebugTriangle
{
    internal const uint StrideBytes = 2 * sizeof(float);
    internal const int VertexCount = 3;

    internal static ReadOnlySpan<float> Vertices => [-0.6f, -0.5f, 0.6f, -0.5f, 0.0f, 0.6f];

    // White shader with no constant block: the triangle must not inherit the textured pipeline state.
    private const string VertexShaderSource = """
        #version 330
        layout (location = 0) in vec2 aPosition;
        void main()
        {
            gl_Position = vec4(aPosition, 0.0, 1.0);
        }
        """;

    private const string FragmentShaderSource = """
        #version 330
        out vec4 FragColor;
        void main()
        {
            FragColor = vec4(1.0, 1.0, 1.0, 1.0);
        }
        """;

    internal readonly struct Resources
    {
        internal BufferHandle VertexBuffer { get; }
        internal PipelineStateHandle PipelineState { get; }
        internal ShaderHandle Shader { get; }

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

    internal static Resources Create(IRenderDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        var bytes = MemoryMarshal.AsBytes(Vertices);
        var vertexBuffer = device.CreateBuffer(new BufferDesc(bytes.Length, BufferUsage.Vertex));
        try
        {
            device.UpdateBuffer(vertexBuffer, bytes);

            var shader = device.CreateShader(new ShaderDesc(VertexShaderSource, FragmentShaderSource));
            try
            {
                var pipelineState = device.CreatePipelineState(
                    new PipelineStateDesc(
                        shader,
                        PrimitiveTopology.TriangleList,
                        CullMode.None,
                        depthTest: false,
                        depthWrite: false,
                        blend: BlendMode.None
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

    internal static void Record(ICommandBuffer commands, in Resources resources)
    {
        ArgumentNullException.ThrowIfNull(commands);

        var setPipeline = new SetPipelineStateCommand(resources.PipelineState);
        commands.Record(in setPipeline);

        var bind = new BindVertexBufferCommand(resources.VertexBuffer, StrideBytes);
        commands.Record(in bind);

        var draw = new DrawCommand(VertexCount, 0);
        commands.Record(in draw);
    }

    internal static void Destroy(IRenderDevice device, in Resources resources)
    {
        ArgumentNullException.ThrowIfNull(device);

        device.DestroyPipelineState(resources.PipelineState);
        device.DestroyShader(resources.Shader);
        device.DestroyBuffer(resources.VertexBuffer);
    }
}
