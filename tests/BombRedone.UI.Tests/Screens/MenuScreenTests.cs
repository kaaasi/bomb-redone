using BombRedone.Formats;
using BombRedone.UI.Screens;
using BombRedone.UI.Tests.Text;

namespace BombRedone.UI.Tests.Screens;

/// <summary>Main menu with synthetic fonts: layout, palette, keys, mouse and palette rotation.</summary>
public class MenuScreenTests
{
    /// <summary>Fonts without visible strokes: the text leaves the boxes as they are.</summary>
    private static readonly MenuFonts Fonts = new(
        BgiFont.Parse(SyntheticFont.Build("EURO", 32, [new SyntheticFont.Glyph(4)])),
        BgiFont.Parse(SyntheticFont.Build("LITT", 32, [new SyntheticFont.Glyph(4)])),
        new BitmapFont(new byte[256 * 8]));

    private sealed class Recorder
    {
        public List<MenuChoice> Choices { get; } = [];

        public IScreen Next { get; } = new PlaceholderScreen();

        public bool StayOnOptions { get; init; } = true;

        public IScreen? Choose(MenuChoice choice, MenuScreen menu)
        {
            Choices.Add(choice);
            return StayOnOptions && choice is MenuChoice.SoundOptions or MenuChoice.GameOptions ? menu : Next;
        }
    }

    private static MenuScreen Create(Recorder? recorder = null, MenuSettings? settings = null, int pick = 1) =>
        new(Fonts, settings ?? new MenuSettings(), n => n == 2 ? pick : throw new InvalidOperationException(),
            (recorder ?? new Recorder()).Choose);

    private static ScreenInput Keys(params ushort[] keys) => new(KeyPressed: keys.Length > 0) { Keys = keys };

    private static ScreenInput Click(MouseButtons buttons, int x, int y) =>
        new(Mouse: buttons) { MouseX = x * 2, MouseY = y };

    private static IScreen? Press(MenuScreen menu, params ushort[] keys) => menu.Update(TimeSpan.Zero, Keys(keys));

    private static byte[] Rgb(MenuScreen menu, int index) => menu.Dac.Slice(index * 3, 3).ToArray();

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 160)]
    public void RandomTwoChoosesTheBackgroundPattern(int pick, int limit)
    {
        var menu = Create(pick: pick);
        Assert.Equal(limit, menu.BackgroundFrameLimit);
        // Outside all boxes: frame 19 on the right, and (145, 60) where only the second pattern reaches.
        Assert.Equal(49, menu.Screen[300, 100]);
        Assert.Equal(pick == 0 ? 30 : 55, menu.Screen[145, 60]);
    }

    /// <summary>#79: without BOMB.CFG the menu starts with the defaults of BOMBCFG.EXE (R1-D5 = C).</summary>
    [Fact]
    public void WithoutBombCfgTheBombCfgExeDefaultsApply()
    {
        Assert.Equal(MenuSettings.FromConfig(GameConfig.BombCfgDefaults, mouseAvailable: true),
            MenuSettings.ForConfig(null, mouseAvailable: true));
        Assert.True(MenuSettings.ForConfig(null, mouseAvailable: true).PaletteUsesRetrace);
    }

    [Fact]
    public void RandomOutsideZeroAndOneIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => Create(pick: 2));
    }

    [Fact]
    public void PaletteHasTheMenuColours()
    {
        var menu = Create();
        Assert.Equal(new byte[] { 0, 0, 42 }, Rgb(menu, 1));
        Assert.Equal(new byte[] { 63, 21, 21 }, Rgb(menu, 12));
        Assert.Equal(new byte[] { 63, 63, 21 }, Rgb(menu, 14));
        Assert.Equal(new byte[] { 42, 42, 42 }, Rgb(menu, 18));
        for (var i = 30; i < 60; i++)
            Assert.Equal(new[] { (byte)i, (byte)0, (byte)i }, Rgb(menu, i));
    }

    [Fact]
    public void SavedBufferHoldsBackgroundAndHeaderButNotTheMenu()
    {
        var menu = Create();
        Assert.Equal(MenuGraphics.FrameFace, menu.Saved[160, 30]);
        Assert.NotEqual(MenuGraphics.FrameFace, menu.Saved[119, 64]);
        Assert.Equal(MenuGraphics.FrameFace, menu.Screen[119, 64]);
        Assert.Equal(MenuGraphics.FrameFace, menu.Screen[25, 150]);
    }

    [Fact]
    public void EntryBoxesShowTheSavedBackgroundInsideASunkenGreenFrame()
    {
        var menu = Create();
        for (var entry = 0; entry < MenuScreen.EntryCount; entry++)
        {
            var top = 91 + 20 * entry;
            for (var y = top; y <= top + 10; y++)
                for (var x = 122; x <= 198; x++)
                    Assert.Equal(menu.Saved[x, y], menu.Screen[x, y]);
            Assert.Equal(MenuGraphics.FrameDark, menu.Screen[160, top - 1]);
            Assert.Equal(15, menu.Screen[160, top + 11]);
        }
    }

    [Fact]
    public void MarkersStartAtTheFirstEntry()
    {
        var menu = Create();
        Assert.Equal(0, menu.Selection);
        Assert.Equal(MenuGraphics.FrameWhite, menu.Screen[112, 95]);
        Assert.Equal(MenuGraphics.FrameWhite, menu.Screen[208, 96]);
    }

    [Fact]
    public void ArrowKeysMoveTheMarkersWithinTheFiveEntries()
    {
        var menu = Create();
        Press(menu, BiosKey.Up);
        Assert.Equal(0, menu.Selection);
        Press(menu, BiosKey.Down);
        Assert.Equal(1, menu.Selection);
        Assert.Equal(menu.Saved[112, 95], menu.Screen[112, 95]);
        Assert.Equal(MenuGraphics.FrameWhite, menu.Screen[112, 115]);
        Press(menu, BiosKey.Down, BiosKey.Down, BiosKey.Down, BiosKey.Down, BiosKey.Down);
        Assert.Equal(4, menu.Selection);
        Assert.Equal(MenuGraphics.FrameWhite, menu.Screen[208, 175]);
        Assert.Equal(menu.Saved[208, 115], menu.Screen[208, 115]);
    }

    [Fact]
    public void EnterOnStartLeavesTheMenu()
    {
        var recorder = new Recorder();
        var menu = Create(recorder);
        Press(menu, BiosKey.Down, BiosKey.Down, BiosKey.Down);
        Assert.Same(menu, Press(menu, BiosKey.Down));
        Assert.Same(recorder.Next, Press(menu, BiosKey.Space));
        Assert.Equal([MenuChoice.Start], recorder.Choices);
    }

    [Theory]
    [InlineData('L')]
    [InlineData('l')]
    public void LetterLLoadsAGame(char key)
    {
        var recorder = new Recorder();
        var menu = Create(recorder);
        Assert.Same(recorder.Next, Press(menu, BiosKey.Char(key)));
        Assert.Equal([MenuChoice.LoadGame], recorder.Choices);
    }

    [Fact]
    public void OptionsOpenWithEnterButNotWithLeftOrRight()
    {
        var recorder = new Recorder();
        var menu = Create(recorder);
        Press(menu, BiosKey.Right, BiosKey.Left);
        Assert.Empty(recorder.Choices);
        Press(menu, BiosKey.Enter);
        Press(menu, BiosKey.Down, BiosKey.Enter);
        Assert.Equal([MenuChoice.SoundOptions, MenuChoice.GameOptions], recorder.Choices);
        Assert.Equal(MenuGraphics.FrameWhite, menu.Screen[112, 115]);
    }

    [Fact]
    public void OptionsScreenTakesOverAndTheMenuIsReadyForTheReturn()
    {
        var recorder = new Recorder { StayOnOptions = false };
        var menu = Create(recorder);
        Assert.Same(recorder.Next, Press(menu, BiosKey.Enter));
        // The markers stay erased while the options are shown; Resume (O2:21EF) draws them again.
        Assert.Equal(menu.Saved[112, 95], menu.Screen[112, 95]);
        menu.Resume();
        Assert.Equal(MenuGraphics.FrameWhite, menu.Screen[112, 95]);
        Assert.Same(menu, Press(menu, BiosKey.Down));
        Assert.Equal(1, menu.Selection);
    }

    [Fact]
    public void PlayersChangeWithinTwoToTenAndEnterWraps()
    {
        var menu = Create();
        Press(menu, BiosKey.Down, BiosKey.Down);
        Press(menu, BiosKey.Left);
        Assert.Equal(2, menu.Players);
        Press(menu, BiosKey.Right);
        Assert.Equal(3, menu.Players);
        for (var i = 0; i < 10; i++)
            Press(menu, BiosKey.Right);
        Assert.Equal(10, menu.Players);
        Press(menu, BiosKey.Enter);
        Assert.Equal(2, menu.Players);
        Press(menu, BiosKey.Enter);
        Assert.Equal(3, menu.Players);
    }

    [Fact]
    public void GamesChangeByOneWithKeysAndByFiveWithEnterWrappingAbove99()
    {
        var menu = Create(settings: new MenuSettings(Games: 97));
        Press(menu, BiosKey.Down, BiosKey.Down, BiosKey.Down);
        Press(menu, BiosKey.Right);
        Assert.Equal(98, menu.Games);
        Press(menu, BiosKey.Enter);
        Assert.Equal(4, menu.Games);
        Press(menu, BiosKey.Left, BiosKey.Left, BiosKey.Left, BiosKey.Left);
        Assert.Equal(1, menu.Games);
    }

    [Fact]
    public void NumberAreaIsRestoredBeforeTheNewValue()
    {
        var menu = Create();
        Press(menu, BiosKey.Down, BiosKey.Down);
        menu.Screen[165, 135] = 99;
        Press(menu, BiosKey.Right);
        Assert.Equal(menu.Saved[165, 135], menu.Screen[165, 135]);
    }

    [Fact]
    public void MouseClickChoosesTheEntryAfterRelease()
    {
        var menu = Create();
        menu.Update(TimeSpan.Zero, Click(MouseButtons.Right, 160, 135));
        Assert.Equal(2, menu.Players);
        menu.Update(TimeSpan.Zero, Click(MouseButtons.Right, 10, 10));
        Assert.Equal(2, menu.Players);
        menu.Update(TimeSpan.Zero, ScreenInput.None);
        Assert.Equal(3, menu.Players);
        Assert.Equal(2, menu.Selection);
        Assert.Equal(MenuGraphics.FrameWhite, menu.Screen[112, 135]);
    }

    [Fact]
    public void MouseButtonsChangeGamesByFiveAndClamp()
    {
        var menu = Create(settings: new MenuSettings(Games: 97));
        menu.Update(TimeSpan.Zero, Click(MouseButtons.Right, 130, 155));
        menu.Update(TimeSpan.Zero, ScreenInput.None);
        Assert.Equal(99, menu.Games);
        menu.Update(TimeSpan.Zero, Click(MouseButtons.Left, 130, 152));
        menu.Update(TimeSpan.Zero, ScreenInput.None);
        Assert.Equal(94, menu.Games);
    }

    [Fact]
    public void MouseOnLoadGameLeavesAtOnce()
    {
        var recorder = new Recorder();
        var menu = Create(recorder);
        Assert.Same(recorder.Next, menu.Update(TimeSpan.Zero, Click(MouseButtons.Left, 50, 150)));
        Assert.Equal([MenuChoice.LoadGame], recorder.Choices);
    }

    [Fact]
    public void WithoutMouseDriverClicksAreIgnored()
    {
        var recorder = new Recorder();
        var menu = Create(recorder, new MenuSettings(MouseAvailable: false));
        menu.Update(TimeSpan.Zero, Click(MouseButtons.Left, 50, 150));
        menu.Update(TimeSpan.Zero, Click(MouseButtons.Left, 160, 175));
        menu.Update(TimeSpan.Zero, ScreenInput.None);
        Assert.Empty(recorder.Choices);
        Assert.Equal(0, menu.Selection);
    }

    [Fact]
    public void ButtonPressedOutsideTheMenuIsRememberedUntilTheNextAction()
    {
        // [ORIGINAL] O2:230E sets the button flags on every press; only O2:21EF clears them after an action.
        var menu = Create(settings: new MenuSettings(Players: 4));
        menu.Update(TimeSpan.Zero, Click(MouseButtons.Left, 250, 30));
        Press(menu, BiosKey.Down, BiosKey.Down);
        Press(menu, BiosKey.Enter);
        Assert.Equal(4, menu.Players);
        Press(menu, BiosKey.Enter);
        Assert.Equal(5, menu.Players);
    }

    [Fact]
    public void KeysTypedWhileWaitingForTheReleaseFollowAfterIt()
    {
        // The first key belongs to the pass that sees the button; the second one waits in the keyboard buffer.
        var recorder = new Recorder();
        var menu2 = Create(recorder);
        menu2.Update(TimeSpan.Zero, Click(MouseButtons.Left, 160, 135) with { Keys = [BiosKey.Down, BiosKey.Down] });
        Assert.Equal(1, menu2.Selection);
        menu2.Update(TimeSpan.Zero, ScreenInput.None);
        Assert.Equal(3, menu2.Selection);
        Assert.Equal(2, menu2.Players);
    }

    [Fact]
    public void PaletteRotatesOneEntryPerTimerTick()
    {
        var menu = Create();
        menu.Update(MenuScreen.TimerTick, ScreenInput.None);
        Assert.Equal(new byte[] { 30, 0, 30 }, Rgb(menu, 30));
        menu.Update(MenuScreen.TimerTick, ScreenInput.None);
        Assert.Equal(new byte[] { 31, 0, 31 }, Rgb(menu, 30));
        Assert.Equal(new byte[] { 30, 0, 30 }, Rgb(menu, 59));
        menu.Update(MenuScreen.TimerTick * 29, ScreenInput.None);
        Assert.Equal(new byte[] { 30, 0, 30 }, Rgb(menu, 30));
        menu.Update(MenuScreen.TimerTick, ScreenInput.None);
        Assert.Equal(new byte[] { 31, 0, 31 }, Rgb(menu, 30));
    }

    [Fact]
    public void PaletteSpeedCountsTicksAndRetraceUsesItsOwnTick()
    {
        // Step 1 (tick 3) shows the buffer at position 0 = unchanged; step 2 (tick 6) moves by one entry.
        var menu = Create(settings: new MenuSettings(PaletteSpeed: 3, PaletteUsesRetrace: true));
        menu.Update(MenuScreen.RetraceTick * 5, ScreenInput.None);
        Assert.Equal(new byte[] { 59, 0, 59 }, Rgb(menu, 59));
        menu.Update(MenuScreen.RetraceTick, ScreenInput.None);
        Assert.Equal(new byte[] { 30, 0, 30 }, Rgb(menu, 59));
        menu.Update(MenuScreen.RetraceTick * 2, ScreenInput.None);
        Assert.Equal(new byte[] { 30, 0, 30 }, Rgb(menu, 59));
    }

    [Fact]
    public void WithoutRotationThePaletteStays()
    {
        var menu = Create(settings: new MenuSettings(PaletteRotation: false));
        menu.Update(TimeSpan.FromSeconds(10), ScreenInput.None);
        Assert.Equal(new byte[] { 30, 0, 30 }, Rgb(menu, 30));
    }

    [Fact]
    public void DrawingDoesNotChangeTheMenu()
    {
        var menu = Create();
        var first = new Surface();
        var second = new Surface();
        menu.Draw(first);
        menu.Draw(second);
        Assert.Equal(first.Pixels, second.Pixels);
        Assert.Equal(first.Palette, second.Palette);
        Assert.Equal(menu.Screen.Pixels, first.Pixels);
    }
}
