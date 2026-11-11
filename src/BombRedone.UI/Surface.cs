namespace BombRedone.UI;

/// <summary>
/// Palette-indexed frame buffer the screens draw into; independent of any window system.
/// The size is a parameter so that further video modes stay possible; the original uses
/// <see cref="OriginalWidth"/> x <see cref="OriginalHeight"/> only.
/// </summary>
public sealed class Surface
{
    /// <summary>[ORIGINAL] BOMB.TXT, "REQUIREMENTS": "This program runs in 320x200x256 color mode."</summary>
    public const int OriginalWidth = 320;

    /// <summary>[ORIGINAL] BOMB.TXT, "REQUIREMENTS": "This program runs in 320x200x256 color mode."</summary>
    public const int OriginalHeight = 200;

    /// <summary>Number of palette entries of the 256-color mode.</summary>
    public const int PaletteSize = 256;

    public Surface(int width = OriginalWidth, int height = OriginalHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
        Pixels = new byte[width * height];
        Palette = new uint[PaletteSize];
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Palette indices, row by row from the top left.</summary>
    public byte[] Pixels { get; }

    /// <summary>Palette as 0xAABBGGRR (byte order R, G, B, A in memory on little-endian hosts).</summary>
    public uint[] Palette { get; }

    public byte this[int x, int y]
    {
        get => Pixels[Index(x, y)];
        set => Pixels[Index(x, y)] = value;
    }

    public void Clear(byte index = 0) => Array.Fill(Pixels, index);

    /// <summary>Converts the indexed image to 32-bit colors; <paramref name="target"/> needs Width * Height entries.</summary>
    public void CopyTo(Span<uint> target)
    {
        if (target.Length < Pixels.Length)
            throw new ArgumentException("Target buffer is too small.", nameof(target));
        for (var i = 0; i < Pixels.Length; i++)
            target[i] = Palette[Pixels[i]];
    }

    private int Index(int x, int y)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            throw new ArgumentOutOfRangeException($"Pixel ({x}, {y}) is outside {Width} x {Height}.");
        return y * Width + x;
    }
}
