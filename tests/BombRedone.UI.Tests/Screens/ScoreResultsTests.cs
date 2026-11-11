using BombRedone.Core.Economy;
using BombRedone.UI.Screens;

namespace BombRedone.UI.Tests.Screens;

/// <summary>End result in text mode (O3:7566).</summary>
public class ScoreResultsTests
{
    [Fact]
    public void LinesFollowTheOriginalLayout()
    {
        var lines = ScoreResults.Build(
            [
                new PlayerAccount(2, "Lob & Shoot") { Wins = 2, Score = 900 },
                new PlayerAccount(1, "Javier") { Wins = 8, Score = 41230 },
            ], gamesCounter: 11);

        Assert.Equal(
            [
                "10 Games Played",
                "   And just in case you missed them:",
                "",
                "             Results ",
                "   Name            Score   Wins",
                " ────────────────────────────────",
                " 1. Javier         41230      8",
                " 2. Lob & Shoot      900      2",
                "",
                "",
            ],
            lines.Select(l => l.Text));
        byte[] colors = [6, 6, 6, 15, 15, 15, 10, 10, 10, 10];
        for (var i = 0; i < lines.Count; i++)
            Assert.All(lines[i].Colors, c => Assert.Equal(colors[i], c));
    }

    [Fact]
    public void OneGameIsSingular() =>
        Assert.Equal("1 Game Played", ScoreResults.Build([new PlayerAccount(1, "A")], 2)[0].Text);

    [Fact]
    public void LongNamesAreOverwrittenByTheColumns()
    {
        var line = ScoreResults.Build([new PlayerAccount(1, "ABCDEFGHIJKLMNOPQRSTUVWXYZ") { Score = 5, Wins = 1 }], 2)[6];
        Assert.Equal(" 1. ABCDEFGHIJKL       5UVW   1", line.Text);
    }

    [Fact]
    public void EveryCharacterHasAColour()
    {
        foreach (var line in ScoreResults.Build([new PlayerAccount(1, "A")], 3))
            Assert.Equal(line.Text.Length, line.Colors.Count);
    }
}
