using System.Security.Cryptography;
using BombRedone.Core.Economy;
using BombRedone.Formats;
using BombRedone.Tests.Support;
using BombRedone.UI.Screens;

namespace BombRedone.UI.Tests.Screens;

/// <summary>
/// Team status screen with the fonts of BOMB.EXE over the background of the single status screen. [APPROX] There is no
/// recording of Team Play (not in the videos, docs/anforderungen/bildschirme.md B5): the checksum fixes the current result
/// of the disassembly (docs/anforderungen/wertung-einkauf.md §7).
/// </summary>
public class ScoreTeamScreenOriginalTests
{
    private static ScoreTeamScreen Create()
    {
        var fonts = ShopFonts.Load(TestGameData.Load(), BitmapFontSubstitute.Create());
        var javier = new PlayerAccount(1, "Javier") { Wins = 1, Score = 4260 };
        var lob = new PlayerAccount(2, "Lob & Shoot");
        var ann = new PlayerAccount(3, "Ann") { Score = 1200 };
        var single = new ScoreScreen(fonts,
            [new ScoreLine(javier, (63, 0, 0)), new ScoreLine(lob, (0, 63, 0)), new ScoreLine(ann, (0, 0, 63))],
            1, 10, buttons: true, (_, _, _) => null);
        return new ScoreTeamScreen(fonts, single.Background, single.Dac,
            [new Team(1, "Red Team", [javier, ann]), new Team(2, "Blue Team", [lob])], 1, 10, buttons: true,
            (_, _, _) => null);
    }

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
    public void LinesAreInColourFourteen()
    {
        var screen = Create().Screen;
        // Line 1 at y = top 75 + 20 = 95, line 2 at 105; heads in colour 17 at y = 80.
        Assert.InRange(Count(screen, ScoreTeamScreen.LineColor, 50, 95, 270, 104), 30, 600);
        Assert.InRange(Count(screen, ScoreTeamScreen.LineColor, 50, 105, 270, 114), 30, 600);
        Assert.InRange(Count(screen, MenuGraphics.FrameDark, 50, 80, 270, 89), 30, 600);
        Assert.InRange(Count(screen, 6, 69, 55, 251, 70), 30, 800);
    }

    [GameDataFact]
    public void ScreenMatchesTheRecordedChecksum()
    {
        var hash = Convert.ToHexString(SHA256.HashData(Create().Screen.Pixels)).ToLowerInvariant();
        Assert.Equal("ed709d4f88469d7a6207752e172522439856c9f673ed600956f8109d4036569a", hash);
    }
}
