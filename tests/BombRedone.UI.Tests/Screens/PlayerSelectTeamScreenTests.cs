using BombRedone.Core.Setup;
using BombRedone.Formats;
using BombRedone.UI.Screens;
using BombRedone.UI.Tests.Text;

namespace BombRedone.UI.Tests.Screens;

/// <summary>Team setup and team names with synthetic fonts (no visible strokes): tanks, keys, mouse, check, names.</summary>
public class PlayerSelectTeamScreenTests
{
    private static readonly BgiFont Stub = BgiFont.Parse(SyntheticFont.Build("LITT", 32, [new SyntheticFont.Glyph(4)]));

    private static readonly MenuFonts Fonts = new(Stub, Stub, new BitmapFont(new byte[256 * 8]));

    private sealed class Harness
    {
        public Harness(int players = 3, bool mouse = true, Func<int, int>? random = null, PlayerRoster? roster = null)
        {
            Menu = new MenuScreen(Fonts, new MenuSettings(Players: players, MouseAvailable: mouse, PaletteRotation: false),
                _ => 1, (_, m) => m);
            Roster = roster ?? new PlayerRoster();
            Roster.Players = players;
            Options = new OptionsSettings { TeamPlay = true };
            Random = random ?? (_ => throw new InvalidOperationException());
            Current = new PlayerSelectTeamScreen(Menu, Roster, new Surface(), Roster.Start(_ => 0),
                new PlayerSelectTeams(Options, Stub), Random, Start);
        }

        public MenuScreen Menu { get; }

        public PlayerRoster Roster { get; }

        public OptionsSettings Options { get; }

        public Func<int, int> Random { get; }

        public IScreen? Current { get; private set; }

        public PlayerSelectTeamScreen Teams => (PlayerSelectTeamScreen)Current!;

        public GameSetup? Setup { get; private set; }

        public IScreen Next { get; } = new PlaceholderScreen();

        public void Press(params ushort[] keys) =>
            Current = Current!.Update(TimeSpan.Zero, new ScreenInput(KeyPressed: keys.Length > 0) { Keys = keys });

        public void Mouse(MouseButtons buttons, int x, int y, double ms = 0) =>
            Current = Current!.Update(TimeSpan.FromMilliseconds(ms), new ScreenInput(Mouse: buttons) { MouseX = x * 2, MouseY = y });

        public void Release() => Current = Current!.Update(TimeSpan.Zero, ScreenInput.None);

        private IScreen Start(GameSetup setup)
        {
            Setup = setup;
            return Next;
        }
    }

    private static byte TankColor(int player) => (byte)(PlayerSelectScreen.PlayerColorBase + player);

    private static bool TankAt(Surface screen, int player, int team)
    {
        var (x, y) = PlayerSelectTeamScreen.TankPoint(player, team);
        return screen[x, y] == TankColor(player) && screen[x, y - 8] == TankColor(player);
    }

    [Fact]
    public void TankHasTheShapeOfTheOriginal()
    {
        var surface = new Surface();
        PlayerSelectTeamScreen.DrawTank(surface, 100, 100, 7);
        string[] expected =
        [
            "....#....", // y − 8 … y − 5: barrel
            "....#....",
            "....#....",
            "....#....",
            "...###...", // y − 4
            ".#######.",
            ".#######.",
            "###.#.###", // y − 1
            "#########",
        ];
        for (var row = 0; row < expected.Length; row++)
        {
            for (var col = 0; col < 9; col++)
                Assert.Equal(expected[row][col] == '#' ? (byte)7 : (byte)0, surface[96 + col, 92 + row]);
        }

        Assert.Equal(0, surface[100, 91]);
    }

    [Fact]
    public void TanksStandInTheColumnsOfTheirTeams()
    {
        var roster = new PlayerRoster();
        roster[2].Team = 3;
        var h = new Harness(players: 3, roster: roster);
        Assert.True(TankAt(h.Teams.Screen, 1, 1));
        Assert.True(TankAt(h.Teams.Screen, 2, 3));
        Assert.True(TankAt(h.Teams.Screen, 3, 3));
        Assert.Equal(MenuGraphics.FrameFace, h.Teams.Screen[PlayerSelectTeamScreen.TankPoint(2, 2).X, PlayerSelectTeamScreen.TankPoint(2, 2).Y]);
    }

    [Fact]
    public void PanelAndButtonsAreDrawn()
    {
        var h = new Harness();
        Assert.Equal(MenuGraphics.FrameFace, h.Teams.Screen[100, 170]);
        Assert.Equal(15, h.Teams.Screen[58, 100]);
        Assert.Equal(MenuGraphics.FrameDark, h.Teams.Screen[262, 100]);
        Assert.Equal(MenuGraphics.FrameFace, h.Teams.Screen[160, 192]);
        Assert.Equal(MenuGraphics.FrameFace, h.Teams.Screen[160, 50]);
    }

    [Fact]
    public void RightAndLeftMoveTheSelectedTankWithinOneToTheCount()
    {
        var h = new Harness(players: 3);
        h.Press(BiosKey.Right);
        Assert.Equal(2, h.Roster[1].Team);
        Assert.True(TankAt(h.Teams.Screen, 1, 2));
        Assert.Equal(MenuGraphics.FrameFace, h.Teams.Screen[PlayerSelectTeamScreen.TankPoint(1, 1).X, PlayerSelectTeamScreen.TankPoint(1, 1).Y]);
        h.Press(BiosKey.Right, BiosKey.Right);
        Assert.Equal(3, h.Roster[1].Team);
        h.Press(BiosKey.Left, BiosKey.Left, BiosKey.Left);
        Assert.Equal(1, h.Roster[1].Team);
    }

    [Fact]
    public void UpAndDownSelectPlayersWithoutWrapping()
    {
        var h = new Harness(players: 3);
        Assert.Equal(1, h.Teams.Selection);
        var (mx, my) = PlayerSelectTeamScreen.MarkerPoint(1);
        Assert.Equal(MenuGraphics.FrameWhite, h.Teams.Screen[mx, my]);
        h.Press(BiosKey.Up);
        Assert.Equal(1, h.Teams.Selection);
        h.Press(BiosKey.Down, BiosKey.Down, BiosKey.Down);
        Assert.Equal(3, h.Teams.Selection);
        Assert.NotEqual(MenuGraphics.FrameWhite, h.Teams.Screen[mx, my]);
        (mx, my) = PlayerSelectTeamScreen.MarkerPoint(3);
        Assert.Equal(MenuGraphics.FrameWhite, h.Teams.Screen[mx, my]);
        h.Press(BiosKey.Right);
        Assert.Equal(1, h.Roster[1].Team);
        Assert.Equal(3, h.Roster[3].Team);
    }

    [Fact]
    public void MouseButtonsMoveThePlayerOfTheRowAfterFortyMilliseconds()
    {
        var h = new Harness(players: 3);
        var row = 64 + 10 * 2;
        h.Mouse(MouseButtons.Left, 100, row);
        Assert.Equal(2, h.Teams.Selection);
        Assert.Equal(2, h.Roster[2].Team);
        h.Mouse(MouseButtons.Left, 100, row, ms: 39);
        Assert.Equal(2, h.Roster[2].Team);
        h.Mouse(MouseButtons.Left, 100, row, ms: 1);
        Assert.Equal(1, h.Roster[2].Team);
        h.Release();

        // [ORIGINAL] A press seen before the delay is carried out after it even if released (team stays at 1).
        h.Mouse(MouseButtons.None, 0, 0, ms: 40);
        Assert.Equal(1, h.Roster[2].Team);
        h.Mouse(MouseButtons.Right, 100, row + 7);
        h.Mouse(MouseButtons.Right, 100, row + 7, ms: 40);
        Assert.Equal(2, h.Roster[2].Team);
    }

    [Fact]
    public void MouseOutsideThePanelOrBetweenRowsDoesNothing()
    {
        var h = new Harness(players: 3);
        h.Mouse(MouseButtons.Left, 60, 84);
        h.Mouse(MouseButtons.Left, 100, 83, ms: 40);
        h.Mouse(MouseButtons.Left, 100, 64 + 10 * 4, ms: 40);
        Assert.Equal(1, h.Teams.Selection);
        Assert.Equal([1, 2, 3], h.Roster.Slots.Take(3).Select(s => s.Team));
    }

    [Fact]
    public void InvalidTeamsAreCancelledWithAMessage()
    {
        var h = new Harness(players: 3);
        h.Roster[2].Team = 1;
        h.Roster[3].Team = 1;
        h.Press(BiosKey.Enter);
        Assert.True(h.Teams.ShowsCancelMessage);
        Assert.Equal(MenuGraphics.FrameFace, h.Teams.Screen[100, 130]);
        h.Release();
        Assert.True(h.Teams.ShowsCancelMessage);
        h.Press(BiosKey.Char('x'));
        Assert.Same(h.Next, h.Current);
        Assert.False(h.Options.TeamPlay);
        Assert.Equal([1, 2, 3], h.Roster.Slots.Take(3).Select(s => s.Team));
        Assert.All(h.Setup!.Players, p => Assert.Equal(0, p.Team));
        Assert.Empty(h.Setup.Teams);
    }

    [Fact]
    public void TheMessageWaitsUntilTheDoneClickIsReleased()
    {
        var h = new Harness(players: 3);
        h.Mouse(MouseButtons.Left, 160, 192);
        Assert.False(h.Teams.ShowsCancelMessage);
        Assert.Equal(PlayerSelectTeamScreen.DoneEntry, h.Teams.Selection);
        h.Release();
        Assert.True(h.Teams.ShowsCancelMessage);
        h.Mouse(MouseButtons.Right, 0, 0);
        Assert.Same(h.Next, h.Current);
    }

    [Theory]
    [InlineData((ushort)0x1C0D)]
    [InlineData((ushort)0x011B)]
    [InlineData((ushort)0x2064)]
    public void EnterEscAndDFinishTheSetup(ushort key)
    {
        var roster = new PlayerRoster();
        foreach (var slot in roster.Slots)
            slot.Control = PlayerControl.Computer;
        roster[2].Team = 1;
        var h = new Harness(players: 3, roster: roster, random: _ => 5);
        h.Press(key);

        // Teams of computers need no input: the team names hand over on their first update.
        Assert.IsType<PlayerSelectTeamNamesScreen>(h.Current);
        h.Release();
        Assert.Same(h.Next, h.Current);
        Assert.Equal([1, 1, 3], h.Setup!.Players.Select(p => p.Team));
        Assert.Equal(2, h.Setup.Teams.Count);
        Assert.Equal("Crackshots", h.Setup.Teams[0].Name);
        Assert.Equal("Player 3", h.Setup.Teams[1].Name);
        Assert.True(h.Options.TeamPlay);
    }

    [Fact]
    public void HumanTeamsAreAskedForTheirName()
    {
        var h = new Harness(players: 3);
        h.Press(BiosKey.Down, BiosKey.Left, BiosKey.Enter);
        var names = Assert.IsType<PlayerSelectTeamNamesScreen>(h.Current);
        Assert.Equal(1, names.EnteringTeam);
        Assert.Equal("Team 1", names.NameBeingEntered.TrimEnd());

        // Home clears, typing appends at the cursor.
        h.Press(0x4700, BiosKey.Char('R'), BiosKey.Char('e'), BiosKey.Char('d'), BiosKey.Enter);
        Assert.Equal(3, names.EnteringTeam);
        Assert.Equal("Player 3", names.NameBeingEntered.TrimEnd());
        h.Press(0x0E08, BiosKey.Char('!'), BiosKey.Enter);
        Assert.Same(h.Next, h.Current);
        Assert.Equal(["Red", "Player !"], h.Setup!.Teams.Select(t => t.Name));
        Assert.Equal([1, 2], h.Setup.Teams[0].Members);
    }

    [Fact]
    public void TeamNamesStartOnTheSaveBufferAndEndWithTheColumnsRestored()
    {
        var h = new Harness(players: 3);
        h.Menu.Saved.Pixels.AsSpan().Fill(42);
        h.Press(BiosKey.Down, BiosKey.Left, BiosKey.Enter);
        var names = (PlayerSelectTeamNamesScreen)h.Current!;
        Assert.Equal(MenuGraphics.FrameFace, names.Screen[50, 55]);
        Assert.Equal(MenuGraphics.FrameFace, names.Screen[50, 70]);
        Assert.Equal(MenuGraphics.FrameFace, names.Screen[200, 90]);
        Assert.Equal(42, names.Screen[300, 10]);
        h.Press(BiosKey.Enter, BiosKey.Enter);
        Assert.Equal(42, names.Screen[50, 70]);
        Assert.Equal(42, names.Screen[200, 90]);
        Assert.Equal(MenuGraphics.FrameFace, names.Screen[50, 55]);
    }

    [Fact]
    public void ThirteenCharacterNamesCanBeEdited()
    {
        var roster = new PlayerRoster();
        roster[1].Name = "ABCDEFGHIJKLM";
        roster[2].Team = 3;
        roster[2].Control = PlayerControl.Computer;
        roster[3].Control = PlayerControl.Computer;
        var h = new Harness(players: 3, roster: roster, random: _ => 0);
        h.Press(BiosKey.Enter);
        Assert.Equal(1, ((PlayerSelectTeamNamesScreen)h.Current!).EnteringTeam);
        h.Press(BiosKey.Char('Z'), BiosKey.Enter);
        Assert.Same(h.Next, h.Current);
        Assert.Equal("ABCDEFGHIJKLM", h.Setup!.Teams[0].Name);
    }

    [Fact]
    public void PlayerSelectionLeadsToTheTeamSetupOnlyWithTeamPlay()
    {
        foreach (var teamPlay in new[] { false, true })
        {
            var menu = new MenuScreen(Fonts, new MenuSettings(Players: 2, PaletteRotation: false), _ => 1, (_, m) => m);
            var next = new PlaceholderScreen();
            var options = new OptionsSettings { TeamPlay = teamPlay };
            var select = new PlayerSelectScreen(menu, new PlayerRoster(), _ => 0, _ => next, new PlayerSelectTeams(options, Stub));
            var result = select.Update(TimeSpan.Zero, new ScreenInput(KeyPressed: true) { Keys = [BiosKey.Char('s')] });
            if (teamPlay)
                Assert.IsType<PlayerSelectTeamScreen>(result);
            else
                Assert.Same(next, result);
        }
    }

    /// <summary>#74: the team screens set DAC 19 to white (O2:29D0) whatever the menu palette holds there.</summary>
    [Fact]
    public void TeamPaletteSetsDacNineteenToWhite()
    {
        var menuDac = new byte[Palette.ByteLength];
        var dac = new byte[Palette.ByteLength];
        PlayerSelectTeamScreen.TeamDac(menuDac, dac);
        Assert.Equal(new byte[] { 63, 63, 63 }, dac.AsSpan(19 * 3, 3).ToArray());
        Assert.Equal(new byte[] { 0, 0, 0 }, dac.AsSpan(18 * 3, 3).ToArray());
        var (r, g, b) = PlayerSelectScreen.PlayerColor(1);
        Assert.Equal(new[] { r, g, b }, dac.AsSpan(20 * 3, 3).ToArray());
    }
}
