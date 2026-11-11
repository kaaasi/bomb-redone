using BombRedone.Formats;
using G = BombRedone.Formats.Tests.SyntheticFont.Glyph;

namespace BombRedone.Formats.Tests;

public class BgiFontTests
{
    private static byte[] TwoGlyphs() => SyntheticFont.Build("TEST", 65,
    [
        new G(5, ('M', 0, 0), ('D', 4, 6), ('S', 1, 1), ('M', 5, 0)),
        new G(3, ('M', 0, -2), ('D', -1, 63), ('M', 3, 0)),
    ], top: 6, bottom: -2);

    [Fact]
    public void ParsesHeaderFields()
    {
        var font = BgiFont.Parse(TwoGlyphs());
        Assert.Equal("TEST", font.Name);
        Assert.Equal("BGI Stroked Font V1.1 - synthetic", font.Description);
        Assert.Equal(65, font.FirstChar);
        Assert.Equal(2, font.CharCount);
        Assert.Equal(6, font.Top);
        Assert.Equal(0, font.Baseline);
        Assert.Equal(-2, font.Bottom);
        Assert.False(font.HasScanFlag);
        Assert.Equal((1, 0), (font.MajorVersion, font.MinorVersion));
        Assert.Equal(TwoGlyphs().Length, font.FileLength);
    }

    [Fact]
    public void DecodesStrokesWithSignedSevenBitCoordinates()
    {
        var font = BgiFont.Parse(TwoGlyphs());
        var a = font.GetGlyph((byte)'A')!;
        Assert.Equal(5, a.Width);
        Assert.Equal(
            [new BgiStroke(BgiStrokeOp.Move, 0, 0), new BgiStroke(BgiStrokeOp.Draw, 4, 6),
             new BgiStroke(BgiStrokeOp.Scan, 1, 1), new BgiStroke(BgiStrokeOp.Move, 5, 0)],
            a.Strokes);
        var b = font.GetGlyph((byte)'B')!;
        Assert.Equal(3, b.Width);
        // -2 and -1 are stored as 7Eh and 7Fh, 63 as 3Fh (largest positive value).
        Assert.Equal(
            [new BgiStroke(BgiStrokeOp.Move, 0, -2), new BgiStroke(BgiStrokeOp.Draw, -1, 63),
             new BgiStroke(BgiStrokeOp.Move, 3, 0)],
            b.Strokes);
    }

    [Theory]
    [InlineData(64)]
    [InlineData(67)]
    [InlineData(0)]
    public void CodesOutsideTheFontHaveNoGlyph(byte code)
    {
        var font = BgiFont.Parse(TwoGlyphs());
        Assert.False(font.Contains(code));
        Assert.Null(font.GetGlyph(code));
    }

    [Fact]
    public void TrailingBytesAreIgnored()
    {
        var data = TwoGlyphs().Concat(new byte[100]).ToArray();
        Assert.Equal(2, BgiFont.Parse(data).CharCount);
    }

    [Fact]
    public void RejectsMissingSignature()
    {
        var data = TwoGlyphs();
        data[0] = (byte)'X';
        Assert.Throws<FormatException>(() => BgiFont.Parse(data));
    }

    [Fact]
    public void RejectsMissingEndOfDescription()
    {
        var data = TwoGlyphs().Select(b => b == 0x1A ? (byte)0x20 : b).ToArray();
        Assert.Throws<FormatException>(() => BgiFont.Parse(data));
    }

    [Fact]
    public void RejectsTruncatedData()
    {
        var data = TwoGlyphs();
        Assert.Throws<FormatException>(() => BgiFont.Parse(data.AsSpan(0, data.Length - 1)));
    }

    [Fact]
    public void RejectsMissingDataSignature()
    {
        var data = TwoGlyphs();
        data[0x80] = (byte)'-';
        Assert.Throws<FormatException>(() => BgiFont.Parse(data));
    }

    [Fact]
    public void RejectsStrokesWithoutEndCommand()
    {
        // The last glyph's end command (two zero bytes) is the end of the data; replacing it lets the strokes run past it.
        var data = TwoGlyphs();
        data[^2] = 0x80;
        data[^1] = 0x80;
        var error = Assert.Throws<FormatException>(() => BgiFont.Parse(data));
        Assert.Contains("past the end", error.Message);
    }

    [Fact]
    public void RejectsInconsistentStrokeOffset()
    {
        var data = TwoGlyphs();
        data[0x80 + 5] = 2; // stroke offset inside the offset table
        Assert.Throws<FormatException>(() => BgiFont.Parse(data));
    }

    [Fact]
    public void FindsEmbeddedFontByName()
    {
        var other = SyntheticFont.Build("ABCD", 32, [new G(1, ('M', 1, 0))]);
        var wanted = TwoGlyphs();
        var program = new byte[37].Concat(other).Concat(new byte[11]).Concat(wanted).Concat(new byte[5]).ToArray();
        var offset = BgiFont.FindEmbedded(program, "TEST");
        Assert.Equal(37 + other.Length + 11, offset);
        Assert.Equal(37, BgiFont.FindEmbedded(program, "ABCD"));
        Assert.Equal(-1, BgiFont.FindEmbedded(program, "NONE"));
        Assert.Equal("TEST", BgiFont.ParseEmbedded(program, "TEST").Name);
        Assert.Throws<FormatException>(() => BgiFont.ParseEmbedded(program, "NONE"));
    }

    [Fact]
    public void LibraryRejectsAProgramWithoutTheFontAtItsOffset()
    {
        Assert.Throws<FormatException>(() => BgiFontLibrary.Parse(new byte[0x10000], BgiFontLibrary.Euro));
        Assert.Throws<ArgumentException>(() => BgiFontLibrary.Parse(new byte[16], "SCRI"));
    }

    [Fact]
    public void LibraryReadsAFontAtItsKnownOffset()
    {
        var program = new byte[BgiFontLibrary.ProgramOffsets[BgiFontLibrary.Tiny]]
            .Concat(SyntheticFont.Build("TINY", 32, [new G(4, ('M', 4, 0))])).ToArray();
        Assert.Equal(4, BgiFontLibrary.Parse(program, BgiFontLibrary.Tiny).GetGlyph(32)!.Width);
    }
}
