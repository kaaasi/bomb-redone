using BombRedone.Formats;
using BombRedone.UI.Screens;
using BombRedone.UI.Tests.Text;

namespace BombRedone.UI.Tests.Screens;

/// <summary>Game Options (O2:11CC) with synthetic fonts: box, markers, keys, mouse, value rules and the way back.</summary>
public class OptionsScreenTests
{
    /// <summary>Fonts without visible strokes: the text leaves the boxes as they are.</summary>
    private static readonly MenuFonts Fonts = new(
        BgiFont.Parse(SyntheticFont.Build("EURO", 32, [new SyntheticFont.Glyph(4)])),
        BgiFont.Parse(SyntheticFont.Build("LITT", 32, [new SyntheticFont.Glyph(4)])),
        new BitmapFont(new byte[256 * 8]));

    private static MenuScreen CreateMenu(MenuSettings? settings = null) =>
        new(Fonts, settings ?? new MenuSettings(), _ => 1, (_, m) => m);

    private static (MenuScreen Menu, OptionsScreen Options, OptionsSettings Values) Create(
        OptionsSettings? values = null, MenuSettings? settings = null)
    {
        var menu = CreateMenu(settings);
        values ??= new OptionsSettings();
        return (menu, new OptionsScreen(menu, values), values);
    }

    private static IScreen? Press(OptionsScreen screen, params ushort[] keys) =>
        screen.Update(TimeSpan.Zero, new ScreenInput(KeyPressed: keys.Length > 0) { Keys = keys });

    private static IScreen? Hold(OptionsScreen screen, MouseButtons buttons, int x, int y) =>
        screen.Update(TimeSpan.Zero, new ScreenInput(Mouse: buttons) { MouseX = x * 2, MouseY = y });

    private static IScreen? Release(OptionsScreen screen) => screen.Update(TimeSpan.Zero, ScreenInput.None);

    /// <summary>Selects a line with the arrow keys from line 0.</summary>
    private static void Select(OptionsScreen screen, int line)
    {
        for (var i = 0; i < line; i++)
            Press(screen, BiosKey.Down);
        Assert.Equal(line, screen.Selection);
    }

    [Fact]
    public void DefaultsAreTheValuesBeforeBombCfgIsRead()
    {
        var values = new OptionsSettings();
        Assert.Equal(10, values.CrumblePercent);
        Assert.Equal(WallSetting.None, values.Walls);
        Assert.False(values.TrailVisible);
        Assert.False(values.InvisibleShot);
        Assert.True(values.TanksFall);
        Assert.True(values.ComputersBuy);
        Assert.Equal(0, values.Background);
        Assert.Equal(WindSetting.Normal, values.Wind);
        Assert.False(values.TeamPlay);
    }

    [Fact]
    public void FromConfigTakesTheConfigFields()
    {
        var config = new GameConfig
        {
            CrumblePercent = 42, Wall = WallSetting.Warp, ShotTrail = true, InvisibleShot = true, TanksFall = false,
            ComputersBuy = false, Background = 7, Wind = WindSetting.Gale, TeamPlay = true,
        };
        var values = OptionsSettings.FromConfig(config);
        Assert.Equal(42, values.CrumblePercent);
        Assert.Equal(WallSetting.Warp, values.Walls);
        Assert.True(values.TrailVisible);
        Assert.True(values.InvisibleShot);
        Assert.False(values.TanksFall);
        Assert.False(values.ComputersBuy);
        Assert.Equal(7, values.Background);
        Assert.Equal(WindSetting.Gale, values.Wind);
        Assert.True(values.TeamPlay);
    }

    [Theory]
    [InlineData(0, "NO")]
    [InlineData(1, "STICK")]
    [InlineData(2, "ELASTIC")]
    [InlineData(3, "ACCEL")]
    [InlineData(4, "WARP")]
    [InlineData(5, "RND")]
    [InlineData(6, "")]
    public void WallNames(int value, string name) => Assert.Equal(name, OptionsSettings.WallName((WallSetting)value));

    [Theory]
    [InlineData(0, "None")]
    [InlineData(1, "Low")]
    [InlineData(2, "Norm")]
    [InlineData(3, "High")]
    [InlineData(4, "Gale")]
    [InlineData(5, "RND")]
    [InlineData(-1, "")]
    public void WindNames(int value, string name) => Assert.Equal(name, OptionsSettings.WindName((WindSetting)value));

    [Fact]
    public void BackgroundNames()
    {
        string[] names = ["None", "Stars", "Bars", "Circles", "Spread", "Sky", "Static", "Rectangle", "Asterisk", "RND", "USER"];
        for (var i = 0; i < names.Length; i++)
            Assert.Equal(names[i], OptionsSettings.BackgroundName(i));
        Assert.Equal("", OptionsSettings.BackgroundName(11));
        Assert.Equal("YES", OptionsSettings.YesNo(true));
        Assert.Equal("NO", OptionsSettings.YesNo(false));
    }

    [Fact]
    public void BoxIsDrawnOverTheMenuWithMarkersAtTheFirstLine()
    {
        var (menu, options, _) = Create();
        Assert.Equal(MenuGraphics.FrameFace, menu.Screen[160, 100]);
        Assert.Equal(15, menu.Screen[93, 45]);
        Assert.Equal(MenuGraphics.FrameDark, menu.Screen[227, 155]);
        Assert.Equal(0, options.Selection);
        Assert.Equal(MenuGraphics.FrameWhite, menu.Screen[89, 52]);
        Assert.Equal(MenuGraphics.FrameWhite, menu.Screen[231, 53]);
    }

    [Fact]
    public void LeavingRestoresTheCoveredAreaAndReturnsTheMenu()
    {
        var menu = CreateMenu();
        var before = menu.Screen.Pixels.ToArray();
        var options = new OptionsScreen(menu, new OptionsSettings());
        Press(options, BiosKey.Down, BiosKey.Space);
        Assert.Same(menu, Press(options, BiosKey.Enter));
        // The markers of the options lie outside the restored area and are erased from the menu buffer.
        Assert.Equal(before, menu.Screen.Pixels);
    }

    [Fact]
    public void EscapeLeavesAsWell()
    {
        var (menu, options, _) = Create();
        Assert.Same(menu, Press(options, BiosKey.Escape));
    }

    [Fact]
    public void ArrowKeysMoveWithinTheTenLines()
    {
        var (menu, options, _) = Create();
        Press(options, BiosKey.Up);
        Assert.Equal(0, options.Selection);
        Press(options, BiosKey.Down);
        Assert.Equal(menu.Saved[89, 52], menu.Screen[89, 52]);
        Assert.Equal(MenuGraphics.FrameWhite, menu.Screen[89, 62]);
        for (var i = 0; i < 12; i++)
            Press(options, BiosKey.Down);
        Assert.Equal(9, options.Selection);
        Assert.Equal(MenuGraphics.FrameWhite, menu.Screen[231, 142]);
    }

    [Fact]
    public void CrumblePercentRunsFromZeroToHundredAndSpaceAddsFiveWithWrap()
    {
        var (_, options, values) = Create(new OptionsSettings { CrumblePercent = 99 });
        Press(options, BiosKey.Right, BiosKey.Right, BiosKey.Right);
        Assert.Equal(100, values.CrumblePercent);
        Press(options, BiosKey.Space);
        Assert.Equal(5, values.CrumblePercent);
        for (var i = 0; i < 8; i++)
            Press(options, BiosKey.Left);
        Assert.Equal(0, values.CrumblePercent);
    }

    [Fact]
    public void WallsStepAndWrapWithSpace()
    {
        var (_, options, values) = Create(new OptionsSettings { Walls = WallSetting.Random });
        Select(options, 1);
        Press(options, BiosKey.Right);
        Assert.Equal(WallSetting.Random, values.Walls);
        Press(options, BiosKey.Space);
        Assert.Equal(WallSetting.None, values.Walls);
        Press(options, BiosKey.Left);
        Assert.Equal(WallSetting.None, values.Walls);
    }

    [Fact]
    public void YesNoLinesAreSetByRightClearedByLeftAndToggledBySpace()
    {
        var (_, options, values) = Create();
        Select(options, 2);
        Press(options, BiosKey.Right);
        Assert.True(values.TrailVisible);
        Press(options, BiosKey.Space);
        Assert.False(values.TrailVisible);

        // Ball Visible shows "not Inviso": Right makes the ball visible.
        Press(options, BiosKey.Down);
        values.InvisibleShot = true;
        Press(options, BiosKey.Right);
        Assert.False(values.InvisibleShot);
        Press(options, BiosKey.Left);
        Assert.True(values.InvisibleShot);

        Press(options, BiosKey.Down, BiosKey.Left);
        Assert.False(values.TanksFall);
        Press(options, BiosKey.Down, BiosKey.Space);
        Assert.False(values.ComputersBuy);
        Press(options, BiosKey.Down, BiosKey.Down, BiosKey.Down, BiosKey.Down, BiosKey.Space);
        Assert.True(values.TeamPlay);
    }

    [Fact]
    public void BackgroundRunsFromZeroToTenAndWrapsWithSpace()
    {
        var (_, options, values) = Create(new OptionsSettings { Background = 9 });
        Select(options, 6);
        Press(options, BiosKey.Right, BiosKey.Right);
        Assert.Equal(10, values.Background);
        Press(options, BiosKey.Space);
        Assert.Equal(0, values.Background);
    }

    [Fact]
    public void CycleDelayChangesTheRunningRotation()
    {
        var (menu, options, _) = Create(settings: new MenuSettings(PaletteSpeed: 249));
        Select(options, 7);
        Press(options, BiosKey.Right, BiosKey.Right);
        Assert.Equal(250, menu.PaletteSpeed);
        Press(options, BiosKey.Space);
        Assert.Equal(0, menu.PaletteSpeed);
        Press(options, BiosKey.Space);
        Assert.Equal(1, menu.PaletteSpeed);

        // Speed 1: every timer tick is a rotation step (the first one shows position 0 again), also while the options are shown.
        var before = menu.Dac.Slice(30 * 3, 3).ToArray();
        options.Update(MenuScreen.TimerTick * 2, ScreenInput.None);
        Assert.NotEqual(before, menu.Dac.Slice(30 * 3, 3).ToArray());
    }

    [Fact]
    public void WindForceRunsFromZeroToFiveAndSpaceWrapsToZero()
    {
        var (_, options, values) = Create(new OptionsSettings { Wind = WindSetting.Gale });
        Select(options, 8);
        Press(options, BiosKey.Right, BiosKey.Right);
        Assert.Equal(WindSetting.Random, values.Wind);
        Press(options, BiosKey.Space);
        Assert.Equal(WindSetting.None, values.Wind);
    }

    [Theory]
    [InlineData(96, 50, 0)]
    [InlineData(224, 58, 0)]
    [InlineData(150, 59, 1)]
    [InlineData(150, 138, 8)]
    [InlineData(150, 139, 9)]
    [InlineData(150, 152, 9)]
    [InlineData(150, 49, OptionsScreen.LeaveLine)]
    [InlineData(150, 153, OptionsScreen.LeaveLine)]
    [InlineData(95, 100, OptionsScreen.LeaveLine)]
    [InlineData(225, 100, OptionsScreen.LeaveLine)]
    public void MouseLines(int x, int y, int line) => Assert.Equal(line, OptionsScreen.LineAt(x, y));

    [Fact]
    public void RightButtonOnALineRaisesItAndWaitsForTheRelease()
    {
        var (_, options, values) = Create();
        Hold(options, MouseButtons.Right, 150, 73);
        Assert.True(values.TrailVisible);
        Hold(options, MouseButtons.Left, 150, 73);
        Assert.True(values.TrailVisible);
        Release(options);
        Hold(options, MouseButtons.Left, 150, 73);
        Assert.False(values.TrailVisible);
        Assert.Equal(2, options.Selection);
    }

    [Fact]
    public void CrumblePercentRepeatsWhileTheButtonIsHeld()
    {
        var (_, options, values) = Create();
        for (var i = 0; i < 3; i++)
            Hold(options, MouseButtons.Left, 150, 52);
        Assert.Equal(7, values.CrumblePercent);
    }

    [Fact]
    public void ClickOutsideTheLinesLeaves()
    {
        var (menu, options, _) = Create();
        Assert.Same(menu, Hold(options, MouseButtons.Left, 20, 20));
    }

    [Fact]
    public void WithoutMouseDriverButtonsAreIgnored()
    {
        var (_, options, _) = Create(settings: new MenuSettings(MouseAvailable: false));
        Assert.Same(options, Hold(options, MouseButtons.Left, 20, 20));
    }

    [Fact]
    public void ChangedValueIsClearedWithTheButtonFace()
    {
        var (menu, options, _) = Create();
        Select(options, 6);
        // Paint around the Bar of line 6 (165, 108)–(225, 118); only its inside gets the button face again.
        foreach (var (x, y) in new[] { (165, 108), (225, 118), (164, 112), (166, 107), (200, 119) })
            menu.Screen[x, y] = 3;
        Press(options, BiosKey.Right);
        Assert.Equal(MenuGraphics.FrameFace, menu.Screen[165, 108]);
        Assert.Equal(MenuGraphics.FrameFace, menu.Screen[225, 118]);
        Assert.Equal(3, menu.Screen[164, 112]);
        Assert.Equal(3, menu.Screen[166, 107]);
        Assert.Equal(3, menu.Screen[200, 119]);
    }

    [Fact]
    public void GameOptionsFromTheMenuOpenTheScreenAndComeBack()
    {
        var values = new OptionsSettings();
        OptionsScreen? opened = null;
        var menu = new MenuScreen(Fonts, new MenuSettings(), _ => 1, (choice, m) =>
            choice == MenuChoice.GameOptions ? opened = new OptionsScreen(m, values) : m);
        menu.Update(TimeSpan.Zero, new ScreenInput(KeyPressed: true) { Keys = [BiosKey.Down] });
        var next = menu.Update(TimeSpan.Zero, new ScreenInput(KeyPressed: true) { Keys = [BiosKey.Enter] });
        Assert.Same(opened, next);
        Assert.Same(menu, Press(opened!, BiosKey.Enter));
        Assert.Equal(1, menu.Selection);
        Assert.Equal(MenuGraphics.FrameWhite, menu.Screen[112, 115]);
    }
}
