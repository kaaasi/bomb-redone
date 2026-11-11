using BombRedone.Core.Setup;
using BombRedone.Formats;
using BombRedone.UI.Screens;
using BombRedone.UI.Tests.Text;

namespace BombRedone.UI.Tests.Screens;

/// <summary>Player selection with synthetic fonts (no visible strokes): layout, keys, mouse, name entry, opponents.</summary>
public class PlayerSelectScreenTests
{
    private static readonly MenuFonts Fonts = new(
        BgiFont.Parse(SyntheticFont.Build("EURO", 32, [new SyntheticFont.Glyph(4)])),
        BgiFont.Parse(SyntheticFont.Build("LITT", 32, [new SyntheticFont.Glyph(4)])),
        new BitmapFont(new byte[256 * 8]));

    private const ushort Backspace = 0x0E08, Home = 0x4700, End = 0x4F00, Delete = 0x5300;

    private sealed class Harness
    {
        public Harness(int players = 2, bool mouse = true, Func<int, int>? random = null, PlayerRoster? roster = null)
        {
            Menu = new MenuScreen(Fonts, new MenuSettings(Players: players, MouseAvailable: mouse, PaletteRotation: false),
                _ => 1, (_, m) => m);
            Roster = roster ?? new PlayerRoster();
            Screen = new PlayerSelectScreen(Menu, Roster, random ?? (_ => throw new InvalidOperationException()), Start);
        }

        public MenuScreen Menu { get; }

        public PlayerRoster Roster { get; }

        public PlayerSelectScreen Screen { get; }

        public GameSetup? Setup { get; private set; }

        public IScreen Next { get; } = new PlaceholderScreen();

        public IScreen? Press(params ushort[] keys) =>
            Screen.Update(TimeSpan.Zero, new ScreenInput(KeyPressed: keys.Length > 0) { Keys = keys });

        public IScreen? Mouse(MouseButtons buttons, int x, int y) =>
            Screen.Update(TimeSpan.Zero, new ScreenInput(Mouse: buttons) { MouseX = x * 2, MouseY = y });

        public IScreen? Release() => Screen.Update(TimeSpan.Zero, ScreenInput.None);

        private IScreen Start(GameSetup setup)
        {
            Setup = setup;
            return Next;
        }
    }

    private static (int X, int Y) MarkerPoint(int number)
    {
        var (x1, _, x2, y2) = PlayerSelectScreen.Box(number);
        return ((x1 + x2) / 2, y2 - 4);
    }

    [Fact]
    public void BoxesAreLaidOutInTwoRowsOfFive()
    {
        Assert.Equal((10, 50, 65, 105), PlayerSelectScreen.Box(1));
        Assert.Equal((254, 50, 309, 105), PlayerSelectScreen.Box(5));
        Assert.Equal((10, 111, 65, 166), PlayerSelectScreen.Box(6));
        Assert.Equal((254, 111, 309, 166), PlayerSelectScreen.Box(10));
    }

    [Fact]
    public void PlayersTakingPartHaveTheButtonFaceOthersTheDarkFace()
    {
        var h = new Harness(players: 3);
        for (var n = 1; n <= 10; n++)
        {
            var (x1, y1, _, _) = PlayerSelectScreen.Box(n);
            Assert.Equal(n <= 3 ? MenuGraphics.FrameFace : MenuGraphics.FrameDark, h.Screen.Screen[x1 + 5, y1 + 30]);
        }
    }

    [Fact]
    public void MarkerSpotsShowTheBackgroundOrABlackPlus()
    {
        var h = new Harness(players: 3);
        var (x, y) = MarkerPoint(1);
        Assert.Equal(MenuGraphics.FrameWhite, h.Screen.Screen[x, y]);
        (x, y) = MarkerPoint(2);
        Assert.Equal(h.Menu.Saved[x, y], h.Screen.Screen[x, y]);
        Assert.Equal(h.Menu.Saved[x + 1, y], h.Screen.Screen[x + 1, y]);
        (x, y) = MarkerPoint(4);
        foreach (var (dx, dy) in new[] { (0, 0), (-1, 0), (1, 0), (0, -1), (0, 1) })
            Assert.Equal(0, h.Screen.Screen[x + dx, y + dy]);
        Assert.Equal(MenuGraphics.FrameDark, h.Screen.Screen[x + 1, y + 1]);
    }

    [Fact]
    public void BackgroundAndHeaderComeFromTheMenuSaveBuffer()
    {
        var h = new Harness();
        Assert.Equal(h.Menu.Saved[5, 5], h.Screen.Screen[5, 5]);
        Assert.Equal(h.Menu.Saved[160, 20], h.Screen.Screen[160, 20]);
        Assert.Equal(h.Menu.Saved[2, 199], h.Screen.Screen[2, 199]);
    }

    [Fact]
    public void StartButtonIsARaisedBox()
    {
        var h = new Harness();
        Assert.Equal(15, h.Screen.Screen[70, 170]);
        Assert.Equal(MenuGraphics.FrameDark, h.Screen.Screen[249, 198]);
        Assert.Equal(MenuGraphics.FrameFace, h.Screen.Screen[100, 175]);
    }

    [Fact]
    public void DrawShowsThePlayerColours()
    {
        var h = new Harness();
        var surface = new Surface();
        h.Screen.Draw(surface);
        var dac = new byte[768];
        h.Menu.Dac.CopyTo(dac);
        for (var n = 1; n <= 10; n++)
        {
            var (r, g, b) = PlayerSelectScreen.PlayerColor(n);
            dac[(19 + n) * 3] = r;
            dac[(19 + n) * 3 + 1] = g;
            dac[(19 + n) * 3 + 2] = b;
        }

        var expected = new Surface();
        MenuGraphics.ApplyDac(dac, expected);
        Assert.Equal(expected.Palette, surface.Palette);
        Assert.Equal((63, 0, 0), PlayerSelectScreen.PlayerColor(1));
        Assert.Equal((0, 63, 0), PlayerSelectScreen.PlayerColor(2));
        Assert.Equal((63, 31, 31), PlayerSelectScreen.PlayerColor(10));
    }

    [Fact]
    public void PlayersAboveTheCountLoseTheirControl()
    {
        var roster = new PlayerRoster();
        var h = new Harness(players: 4, roster: roster);
        Assert.Equal(4, roster.Players);
        Assert.All(roster.Slots.Take(4), s => Assert.Equal(PlayerControl.Keyboard, s.Control));
        Assert.All(roster.Slots.Skip(4), s => Assert.Equal(PlayerControl.None, s.Control));
        Assert.Same(h.Screen, h.Release());
    }

    [Theory]
    [InlineData(4, new ushort[] { BiosKey.Right }, 1)]
    [InlineData(4, new ushort[] { BiosKey.Left }, 3)]
    [InlineData(4, new ushort[] { BiosKey.Right, BiosKey.Right, BiosKey.Right, BiosKey.Right }, 0)]
    [InlineData(10, new ushort[] { BiosKey.Down }, 5)]
    [InlineData(10, new ushort[] { BiosKey.Up }, 5)]
    [InlineData(10, new ushort[] { BiosKey.Down, BiosKey.Down }, 0)]
    [InlineData(4, new ushort[] { BiosKey.Down }, 0)]
    [InlineData(7, new ushort[] { BiosKey.Right, BiosKey.Down }, 6)]
    [InlineData(7, new ushort[] { BiosKey.Right, BiosKey.Right, BiosKey.Down }, 2)]
    public void ArrowKeysMoveTheSelection(int players, ushort[] keys, int expected)
    {
        var h = new Harness(players: players);
        h.Press(keys);
        Assert.Equal(expected, h.Screen.Selection);
        var (x, y) = MarkerPoint(expected + 1);
        Assert.Equal(MenuGraphics.FrameWhite, h.Screen.Screen[x, y]);
    }

    [Fact]
    public void MovingTheSelectionRestoresTheOldMarkerSpot()
    {
        var h = new Harness();
        var (x, y) = MarkerPoint(1);
        h.Press(BiosKey.Right);
        Assert.Equal(h.Menu.Saved[x, y], h.Screen.Screen[x, y]);
    }

    [Fact]
    public void KeysKMAndCChooseTheControl()
    {
        var h = new Harness(random: _ => 0);
        h.Press(BiosKey.Char('m'));
        Assert.Equal(PlayerControl.Mouse, h.Roster[1].Control);
        h.Press(BiosKey.Char('K'));
        Assert.Equal(PlayerControl.Keyboard, h.Roster[1].Control);
        h.Press(BiosKey.Char('c'));
        Assert.Equal(PlayerControl.Computer, h.Roster[1].Control);
        Assert.True(h.Screen.ChoosingOpponent);
    }

    [Fact]
    public void ComputerListStartsAtRandomAndEscapeChoosesRandom()
    {
        var h = new Harness();
        h.Press(BiosKey.Char('C'));
        Assert.Equal(0, h.Screen.ListSelection);
        h.Press(BiosKey.Down, BiosKey.Down);
        Assert.Equal(2, h.Screen.ListSelection);
        h.Press(BiosKey.Escape);
        Assert.False(h.Screen.ChoosingOpponent);
        Assert.Equal("Random", h.Roster[1].Name);
    }

    [Theory]
    [InlineData('?', "Random")]
    [InlineData('s', "Mr. Stupid")]
    [InlineData('l', "Lobber")]
    [InlineData('R', "Rifleman")]
    [InlineData('b', "Lob & Shoot")]
    [InlineData('w', "Windless Wit")]
    [InlineData('t', "Twanger")]
    [InlineData('m', "Wind Master")]
    public void LetterKeysChooseAnOpponent(char key, string name)
    {
        var h = new Harness();
        h.Press(BiosKey.Char('c'), BiosKey.Char(key));
        Assert.Equal(name, h.Roster[1].Name);
        Assert.False(h.Screen.ChoosingOpponent);
    }

    [Fact]
    public void EnterChoosesTheSelectedOpponentAndTheListStartsAtTheCurrentName()
    {
        var h = new Harness();
        h.Press(BiosKey.Char('c'), BiosKey.Down, BiosKey.Down, BiosKey.Down, BiosKey.Enter);
        Assert.Equal("Rifleman", h.Roster[1].Name);
        h.Press(BiosKey.Space);
        Assert.True(h.Screen.ChoosingOpponent);
        Assert.Equal(3, h.Screen.ListSelection);
        h.Press(BiosKey.Up, BiosKey.Up, BiosKey.Up, BiosKey.Up, BiosKey.Space);
        Assert.Equal("Random", h.Roster[1].Name);
    }

    [Fact]
    public void WindMasterIsNotPreselected()
    {
        // [ORIGINAL] O2:4894–48C0 compares only the names 1..6.
        var h = new Harness();
        h.Press(BiosKey.Char('c'), BiosKey.Char('m'), BiosKey.Enter);
        Assert.True(h.Screen.ChoosingOpponent);
        Assert.Equal(0, h.Screen.ListSelection);
    }

    [Fact]
    public void ListRestoresTheCoveredAreaAndTheNameBoxIsRedrawn()
    {
        var h = new Harness();
        var before = h.Screen.Screen.Pixels.ToArray();
        h.Press(BiosKey.Char('c'));
        Assert.Equal(MenuGraphics.FrameFace, h.Screen.Screen[150, 100]);
        h.Press(BiosKey.Escape);
        var after = h.Screen.Screen.Pixels;
        for (var y = PlayerSelectScreen.ListTop; y <= PlayerSelectScreen.ListBottom; y++)
            for (var x = PlayerSelectScreen.ListLeft; x <= PlayerSelectScreen.ListRight; x++)
                Assert.Equal(before[y * 320 + x], after[y * 320 + x]);
    }

    [Fact]
    public void FormerComputerIsAskedForANameWhenItBecomesHuman()
    {
        var h = new Harness();
        h.Press(BiosKey.Char('c'), BiosKey.Char('l'));
        Assert.Equal("Lobber", h.Roster[1].Name);
        h.Press(BiosKey.Char('k'));
        Assert.Equal(PlayerControl.Keyboard, h.Roster[1].Control);
        Assert.True(h.Screen.EnteringName);
        h.Press(BiosKey.Enter);
        Assert.False(h.Screen.EnteringName);
        Assert.Equal("Lobber", h.Roster[1].Name);
        h.Press(BiosKey.Char('m'));
        Assert.False(h.Screen.EnteringName);
    }

    [Fact]
    public void NameEntryAppendsAfterTheOldName()
    {
        var h = new Harness();
        h.Press(BiosKey.Enter);
        Assert.True(h.Screen.EnteringName);
        Assert.True(h.Screen.CursorShown);
        Assert.Equal("Player 1 ", h.Screen.NameBeingEntered);
        h.Press(BiosKey.Char('X'), BiosKey.Enter);
        Assert.Equal("Player 1X", h.Roster[1].Name);
    }

    [Fact]
    public void BackspaceClearsAndTrailingSpacesAreDropped()
    {
        var h = new Harness();
        h.Press(BiosKey.Space);
        for (var i = 0; i < 8; i++)
            h.Press(Backspace);
        h.Press(BiosKey.Char('J'), BiosKey.Char('o'), BiosKey.Enter);
        Assert.Equal("Jo", h.Roster[1].Name);
    }

    [Fact]
    public void HomeClearsTheWholeNameAndTypingStartsAtTheFirstCharacter()
    {
        var h = new Harness();
        h.Press(BiosKey.Enter, Home);
        Assert.False(h.Screen.CursorShown);
        Assert.Equal("  ", h.Screen.NameBeingEntered);
        h.Press(BiosKey.Char('A'), BiosKey.Char('l'), BiosKey.Escape);
        Assert.Equal("Al", h.Roster[1].Name);
    }

    [Fact]
    public void LeftRightDeleteAndEndEditInsideTheName()
    {
        var h = new Harness();
        h.Press(BiosKey.Enter);
        for (var i = 0; i < 9; i++)
            h.Press(BiosKey.Left);
        h.Press(Delete, BiosKey.Right, BiosKey.Char('L'), End, BiosKey.Char('!'), BiosKey.Enter);
        Assert.Equal(" Layer 1!", h.Roster[1].Name);
    }

    [Fact]
    public void NamesStopAtThirteenCharacters()
    {
        var h = new Harness();
        h.Press(BiosKey.Enter);
        foreach (var c in "abcdefghij")
            h.Press(BiosKey.Char(c));
        h.Press(BiosKey.Enter);
        Assert.Equal("Player 1abcdj", h.Roster[1].Name);
    }

    [Fact]
    public void NameEntryRestoresTheCoveredArea()
    {
        var h = new Harness();
        h.Press(BiosKey.Right);
        var before = h.Screen.Screen.Pixels.ToArray();
        h.Press(BiosKey.Enter, BiosKey.Enter);
        var after = h.Screen.Screen.Pixels;
        for (var y = PlayerSelectScreen.NameTop; y <= PlayerSelectScreen.NameBottom; y++)
            for (var x = PlayerSelectScreen.NameLeft; x <= PlayerSelectScreen.NameRight; x++)
                Assert.Equal(before[y * 320 + x], after[y * 320 + x]);
    }

    [Theory]
    [InlineData((ushort)0x011B)]
    [InlineData((ushort)'g')]
    [InlineData((ushort)'P')]
    [InlineData((ushort)'s')]
    public void EscapeGPAndSStartTheGame(ushort key)
    {
        var h = new Harness();
        var next = h.Press(key);
        Assert.Same(h.Next, next);
        Assert.NotNull(h.Setup);
        Assert.Equal(2, h.Setup.Players.Count);
    }

    [Fact]
    public void StartResolvesRandomComputersWithTheGivenGenerator()
    {
        var calls = 0;
        var h = new Harness(random: n =>
        {
            Assert.Equal(7, n);
            calls++;
            return 4;
        });
        h.Press(BiosKey.Right, BiosKey.Char('c'), BiosKey.Escape);
        Assert.Equal(0, calls);
        h.Press(BiosKey.Char('S'));
        Assert.Equal(1, calls);
        Assert.Equal("Windless Wit", h.Setup!.Players[1].Name);
        Assert.Equal(5, h.Setup.Players[1].Intelligence);
    }

    [Fact]
    public void MouseOnTheControlWordsChoosesTheControl()
    {
        var h = new Harness();
        var (x1, y1, _, _) = PlayerSelectScreen.Box(2);
        h.Mouse(MouseButtons.Left, x1 + 20, y1 + 30);
        Assert.Equal(PlayerControl.Mouse, h.Roster[2].Control);
        Assert.Equal(1, h.Screen.Selection);
        h.Mouse(MouseButtons.Left, x1 + 20, y1 + 20);
        Assert.Equal(PlayerControl.Keyboard, h.Roster[2].Control);
    }

    [Fact]
    public void MouseIsIgnoredWithoutDriver()
    {
        var h = new Harness(mouse: false);
        var (x1, y1, _, _) = PlayerSelectScreen.Box(2);
        h.Mouse(MouseButtons.Left, x1 + 20, y1 + 30);
        Assert.Equal(PlayerControl.Keyboard, h.Roster[2].Control);
        Assert.Equal(0, h.Screen.Selection);
    }

    [Fact]
    public void MouseOnABoxOnlyMovesTheSelection()
    {
        var h = new Harness();
        var (x1, y1, _, _) = PlayerSelectScreen.Box(2);
        h.Mouse(MouseButtons.Right, x1 + 2, y1 + 50);
        Assert.Equal(1, h.Screen.Selection);
        Assert.False(h.Screen.EnteringName);
        // Boxes above the player count do not react.
        (x1, y1, _, _) = PlayerSelectScreen.Box(3);
        h.Mouse(MouseButtons.Right, x1 + 20, y1 + 20);
        Assert.Equal(1, h.Screen.Selection);
        Assert.Equal(PlayerControl.None, h.Roster[3].Control);
    }

    [Fact]
    public void ComputerByMouseOpensTheListAfterTheReleaseAndAClickOutsideChoosesRandom()
    {
        var h = new Harness();
        var (x1, y1, _, _) = PlayerSelectScreen.Box(1);
        h.Mouse(MouseButtons.Left, x1 + 20, y1 + 40);
        Assert.Equal(PlayerControl.Computer, h.Roster[1].Control);
        Assert.False(h.Screen.ChoosingOpponent);
        h.Release();
        Assert.True(h.Screen.ChoosingOpponent);

        h.Mouse(MouseButtons.Left, 160, 85);
        Assert.True(h.Screen.ChoosingOpponent);
        Assert.Equal(2, h.Screen.ListSelection);
        h.Release();
        Assert.Equal("Lobber", h.Roster[1].Name);

        h.Press(BiosKey.Enter);
        h.Mouse(MouseButtons.Left, 10, 10);
        h.Release();
        Assert.False(h.Screen.ChoosingOpponent);
        Assert.Equal("Random", h.Roster[1].Name);
    }

    [Theory]
    [InlineData(61, 0)]
    [InlineData(69, 0)]
    [InlineData(70, 1)]
    [InlineData(129, 6)]
    [InlineData(130, 7)]
    [InlineData(141, 7)]
    [InlineData(142, 0)]
    [InlineData(60, 0)]
    public void ListRowsByMouse(int y, int entry)
    {
        var h = new Harness();
        h.Press(BiosKey.Char('c'));
        h.Mouse(MouseButtons.Left, 160, y);
        h.Release();
        Assert.Equal(ComputerOpponents.Names[entry], h.Roster[1].Name);
    }

    [Fact]
    public void MouseOnTheNameOpensTheNameEntry()
    {
        var h = new Harness();
        var (x1, y1, _, _) = PlayerSelectScreen.Box(2);
        h.Mouse(MouseButtons.Left, x1 + 10, y1 + 8);
        Assert.True(h.Screen.EnteringName);
        h.Mouse(MouseButtons.Left, 160, 180);
        Assert.True(h.Screen.EnteringName);
    }

    [Fact]
    public void MouseOnStartWaitsForTheRelease()
    {
        var h = new Harness();
        Assert.Same(h.Screen, h.Mouse(MouseButtons.Left, 160, 180));
        Assert.Null(h.Setup);
        Assert.Same(h.Next, h.Release());
        Assert.NotNull(h.Setup);
    }
}
