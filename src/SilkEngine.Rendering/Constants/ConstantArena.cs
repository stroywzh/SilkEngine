using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering;

// Render-thread only. One uniform buffer split into per-frame slices: each frame writes CPU staging and
// uploads the used range once; a slice must not be rewritten while an in-flight frame still references it.
internal sealed class ConstantArena
{
    internal const int SliceCount = 3;
    internal const int SliceSizeBytes = 64 * 1024;
    internal const int Alignment = 256;

    private readonly IRenderDevice _device;
    private readonly BufferHandle _buffer;
    private readonly byte[] _staging = new byte[SliceCount * SliceSizeBytes];
    private int _sliceIndex;
    private int _usedBytes;

    internal ConstantArena(IRenderDevice device)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _buffer = device.CreateBuffer(
            new BufferDesc(SliceCount * SliceSizeBytes, BufferUsage.Uniform)
        );
    }

    internal BufferHandle Buffer => _buffer;

    internal void BeginFrame(ulong frameIndex)
    {
        _sliceIndex = (int)(frameIndex % SliceCount);
        _usedBytes = 0;
    }

    internal uint Write<T>(in T value)
        where T : unmanaged
    {
        int size = Unsafe.SizeOf<T>();
        int offsetInSlice = (_usedBytes + Alignment - 1) / Alignment * Alignment;
        if (offsetInSlice + size > SliceSizeBytes)
        {
            throw new InvalidOperationException(
                $"Constant arena slice overflow: {offsetInSlice + size} B exceeds {SliceSizeBytes} B."
            );
        }

        uint absoluteOffset = (uint)(_sliceIndex * SliceSizeBytes + offsetInSlice);
        MemoryMarshal.Write(_staging.AsSpan((int)absoluteOffset, size), in value);
        _usedBytes = offsetInSlice + size;
        return absoluteOffset;
    }

    internal void Upload()
    {
        if (_usedBytes == 0)
            return;

        int sliceBase = _sliceIndex * SliceSizeBytes;
        _device.UpdateBuffer(_buffer, _staging.AsSpan(sliceBase, _usedBytes), sliceBase);
    }

    internal void Dispose() => _device.DestroyBuffer(_buffer);
}
