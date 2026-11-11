using BombRedone.Formats;
using BombRedone.Tests.Support;

namespace BombRedone.Formats.Tests;

/// <summary>The three stroked fonts in the original BOMB.EXE (values read from the file, docs/formate/schriften.md).</summary>
public class BgiFontOriginalTests
{
    private static readonly Lazy<IReadOnlyDictionary<string, BgiFont>> Fonts =
        new(() => BgiFontLibrary.Load(TestGameData.Load()));

    [GameDataTheory]
    [InlineData("LITT", 223, 7, -2, 5131, 6, 6, 1231)]
    [InlineData("EURO", 223, 45, -10, 8467, 37, 29, 6177)]
    [InlineData("TINY", 96, 4, -7, 1540, 4, 3, 265)]
    public void ReadsTheEmbeddedFonts(string name, int count, int top, int bottom, int fileLength, int widthOfA,
        int widthOfSpace, int sumOfWidths)
    {
        var font = Fonts.Value[name];
        Assert.Equal(name, font.Name);
        Assert.Equal(32, font.FirstChar);
        Assert.Equal(count, font.CharCount);
        Assert.Equal(top, font.Top);
        Assert.Equal(0, font.Baseline);
        Assert.Equal(bottom, font.Bottom);
        Assert.False(font.HasScanFlag);
        Assert.Equal((1, 0), (font.MajorVersion, font.MinorVersion));
        Assert.Equal(fileLength, font.FileLength);
        Assert.Equal(widthOfA, font.GetGlyph((byte)'A')!.Width);
        Assert.Equal(widthOfSpace, font.GetGlyph((byte)' ')!.Width);
        var sum = 0;
        for (var code = 32; code < 32 + count; code++) sum += font.GetGlyph((byte)code)!.Width;
        Assert.Equal(sumOfWidths, sum);
    }

    [GameDataTheory]
    [InlineData("LITT", 0)]
    [InlineData("EURO", 0)]
    [InlineData("TINY", 27)]
    public void LastStrokeAndWidthTableDisagreeOnlyInTiny(string name, int disagreeing)
    {
        // Every glyph ends with a move; the kernel advances by that point, TextWidth sums the width table (GraphText).
        // In TINY 27 characters (among them the empty lowercase letters b..z) have width 0 in the table but end with a move to (3, 0).
        var font = Fonts.Value[name];
        var found = 0;
        for (var code = font.FirstChar; code < font.FirstChar + font.CharCount; code++)
        {
            var glyph = font.GetGlyph((byte)code)!;
            var last = glyph.Strokes[^1];
            Assert.Equal(BgiStrokeOp.Move, last.Op);
            Assert.Equal(0, last.Y);
            if (last.X == glyph.Width)
                continue;
            found++;
            Assert.Equal((0, 3), (glyph.Width, last.X));
        }

        Assert.Equal(disagreeing, found);
    }

    [GameDataFact]
    public void FontsAreFoundByNameAtTheDocumentedOffsets()
    {
        var program = File.ReadAllBytes(TestGameData.Load().Require(BgiFontLibrary.ProgramFile));
        foreach (var (name, offset) in BgiFontLibrary.ProgramOffsets)
            Assert.Equal(offset, BgiFont.FindEmbedded(program, name));
    }
}
