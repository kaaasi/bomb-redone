using BombRedone.Core.Graphics;

namespace BombRedone.Core.Round;

/// <summary>Pixel access of unit 0CC8 with the 16-bit address x + 320 · y (beyond the screen dropped or 0).</summary>
internal static class Pixels
{
    private static ushort Address(int x, int y) => (ushort)(x + (y << 8) + (y << 6));

    /// <summary>[ORIGINAL] UCC8:0BFF (BOMB.EXE 0x0EF1F).</summary>
    public static byte Get(ReadOnlySpan<byte> screen, int x, int y)
    {
        var a = Address(x, y);
        return a < IndexBuffer.Size ? screen[a] : (byte)0;
    }

    /// <summary>[ORIGINAL] UCC8:0BB6 (BOMB.EXE 0x0EED6).</summary>
    public static void Put(Span<byte> screen, int x, int y, byte color)
    {
        var a = Address(x, y);
        if (a < IndexBuffer.Size)
            screen[a] = color;
    }

    /// <summary>[ORIGINAL] UCC8:0BD8 (BOMB.EXE 0x0EEF8): the pixel from the background buffer.</summary>
    public static void Copy(ReadOnlySpan<byte> background, Span<byte> screen, int x, int y)
    {
        var a = Address(x, y);
        if (a < IndexBuffer.Size)
            screen[a] = background[a];
    }
}
