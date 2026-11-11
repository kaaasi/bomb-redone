namespace BombRedone.Formats;

/// <summary>
/// A title image as stored in <c>BOMB1.RAW</c> / <c>BOMB2.RAW</c>: 320 x 200 palette indices, row by row from the top
/// left, followed by the palette (256 x R, G, B, 6 bits each). No header.
/// [ORIGINAL] Both files are 64,768 bytes = 64,000 + 768, all palette values at most 63 (docs/originaldateien.md,
/// checked in docs/formate/dateiformate.md). Which program routine reads them, and how, is not yet derived from the disassembly.
/// </summary>
public sealed class RawImage
{
    public const int Width = 320;
    public const int Height = 200;
    public const int PixelCount = Width * Height;

    /// <summary>Exact file size: pixels plus palette.</summary>
    public const int FileLength = PixelCount + Palette.ByteLength;

    private readonly byte[] _pixels;

    public RawImage(byte[] pixels, Palette palette)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentNullException.ThrowIfNull(palette);
        if (pixels.Length != PixelCount)
            throw new ArgumentException($"Image needs {PixelCount} pixels, got {pixels.Length}.", nameof(pixels));
        _pixels = (byte[])pixels.Clone();
        Palette = palette;
    }

    public Palette Palette { get; }

    /// <summary>Palette indices, row by row (copy).</summary>
    public byte[] GetPixels() => (byte[])_pixels.Clone();

    public ReadOnlySpan<byte> Pixels => _pixels;

    public byte this[int x, int y]
    {
        get
        {
            if ((uint)x >= Width || (uint)y >= Height)
                throw new ArgumentOutOfRangeException($"Pixel ({x}, {y}) is outside {Width} x {Height}.");
            return _pixels[y * Width + x];
        }
    }

    /// <summary>Number of different palette indices that occur in the image.</summary>
    public int UsedColorCount()
    {
        var seen = new bool[Palette.Size];
        foreach (var p in _pixels) seen[p] = true;
        return seen.Count(s => s);
    }

    /// <summary>Parses the file contents.</summary>
    /// <exception cref="FormatException">Not exactly <see cref="FileLength"/> bytes, or a palette value above 63.</exception>
    public static RawImage Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length != FileLength)
            throw new FormatException(
                $"RAW image needs exactly {FileLength} bytes ({PixelCount} pixels + {Palette.ByteLength} palette), got {data.Length}.");
        return new RawImage(data[..PixelCount].ToArray(), Palette.FromBytes(data[PixelCount..]));
    }

    /// <summary>Reads a <c>*.RAW</c> file.</summary>
    public static RawImage Load(string path) => Parse(File.ReadAllBytes(path));

    /// <summary>The file contents; <see cref="Parse"/> inverts this.</summary>
    public byte[] ToBytes()
    {
        var result = new byte[FileLength];
        _pixels.CopyTo(result, 0);
        Palette.ToBytes().CopyTo(result, PixelCount);
        return result;
    }
}
