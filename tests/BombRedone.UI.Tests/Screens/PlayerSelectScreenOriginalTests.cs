using System.Security.Cryptography;
using System.Text;
using BombRedone.Core.Setup;
using BombRedone.Formats;
using BombRedone.Tests.Support;
using BombRedone.UI.Screens;

namespace BombRedone.UI.Tests.Screens;

/// <summary>
/// Player selection with the fonts of BOMB.EXE and the 8 x 8 substitute. [APPROX] No DOSBox image of the screen yet:
/// the checksums fix the current result; the layout was compared with Longplay frames 00027, 00028 and 00031 by
/// position and shape (docs/anforderungen/spielerauswahl.md).
/// </summary>
public class PlayerSelectScreenOriginalTests
{
    private static string Sha(ReadOnlySpan<byte> data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    private static PlayerSelectScreen Create(int players, PlayerRoster? roster = null)
    {
        var fonts = MenuFonts.Load(TestGameData.Load(), BitmapFontSubstitute.Create());
        var menu = new MenuScreen(fonts, new MenuSettings(Players: players), _ => 1, (_, m) => m);
        return new PlayerSelectScreen(menu, roster ?? new PlayerRoster(), _ => 0, _ => null);
    }

    private static void Press(PlayerSelectScreen screen, params ushort[] keys) =>
        screen.Update(TimeSpan.Zero, new ScreenInput(KeyPressed: true) { Keys = keys });

    private static int Count(PlayerSelectScreen screen, byte color, int x1, int y1, int x2, int y2)
    {
        var count = 0;
        for (var y = y1; y <= y2; y++)
            for (var x = x1; x <= x2; x++)
                if (screen.Screen[x, y] == color)
                    count++;
        return count;
    }

    [GameDataFact]
    public void TextsAreThePascalStringsOfBombOvr()
    {
        var ovr = File.ReadAllBytes(TestGameData.Load().Find("BOMB.OVR")!);
        Assert.Equal(27, PlayerSelectScreen.OriginalTexts.Count);
        foreach (var (at, text) in PlayerSelectScreen.OriginalTexts)
            Assert.Equal(text, Encoding.Latin1.GetString(ovr, at + 1, ovr[at]));
    }

    [GameDataTheory]
    [InlineData(2, "")]
    [InlineData(10, "")]
    [InlineData(2, "c")]
    [InlineData(2, "n")]
    public void ScreenMatchesTheRecordedChecksum(int players, string state)
    {
        var screen = Create(players);
        if (state == "c")
            Press(screen, BiosKey.Char('c'));
        if (state == "n")
            Press(screen, BiosKey.Enter, BiosKey.Char('J'));
        Assert.Equal(Expected[(players, state)], Sha(screen.Screen.Pixels));
    }

    private static readonly Dictionary<(int, string), string> Expected = new()
    {
        [(2, "")] = "cb95386a206ad0992acbb30108636675826966db8b6391cdb239e0e2ce13330b",
        [(10, "")] = "f9535ad09bcb38244e02dc5276474c9a61f07bd0ac5a6853cf16c5ed7614ae2f",
        [(2, "c")] = "4b0b3bc237a874b9c059199aee519f277a63fbd57f0135e79904a87cd4b4b966",
        [(2, "n")] = "d593c9f1f16666f1f64e50cfed1cec5341c05c5eef15d6eb372b0f936a21a59c",
    };

    [GameDataFact]
    public void TextsAreDrawnInTheirColours()
    {
        var screen = Create(2);
        var (x1, y1, x2, _) = PlayerSelectScreen.Box(1);
        Assert.InRange(Count(screen, 20, x1, y1, x2, y1 + 12), 20, 300);
        Assert.InRange(Count(screen, 9, x1, y1 + 14, x2, y1 + 24), 20, 300);
        Assert.Equal(0, Count(screen, 9, x1, y1 + 25, x2, y1 + 45));
        (x1, y1, x2, _) = PlayerSelectScreen.Box(2);
        Assert.InRange(Count(screen, 21, x1, y1, x2, y1 + 12), 20, 300);
        (x1, y1, x2, _) = PlayerSelectScreen.Box(3);
        Assert.Equal(0, Count(screen, 22, x1, y1, x2, y1 + 12));
        Assert.InRange(Count(screen, 12, 71, 171, 248, 197), 20, 2000);
        Assert.InRange(Count(screen, 14, 71, 171, 248, 197), 100, 4000);
    }

    [GameDataFact]
    public void LongNamesAreSqueezedIntoTheNameBox()
    {
        var roster = new PlayerRoster();
        roster[1].Name = "WWWWWWWWWWWWW";
        var screen = Create(2, roster);
        var (x1, y1, x2, _) = PlayerSelectScreen.Box(1);
        Assert.Equal(0, Count(screen, 20, 0, y1, x1 + 2, y1 + 12));
        Assert.Equal(0, Count(screen, 20, x2 - 2, y1, 319, y1 + 12));
        Assert.InRange(Count(screen, 20, x1, y1, x2, y1 + 12), 20, 400);
    }

    [GameDataFact]
    public void ChangingTheControlRecoloursOnlyTheWords()
    {
        var screen = Create(2);
        var before = screen.Screen.Pixels.ToArray();
        Press(screen, BiosKey.Char('m'), BiosKey.Char('k'));
        Assert.Equal(before, screen.Screen.Pixels);
    }
}
