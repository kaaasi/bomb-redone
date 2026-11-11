using System.Security.Cryptography;
using BombRedone.Formats;
using BombRedone.Tests.Support;
using BombRedone.UI.Screens;

namespace BombRedone.UI.Tests.Screens;

/// <summary>
/// Main menu with the fonts of BOMB.EXE. The 8 x 8 font is the built-in substitute so the checksums do not depend on
/// the optional reference font. [APPROX] No DOSBox image of the menu yet: the checksums fix the current result, the
/// layout was compared with Longplay frame 00010 by position and shape (docs/anforderungen/menue.md).
/// </summary>
public class MenuScreenOriginalTests
{
    private static string Sha(ReadOnlySpan<byte> data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    private static MenuScreen Create(int pick) =>
        new(MenuFonts.Load(TestGameData.Load(), BitmapFontSubstitute.Create()), new MenuSettings(), _ => pick, (_, m) => m);

    private static int Count(MenuScreen menu, byte color, int x1, int y1, int x2, int y2)
    {
        var count = 0;
        for (var y = y1; y <= y2; y++)
            for (var x = x1; x <= x2; x++)
                if (menu.Screen[x, y] == color)
                    count++;
        return count;
    }

    [GameDataTheory]
    [InlineData(0, "a8c28cc2a08c0babfd002580b285cfbfc1f7f71f7e77bd5fcb7e5c15f7c10876")]
    [InlineData(1, "425b4711b271b474bee58af1e11e0b2ea31d6beef90dd8dec9d9d77f10d55d50")]
    public void ScreenMatchesTheRecordedChecksum(int pick, string sha)
    {
        Assert.Equal(sha, Sha(Create(pick).Screen.Pixels));
    }

    [GameDataFact]
    public void TextsAreDrawnInTheirColoursAndBoxes()
    {
        var menu = Create(1);
        Assert.InRange(Count(menu, 12, 12, 8, 308, 40), 50, 5000);
        Assert.InRange(Count(menu, 14, 12, 8, 308, 40), 50, 5000);
        Assert.InRange(Count(menu, 1, 118, 63, 202, 77), 30, 500);
        Assert.InRange(Count(menu, 10, 122, 91, 198, 101), 30, 500);
        Assert.InRange(Count(menu, 13, 122, 171, 198, 181), 10, 500);
        Assert.InRange(Count(menu, 14, 122, 131, 198, 141), 3, 100);
        Assert.InRange(Count(menu, 12, 20, 145, 80, 155), 3, 50);
        // Nothing is drawn outside the header, caption, panel and Load Game button except the background.
        Assert.Equal(0, Count(menu, 10, 210, 50, 319, 199));
    }

    [GameDataFact]
    public void PlayersNumberIsRedrawnLikeTheFirstDrawing()
    {
        var menu = Create(1);
        var before = menu.Screen.Pixels.ToArray();
        foreach (var key in new[] { BiosKey.Down, BiosKey.Down, BiosKey.Right, BiosKey.Left })
            menu.Update(TimeSpan.Zero, new ScreenInput(KeyPressed: true) { Keys = [key] });
        Assert.Equal(2, menu.Players);
        var after = menu.Screen.Pixels;
        // Markers moved from entry 0 to entry 2; everything else is as before.
        var changed = Enumerable.Range(0, before.Length).Where(i => before[i] != after[i])
            .Select(i => (i % 320, i / 320)).ToList();
        Assert.All(changed, p => Assert.True(p.Item2 is >= 94 and <= 96 or >= 134 and <= 136, p.ToString()));
    }
}
