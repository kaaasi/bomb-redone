using System.Text;

namespace BombRedone.Formats;

/// <summary>Kind of a stroke command of a BGI stroked font.</summary>
public enum BgiStrokeOp
{
    /// <summary>Move the pen to the point without drawing (op bits 1, 0).</summary>
    Move,

    /// <summary>Draw a line from the previous point to this point (op bits 1, 1).</summary>
    Draw,

    /// <summary>"Scan" command (op bits 0, 1); the Graph kernel skips it (BOMB.EXE 0x132D1 jump table, entry 1).</summary>
    Scan,
}

/// <summary>One stroke command: operation and point relative to the character origin (y grows upwards).</summary>
public readonly record struct BgiStroke(BgiStrokeOp Op, int X, int Y);

/// <summary>One character of a stroked font: advance width from the width table and the stroke commands.</summary>
public sealed class BgiGlyph
{
    public BgiGlyph(int width, IReadOnlyList<BgiStroke> strokes)
    {
        ArgumentNullException.ThrowIfNull(strokes);
        Width = width;
        Strokes = strokes;
    }

    /// <summary>Entry of the width table (unscaled).</summary>
    public int Width { get; }

    /// <summary>Stroke commands up to, not including, the end command.</summary>
    public IReadOnlyList<BgiStroke> Strokes { get; }
}

/// <summary>
/// A Borland BGI stroked font (<c>*.CHR</c>, "PK\x08\x08 BGI ..."), as linked into <c>BOMB.EXE</c> (LITT, EURO, TINY).
/// Layout: docs/formate/schriften.md.
/// </summary>
/// <remarks>
/// [ORIGINAL] Layout of the font data header as read by the Graph kernel: the kernel copies 16 bytes from the start of the
/// font data and checks the signature '+' (BOMB.EXE 0x131E0..0x131FB), takes the character count from +1, the first
/// character from +5 (0x13295), the stroke offset from +7 (0x132B2), the scan flag from +9 (0x132B6), the heights
/// top/bottom from +10/+12 (0x13214) and the offset table at +16 followed by the width table (0x13247, 0x132AA).
/// The file header (size of the header at the byte after 1Ah, name, data size, versions) follows the three embedded
/// fonts (BOMB.EXE 0x08BE9, 0x09FF5, 0x0C0FA); its use by the kernel is not derived.
/// </remarks>
public sealed class BgiFont
{
    /// <summary>File signature "PK" 08h 08h.</summary>
    public static ReadOnlySpan<byte> FileSignature => [0x50, 0x4B, 0x08, 0x08];

    /// <summary>Signature byte of the font data header.</summary>
    public const byte DataSignature = (byte)'+';

    /// <summary>Length of the font data header before the offset table.</summary>
    public const int DataHeaderLength = 16;

    private const byte EndOfText = 0x1A;

    private readonly BgiGlyph[] _glyphs;

    private BgiFont(string name, string description, int majorVersion, int minorVersion, int firstChar, int top,
        int baseline, int bottom, bool scanFlag, BgiGlyph[] glyphs, int fileLength)
    {
        Name = name;
        Description = description;
        MajorVersion = majorVersion;
        MinorVersion = minorVersion;
        FirstChar = firstChar;
        Top = top;
        Baseline = baseline;
        Bottom = bottom;
        HasScanFlag = scanFlag;
        _glyphs = glyphs;
        FileLength = fileLength;
    }

    /// <summary>Four-character name from the file header, e.g. "EURO".</summary>
    public string Name { get; }

    /// <summary>Text before the 1Ah byte (driver description and copyright).</summary>
    public string Description { get; }

    public int MajorVersion { get; }

    public int MinorVersion { get; }

    /// <summary>Code of the first character in the font.</summary>
    public int FirstChar { get; }

    /// <summary>Number of characters in the font.</summary>
    public int CharCount => _glyphs.Length;

    /// <summary>Origin to top of capital (font data header +10, signed).</summary>
    public int Top { get; }

    /// <summary>Origin to baseline (font data header +11, signed).</summary>
    public int Baseline { get; }

    /// <summary>Origin to bottom of descender (font data header +12, signed).</summary>
    public int Bottom { get; }

    /// <summary>Scan flag of the font data header (+9); the kernel then fills characters, unused by the three fonts.</summary>
    public bool HasScanFlag { get; }

    /// <summary>Bytes of the whole font file: header plus font data.</summary>
    public int FileLength { get; }

    /// <summary>True if the character code lies in the font.</summary>
    public bool Contains(byte code) => code - FirstChar >= 0 && code - FirstChar < _glyphs.Length;

    /// <summary>The character, or null if the code is outside the font (the kernel skips it, BOMB.EXE 0x13299).</summary>
    public BgiGlyph? GetGlyph(byte code) => Contains(code) ? _glyphs[code - FirstChar] : null;

    /// <summary>Parses a font file starting at its signature; trailing bytes after the font are ignored.</summary>
    /// <exception cref="FormatException">Signature, header or stroke data are missing or inconsistent.</exception>
    public static BgiFont Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < FileSignature.Length || !data[..FileSignature.Length].SequenceEqual(FileSignature))
            throw new FormatException("BGI font: signature \"PK\\x08\\x08\" missing.");
        var eot = data.IndexOf(EndOfText);
        if (eot < 0 || eot + 11 > data.Length)
            throw new FormatException("BGI font: end of the description (1Ah) or file header missing.");
        var description = Encoding.Latin1.GetString(data[FileSignature.Length..eot]).TrimEnd('\0', '\r', '\n');
        var header = eot + 1;
        var headerSize = ReadUInt16(data, header);
        var name = Encoding.Latin1.GetString(data.Slice(header + 2, 4));
        var dataSize = ReadUInt16(data, header + 6);
        var major = data[header + 8];
        var minor = data[header + 9];
        if (headerSize <= header || headerSize + dataSize > data.Length)
            throw new FormatException(
                $"BGI font {name}: header size {headerSize} and data size {dataSize} do not fit {data.Length} bytes.");

        var font = data.Slice(headerSize, dataSize);
        if (font.Length < DataHeaderLength || font[0] != DataSignature)
            throw new FormatException($"BGI font {name}: font data signature '+' missing.");
        var count = ReadUInt16(font, 1);
        var first = font[4];
        var strokeOffset = ReadUInt16(font, 5);
        var scan = font[7] != 0;
        var top = (sbyte)font[8];
        var baseline = (sbyte)font[9];
        var bottom = (sbyte)font[10];
        var widthTable = DataHeaderLength + 2 * count;
        if (count == 0 || first + count > 256 || widthTable + count > strokeOffset || strokeOffset > font.Length)
            throw new FormatException(
                $"BGI font {name}: {count} characters from {first} with stroke offset {strokeOffset} do not fit {font.Length} bytes.");

        var glyphs = new BgiGlyph[count];
        for (var i = 0; i < count; i++)
        {
            var start = strokeOffset + ReadUInt16(font, DataHeaderLength + 2 * i);
            glyphs[i] = new BgiGlyph(font[widthTable + i], ReadStrokes(font, start, name, first + i));
        }

        return new BgiFont(name, description, major, minor, first, top, baseline, bottom, scan, glyphs,
            headerSize + dataSize);
    }

    /// <summary>
    /// Offset of the embedded font with the given name in a program file (signature followed by a header naming it),
    /// or -1.
    /// </summary>
    public static int FindEmbedded(ReadOnlySpan<byte> program, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length != 4)
            throw new ArgumentException("BGI font names have four characters.", nameof(name));
        var wanted = Encoding.Latin1.GetBytes(name);
        var offset = 0;
        while (offset < program.Length)
        {
            var hit = program[offset..].IndexOf(FileSignature);
            if (hit < 0)
                return -1;
            var start = offset + hit;
            var rest = program[start..];
            var eot = rest[..Math.Min(rest.Length, 0x80)].IndexOf(EndOfText);
            if (eot >= 0 && eot + 7 <= rest.Length && rest.Slice(eot + 3, 4).SequenceEqual(wanted))
                return start;
            offset = start + 1;
        }

        return -1;
    }

    /// <summary>Reads the embedded font with the given name from a program file.</summary>
    /// <exception cref="FormatException">The font is not embedded or damaged.</exception>
    public static BgiFont ParseEmbedded(ReadOnlySpan<byte> program, string name)
    {
        var offset = FindEmbedded(program, name);
        if (offset < 0)
            throw new FormatException($"BGI font {name} not found in the program file.");
        return Parse(program[offset..]);
    }

    private static BgiStroke[] ReadStrokes(ReadOnlySpan<byte> font, int position, string name, int code)
    {
        var strokes = new List<BgiStroke>();
        while (true)
        {
            if (position + 2 > font.Length)
                throw new FormatException($"BGI font {name}: strokes of character {code} run past the end of the data.");
            var first = font[position];
            var second = font[position + 1];
            position += 2;
            // [ORIGINAL] BOMB.EXE 0x1331E: op = (bit 7 of the first byte) * 2 + bit 7 of the second byte;
            // 0x133A9: x and y are the low seven bits, sign-extended from bit 6.
            var op = ((first & 0x80) != 0 ? 2 : 0) + ((second & 0x80) != 0 ? 1 : 0);
            if (op == 0)
                return strokes.ToArray();
            strokes.Add(new BgiStroke(op switch { 1 => BgiStrokeOp.Scan, 2 => BgiStrokeOp.Move, _ => BgiStrokeOp.Draw },
                SignExtend7(first), SignExtend7(second)));
        }
    }

    private static int SignExtend7(byte value) => (value & 0x40) != 0 ? (value & 0x7F) - 0x80 : value & 0x7F;

    private static int ReadUInt16(ReadOnlySpan<byte> data, int offset) => data[offset] | data[offset + 1] << 8;
}
