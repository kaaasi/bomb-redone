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
        [(2, "")] = "e9863b3fee6bf98e166278beb805c951b893eb499ca832016eda5570e27a66ca",
        [(10, "")] = "d9a3d234bbb7a25853b8ad5695d3f53d8acc133e36eb516632e1480aa71c7cb9",
        [(2, "c")] = "9688b4877bfe2eed5eb3e444604073618bba4c641b37c184a0fc02fe2df222e9",
        [(2, "n")] = "4031da7a231101b652414911f2802fa75fba8a2dc54648beee11f64bb4f5c843",
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
