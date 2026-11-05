using Silk.NET.Core.Contexts;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.BuiltIn;

/// <summary>GL command implementation: the only place that talks to the GL API for draw/state/resource work.</summary>
/// <remarks>
/// <para>
/// Context, presentation and window ownership live in <see cref="OpenGLDevice"/>; this type keeps
/// only the GL objects it creates and receives the device instead of a raw context, so every GL call
/// stays on the device's render thread.
/// </para>
/// <para>
/// Resource scope is the minimum viable set: buffers, 2D RGBA8 textures, linked
/// shader programs and one pipeline state. Samplers, MRT, compute and indirect are out of scope.
/// Capabilities that are not implemented throw <see cref="NotSupportedException"/> (G7).
/// </para>
/// </remarks>
internal sealed unsafe class OpenGLDevice : IRenderDeviceV2
{
    // GL enums used for uniform-buffer binding; kept as literals so the device does not depend on
    // optional Silk.NET enum members.
    private const int GlUniformBufferOffsetAlignment = 0x8A34;
    private const int GlUniformBlockDataSize = 0x8A40;

    // Uniform block name shared with the built-in textured shader; its std140 size is fixed at 32 B.
    private const string DrawConstantsBlockName = "DrawConstants";
    private const int DrawConstantsBlockSize = 32;

    // Prototype shader: used when a draw happens without a pipeline state (REV-051). The prototype
    // geometry itself lives in BuiltIn/DebugTriangle.cs and is bound by the pipeline.
    private const string PrototypeVertexShaderSource = """
        #version 330
        layout (location = 0) in vec2 aPosition;
        void main()
        {
            gl_Position = vec4(aPosition, 0.0, 1.0);
        }
        """;

    private const string PrototypeFragmentShaderSource = """
        #version 330
        out vec4 FragColor;
        void main()
        {
            FragColor = vec4(1.0, 1.0, 1.0, 1.0);
        }
        """;

    /// <summary>Allocation record of one buffer: what it is used for and how large it is.</summary>
    private readonly record struct BufferRecord(BufferUsage Usage, int SizeBytes);

    private readonly GL _gl;
    private readonly Dictionary<uint, BufferRecord> _buffers = new();
    private readonly HashSet<uint> _textures = new();
    private readonly HashSet<uint> _shaders = new();
    private readonly Dictionary<uint, PipelineStateDesc> _pipelines = new();
    private uint _nextPipelineId = 1;

    private readonly int _uniformBufferOffsetAlignment;

    private uint _userVertexArray;
    private uint _boundVertexBuffer;
    private IndexFormat _indexFormat = IndexFormat.UInt32;
    private PrimitiveType _topology = PrimitiveType.Triangles;

    private uint _prototypeProgram;
    private bool _hasPipelineState;
    private bool _disposed;

    private readonly IWindow _window;
    private readonly IGLContext _context;

    /// <summary>Creates the device around an initialized window and its GL API.</summary>
    /// <param name="window">Initialized window that owns the context.</param>
    /// <param name="gl">GL API entry point created from the same window.</param>
    internal OpenGLDevice(IWindow window, GL gl)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _gl = gl ?? throw new ArgumentNullException(nameof(gl));
        _context =
            window.GLContext
            ?? throw new InvalidOperationException("The window has no GL context.");
        _uniformBufferOffsetAlignment = Math.Max(
            1,
            _gl.GetInteger((GetPName)GlUniformBufferOffsetAlignment)
        );
    }

    /// <summary>Gets what this implementation actually supports.</summary>
    /// <remarks>Barrier is not implemented, so <see cref="RenderCapabilities.SupportsBarrier"/> stays false.</remarks>
    public RenderCapabilities Capabilities =>
        new(true, true, true, true, barrier: false, constantBuffers: true);

    // ───────────────────────── state / render target ─────────────────────────

    public void BindRenderTarget(RenderTargetHandle target) =>
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, target.Id);

    public void UnbindRenderTarget() => _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

    public void SetViewport(int x, int y, int width, int height) =>
        _gl.Viewport(x, y, (uint)width, (uint)height);

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

    // ───────────────────────── resources (MVS) ─────────────────────────

    public BufferHandle CreateBuffer(in BufferDesc desc)
    {
        if (desc.SizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(desc),
                desc.SizeBytes,
                "Buffer size must be positive."
            );
        }

        var target = TargetOf(desc.Usage);
        var id = _gl.GenBuffer();
        _gl.BindBuffer(target, id);
        _gl.BufferData(target, (nuint)desc.SizeBytes, null, UsageOf(desc.Usage));
        _buffers.Add(id, new BufferRecord(desc.Usage, desc.SizeBytes));
        return new BufferHandle(id);
    }

    public void UpdateBuffer(BufferHandle buffer, ReadOnlySpan<byte> data, int offsetBytes)
    {
        if (!_buffers.TryGetValue(buffer.Id, out var record))
        {
            throw new ArgumentException("Unknown buffer handle.", nameof(buffer));
        }
        if (offsetBytes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offsetBytes),
                offsetBytes,
                "The offset must not be negative."
            );
        }
        if ((long)offsetBytes + data.Length > record.SizeBytes)
        {
            // REN-N-04: the documented contract is enforced, not just the negative-offset case.
            throw new ArgumentOutOfRangeException(
                nameof(data),
                data.Length,
                $"Uploading {data.Length} bytes at offset {offsetBytes} exceeds the "
                    + $"{record.SizeBytes}-byte buffer."
            );
        }

        var target = TargetOf(record.Usage);
        _gl.BindBuffer(target, buffer.Id);
        fixed (byte* pointer = data)
        {
            _gl.BufferSubData(target, (nint)offsetBytes, (nuint)data.Length, pointer);
        }
    }

    public void DestroyBuffer(BufferHandle buffer)
    {
        if (!_buffers.Remove(buffer.Id))
        {
            return;
        }

        if (_boundVertexBuffer == buffer.Id)
        {
            _boundVertexBuffer = 0;
        }

        _gl.DeleteBuffer(buffer.Id);
    }

    public void BindVertexBuffer(BufferHandle buffer, uint stride)
    {
        if (stride == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stride), "Vertex stride must be positive.");
        }
        // REN-N-05: a vertex buffer must actually be a Vertex buffer; index/uniform buffers are
        // rejected instead of silently binding the wrong target.
        if (!_buffers.TryGetValue(buffer.Id, out var record) || record.Usage != BufferUsage.Vertex)
        {
            throw new ArgumentException(
                "Unknown vertex buffer handle (expected BufferUsage.Vertex).",
                nameof(buffer)
            );
        }

        // Prototype convention: attribute 0 is the position, two floats at offset 0.
        EnsureUserVertexArray();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer.Id);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(0);
        _boundVertexBuffer = buffer.Id;
    }

    public void BindConstantBuffer(
        ushort binding,
        BufferHandle buffer,
        uint offsetBytes,
        uint sizeBytes
    )
    {
        if (!_buffers.TryGetValue(buffer.Id, out var record) || record.Usage != BufferUsage.Uniform)
        {
            throw new ArgumentException(
                "Unknown uniform buffer handle (expected BufferUsage.Uniform).",
                nameof(buffer)
            );
        }
        if (sizeBytes == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sizeBytes),
                "The constant range must not be empty."
            );
        }
        if ((ulong)offsetBytes + sizeBytes > (ulong)record.SizeBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sizeBytes),
                $"Constant range {offsetBytes}+{sizeBytes} exceeds the {record.SizeBytes}-byte buffer."
            );
        }
        if ((long)offsetBytes % _uniformBufferOffsetAlignment != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offsetBytes),
                $"Constant offset {offsetBytes} is not aligned to {_uniformBufferOffsetAlignment}."
            );
        }

        _gl.BindBufferRange(
            BufferTargetARB.UniformBuffer,
            binding,
            buffer.Id,
            (nint)offsetBytes,
            sizeBytes
        );
    }

    public void BindIndexBuffer(BufferHandle buffer, IndexFormat format)
    {
        if (!_buffers.TryGetValue(buffer.Id, out var record) || record.Usage != BufferUsage.Index)
        {
            throw new ArgumentException("Unknown index buffer handle.", nameof(buffer));
        }

        EnsureUserVertexArray();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, buffer.Id);
        _indexFormat = format;
    }

    public TextureHandle CreateTexture(int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }
        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        var expected = (long)width * height * 4;
        if (rgbaPixels.Length != expected)
        {
            throw new ArgumentException(
                $"Expected {expected} RGBA bytes for {width}x{height}.",
                nameof(rgbaPixels)
            );
        }

        var id = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, id);
        fixed (byte* pointer = rgbaPixels)
        {
            _gl.TexImage2D(
                TextureTarget.Texture2D,
                0,
                (int)InternalFormat.Rgba8,
                (uint)width,
                (uint)height,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                pointer
            );
        }
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMinFilter,
            (int)GLEnum.Linear
        );
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMagFilter,
            (int)GLEnum.Linear
        );
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureWrapS,
            (int)GLEnum.ClampToEdge
        );
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureWrapT,
            (int)GLEnum.ClampToEdge
        );
        _textures.Add(id);
        return new TextureHandle(id);
    }

    public void BindTexture(uint slot, TextureHandle texture)
    {
        // REN-N-07: unknown handles are rejected like buffer/shader/pipeline handles are.
        if (!_textures.Contains(texture.Id))
        {
            throw new ArgumentException("Unknown texture handle.", nameof(texture));
        }

        _gl.ActiveTexture((TextureUnit)((int)TextureUnit.Texture0 + (int)slot));
        _gl.BindTexture(TextureTarget.Texture2D, texture.Id);
    }

    public ShaderHandle CreateShader(in ShaderDesc desc)
    {
        if (string.IsNullOrEmpty(desc.VertexSource) || string.IsNullOrEmpty(desc.FragmentSource))
        {
            throw new ArgumentException("Shader sources must not be empty.", nameof(desc));
        }

        var vertex = CompileShader(ShaderType.VertexShader, desc.VertexSource);
        var fragment = CompileShader(ShaderType.FragmentShader, desc.FragmentSource);

        var program = _gl.CreateProgram();
        _gl.AttachShader(program, vertex);
        _gl.AttachShader(program, fragment);
        _gl.LinkProgram(program);

        _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out var linked);
        if (linked == 0)
        {
            var log = _gl.GetProgramInfoLog(program);
            _gl.DeleteProgram(program);
            throw new RenderDeviceException($"Shader link failed: {log}");
        }

        _gl.DetachShader(program, vertex);
        _gl.DetachShader(program, fragment);
        _gl.DeleteShader(vertex);
        _gl.DeleteShader(fragment);

        _shaders.Add(program);
        return new ShaderHandle(program);
    }

    public PipelineStateHandle CreatePipelineState(in PipelineStateDesc desc)
    {
        if (!_shaders.Contains(desc.Shader.Id))
        {
            throw new ArgumentException("Unknown shader handle.", nameof(desc));
        }

        var id = _nextPipelineId++;
        _pipelines.Add(id, desc);
        BindDrawConstantsBlock(desc.Shader.Id);
        return new PipelineStateHandle(id);
    }

    public void SetPipelineState(PipelineStateHandle pipelineState)
    {
        if (!_pipelines.TryGetValue(pipelineState.Id, out var pipeline))
        {
            throw new ArgumentException("Unknown pipeline state handle.", nameof(pipelineState));
        }

        _gl.UseProgram(pipeline.Shader.Id);
        _hasPipelineState = true;

        if (pipeline.Cull == CullMode.None)
        {
            _gl.Disable(EnableCap.CullFace);
        }
        else
        {
            _gl.Enable(EnableCap.CullFace);
            _gl.CullFace(
                pipeline.Cull == CullMode.Back ? TriangleFace.Back : TriangleFace.Front
            );
        }

        if (pipeline.DepthTest)
        {
            _gl.Enable(EnableCap.DepthTest);
        }
        else
        {
            _gl.Disable(EnableCap.DepthTest);
        }
        _gl.DepthMask(pipeline.DepthWrite);
        ApplyBlend(pipeline.Blend);

        _topology =
            pipeline.Topology == PrimitiveTopology.LineList
                ? PrimitiveType.Lines
                : PrimitiveType.Triangles;
    }

    public void DestroyTexture(TextureHandle texture)
    {
        if (!_textures.Remove(texture.Id))
        {
            return;
        }

        _gl.DeleteTexture(texture.Id);
    }

    public void DestroyShader(ShaderHandle shader)
    {
        if (!_shaders.Remove(shader.Id))
        {
            return;
        }

        // Pipelines that referenced this program become invalid; drop them so a later SetPipelineState
        // reports an unknown handle instead of switching to a deleted program.
        var stale = new List<uint>();
        foreach (var pair in _pipelines)
        {
            if (pair.Value.Shader.Id == shader.Id)
            {
                stale.Add(pair.Key);
            }
        }

        foreach (var id in stale)
        {
            _pipelines.Remove(id);
        }

        _gl.DeleteProgram(shader.Id);
    }

    public void DestroyPipelineState(PipelineStateHandle pipelineState) =>
        _pipelines.Remove(pipelineState.Id);

    /// <summary>Binds the draw-constant block of a program and validates its std140 size.</summary>
    private void BindDrawConstantsBlock(uint program)
    {
        uint blockIndex = _gl.GetUniformBlockIndex(program, DrawConstantsBlockName);
        if (blockIndex == uint.MaxValue)
        {
            return; // The program does not read draw constants (for example the white triangle).
        }

        int blockSize = _gl.GetActiveUniformBlock(
            program,
            blockIndex,
            (UniformBlockPName)GlUniformBlockDataSize
        );
        if (blockSize != DrawConstantsBlockSize)
        {
            throw new RenderDeviceException(
                $"Uniform block '{DrawConstantsBlockName}' is {blockSize} B; expected {DrawConstantsBlockSize} B (std140 two vec4)."
            );
        }

        _gl.UniformBlockBinding(program, blockIndex, ShaderConstantBindings.Draw);
    }

    // ───────────────────────── drawing ─────────────────────────

    /// <summary>Draws indexed geometry from the bound vertex and index buffers.</summary>
    public void DrawIndexed(int indexCount, int startIndex, int baseVertex)
    {
        if (indexCount <= 0)
        {
            return;
        }
        if (_boundVertexBuffer == 0)
        {
            // REN-N-06: indexed drawing without a bound vertex buffer would silently read the debug
            // triangle's attribute state; fail instead of drawing the wrong geometry.
            throw new InvalidOperationException(
                "DrawIndexed requires a bound vertex buffer (call BindVertexBuffer first)."
            );
        }

        EnsureUserVertexArray();

        var elementSize = _indexFormat == IndexFormat.UInt16 ? sizeof(ushort) : sizeof(uint);
        var type =
            _indexFormat == IndexFormat.UInt16
                ? DrawElementsType.UnsignedShort
                : DrawElementsType.UnsignedInt;
        var offset = (nint)(startIndex * elementSize);
        _gl.DrawElementsBaseVertex(_topology, (uint)indexCount, type, in offset, baseVertex);
    }

    /// <summary>Draws from the currently bound vertex buffer.</summary>
    public void Draw(int vertexCount, int startVertex)
    {
        if (vertexCount <= 0)
        {
            return;
        }
        if (_boundVertexBuffer == 0)
        {
            throw new InvalidOperationException(
                "Draw requires a bound vertex buffer (bind one, or record the prototype triangle "
                    + "through BuiltInPipeline)."
            );
        }

        // Prototype shader fallback: without a pipeline state no program is bound, so an untextured
        // prototype draw would render nothing.
        if (!_hasPipelineState)
        {
            _gl.UseProgram(EnsurePrototypeProgram());
        }

        EnsureUserVertexArray();
        _gl.DrawArrays(_topology, startVertex, (uint)vertexCount);
    }

    /// <summary>Compiles the prototype shader lazily; the device owns no prototype geometry.</summary>
    private uint EnsurePrototypeProgram()
    {
        if (_prototypeProgram != 0)
            return _prototypeProgram;

        var vertex = CompileShader(ShaderType.VertexShader, PrototypeVertexShaderSource);
        var fragment = CompileShader(ShaderType.FragmentShader, PrototypeFragmentShaderSource);

        var program = _gl.CreateProgram();
        _gl.AttachShader(program, vertex);
        _gl.AttachShader(program, fragment);
        _gl.LinkProgram(program);

        _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out var linked);
        if (linked == 0)
        {
            throw new RenderDeviceException(
                $"Prototype shader link failed: {_gl.GetProgramInfoLog(program)}"
            );
        }

        _gl.DetachShader(program, vertex);
        _gl.DetachShader(program, fragment);
        _gl.DeleteShader(vertex);
        _gl.DeleteShader(fragment);

        _prototypeProgram = program;
        return program;
    }

    /// <summary>Not implemented by the built-in device.</summary>
    public void Barrier() =>
        throw new NotSupportedException(
            "Barrier is not implemented by the built-in OpenGL device."
        );

    // ───────────────────────── lifecycle ─────────────────────────

    /// <inheritdoc />
    public bool IsClosing => _window.IsClosing;

    /// <inheritdoc />
    public (int Width, int Height) FramebufferSize
    {
        get
        {
            var size = _window.FramebufferSize;
            return (size.X, size.Y);
        }
    }

    /// <inheritdoc />
    public void PumpEvents() => _window.DoEvents();

    /// <inheritdoc />
    public void MakeCurrent() => _context.MakeCurrent();

    /// <inheritdoc />
    public void Present() => _context.SwapBuffers();

    /// <inheritdoc />
    public void WaitIdle() => _gl.Finish();

    /// <summary>Releases GL objects, the GL API and the window; safe to call twice.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        var errors = new List<Exception>();

        void Cleanup(Action action)
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
        }

        Cleanup(DisposeGlObjects);
        Cleanup(_gl.Finish);
        Cleanup(_gl.Dispose);

        // A recording fault can leave prior GL work queued; cleanup must not depend on the context
        // accepting it.
        Cleanup(() => _window.GLContext?.MakeCurrent());
        Cleanup(_window.Dispose);

        if (errors.Count > 0)
            throw new AggregateException("GL device cleanup failed.", errors);
    }

    /// <summary>Deletes the GL objects owned by this device.</summary>
    private void DisposeGlObjects()
    {
        _pipelines.Clear();

        foreach (var program in _shaders)
        {
            _gl.DeleteProgram(program);
        }
        _shaders.Clear();

        foreach (var texture in _textures)
        {
            _gl.DeleteTexture(texture);
        }
        _textures.Clear();

        foreach (var buffer in _buffers.Keys)
        {
            _gl.DeleteBuffer(buffer);
        }
        _buffers.Clear();

        if (_userVertexArray != 0)
        {
            _gl.DeleteVertexArray(_userVertexArray);
            _userVertexArray = 0;
        }

        if (_prototypeProgram != 0)
        {
            _gl.DeleteProgram(_prototypeProgram);
            _prototypeProgram = 0;
        }

        _hasPipelineState = false;
        _boundVertexBuffer = 0;
    }

    private void EnsureUserVertexArray()
    {
        if (_userVertexArray == 0)
        {
            _userVertexArray = _gl.GenVertexArray();
        }

        _gl.BindVertexArray(_userVertexArray);
    }

    private uint CompileShader(ShaderType type, string source)
    {
        var shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);

        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out var status);
        if (status == 0)
        {
            var log = _gl.GetShaderInfoLog(shader);
            _gl.DeleteShader(shader);
            throw new RenderDeviceException($"{type} compile failed: {log}");
        }

        return shader;
    }

    private void ApplyBlend(BlendMode mode)
    {
        if (mode == BlendMode.None)
        {
            _gl.Disable(EnableCap.Blend);
            return;
        }

        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(
            BlendingFactor.SrcAlpha,
            mode == BlendMode.Additive ? BlendingFactor.One : BlendingFactor.OneMinusSrcAlpha
        );
    }

    private static BufferTargetARB TargetOf(BufferUsage usage) =>
        usage == BufferUsage.Index
            ? BufferTargetARB.ElementArrayBuffer
            : BufferTargetARB.ArrayBuffer;

    private static BufferUsageARB UsageOf(BufferUsage usage) =>
        usage == BufferUsage.Uniform ? BufferUsageARB.DynamicDraw : BufferUsageARB.StaticDraw;
}
