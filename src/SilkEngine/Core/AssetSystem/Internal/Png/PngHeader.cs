using System.Buffers.Binary;
using System.IO;

namespace SilkEngine.Core.AssetSystem.Internal.Png;

/// <summary>PNG IHDR header: only 8-bit, non-interlaced, compression/filter method 0 are accepted.</summary>
internal sealed class PngHeader
{
    // Per-side pixel cap; together with the decoded-buffer cap it blocks huge allocations from malformed IHDR.
    private const int MaxDimension = 16384;

    // Decoded RGBA32 buffer cap (512 MiB).
    private const long MaxDecodedBytes = 512L * 1024 * 1024;

    public int Width { get; private init; }

    public int Height { get; private init; }

    public byte BitDepth { get; private init; }

    // 0 = Gray, 2 = RGB, 3 = Index, 4 = Gray+A, 6 = RGBA。
    public byte ColorType { get; private init; }

    public byte Compression { get; private init; }

    public byte Filter { get; private init; }

    public byte Interlace { get; private init; }

    // Invalid color type throws InvalidDataException.
    public int Channels =>
        ColorType switch
        {
            0 => 1,
            2 => 3,
            3 => 1,
            4 => 2,
            6 => 4,
            _ => throw new InvalidDataException($"Invalid colorType {ColorType}"),
        };

    // data is the IHDR payload (no length/type/CRC); must be 13 bytes.
    public static PngHeader Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length != 13)
            throw new InvalidDataException($"IHDR must be 13 bytes, got {data.Length}.");

        var h = new PngHeader
        {
            Width = BinaryPrimitives.ReadInt32BigEndian(data[..4]),
            Height = BinaryPrimitives.ReadInt32BigEndian(data[4..8]),
            BitDepth = data[8],
            ColorType = data[9],
            Compression = data[10],
            Filter = data[11],
            Interlace = data[12],
        };
        h.Validate();
        return h;
    }

    private void Validate()
    {
        if (Width <= 0 || Height <= 0)
            throw new InvalidDataException($"Invalid size {Width}x{Height}.");
        if (Width > MaxDimension || Height > MaxDimension)
            throw new InvalidDataException(
                $"PNG size {Width}x{Height} exceeds the {MaxDimension}x{MaxDimension} pixel limit.");
        long rgbaBytes = (long)Width * Height * 4;
        if (rgbaBytes > MaxDecodedBytes)
            throw new InvalidDataException(
                $"PNG {Width}x{Height} needs {rgbaBytes} bytes of RGBA32 output, above the {MaxDecodedBytes} byte limit.");
        if (Compression != 0)
            throw new NotSupportedException($"compression={Compression} not supported.");
        if (Filter != 0)
            throw new NotSupportedException($"filter={Filter} not supported.");
        if (Interlace != 0)
            throw new NotSupportedException("Adam7 interlace not supported.");
        if (BitDepth != 8)
            throw new NotSupportedException($"Only bitDepth=8 supported, got {BitDepth}.");

        bool valid = ColorType switch
        {
            0 or 2 or 3 or 4 or 6 => true,
            _ => false,
        };
        if (!valid)
            throw new NotSupportedException($"colorType={ColorType} not supported.");
    }
}
