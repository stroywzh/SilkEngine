using Silk.NET.Core.Contexts;
using Silk.NET.OpenGL;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.BuiltIn;

/// <summary>OpenGL RHI: the only place that talks to the GL device.</summary>
/// <remarks>
/// Prototype mapping: the logical target id is used directly as the GL framebuffer name, so the
/// prototype window target (0) is the default framebuffer. No mapping table exists yet.
/// <para>
/// There is no vertex-buffer API yet, so <see cref="Draw"/> renders one built-in debug triangle.
/// The geometry, shader and program are created lazily on the first draw and released by
/// <see cref="Dispose"/>, which the backend calls before tearing the device down.
/// </para>
/// </remarks>
internal sealed class OpenGLHardware : IRenderHardware
{
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

    private static readonly float[] DebugTriangleVertices =
    [
        -0.6f,
        -0.5f,
        0.6f,
        -0.5f,
        0.0f,
        0.6f,
    ];

    private readonly GL _gl;
    private readonly IGLContext _context;
    private uint _debugProgram;
    private uint _debugVertexArray;
    private uint _debugVertexBuffer;
    private int _debugVertexCount;
    private bool _disposed;

    internal OpenGLHardware(GL gl, IGLContext context)
    {
        _gl = gl;
        _context = context;
    }

    public void BeginFrame() => _context.MakeCurrent();

    public void EndFrame() => _context.SwapBuffers();

    public void BindRenderTarget(RenderTargetHandle target) =>
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, target.Id);

    public void UnbindRenderTarget() => _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

    public void SetViewport(int x, int y, int w, int h) => _gl.Viewport(x, y, (uint)w, (uint)h);

    public void SetScissor(int x, int y, int w, int h) => _gl.Scissor(x, y, (uint)w, (uint)h);

    public void ClearColor(Color color)
    {
        _gl.ClearColor(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
        _gl.Clear(ClearBufferMask.ColorBufferBit);
    }

    public void ClearDepth(float depth)
    {
        _gl.ClearDepth(depth);
        _gl.Clear(ClearBufferMask.DepthBufferBit);
    }

    public void SetVertexBuffer() => throw new NotImplementedException();

    public void SetIndexBuffer() => throw new NotImplementedException();

    public void SetTexture(int slot, TextureHandle tex) => throw new NotImplementedException();

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

    /// <summary>Draws the built-in debug triangle; the viewport decides where it lands.</summary>
    public void Draw(int vertexCount, int startVertex)
    {
        EnsureDebugGeometry();

        var available = _debugVertexCount - startVertex;
        if (available <= 0)
            return;

        var count = Math.Min(vertexCount, available);
        if (count <= 0)
            return;

        _gl.UseProgram(_debugProgram);
        _gl.BindVertexArray(_debugVertexArray);
        _gl.DrawArrays(PrimitiveType.Triangles, startVertex, (uint)count);
    }

    public void Barrier() => throw new NotImplementedException();

    /// <summary>Releases the device objects owned by this RHI; safe to call twice.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (_debugProgram != 0)
            _gl.DeleteProgram(_debugProgram);
        if (_debugVertexBuffer != 0)
            _gl.DeleteBuffer(_debugVertexBuffer);
        if (_debugVertexArray != 0)
            _gl.DeleteVertexArray(_debugVertexArray);

        _debugProgram = 0;
        _debugVertexBuffer = 0;
        _debugVertexArray = 0;
        _debugVertexCount = 0;
    }

    private unsafe void EnsureDebugGeometry()
    {
        if (_debugProgram != 0)
            return;

        _debugVertexArray = _gl.GenVertexArray();
        _gl.BindVertexArray(_debugVertexArray);

        _debugVertexBuffer = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _debugVertexBuffer);
        _gl.BufferData(
            BufferTargetARB.ArrayBuffer,
            DebugTriangleVertices.AsSpan(),
            BufferUsageARB.StaticDraw
        );
        _gl.VertexAttribPointer(
            0,
            2,
            VertexAttribPointerType.Float,
            false,
            2 * sizeof(float),
            (void*)0
        );
        _gl.EnableVertexAttribArray(0);

        var vertexShader = CompileShader(ShaderType.VertexShader, VertexShaderSource);
        var fragmentShader = CompileShader(ShaderType.FragmentShader, FragmentShaderSource);

        _debugProgram = _gl.CreateProgram();
        _gl.AttachShader(_debugProgram, vertexShader);
        _gl.AttachShader(_debugProgram, fragmentShader);
        _gl.LinkProgram(_debugProgram);

        _gl.GetProgram(_debugProgram, ProgramPropertyARB.LinkStatus, out var linked);
        if (linked == 0)
            throw new InvalidOperationException(
                $"Debug shader link failed: {_gl.GetProgramInfoLog(_debugProgram)}"
            );

        _gl.DetachShader(_debugProgram, vertexShader);
        _gl.DetachShader(_debugProgram, fragmentShader);
        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);

        _debugVertexCount = DebugTriangleVertices.Length / 2;
    }

    private uint CompileShader(ShaderType type, string source)
    {
        var shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);

        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out var status);
        if (status == 0)
            throw new InvalidOperationException(
                $"{type} compile failed: {_gl.GetShaderInfoLog(shader)}"
            );

        return shader;
    }
}
