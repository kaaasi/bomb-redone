using BombRedone.Core.Economy;
using BombRedone.UI.Screens;

namespace BombRedone.UI.Tests.Screens;

/// <summary>Status screen (O3:5AEF) with synthetic fonts: layout, ranking, keys, mouse and the automatic continue.</summary>
public class ScoreScreenTests
{
    private static readonly List<(ScoreChoice Choice, int Line)> Choices = [];

    private static ScoreLine Line(int number, string name, int wins, int score) =>
        new(new PlayerAccount(number, name) { Wins = wins, Score = score }, (63, 0, 0));

    private static ScoreScreen Create(int played = 1, int total = 10, bool buttons = true, int players = 2)
    {
        var lines = Enumerable.Range(1, players).Select(n => Line(n, $"P{n}", 0, n * 100)).ToList();
        Choices.Clear();
        return new ScoreScreen(ShopScreenTests.Fonts, lines, played, total, buttons, (c, l, s) =>
        {
            Choices.Add((c, l));
            return c is ScoreChoice.Add or ScoreChoice.Statistics ? s : null;
        });
    }

    private static IScreen? Press(ScoreScreen screen, ushort key) =>
        screen.Update(TimeSpan.Zero, new ScreenInput(KeyPressed: true) { Keys = [key] });

    [Theory]
    [InlineData(1, 80, 120)]
    [InlineData(2, 75, 125)]
    [InlineData(10, 35, 165)]
    public void BoxGrowsWithThePlayers(int players, int top, int bottom) =>
        Assert.Equal((top, bottom), ScoreScreen.Box(players));

    [Theory]
    [InlineData(1, 10, "1 Game Played out of 10")]
    [InlineData(2, 10, "2 Games Played out of 10")]
    public void TitleUsesSingularForOneGame(int played, int total, string title) =>
        Assert.Equal(title, ScoreScreen.Title(played, total));

    [Theory]
    [InlineData(1, " 1.")]
    [InlineData(10, "10.")]
    public void RankTextIsPaddedBelowTen(int rank, string text) => Assert.Equal(text, ScoreScreen.RankText(rank));

    [Fact]
    public void PlayersAreRankedAndColoured()
    {
        var screen = Create(players: 3);
        Assert.Equal([3, 2, 1], screen.Standings.Select(s => s.Player.Number));
        Assert.Equal(new byte[] { 63, 0, 0 }, screen.Dac.Slice(20 * 3, 3).ToArray());
    }

    [Fact]
    public void BackgroundIsMagentaStripes()
    {
        var screen = Create();
        Assert.Equal(30, screen.Background[0, 0]);
        Assert.Equal(59, screen.Background[319, 29]);
        Assert.Equal(30, screen.Background[5, 30]);
        Assert.Equal(new byte[] { 45, 0, 45 }, screen.Dac.Slice(45 * 3, 3).ToArray());
        Assert.Equal(MenuGraphics.FrameFace, screen.Screen[160, 100]);
        Assert.NotEqual(MenuGraphics.FrameFace, screen.Background[160, 100]);
    }

    [Fact]
    public void ButtonsSitBelowTheBox()
    {
        var screen = Create();
        Assert.Equal(MenuGraphics.FrameFace, screen.Screen[62, 135]); // Add (60..110, bottom 125 + 5..15)
        Assert.Equal(MenuGraphics.FrameFace, screen.Screen[184, 139]); // Continue
        Assert.Equal(MenuGraphics.FrameFace, screen.Screen[259, 131]); // Save
    }

    [Fact]
    public void WithoutButtonsTheScreenContinuesAfterTwoSeconds()
    {
        var screen = Create(buttons: false);
        Assert.NotEqual(MenuGraphics.FrameFace, screen.Screen[62, 135]);
        Assert.Same(screen, screen.Update(TimeSpan.FromMilliseconds(1999), ScreenInput.None));
        Assert.Null(screen.Update(TimeSpan.FromMilliseconds(1), ScreenInput.None));
        Assert.Equal([(ScoreChoice.Continue, 0)], Choices);
    }

    [Fact]
    public void LastGameAlwaysHasButtons()
    {
        var screen = Create(played: 10, total: 10, buttons: false);
        Assert.Same(screen, screen.Update(TimeSpan.FromSeconds(5), ScreenInput.None));
        Assert.Equal(MenuGraphics.FrameFace, screen.Screen[62, 135]);
    }

    [Theory]
    [InlineData('s', ScoreChoice.Save, 0)]
    [InlineData('A', ScoreChoice.Add, 0)]
    [InlineData('x', ScoreChoice.Continue, 0)]
    [InlineData('2', ScoreChoice.Statistics, 2)]
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
        Assert.Same(screen, Press(screen, BiosKey.Char('0')));
        Assert.Empty(Choices);
    }

    [Theory]
    [InlineData(160, 135, ScoreChoice.Continue)]
    [InlineData(85, 135, ScoreChoice.Add)]
    [InlineData(235, 135, ScoreChoice.Save)]
    public void ButtonsAnswerTheMouse(int x, int y, ScoreChoice choice)
    {
        var screen = Create();
        screen.Update(TimeSpan.Zero, new ScreenInput(Mouse: MouseButtons.Left) { MouseX = 2 * x, MouseY = y });
        Assert.Equal([(choice, 0)], Choices);
    }

    [Fact]
    public void ClickOnAPlayerLineOpensStatisticsAfterRelease()
    {
        var screen = Create();
        // Line 2: top 75 + 12 + 20 = 107 .. 116.
        screen.Update(TimeSpan.Zero, new ScreenInput(Mouse: MouseButtons.Left) { MouseX = 200, MouseY = 110 });
        Assert.Empty(Choices);
        screen.Update(TimeSpan.Zero, ScreenInput.None);
        Assert.Equal([(ScoreChoice.Statistics, 2)], Choices);
    }
}
