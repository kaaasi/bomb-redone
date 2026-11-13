namespace BombRedone.Core.Graphics;

/// <summary>
/// Palette-indexed 320 x 200 pixel buffer of the game core, laid out like segment A000h in mode 13h
/// (address = y * 320 + x). The playfield keeps its screen and background buffer in this form because the rules
/// of the original read pixels back (docs/arbeitspakete.md, spur gelaende; Review #104 P2-2).
/// </summary>
public sealed class IndexBuffer
{
    /// <summary>[ORIGINAL] UCC8:0015 (BOMB.EXE 0x0E335): row length of mode 13h.</summary>
    public const int Width = 320;

    /// <summary>[ORIGINAL] Mode 13h, 320 x 200 (BOMB.TXT "REQUIREMENTS").</summary>
    public const int Height = 200;

    /// <summary>Bytes of one buffer; FillChar(DS:E5C0^, 64000, 0) in O8:0000 clears exactly this many.</summary>
    public const int Size = Width * Height;

    public byte[] Pixels { get; } = new byte[Size];

    public byte this[int x, int y]
    {
        get => Pixels[VgaDraw.Address(x, y)];
        set => Pixels[VgaDraw.Address(x, y)] = value;
    }

    public void Clear(byte index = 0) => Array.Fill(Pixels, index);

    public void CopyTo(IndexBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);
        Pixels.CopyTo(target.Pixels, 0);
    }
}
