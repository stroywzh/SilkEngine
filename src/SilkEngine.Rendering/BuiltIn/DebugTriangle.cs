using System.Runtime.InteropServices;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.BuiltIn;

//DEBUG_Only
internal static class DebugTriangle
{
    /// <summary>Bytes between consecutive vertices (two floats).</summary>
    internal const uint StrideBytes = 2 * sizeof(float);

    /// <summary>Number of vertices in the triangle.</summary>
    internal const int VertexCount = 3;

    /// <summary>Local positions, row-major, two floats per vertex.</summary>
    internal static ReadOnlySpan<float> Vertices => [-0.6f, -0.5f, 0.6f, -0.5f, 0.0f, 0.6f];

    /// <summary>Creates the vertex buffer on the device.</summary>
    /// <param name="device">Device that owns the buffer.</param>
    /// <returns>The vertex buffer handle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="device"/> is null.</exception>
    internal static BufferHandle Create(IRenderDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        var bytes = MemoryMarshal.AsBytes(Vertices);
        var handle = device.CreateBuffer(new BufferDesc(bytes.Length, BufferUsage.Vertex));
        device.UpdateBuffer(handle, bytes);
        return handle;
    }

    /// <summary>Records the bind + draw pair for one frame.</summary>
    /// <param name="commands">Writable buffer for the current frame.</param>
    /// <param name="buffer">Vertex buffer created by <see cref="Create"/>.</param>
    internal static void Record(ICommandBuffer commands, BufferHandle buffer)
    {
        var bind = new BindVertexBufferCommand(buffer, StrideBytes);
        commands.Record(in bind);

        var draw = new DrawCommand(VertexCount, 0);
        commands.Record(in draw);
    }
}
