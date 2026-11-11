using System.Text;

namespace BombRedone.UI.Tests.Text;

/// <summary>Builds BGI stroked font files from scratch (no original data) for the tests.</summary>
internal static class SyntheticFont
{
    /// <summary>One character: width and stroke commands (op 'M' move, 'D' draw, 'S' scan) with x, y.</summary>
    public sealed record Glyph(int Width, params (char Op, int X, int Y)[] Strokes);

    public static byte[] Build(string name, int first, IReadOnlyList<Glyph> glyphs, int top = 7, int bottom = -2,
        string description = "BGI Stroked Font V1.1 - synthetic", int headerSize = 0x80)
    {
        var strokes = new List<byte>();
        var offsets = new List<int>();
        foreach (var glyph in glyphs)
        {
            offsets.Add(strokes.Count);
            foreach (var (op, x, y) in glyph.Strokes)
            {
                var (bit1, bit2) = op switch { 'M' => (0x80, 0), 'D' => (0x80, 0x80), 'S' => (0, 0x80), _ => throw new ArgumentException(op.ToString()) };
                strokes.Add((byte)(bit1 | (x & 0x7F)));
                strokes.Add((byte)(bit2 | (y & 0x7F)));
            }

            strokes.Add(0);
            strokes.Add(0);
        }

        var strokeOffset = 16 + 3 * glyphs.Count;
        var data = new List<byte> { (byte)'+', (byte)glyphs.Count, (byte)(glyphs.Count >> 8), 0, (byte)first,
            (byte)strokeOffset, (byte)(strokeOffset >> 8), 0, (byte)(sbyte)top, 0, (byte)(sbyte)bottom, 0, 0, 0, 0, 0 };
        foreach (var offset in offsets) { data.Add((byte)offset); data.Add((byte)(offset >> 8)); }
        foreach (var glyph in glyphs) data.Add((byte)glyph.Width);
        data.AddRange(strokes);

        var file = new List<byte> { 0x50, 0x4B, 0x08, 0x08 };
        file.AddRange(Encoding.ASCII.GetBytes(description + "\r\n"));
        file.Add(0x1A);
        file.Add((byte)headerSize); file.Add((byte)(headerSize >> 8));
        file.AddRange(Encoding.ASCII.GetBytes(name));
        file.Add((byte)data.Count); file.Add((byte)(data.Count >> 8));
        file.AddRange([1, 0, 1, 0]);
        while (file.Count < headerSize) file.Add(0);
        file.AddRange(data);
        return file.ToArray();
    }
}
