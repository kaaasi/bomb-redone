namespace BombRedone.Formats;

/// <summary>
/// 8 x 8 bitmap font in the layout of the PC ROM font: eight bytes per character, one byte per row from the top,
/// bit 7 is the leftmost pixel. Used for the DefaultFont of the Graph unit.
/// </summary>
/// <remarks>
/// [ORIGINAL] The DefaultFont is not part of the original files: the VGA256 driver linked into BOMB.EXE takes
/// characters 0-127 from the ROM BIOS at F000:FA6E and characters 128-255 from the table the interrupt vector 1Fh
/// points to (BOMB.EXE 0x08769-0x087A2: code * 8, "cmp al,80h", vector at 0000:007C, offset FA6Eh, segment F000h).
/// The glyphs therefore come from the machine (DOSBox: its built-in VGA font), not from the game; this type only holds
/// the layout. Where the data comes from is decided outside the Formats layer (docs/formate/schriften.md).
/// </remarks>
public sealed class BitmapFont
{
    /// <summary>Width and height of a character cell in pixels.</summary>
    public const int CellSize = 8;

    /// <summary>Bytes per character.</summary>
    public const int BytesPerChar = CellSize;

    private readonly byte[] _rows;

    /// <param name="rows">Eight bytes per character, starting with character 0; 128 or 256 characters.</param>
    public BitmapFont(ReadOnlySpan<byte> rows)
    {
        if (rows.Length != 128 * BytesPerChar && rows.Length != 256 * BytesPerChar)
            throw new FormatException(
                $"8 x 8 font needs {128 * BytesPerChar} or {256 * BytesPerChar} bytes, got {rows.Length}.");
        _rows = rows.ToArray();
    }

    /// <summary>Number of characters (128 or 256).</summary>
    public int CharCount => _rows.Length / BytesPerChar;

    /// <summary>The eight rows of a character; an empty cell for codes beyond <see cref="CharCount"/>.</summary>
    public ReadOnlySpan<byte> GetRows(byte code) =>
        code < CharCount ? _rows.AsSpan(code * BytesPerChar, BytesPerChar) : new byte[BytesPerChar];

    /// <summary>True if the pixel (column x, row y from the top left of the cell) is set.</summary>
    public bool IsSet(byte code, int x, int y)
    {
        if ((uint)x >= CellSize || (uint)y >= CellSize)
            throw new ArgumentOutOfRangeException($"Pixel ({x}, {y}) is outside the 8 x 8 cell.");
        return (GetRows(code)[y] & (0x80 >> x)) != 0;
    }
}
