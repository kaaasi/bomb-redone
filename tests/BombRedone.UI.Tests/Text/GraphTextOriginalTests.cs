using System.Security.Cryptography;
using BombRedone.Formats;
using BombRedone.Tests.Support;
using BombRedone.UI.Text;

namespace BombRedone.UI.Tests.Text;

/// <summary>Texts drawn with the fonts from the original BOMB.EXE.</summary>
public class GraphTextOriginalTests
{
    private static readonly Lazy<IReadOnlyDictionary<string, BgiFont>> Fonts =
        new(() => BgiFontLibrary.Load(TestGameData.Load()));

    private static (int Left, int Top, int Right, int Bottom) Bounds(Surface surface)
    {
        int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        for (var y = 0; y < surface.Height; y++)
        for (var x = 0; x < surface.Width; x++)
        {
            if (surface[x, y] == 0)
                continue;
            left = Math.Min(left, x);
            right = Math.Max(right, x);
            top = Math.Min(top, y);
            bottom = Math.Max(bottom, y);
        }

        return (left, top, right, bottom);
    }

    /// <summary>
    /// Main menu header as drawn by O2:06BD (BOMB.OVR 0x00CA5): SetColor(14), SetTextStyle(EURO, horizontal, 1),
    /// SetTextJustify(center, center), OutTextXY at (163, 23), (162, 22), (161, 21), then SetColor(12) and (160, 20).
    /// Text: "TANK WARS  " (BOMB.OVR 0x00C99) + version string (DS:0056, BOMB.EXE 0x16446: length 4, "3.2" and NUL).
    /// </summary>
    private static Surface MenuHeader()
    {
        var surface = new Surface();
        var text = new GraphText();
        text.SetTextStyle(Fonts.Value[BgiFontLibrary.Euro], TextDirection.Horizontal, 1);
        text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Center);
        const string header = "TANK WARS  3.2\0";
        text.Color = 14;
        text.OutTextXY(surface, 163, 23, header);
        text.OutTextXY(surface, 162, 22, header);
        text.OutTextXY(surface, 161, 21, header);
        text.Color = 12;
        text.OutTextXY(surface, 160, 20, header);
        return surface;
    }

    [GameDataFact]
    public void MenuHeaderLiesWhereTheVideoShowsIt()
    {
        var surface = MenuHeader();
        // [APPROX] Longplay frame 00009 (576 x 360, factor 1.8): text pixels from x 50..519 and y 18..70, i.e.
        // 27.8..288.3 and 10.0..38.9 in 320 x 200. The video is scaled and compressed, so one pixel of tolerance.
        var (left, top, right, bottom) = Bounds(surface);
        Assert.InRange(left, 27, 29);
        Assert.InRange(right, 287, 289);
        Assert.InRange(top, 10, 11);
        Assert.InRange(bottom, 38, 40);
        Assert.Equal((28, 11, 288, 39), (left, top, right, bottom));
    }

    [GameDataFact]
    public void MenuEntryLiesWhereTheVideoShowsIt()
    {
        // O2:1E7F (BOMB.OVR 0x02467): SetTextStyle(SmallFont, horizontal, 1), then SetUserCharSize(1, 1, 1, 1)
        // (0E26:16E3, BOMB.OVR 0x024B8), SetTextJustify(center, center), OutTextXY(160, 95, "Sound Options").
        var surface = new Surface();
        var text = new GraphText { Color = 10 };
        text.SetTextStyle(Fonts.Value[BgiFontLibrary.Small], TextDirection.Horizontal, 1);
        text.SetUserCharSize(1, 1, 1, 1);
        text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Center);
        text.OutTextXY(surface, 160, 95, "Sound Options");
        // [APPROX] Longplay frame 00009: green text pixels at 124.4..196.1 x 92.2..99.4 (320 x 200); two pixels of
        // tolerance because the thin strokes are blurred in the video.
        var (left, top, right, bottom) = Bounds(surface);
        Assert.InRange(left, 122, 126);
        Assert.InRange(right, 194, 198);
        Assert.InRange(top, 90, 94);
        Assert.InRange(bottom, 97, 101);
    }

    [GameDataFact]
    public void MenuHeaderMeasures()
    {
        var text = new GraphText();
        text.SetTextStyle(Fonts.Value[BgiFontLibrary.Euro], TextDirection.Horizontal, 1);
        Assert.Equal(266, text.TextWidth("TANK WARS  3.2\0"));
        Assert.Equal(33, text.TextHeight("TANK WARS  3.2\0"));
    }

    [GameDataFact]
    public void TinyLowercaseDrawsNothingButMovesTheOrigin()
    {
        // TINY has no lowercase letters: 'b'..'z' are a bare move to (3, 0) with width 0 in the table, so TextWidth
        // ignores them while the output moves on by three units each.
        var tiny = Fonts.Value[BgiFontLibrary.Tiny];
        var text = new GraphText();
        text.SetTextStyle(tiny, TextDirection.Horizontal, 4);
        text.SetTextJustify(HorizontalJustify.Left, VerticalJustify.Top);
        Assert.Equal(tiny.GetGlyph((byte)'A')!.Width, text.TextWidth("bcA"));
        var plain = new Surface();
        text.OutTextXY(plain, 10, 0, "A");
        var shifted = new Surface();
        text.OutTextXY(shifted, 10, 0, "bcA");
        Assert.Equal(Bounds(plain).Left + 6, Bounds(shifted).Left);
    }

    /// <summary>
    /// Regression checksums (SHA-256 of the palette indices) of probe texts in the fonts and sizes the program uses
    /// (docs/analyse/re-grundlagen.md section 4). They lock the current output; they are not taken from the original.
    /// </summary>
    [GameDataTheory]
    [InlineData("menu-header", "1D6315E3FFD57484C89818238CF116037ECE7690BFD5D385CDE86C4B7A6CFB7F")]
    [InlineData("LITT", "8C20BED2E339B606E2898AD4AC410B7D00E9BCA1E147FAFE913C30C5F28DC0D2")]
    [InlineData("TINY", "F19B7ED10E2F79157BC04FF729AF493934380CB8DFA5845424FA8DBC527AACD7")]
    [InlineData("EURO4", "EFC4E00C05E946BBFAB210813410AC29C1B92B1E20DDB9153A95D875DAC8656F")]
    [InlineData("LITT-vertical", "7440A9A1FEA7FDB0BACEAAE6A7BA4714EAEBC20FABA739437EABA5CBFC261DC4")]
    public void ProbeTextsKeepTheirChecksums(string probe, string sha256)
    {
        var surface = probe == "menu-header" ? MenuHeader() : Probe(probe);
        var actual = Convert.ToHexString(SHA256.HashData(surface.Pixels));
        Assert.True(sha256 == actual, $"{probe}: expected {sha256}, got {actual}");
    }

    private static Surface Probe(string probe)
    {
        var surface = new Surface();
        var text = new GraphText { Color = 15 };
        text.SetTextJustify(HorizontalJustify.Left, VerticalJustify.Top);
        switch (probe)
        {
            case "LITT":
                text.SetTextStyle(Fonts.Value[BgiFontLibrary.Small], TextDirection.Horizontal, 1);
                text.OutTextXY(surface, 2, 2, "Sound Options  Game Options  Players  Games");
                text.SetTextStyle(Fonts.Value[BgiFontLibrary.Small], TextDirection.Horizontal, 4);
                text.OutTextXY(surface, 2, 30, "0123456789 !\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~");
                break;
            case "TINY":
                text.SetTextStyle(Fonts.Value[BgiFontLibrary.Tiny], TextDirection.Horizontal, 4);
                text.OutTextXY(surface, 2, 2, "ABCDEFGHIJKLMNOPQRSTUVWXYZ");
                text.OutTextXY(surface, 2, 40, "abcdefghijklmnopqrstuvwxyz 0123456789");
                break;
            case "EURO4":
                text.SetTextStyle(Fonts.Value[BgiFontLibrary.Euro], TextDirection.Horizontal, 4);
                text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Center);
                text.OutTextXY(surface, 160, 100, "Purchase");
                break;
            case "LITT-vertical":
                text.SetTextStyle(Fonts.Value[BgiFontLibrary.Small], TextDirection.Vertical, 1);
                text.SetTextJustify(HorizontalJustify.Left, VerticalJustify.Center);
                text.OutTextXY(surface, 2, 100, "ACCELERATE");
                break;
        }

        return surface;
    }
}
