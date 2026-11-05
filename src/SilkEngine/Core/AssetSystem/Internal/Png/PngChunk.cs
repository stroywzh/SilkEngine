using System.Buffers.Binary;
using System.Text;

namespace SilkEngine.Core.AssetSystem.Internal.Png;

/// <summary>One PNG chunk: 4-byte ASCII type + data (CRC excluded).</summary>
internal readonly struct PngChunk
{
    public readonly string Type;

    public readonly byte[] Data;

    public PngChunk(string type, byte[] data)
    {
        Type = type;
        Data = data;
    }
}

/// <summary>Sequential PNG chunk reader.</summary>
internal static class PngChunkReader
{
    private static ReadOnlySpan<byte> Signature =>
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    // Per-chunk data cap (256 MiB): check first, then allocate, so malformed lengths cannot demand memory.
    private const uint MaxChunkBytes = 256u * 1024 * 1024;

    // Lazy enumeration: iterate to IEND (normal return) or an exception; CRC is not validated.
    public static IEnumerable<PngChunk> Read(Stream stream)
    {
        var sig = new byte[8];
        stream.ReadExactly(sig);
        if (!sig.AsSpan().SequenceEqual(Signature))
            throw new InvalidDataException("Not a PNG (bad signature).");

        var headerBuf = new byte[8];
        var crcBuf = new byte[4];

        while (true)
        {
            stream.ReadExactly(headerBuf);
            uint len = BinaryPrimitives.ReadUInt32BigEndian(headerBuf);
            string type = Encoding.ASCII.GetString(headerBuf, 4, 4);

            if (len > MaxChunkBytes)
                throw new InvalidDataException(
                    $"Chunk '{type}' declares {len} bytes, above the {MaxChunkBytes} byte per-chunk limit.");

            // Seekable streams are also checked against the remaining bytes: no allocation before failure.
            if (stream.CanSeek)
            {
                long remaining = stream.Length - stream.Position; // chunk data + CRC
                if (len + 4L > remaining)
                    throw new InvalidDataException(
                        $"Chunk '{type}' declares {len} bytes but only {Math.Max(remaining - 4L, 0L)} bytes remain in the stream.");
            }

            var data = new byte[len];
            stream.ReadExactly(data);
            stream.ReadExactly(crcBuf); // CRC validation is skipped in the prototype.

            yield return new PngChunk(type, data);

            if (type == "IEND")
                yield break;
        }
    }
}

