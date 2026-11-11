using System.Security.Cryptography;
using BombRedone.Core.Economy;
using BombRedone.Formats;
using BombRedone.Tests.Support;
using BombRedone.UI.Screens;

namespace BombRedone.UI.Tests.Screens;

/// <summary>
/// Status screen with the fonts of BOMB.EXE in the situation of Longplay frame 00064 ("1. Javier 1 4260",
/// "2. Lob &amp; Shoot 0 0", game 1 of 10). [APPROX] No DOSBox image yet: the checksum fixes the current result; layout,
/// texts and colours were compared with LP 00064 by position and shape (docs/anforderungen/wertung-einkauf.md §5).
/// </summary>
public class ScoreScreenOriginalTests
{
    private static ScoreScreen Create() => new(ShopFonts.Load(TestGameData.Load(), BitmapFontSubstitute.Create()),
        [
            new ScoreLine(new PlayerAccount(1, "Javier") { Wins = 1, Score = 4260 }, (63, 0, 0)),
            new ScoreLine(new PlayerAccount(2, "Lob & Shoot"), (0, 63, 0)),
        ], 1, 10, buttons: true, (_, _, _) => null);

    private static int Count(Surface surface, byte color, int x1, int y1, int x2, int y2)
    {
        var count = 0;
        for (var y = y1; y <= y2; y++)
            for (var x = x1; x <= x2; x++)
                if (surface[x, y] == color)
                    count++;
        return count;
    }

    [GameDataFact]
    public void LinesAreInThePlayerColours()
    {
        var screen = Create().Screen;
        // Line 1 (y 95..104): name and numbers in colour 20; line 2 (y 105..114) in colour 21.
        Assert.InRange(Count(screen, 20, 50, 95, 270, 104), 30, 600);
        Assert.InRange(Count(screen, 21, 50, 105, 270, 114), 30, 600);
        Assert.Equal(0, Count(screen, 21, 50, 95, 270, 103));
        // Title in colour 6 in the upper box.
        Assert.InRange(Count(screen, 6, 65, 55, 255, 70), 30, 800);
    }

    [GameDataFact]
    public void ScreenMatchesTheRecordedChecksum()
    {
        var hash = Convert.ToHexString(SHA256.HashData(Create().Screen.Pixels)).ToLowerInvariant();
        Assert.Equal("e17a25a9509dea91c12870f0767f59e77e63a92fa2fa579138a1bc5470326c87", hash);
    }
}
