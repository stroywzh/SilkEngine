using System.IO;
using System.IO.Compression;

namespace SilkEngine.Core.AssetSystem.Internal.Png;

/// <summary>PNG decode result: size + RGBA32 pixels.</summary>
internal sealed class PngDecodedImage
{
    public required int Width { get; init; }

    public required int Height { get; init; }

    // Length = Width * Height * 4.
    public required byte[] Rgba32 { get; init; }
}

/// <summary>Managed-only PNG decoder: 8-bit, non-interlaced, RGBA32.</summary>
internal static class PngDecoder
{
    // stream must be at the PNG signature; unsupported encoding throws NotSupportedException.
    public static PngDecodedImage Decode(Stream stream)
    {
        PngHeader? header = null;
        byte[]? palette = null;
        byte[]? trns = null;
        using var idat = new MemoryStream();

        foreach (var chunk in PngChunkReader.Read(stream))
        {
            switch (chunk.Type)
            {
                case "IHDR":
                    header = PngHeader.Parse(chunk.Data);
                    break;
                case "PLTE":
                    palette = chunk.Data;
                    break;
                case "tRNS":
                    trns = chunk.Data;
                    break;
                case "IDAT":
                    idat.Write(chunk.Data);
                    break;
                // IEND is terminated by PngChunkReader.
            }
        }

        if (header is null)
            throw new InvalidDataException("Missing IHDR.");
        if (header.ColorType == 3 && palette is null)
            throw new InvalidDataException("Missing PLTE for indexed color.");

        // Inflate cap = exact unfiltered scanline buffer size; a larger stream fails immediately (zip-bomb guard).
        byte[] inflated = InflateZlib(idat.ToArray(), ComputeRawSize(header));
        byte[] unfiltered = Unfilter(inflated, header);
        byte[] rgba = ToRgba32(unfiltered, header, palette, trns);

        return new PngDecodedImage
        {
            Width = header.Width,
            Height = header.Height,
            Rgba32 = rgba,
        };
    }

    // ---------- zlib ----------

    private static byte[] InflateZlib(byte[] idat, int maxBytes)
    {
        if (idat.Length < 6)
            throw new InvalidDataException("zlib stream too short.");

        byte cmf = idat[0],
            flg = idat[1];
        if ((cmf & 0x0F) != 8)
            throw new InvalidDataException("zlib: not deflate.");
        if (((cmf << 8) | flg) % 31 != 0)
            throw new InvalidDataException("zlib: bad header check.");
        if ((flg & 0x20) != 0)
            throw new NotSupportedException("zlib: preset dictionary unsupported.");

        // Strip the 2-byte header + 4-byte Adler-32; the rest is raw deflate.
        int dataLen = idat.Length - 6;
        using var ms = new MemoryStream(idat, 2, dataLen, writable: false);
        using var z = new DeflateStream(ms, CompressionMode.Decompress);
        using var outMs = new MemoryStream(Math.Min(maxBytes, 64 * 1024));

        var buffer = new byte[64 * 1024];
        int read;
        while ((read = z.Read(buffer, 0, buffer.Length)) > 0)
        {
            // Accumulate per chunk; stop once the expected decoded size is exceeded: no more reads, no growth.
            if (outMs.Length + read > maxBytes)
                throw new InvalidDataException(
                    $"zlib output exceeds the expected {maxBytes} decoded bytes (possible decompression bomb).");

            outMs.Write(buffer, 0, read);
        }

        return outMs.ToArray();
    }

    // Size of the unfiltered scanline buffer: width*channels bytes + one filter byte per row.
    private static int ComputeRawSize(PngHeader header)
    {
        int stride = header.Width * header.Channels;
        return checked(header.Height * (stride + 1));
    }

    // ---------- Unfiltering ----------

    private static byte[] Unfilter(byte[] raw, PngHeader header)
    {
        int channels = header.Channels;
        int bpp = channels * (header.BitDepth / 8); // 8-bit: equals channels
        int stride = header.Width * bpp;
        int rowLen = stride + 1; // +1 is the filter-type byte
        int expected = ComputeRawSize(header);

        if (raw.Length != expected)
            throw new InvalidDataException(
                $"Decompressed size mismatch: expected {expected}, got {raw.Length}."
            );

        var output = new byte[header.Height * stride];
        var prev = new byte[stride];

        for (int y = 0; y < header.Height; y++)
        {
            int filter = raw[y * rowLen];
            var src = raw.AsSpan(y * rowLen + 1, stride);
            var dst = output.AsSpan(y * stride, stride);

            switch (filter)
            {
                case 0:
                    src.CopyTo(dst);
                    break;
                case 1:
                    FilterSub(dst, src, bpp);
                    break;
                case 2:
                    FilterUp(dst, src, prev);
                    break;
                case 3:
                    FilterAverage(dst, src, prev, bpp);
                    break;
                case 4:
                    FilterPaeth(dst, src, prev, bpp);
                    break;
                default:
                    throw new InvalidDataException($"Bad filter {filter} at row {y}.");
            }
            dst.CopyTo(prev);
        }
        return output;
    }

    private static void FilterSub(Span<byte> dst, ReadOnlySpan<byte> src, int bpp)
    {
        for (int i = 0; i < src.Length; i++)
            dst[i] = (byte)(src[i] + (i >= bpp ? dst[i - bpp] : 0));
    }

    private static void FilterUp(Span<byte> dst, ReadOnlySpan<byte> src, byte[] prev)
    {
        for (int i = 0; i < src.Length; i++)
            dst[i] = (byte)(src[i] + prev[i]);
    }

    private static void FilterAverage(Span<byte> dst, ReadOnlySpan<byte> src, byte[] prev, int bpp)
    {
        for (int i = 0; i < src.Length; i++)
        {
            int a = i >= bpp ? dst[i - bpp] : 0;
            int b = prev[i];
            dst[i] = (byte)(src[i] + ((a + b) >> 1));
        }
    }

    private static void FilterPaeth(Span<byte> dst, ReadOnlySpan<byte> src, byte[] prev, int bpp)
    {
        for (int i = 0; i < src.Length; i++)
        {
            int a = i >= bpp ? dst[i - bpp] : 0;
            int b = prev[i];
            int c = i >= bpp ? prev[i - bpp] : 0;
            int p = a + b - c;
            int pa = Math.Abs(p - a);
            int pb = Math.Abs(p - b);
            int pc = Math.Abs(p - c);
            int predictor = (pa <= pb && pa <= pc) ? a : (pb <= pc ? b : c);
            dst[i] = (byte)(src[i] + predictor);
        }
    }

    // ---------- Color conversion ----------

    private static byte[] ToRgba32(byte[] pixels, PngHeader h, byte[]? palette, byte[]? trns)
    {
        int w = h.Width,
            ht = h.Height;
        var outp = new byte[w * ht * 4];

        switch (h.ColorType)
        {
            case 6: // RGBA
                Buffer.BlockCopy(pixels, 0, outp, 0, pixels.Length);
                return outp;

            case 2: // RGB
                for (int i = 0, j = 0; i < pixels.Length; i += 3, j += 4)
                {
                    outp[j] = pixels[i];
                    outp[j + 1] = pixels[i + 1];
                    outp[j + 2] = pixels[i + 2];
                    outp[j + 3] = 255;
                }
                return outp;

            case 0: // Gray
                for (int i = 0, j = 0; i < pixels.Length; i++, j += 4)
                {
                    byte g = pixels[i];
                    outp[j] = outp[j + 1] = outp[j + 2] = g;
                    outp[j + 3] = 255;
                }
                return outp;

            case 4: // Gray + Alpha
                for (int i = 0, j = 0; i < pixels.Length; i += 2, j += 4)
                {
                    byte g = pixels[i];
                    outp[j] = outp[j + 1] = outp[j + 2] = g;
                    outp[j + 3] = pixels[i + 1];
                }
                return outp;

            case 3: // Indexed
                ExpandIndexed(pixels, w * ht, palette!, trns, outp);
                return outp;

            default:
                throw new NotSupportedException($"colorType={h.ColorType}");
        }
    }

    private static void ExpandIndexed(
        byte[] indices,
        int count,
        byte[] palette,
        byte[]? trns,
        byte[] outp
    )
    {
        for (int i = 0, j = 0; i < count; i++, j += 4)
        {
            int idx = indices[i];
            int p = idx * 3;
            if (p + 2 >= palette.Length)
                throw new InvalidDataException($"Palette index {idx} out of range.");
            outp[j] = palette[p];
            outp[j + 1] = palette[p + 1];
            outp[j + 2] = palette[p + 2];
            outp[j + 3] = (trns is not null && idx < trns.Length) ? trns[idx] : (byte)255;
        }
    }
}
