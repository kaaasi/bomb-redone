using BombRedone.Formats;
using BombRedone.UI.Text;

namespace BombRedone.UI.Screens;

/// <summary>What the player chose in the main menu.</summary>
public enum MenuChoice
{
    /// <summary>Entry "Sound Options" (O2:09E7).</summary>
    SoundOptions,

    /// <summary>Entry "Game Options" (O2:11CC).</summary>
    GameOptions,

    /// <summary>Entry "START": the main menu ends and the player selection follows (O2:52DD).</summary>
    Start,

    /// <summary>Button "Load Game" or key L: the main menu ends and the saved game is loaded (O3[2]).</summary>
    LoadGame,
}

/// <summary>Fonts the menus draw with.</summary>
/// <param name="Euro">EURO, registered as DS:E910 (header).</param>
/// <param name="Small">SmallFont LITT (entries).</param>
/// <param name="Default">8 x 8 DefaultFont ("Main Menu").</param>
public sealed record MenuFonts(BgiFont Euro, BgiFont Small, BitmapFont Default)
{
    /// <summary>Fonts from BOMB.EXE and the 8 x 8 font chosen by <see cref="BitmapFontSource"/>.</summary>
    public static MenuFonts Load(GameData gameData, BitmapFont defaultFont)
    {
        var fonts = BgiFontLibrary.Load(gameData);
        return new MenuFonts(fonts[BgiFontLibrary.Euro], fonts[BgiFontLibrary.Small], defaultFont);
    }
}

/// <summary>Settings the main menu starts with and the palette rotation it uses.</summary>
/// <param name="Players">
/// Number of players (DS:47DA). [ORIGINAL] BOMB.OVR 0x06C3A (O2:6652) sets 2 before BOMB.CFG is read.
/// </param>
/// <param name="Games">
/// Games to play (DS:E8A6). [ORIGINAL] BOMB.OVR 0x06C2F (O2:6647) sets 10, BOMB.CFG overwrites it (GamesToPlay).
/// </param>
/// <param name="MouseAvailable">Mouse driver found and enabled (DS:E94E); without it the mouse is ignored.</param>
/// <param name="PaletteRotation">"Palette" of BOMB.CFG (DS:E8B1); only then the rotation interrupt is installed.</param>
/// <param name="PaletteSpeed">"Pspeed" of BOMB.CFG (DS:E8C2): ticks per rotation step.</param>
/// <param name="PaletteUsesRetrace">"PV" (DS:E8BE): rotation on the vertical retrace instead of the timer.</param>
public sealed record MenuSettings(
    int Players = 2,
    int Games = 10,
    bool MouseAvailable = true,
    bool PaletteRotation = true,
    int PaletteSpeed = 0,
    bool PaletteUsesRetrace = false)
{
    /// <summary>Settings from BOMB.CFG as the original applies them before the main menu.</summary>
    public static MenuSettings FromConfig(GameConfig config, bool mouseAvailable)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new MenuSettings(
            Games: config.GamesToPlay,
            MouseAvailable: mouseAvailable && config.MouseEnabled,
            PaletteRotation: config.PaletteRotation,
            PaletteSpeed: config.PaletteSpeed,
            PaletteUsesRetrace: config.PaletteUsesRetrace);
    }
}

/// <summary>
/// Main menu of the original: O2[2] = O2:3D49 (BOMB.OVR 0x04331) draws background, header and menu and runs the menu
/// loop O2:1E7F (BOMB.OVR 0x02467); docs/anforderungen/menue.md.
/// </summary>
/// <remarks>
/// The screen keeps its own copy of video memory (<see cref="Screen"/>) and of the save buffer DS:E5BC
/// (<see cref="Saved"/>), both changed only by <see cref="Update"/>. Busy waits of the original (mouse release) become
/// states that last until the next update with the expected input.
/// </remarks>
public sealed class MenuScreen : IScreen
{
    /// <summary>Number of entries of the menu panel.</summary>
    public const int EntryCount = 5;

    /// <summary>[ORIGINAL] O2:2297/22A9: the selection runs from 0 (Sound Options) to 4 (START).</summary>
    public const int LastEntry = EntryCount - 1;

    /// <summary>[ORIGINAL] O2:24B9/24D0/24E5: players 2..10, wrapping to 2 above 10 on Enter.</summary>
    public const int MinPlayers = 2, MaxPlayers = 10;

    /// <summary>[ORIGINAL] O2:2599/25BC/25D9/25E6: games 1..99.</summary>
    public const int MinGames = 1, MaxGames = 99;

    /// <summary>[ORIGINAL] O2:3B9C–3BC0: background ramp in DAC entries 30..59.</summary>
    public const int BackgroundFirstColor = 30, BackgroundColorCount = 30;

    /// <summary>[ORIGINAL] O2:3BCD–3BEE: Random(2) = 0 draws frames 0..100, 1 draws 0..160.</summary>
    public static readonly IReadOnlyList<int> BackgroundFrameLimits = [100, 160];

    /// <summary>
    /// [APPROX] Timer tick of the rotation without "PV": INT 1Ch at 1193182 / 65536 Hz (PC hardware, not from the files).
    /// </summary>
    public static readonly TimeSpan TimerTick = TimeSpan.FromTicks(549254);

    /// <summary>[APPROX] Retrace tick with "PV": 70 Hz of VGA mode 13h (hardware, not measured in DOSBox).</summary>
    public static readonly TimeSpan RetraceTick = TimeSpan.FromTicks(142857);

    /// <summary>[ORIGINAL] O2:2211–222C: markers left and right of the selected entry at y = 20 · selection + 95.</summary>
    public const int MarkerLeftX = 112, MarkerRightX = 208, MarkerTopY = 95, EntryPitch = 20;

    private static readonly string[] EntryTexts = ["Sound Options", "Game Options", "Players: ", "Games: ", "START"];

    private readonly MenuFonts _fonts;
    private readonly MenuSettings _settings;
    private readonly Func<MenuChoice, MenuScreen, IScreen?> _choose;
    private readonly GraphText _text;
    private readonly byte[] _dac = new byte[Formats.Palette.ByteLength];
    private readonly byte[] _rotation = new byte[BackgroundColorCount * 3 * 2];
    private readonly Queue<ushort> _keys = new();
    private TimeSpan _tickTime;
    private int _tickCounter;
    private int _rotationPosition;

    // Locals of O2:1E7F: [bp-2] selection, [bp-4] previous selection, [bp-8] chosen entry or -1, [bp-9] Left key,
    // [bp-A] Right key, [bp-B] Enter/Space, [bp-C] left button, [bp-D] right button, [bp-E] START chosen.
    private int _previous;
    private int _chosen = -1;
    private bool _leftKey, _rightKey, _enter, _leftButton, _rightButton, _loadGame;
    private (int X, int Y)? _waitingForRelease;

    /// <param name="fonts">Fonts for header, entries and caption.</param>
    /// <param name="settings">Start values and palette rotation.</param>
    /// <param name="random">Turbo Pascal Random(n) of the game (called once, with n = 2, for the background).</param>
    /// <param name="choose">
    /// Called when an entry is activated; returns the next screen. For the options entries return this menu to stay
    /// (the options screens follow in later parts).
    /// </param>
    public MenuScreen(MenuFonts fonts, MenuSettings settings, Func<int, int> random, Func<MenuChoice, MenuScreen, IScreen?> choose)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(choose);
        _fonts = fonts;
        _settings = settings;
        _choose = choose;
        _text = new GraphText(fonts.Default);
        Players = settings.Players;
        Games = settings.Games;
        BuildScreen(random);
    }

    /// <summary>Video memory as the original leaves it (palette indices).</summary>
    public Surface Screen { get; } = new();

    /// <summary>Save buffer DS:E5BC: the screen after background and header, used to restore boxes and markers.</summary>
    public Surface Saved { get; } = new();

    /// <summary>The 6-bit DAC (768 values) including the current rotation step.</summary>
    public ReadOnlySpan<byte> Dac => _dac;

    /// <summary>Selected entry (0 = Sound Options … 4 = START).</summary>
    public int Selection { get; private set; }

    /// <summary>Number of players (DS:47DA).</summary>
    public int Players { get; private set; }

    /// <summary>Games to play (DS:E8A6).</summary>
    public int Games { get; private set; }

    /// <summary>Which of the two background patterns Random(2) chose (frame limit 100 or 160).</summary>
    public int BackgroundFrameLimit { get; private set; }

    public IScreen? Update(TimeSpan elapsed, ScreenInput input)
    {
        if (elapsed < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(elapsed), "Time cannot run backwards.");
        AdvanceRotation(elapsed);
        foreach (var key in input.Keys)
            _keys.Enqueue(key);

        if (_waitingForRelease is { } press)
        {
            // [ORIGINAL] O2:2397–23AF: wait until both buttons are released.
            if (ButtonHeld(input))
                return this;
            _waitingForRelease = null;
            var next = AfterMouse(press.X, press.Y);
            if (next is not null)
                return next.Screen;
        }

        // One pass of the inner loop O2:223C per key typed, at least one pass for the mouse.
        do
        {
            var key = _keys.Count > 0 ? _keys.Dequeue() : (ushort?)null;
            var result = Pass(key, input);
            if (result is not null)
                return result.Screen;
            if (_waitingForRelease is not null)
                return this;
        }
        while (_keys.Count > 0);

        return this;
    }

    public void Draw(Surface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (surface.Width != Screen.Width || surface.Height != Screen.Height)
            throw new ArgumentException("The menu draws into a 320 x 200 surface only.", nameof(surface));
        Screen.Pixels.CopyTo(surface.Pixels, 0);
        MenuGraphics.ApplyDac(_dac, surface);
    }

    private sealed record Leave(IScreen? Screen);

    private bool ButtonHeld(ScreenInput input) =>
        _settings.MouseAvailable && (input.MouseHolds(MouseButtons.Left) || input.MouseHolds(MouseButtons.Right));

    /// <summary>One pass of the loop O2:223C–23F5; returns non-null when the menu is left.</summary>
    private Leave? Pass(ushort? key, ScreenInput input)
    {
        if (key is { } word)
            ReadKey(word);

        // [ORIGINAL] O2:22D8–22F2: mouse only while button 1 or 2 is held (UE11:0077 returns 0 without a mouse driver).
        if (!ButtonHeld(input))
            return AfterPass();

        // [ORIGINAL] DBD:022A: x = Trunc(CX / 2.0); DBD:0256: y = DX.
        var x = input.MouseX / 2;
        var y = input.MouseY;
        _leftButton = input.MouseHolds(MouseButtons.Left);
        _rightButton = input.MouseHolds(MouseButtons.Right);

        // [ORIGINAL] O2:231D–2392: inside 122 < x < 198 the boxes y 91..101, 111..121, … choose an entry, and the menu
        // waits for the release.
        if (x > 0x7A && x < 0xC6)
        {
            for (var entry = 0; entry < EntryCount; entry++)
            {
                var top = 91 + entry * EntryPitch;
                if (y >= top && y <= top + 10)
                    _chosen = entry;
            }

            _waitingForRelease = (x, y);
            return null;
        }

        return AfterMouse(x, y);
    }

    /// <summary>O2:23B1–23D8 after the mouse test: Load Game button, then the chosen entry becomes the selection.</summary>
    private Leave? AfterMouse(int x, int y)
    {
        // [ORIGINAL] O2:23B1–23CF: 20 < x < 80 and 145 < y < 155 is the Load Game button.
        if (x > 0x14 && x < 0x50 && y > 0x91 && y < 0x9B)
            _loadGame = true;
        if (_chosen >= 0)
            Selection = _chosen;
        return AfterPass();
    }

    /// <summary>
    /// [ORIGINAL] O2:2248–22D5: Enter/Space choose the selection, L/l loads, extended keys from the table DS:3716
    /// (BOMB.EXE 0x19B06: 48h, 50h, 4Dh, 4Bh) move up/down or choose with Right/Left.
    /// </summary>
    private void ReadKey(ushort word)
    {
        var character = (byte)(word & 0xFF);
        if (character is 13 or 32)
        {
            _chosen = Selection;
            _enter = true;
        }

        if (character is (byte)'L' or (byte)'l')
            _loadGame = true;
        if (character != 0)
            return;

        var scan = (byte)(word >> 8);
        if (scan == 0x48 && Selection > 0)
            Selection--;
        if (scan == 0x50 && Selection < LastEntry)
            Selection++;
        if (scan == 0x4D)
        {
            _rightKey = true;
            _chosen = Selection;
        }

        if (scan == 0x4B)
        {
            _leftKey = true;
            _chosen = Selection;
        }
    }

    /// <summary>O2:23E0–26B7: leave the inner loop on a new selection, a chosen entry or Load Game.</summary>
    private Leave? AfterPass()
    {
        if (_previous == Selection && _chosen < 0 && !_loadGame)
            return null;

        // [ORIGINAL] O2:23F8–242E: hide the mouse, erase both markers at the previous selection.
        var markerY = EntryPitch * _previous + MarkerTopY;
        MenuGraphics.EraseMarker(Screen, Saved, MarkerLeftX, markerY);
        MenuGraphics.EraseMarker(Screen, Saved, MarkerRightX, markerY);
        if (_chosen is < 0 or > LastEntry && !_loadGame)
        {
            DrawMarkers();
            return null;
        }

        var leave = Activate();
        _chosen = -1;
        if (leave is not null)
            return leave;

        // [ORIGINAL] O2:21EF–2208: clear all flags, then draw the markers again.
        _leftKey = _rightKey = _enter = _leftButton = _rightButton = false;
        DrawMarkers();
        return null;
    }

    /// <summary>O2:244D–26A1: the action of the chosen entry.</summary>
    private Leave? Activate()
    {
        _text.Color = 14;
        if (_loadGame)
            return new Leave(_choose(MenuChoice.LoadGame, this));

        switch (_chosen)
        {
            case 0 or 1:
                // [ORIGINAL] O2:2468/2486: only Enter or a mouse button open the options, not Left/Right.
                if (_enter || _leftButton || _rightButton)
                {
                    var next = _choose(_chosen == 0 ? MenuChoice.SoundOptions : MenuChoice.GameOptions, this);
                    if (!ReferenceEquals(next, this))
                    {
                        // The options return into the loop at O2:21EF: prepare that state before handing over.
                        _chosen = -1;
                        _leftKey = _rightKey = _enter = _leftButton = _rightButton = false;
                        DrawMarkers();
                        return new Leave(next);
                    }
                }

                return null;
            case 2:
                ChangePlayers();
                return null;
            case 3:
                ChangeGames();
                return null;
            default:
                return new Leave(_choose(MenuChoice.Start, this));
        }
    }

    /// <summary>[ORIGINAL] O2:24A7–2582.</summary>
    private void ChangePlayers()
    {
        if ((_rightKey || _rightButton) && Players < MaxPlayers)
            Players++;
        if ((_leftKey || _leftButton) && Players > MinPlayers)
            Players--;
        if (_enter)
            Players++;
        if (Players > MaxPlayers)
            Players = MinPlayers;
        RedrawNumber(EntryTexts[2], Players, 135, -4, 10);
    }

    /// <summary>[ORIGINAL] O2:258D–269A: keys change by 1, mouse buttons by 5, Enter adds 5 and wraps above 99.</summary>
    private void ChangeGames()
    {
        if (_rightKey && Games < MaxGames)
            Games++;
        if (_rightButton && Games < MaxGames)
            Games += 5;
        if (_leftKey && Games > MinGames)
            Games--;
        if (_leftButton && Games > MinGames)
            Games -= 5;
        Games = Math.Clamp(Games, MinGames, MaxGames);
        if (_enter)
            Games += 5;
        if (Games > MaxGames)
            Games -= MaxGames;
        RedrawNumber(EntryTexts[3], Games, 155, -5, 15);
    }

    /// <summary>
    /// Restores the number area from the save buffer and writes the new value in yellow.
    /// [ORIGINAL] O2:24F2–257D / O2:260A–2695: the area runs from x − <paramref name="left"/> to x + <paramref name="right"/>
    /// and y − 2 to y + 4 with x = 160 + TextWidth(label) / 2, the SmallFont state of the menu is still set.
    /// </summary>
    private void RedrawNumber(string label, int value, int y, int left, int right)
    {
        _text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Center);
        var x = (_text.TextWidth(label) >> 1) + 160;
        MenuGraphics.CopyRect(Saved, Screen, x + left, y - 2, x + right, y + 4);
        _text.OutTextXY(Screen, x, y, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private void DrawMarkers()
    {
        // [ORIGINAL] O2:220B–2236: markers at the selection, then [bp-4] := selection.
        var y = EntryPitch * Selection + MarkerTopY;
        MenuGraphics.Marker(Screen, MarkerLeftX, y);
        MenuGraphics.Marker(Screen, MarkerRightX, y);
        _previous = Selection;
    }

    /// <summary>O2:3E0A–3E5B: palette, background, header, save buffer, caption and menu.</summary>
    private void BuildScreen(Func<int, int> random)
    {
        MenuGraphics.DefaultColors.CopyTo(_dac);
        MenuGraphics.SetFrameColors(_dac);
        DrawBackground(random);
        DrawHeader();

        // [ORIGINAL] O2:3E3F–3E51: copy of video memory (64000 bytes) to the buffer DS:E5BC.
        Screen.Pixels.CopyTo(Saved.Pixels, 0);
        DrawCaption();
        DrawMenu();
        DrawMarkers();
    }

    /// <summary>
    /// [ORIGINAL] O2:3B96 with argument 0 (BOMB.OVR 0x0417E): DAC entries 30..59 get (i, 0, i); frames k = 0..limit with
    /// corners (k, k) and (319 − k, 199 − k) in colour k mod 30 + 30 (UCC8:0031); limit 100 or 160 by Random(2);
    /// rotation of the entries 30..59 starts (O2:3C37–3C8E).
    /// </summary>
    private void DrawBackground(Func<int, int> random)
    {
        for (var i = BackgroundFirstColor; i < BackgroundFirstColor + BackgroundColorCount; i++)
            MenuGraphics.SetRgb(_dac, i, i, 0, i);

        var pick = random(2);
        if (pick is < 0 or > 1)
            throw new InvalidOperationException($"Random(2) returned {pick}.");
        BackgroundFrameLimit = BackgroundFrameLimits[pick];
        for (var k = 0; k <= BackgroundFrameLimit; k++)
            MenuGraphics.Rectangle(Screen, k, k, 319 - k, 199 - k, (byte)(k % 30 + BackgroundFirstColor));

        // [ORIGINAL] UCC8:0C20 reads the 30 DAC entries twice into DS:DFA6 and DS:E000 (a window that can wrap).
        var span = _dac.AsSpan(BackgroundFirstColor * 3, BackgroundColorCount * 3);
        span.CopyTo(_rotation);
        span.CopyTo(_rotation.AsSpan(span.Length));
    }

    /// <summary>
    /// [ORIGINAL] O2:06BD (BOMB.OVR 0x00CA5): button (10, 6)–(310, 42); EURO size 1, centred; "TANK WARS  " + version
    /// (DS:0056 = "3.2") at (163, 23), (162, 22), (161, 21) in yellow (14) and at (160, 20) in light red (12).
    /// </summary>
    private void DrawHeader()
    {
        MenuGraphics.Frame(Screen, 10, 6, 310, 42, MenuGraphics.FrameFace, 15, MenuGraphics.FrameDark);
        _text.SetTextStyle(_fonts.Euro, TextDirection.Horizontal, 1);
        _text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Center);
        const string title = "TANK WARS  3.2";
        _text.Color = 14;
        _text.OutTextXY(Screen, 163, 23, title);
        _text.OutTextXY(Screen, 162, 22, title);
        _text.OutTextXY(Screen, 161, 21, title);
        _text.Color = 12;
        _text.OutTextXY(Screen, 160, 20, title);
    }

    /// <summary>
    /// [ORIGINAL] O2:0909 (BOMB.OVR 0x00EF1): button (118, 63)–(202, 77), "Main Menu" in DefaultFont, blue (1), centred
    /// at (160, 70).
    /// </summary>
    private void DrawCaption()
    {
        MenuGraphics.Frame(Screen, 118, 63, 202, 77, MenuGraphics.FrameFace, 15, MenuGraphics.FrameDark);
        _text.Color = 1;
        _text.SetTextStyle(null, TextDirection.Horizontal, 1);
        _text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Center);
        _text.OutTextXY(Screen, 160, 70, "Main Menu");
    }

    /// <summary>
    /// [ORIGINAL] O2:1E7F–21D7 (BOMB.OVR 0x02467): panel (118, 85)–(202, 187); per entry a sunken green box
    /// (122, 91 + 20n)–(198, 101 + 20n) whose inside is restored from the save buffer; SmallFont with
    /// SetUserCharSize(1, 1, 1, 1), centred at (160, 95 + 20n); colours light green (10), numbers yellow (14), START light
    /// magenta (13); Load Game button (20, 145)–(80, 155) with a red "L" over the green text at (50, 149).
    /// </summary>
    private void DrawMenu()
    {
        MenuGraphics.Frame(Screen, 118, 85, 202, 187, MenuGraphics.FrameFace, 15, MenuGraphics.FrameDark);
        _text.Color = 10;
        _text.SetTextStyle(_fonts.Small, TextDirection.Horizontal, 1);
        _text.SetUserCharSize(1, 1, 1, 1);
        for (var entry = 0; entry < EntryCount; entry++)
        {
            var top = 91 + entry * EntryPitch;
            MenuGraphics.Frame(Screen, 122, top, 198, top + 10, 2, MenuGraphics.FrameDark, 15);
            MenuGraphics.CopyRect(Saved, Screen, 122, top, 198, top + 10);
            _text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Center);
            var y = top + 4;
            switch (entry)
            {
                case 2:
                case 3:
                    _text.Color = 10;
                    _text.OutTextXY(Screen, 160, y, EntryTexts[entry]);
                    _text.Color = 14;
                    var x = (_text.TextWidth(EntryTexts[entry]) >> 1) + 160;
                    var value = entry == 2 ? Players : Games;
                    _text.OutTextXY(Screen, x, y, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case 4:
                    _text.Color = 13;
                    _text.OutTextXY(Screen, 160, y, EntryTexts[entry]);
                    break;
                default:
                    _text.OutTextXY(Screen, 160, y, EntryTexts[entry]);
                    break;
            }
        }

        _text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Center);
        MenuGraphics.Frame(Screen, 20, 145, 80, 155, MenuGraphics.FrameFace, 15, MenuGraphics.FrameDark);
        _text.Color = 10;
        _text.OutTextXY(Screen, 50, 149, "Load Game");
        _text.Color = 12;
        _text.OutTextXY(Screen, 50, 149, "L        ");
    }

    /// <summary>
    /// Palette rotation of the entries 30..59 by the interrupt handler UC79:00E3 (BOMB.EXE 0x0DF13).
    /// [ORIGINAL] Per tick the counter DS:E8DE rises; when it reaches the speed DS:E8DD it is reset and the 30 entries are
    /// set from the doubled buffer at position DS:E8DA (0..29, reset above 29), which then advances by one.
    /// [APPROX] Tick lengths from PC hardware (<see cref="TimerTick"/>, <see cref="RetraceTick"/>); the counter starts at 0.
    /// </summary>
    private void AdvanceRotation(TimeSpan elapsed)
    {
        if (!_settings.PaletteRotation)
            return;
        var tick = _settings.PaletteUsesRetrace ? RetraceTick : TimerTick;
        _tickTime += elapsed;
        while (_tickTime >= tick)
        {
            _tickTime -= tick;
            _tickCounter = (_tickCounter + 1) & 0xFF;
            if (_tickCounter < (_settings.PaletteSpeed & 0xFF))
                continue;
            _tickCounter = 0;
            if (_rotationPosition > BackgroundColorCount - 1)
                _rotationPosition = 0;
            _rotation.AsSpan(_rotationPosition * 3, BackgroundColorCount * 3)
                .CopyTo(_dac.AsSpan(BackgroundFirstColor * 3));
            _rotationPosition++;
        }
    }
}
