using BombRedone.Core.Economy;
using BombRedone.UI.Screens;

namespace BombRedone.UI.Tests.Screens;

/// <summary>Team status screen (O3:6696) with synthetic fonts: layout, texts, keys, mouse and the automatic continue.</summary>
public class ScoreTeamScreenTests
{
    private static readonly List<(ScoreChoice Choice, int Line)> Choices = [];

    private static ScoreTeamScreen Create(int played = 1, int total = 10, bool buttons = true, Surface? background = null)
    {
        Choices.Clear();
        var dac = new byte[768];
        dac[3 * 7] = 42;
        return new ScoreTeamScreen(ShopScreenTests.Fonts, background ?? new Surface(), dac,
            [
                new Team(1, "A", [new PlayerAccount(1, "P1") { Wins = 1, Score = 10 }, new PlayerAccount(2, "P2")]),
                new Team(2, "B", [new PlayerAccount(3, "P3") { Wins = 1, Score = 20 }]),
            ], played, total, buttons, (c, l, s) =>
            {
                Choices.Add((c, l));
                return c is ScoreChoice.Statistics ? s : null;
            });
    }

    private static IScreen? Press(ScoreTeamScreen screen, ushort key) =>
        screen.Update(TimeSpan.Zero, new ScreenInput(KeyPressed: true) { Keys = [key] });

    [Theory]
    [InlineData(1, 80, 120)]
    [InlineData(2, 75, 125)]
    public void BoxGrowsWithTheTeams(int teams, int top, int bottom) =>
        Assert.Equal((top, bottom), ScoreTeamScreen.Box(teams));

    [Theory]
    [InlineData(4260, "      4260")]
    [InlineData(0, "         0")]
    public void ScoreIsTenCharactersWide(long score, string text) => Assert.Equal(text, ScoreTeamScreen.ScoreText(score));

    [Theory]
    [InlineData(1, 2, " 0.50")]
    [InlineData(1, 3, " 0.33")]
    [InlineData(2, 3, " 0.67")]
    [InlineData(1, 8, " 0.13")]
    [InlineData(25, 2, "12.50")]
    public void WinsAreAveragedWithTwoDecimals(int wins, int members, string text) =>
        Assert.Equal(text, ScoreTeamScreen.WinsText(wins, members));

    [Fact]
    public void TeamsAreRankedByAverageWins()
    {
        var screen = Create();
        Assert.Equal(["B", "A"], screen.Ranking.Lines.Select(l => l.Team.Name));
    }

    [Fact]
    public void DrawsOverThePreviousScreenAndKeepsItsPalette()
    {
        var background = new Surface();
        background.Pixels.AsSpan().Fill(33);
        var screen = Create(background: background);
        Assert.Equal(33, screen.Screen[0, 0]);
        Assert.Equal(MenuGraphics.FrameFace, screen.Screen[160, 100]);
        Assert.Equal(MenuGraphics.FrameFace, screen.Screen[100, 65]); // title box 69..251, top − 20..top − 5
        Assert.Equal(33, screen.Screen[66, 65]);
        Assert.Equal(42, screen.Dac[21]);
        Assert.Equal(MenuGraphics.FrameDark, screen.Screen[50, 91]); // line under the heads at top + 16
    }

    [Fact]
    public void ButtonsSitBelowTheBox()
    {
        var screen = Create();
        Assert.Equal(MenuGraphics.FrameFace, screen.Screen[86, 131]); // Continue 85..135, bottom 125 + 5..15
        Assert.Equal(MenuGraphics.FrameFace, screen.Screen[234, 131]); // Save 185..235
        Assert.NotEqual(MenuGraphics.FrameFace, screen.Screen[62, 135]); // no Add
    }

    [Fact]
    public void WithoutButtonsTheScreenContinuesAfterFiveSeconds()
    {
        var screen = Create(buttons: false);
        Assert.NotEqual(MenuGraphics.FrameFace, screen.Screen[86, 131]);
        Assert.Same(screen, screen.Update(TimeSpan.FromMilliseconds(4999), ScreenInput.None));
        Assert.Null(screen.Update(TimeSpan.FromMilliseconds(1), ScreenInput.None));
        Assert.Equal([(ScoreChoice.Continue, 0)], Choices);
    }

    [Fact]
    public void LastGameWithoutButtonsStillContinuesByItself()
    {
        var screen = Create(played: 10, total: 10, buttons: false);
        Assert.Null(screen.Update(TimeSpan.FromSeconds(5), ScreenInput.None));
    }

    [Theory]
    [InlineData('S', ScoreChoice.Save, 0)]
    [InlineData('a', ScoreChoice.Continue, 0)]
    [InlineData('3', ScoreChoice.Statistics, 3)]
    public void KeysChoose(char key, ScoreChoice choice, int line)
    {
        var screen = Create();
        Press(screen, BiosKey.Char(key));
        Assert.Equal([(choice, line)], Choices);
    }

    [Fact]
    public void DigitBeyondThePlayersDoesNothing()
    {
        var screen = Create();
        Assert.Same(screen, Press(screen, BiosKey.Char('4')));
        Assert.Empty(Choices);
    }

    [Theory]
    [InlineData(110, 135, ScoreChoice.Continue)]
    [InlineData(210, 135, ScoreChoice.Save)]
    public void ButtonsAnswerTheMouse(int x, int y, ScoreChoice choice)
    {
        var screen = Create();
        screen.Update(TimeSpan.Zero, new ScreenInput(Mouse: MouseButtons.Right) { MouseX = 2 * x, MouseY = y });
        Assert.Equal([(choice, 0)], Choices);
    }

    [Fact]
    public void ClickOnATeamLineOpensStatisticsAfterRelease()
    {
        var screen = Create();
        // Line 2: top 75 + 12 + 20 = 107 .. 116.
        screen.Update(TimeSpan.Zero, new ScreenInput(Mouse: MouseButtons.Left) { MouseX = 200, MouseY = 116 });
        Assert.Empty(Choices);
        screen.Update(TimeSpan.Zero, ScreenInput.None);
        Assert.Equal([(ScoreChoice.Statistics, 2)], Choices);
    }
}
