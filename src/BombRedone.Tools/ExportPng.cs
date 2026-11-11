using System.Buffers.Binary;
using System.IO.Compression;
using BombRedone.Formats;

namespace BombRedone.Tools;

/// <summary>Minimal PNG writer for palette images (color type 3, 8 bits), without external packages.</summary>
public static class ExportPng
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Encodes the image as an indexed PNG; the palette is widened with <paramref name="scaling"/>.</summary>
    public static byte[] Encode(RawImage image, PaletteScaling scaling = Palette.DefaultScaling)
    {
        using var output = new MemoryStream();
        output.Write(Signature);

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header[0..4], RawImage.Width);
        BinaryPrimitives.WriteUInt32BigEndian(header[4..8], RawImage.Height);
        header[8] = 8; // bit depth
        header[9] = 3; // color type: palette
        header[10] = 0; // compression
        header[11] = 0; // filter method
        header[12] = 0; // no interlace
        WriteChunk(output, "IHDR", header);

        var plte = new byte[Palette.ByteLength];
        for (var i = 0; i < Palette.Size; i++)
        {
            var (r, g, b) = image.Palette.GetRgb(i, scaling);
            plte[i * 3] = r;
            plte[i * 3 + 1] = g;
            plte[i * 3 + 2] = b;
        }
        WriteChunk(output, "PLTE", plte);

        // Scanlines with filter type 0 (none).
        var raw = new byte[(RawImage.Width + 1) * RawImage.Height];
        for (var y = 0; y < RawImage.Height; y++)
            image.Pixels.Slice(y * RawImage.Width, RawImage.Width).CopyTo(raw.AsSpan(y * (RawImage.Width + 1) + 1));

        using var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            z.Write(raw);
        WriteChunk(output, "IDAT", compressed.ToArray());
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    private static void WriteChunk(Stream output, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(number, (uint)data.Length);
        output.Write(number);

        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);

        var crc = Crc32(Crc32(0xFFFFFFFFu, typeBytes), data) ^ 0xFFFFFFFFu;
        BinaryPrimitives.WriteUInt32BigEndian(number, crc);
        output.Write(number);
    }

    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }

    private static uint Crc32(uint state, ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
            state = CrcTable[(state ^ b) & 0xFF] ^ (state >> 8);
        return state;
    }
}
