using System.Globalization;
using BombRedone.Formats;
using BombRedone.UI.Text;

namespace BombRedone.UI.Screens;

/// <summary>
/// "Game Options" of the main menu: O2:11CC (BOMB.OVR 0x017B4), called from the menu loop at O2:2499;
/// docs/anforderungen/menue.md §6.
/// </summary>
/// <remarks>
/// The screen draws on top of the main menu: it changes <see cref="MenuScreen.Screen"/> and hands the menu back when it
/// is left, after restoring the area it covered. The palette rotation of the menu keeps running (it is an interrupt in
/// the original). Busy waits (mouse release) become states that last until the next update with the expected input;
/// one update runs one pass of the loop per key typed, at least one for the mouse.
/// </remarks>
public sealed class OptionsScreen : IScreen
{
    /// <summary>Number of option lines; line <see cref="LeaveLine"/> stands for "leave the screen".</summary>
    public const int LineCount = 10;

    /// <summary>[ORIGINAL] O2:15D5–16B7, O2:151D: Enter, Esc and a click outside the lines choose line 10 = leave.</summary>
    public const int LeaveLine = 10;

    /// <summary>[ORIGINAL] O2:11E2–11F7 / O2:1DB0–1DC0: GetImage and PutImage of (93, 45)–(227, 155).</summary>
    public const int SavedLeft = 93, SavedTop = 45, SavedRight = 227, SavedBottom = 155;

    /// <summary>[ORIGINAL] O2:14AE–14CF: markers at x = 89 and 231, y = 10 · line + 52.</summary>
    public const int MarkerLeftX = 89, MarkerRightX = 231, LineTopY = 52, LinePitch = 10;

    /// <summary>[ORIGINAL] O2:1231–1344 and O2:135F–147F: labels left-aligned at x = 100, values right-aligned at 223.</summary>
    public const int LabelX = 100, ValueX = 223;

    private static readonly string[] Labels =
    [
        "Crumble Percent:", "Walls:", "Trail Visible:", "Ball Visible:", "Tanks Fall:", "Computer Can Buy:",
        "Background:", "Cycle Delay:", "Wind Force:", "Team Play:",
    ];

    /// <summary>
    /// [ORIGINAL] Bar that clears a value before it is redrawn (O2:17A5, 183B, 18D3, 1974, 1A1C, 1AB6, 1B5B, 1C1F, 1CAD,
    /// 1D45): left x and bottom y per line; top y − 4, right 225. Lines 6 and 8 reach two pixels lower.
    /// </summary>
    private static readonly int[] ClearLeft = [195, 175, 195, 195, 195, 195, 165, 195, 195, 195];

    private static readonly int[] ClearBottom = [56, 66, 76, 86, 96, 106, 118, 126, 138, 146];

    /// <summary>
    /// [ORIGINAL] Lines after whose change the screen waits until both mouse buttons are released: all but Crumble
    /// Percent (O2:17E2) and Cycle Delay (O2:1C52), which repeat on every pass while a button is held.
    /// </summary>
    private static readonly bool[] WaitsForRelease = [false, true, true, true, true, true, true, false, true, true];

    private readonly MenuScreen _menu;
    private readonly OptionsSettings _options;
    private readonly GraphText _text;
    private readonly Surface _covered = new();
    private readonly Queue<ushort> _keys = new();

    // Locals of O2:11CC: [bp-6] line, [bp-8] previous line, [bp-A] chosen line or -1, [bp-B] leave, [bp-C] left button,
    // [bp-D] right button, [bp-F] Left key, [bp-10] Right key, [bp-11] Space.
    private int _previous;
    private int _chosen = -1;
    private bool _leftButton, _rightButton, _leftKey, _rightKey, _space;
    private bool _waitingForRelease;

    /// <param name="menu">The main menu the options are drawn on and that is shown again afterwards.</param>
    /// <param name="options">The settings to show and change.</param>
    public OptionsScreen(MenuScreen menu, OptionsSettings options)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(options);
        _menu = menu;
        _options = options;

        // Text state the main menu leaves (O2:1E7F: SmallFont, SetUserCharSize(1, 1, 1, 1)); the options set only colour
        // and justification.
        _text = new GraphText(menu.Fonts.Default);
        _text.SetTextStyle(menu.Fonts.Small, TextDirection.Horizontal, 1);
        _text.SetUserCharSize(1, 1, 1, 1);
        BuildScreen();
    }

    /// <summary>Line with the markers (0 = Crumble Percent … 9 = Team Play).</summary>
    public int Selection { get; private set; }

    /// <summary>The main menu below the options.</summary>
    public MenuScreen Menu => _menu;

    public IScreen? Update(TimeSpan elapsed, ScreenInput input)
    {
        _menu.RunPaletteRotation(elapsed);
        foreach (var key in input.Keys)
            _keys.Enqueue(key);

        if (_waitingForRelease)
        {
            // [ORIGINAL] e.g. O2:186B–1883: wait until both buttons are released.
            if (ButtonHeld(input))
                return this;
            _waitingForRelease = false;
            StartRound();
        }

        // One pass of the loop O2:14DF–16D6 per key typed, at least one for the mouse.
        do
        {
            var key = _keys.Count > 0 ? _keys.Dequeue() : (ushort?)null;
            if (Pass(key, input))
                return Leave();
            if (_waitingForRelease)
                return this;
        }
        while (_keys.Count > 0);

        return this;
    }

    public void Draw(Surface surface) => _menu.Draw(surface);

    private bool ButtonHeld(ScreenInput input) =>
        _menu.Settings.MouseAvailable && (input.MouseHolds(MouseButtons.Left) || input.MouseHolds(MouseButtons.Right));

    /// <summary>
    /// [ORIGINAL] O2:1DB0–1DCF: PutImage of the saved area, FreeMem; the menu continues at O2:249C (markers again,
    /// <see cref="MenuScreen.Resume"/>).
    /// </summary>
    private MenuScreen Leave()
    {
        MenuGraphics.CopyRect(_covered, _menu.Screen, SavedLeft, SavedTop, SavedRight, SavedBottom);
        _menu.Resume();
        return _menu;
    }

    /// <summary>One pass; returns true when the screen is left.</summary>
    private bool Pass(ushort? key, ScreenInput input)
    {
        if (key is { } word)
            ReadKey(word);

        // [ORIGINAL] O2:157B–16B7: mouse only while button 1 or 2 is held.
        if (ButtonHeld(input))
        {
            _leftButton = input.MouseHolds(MouseButtons.Left);
            _rightButton = input.MouseHolds(MouseButtons.Right);
            _chosen = LineAt(input.MouseX / 2, input.MouseY);
        }

        // [ORIGINAL] O2:16BC–16D6: a chosen line becomes the selection; nothing new: next pass.
        if (_chosen >= 0)
            Selection = _chosen;
        if (_previous == Selection && _chosen < 0)
            return false;

        // [ORIGINAL] O2:16D9–170F: hide the mouse, erase both markers at the previous line from the menu buffer DS:E5BC.
        var y = LinePitch * _previous + LineTopY;
        MenuGraphics.EraseMarker(_menu.Screen, _menu.Saved, MarkerLeftX, y);
        MenuGraphics.EraseMarker(_menu.Screen, _menu.Saved, MarkerRightX, y);

        // [ORIGINAL] O2:1714–1724: without a chosen line (0..10) only the markers move (back to O2:14AE).
        if (_chosen is < 0 or > LeaveLine)
        {
            DrawMarkers();
            return false;
        }

        var line = _chosen;
        if (line == LeaveLine)
            return true;
        Change(line);

        // [ORIGINAL] O2:1DA2: [bp-A] := -1, back to O2:1492 – after the wait for the release where the line has one.
        _chosen = -1;
        if (WaitsForRelease[line] && ButtonHeld(input))
        {
            _waitingForRelease = true;
            return false;
        }

        StartRound();
        return false;
    }

    /// <summary>
    /// [ORIGINAL] O2:14DF–157B: Space changes the selected line forwards, Enter and Esc (set O2:118C = [13, 27]) leave;
    /// after a zero character the extended key from DS:3716: up/down move the selection (0..9, no wrap), right/left
    /// change the selected line.
    /// </summary>
    private void ReadKey(ushort word)
    {
        var character = (byte)(word & 0xFF);
        if (character == 32)
        {
            _chosen = Selection;
            _space = true;
        }

        if (character is 13 or 27)
            _chosen = LeaveLine;
        if (character != 0)
            return;

        var scan = (byte)(word >> 8);
        if (scan == 0x48 && Selection > 0)
            Selection--;
        if (scan == 0x50 && Selection < LineCount - 1)
            Selection++;
        if (scan == 0x4D)
        {
            _chosen = Selection;
            _rightKey = true;
        }

        if (scan == 0x4B)
        {
            _chosen = Selection;
            _leftKey = true;
        }
    }

    /// <summary>
    /// [ORIGINAL] O2:15C0–16B7: inside 95 &lt; x &lt; 225 line n covers 49 + 10n &lt; y ≤ 58 + 10n (line 0 from y = 50,
    /// line 9 from 139 to 152); everything else, also outside the box, is line 10 = leave.
    /// </summary>
    public static int LineAt(int x, int y)
    {
        if (x <= 0x5F || x >= 0xE1)
            return LeaveLine;
        if (y > 0x31 && y <= 0x3A)
            return 0;
        for (var line = 1; line < LineCount - 1; line++)
        {
            if (y > 48 + LinePitch * line && y <= 58 + LinePitch * line)
                return line;
        }

        return y > 0x8A && y < 0x99 ? LineCount - 1 : LeaveLine;
    }

    /// <summary>[ORIGINAL] O2:1492–14D9: clear all flags, markers at the selection, show the mouse, remember the line.</summary>
    private void StartRound()
    {
        _leftButton = _rightButton = _leftKey = _rightKey = _space = false;
        DrawMarkers();
    }

    private void DrawMarkers()
    {
        var y = LinePitch * Selection + LineTopY;
        MenuGraphics.Marker(_menu.Screen, MarkerLeftX, y);
        MenuGraphics.Marker(_menu.Screen, MarkerRightX, y);
        _previous = Selection;
    }

    private bool Up => _rightKey || _rightButton;

    private bool Down => _leftKey || _leftButton;

    /// <summary>
    /// [ORIGINAL] O2:1727–1D97: SetColor(14), SetFillStyle(SolidFill, 18); change the value, clear its place with Bar
    /// (ClearLeft, y − 4)–(225, ClearBottom) and write it right-aligned at (223, y).
    /// </summary>
    private void Change(int line)
    {
        switch (line)
        {
            case 0:
                // [ORIGINAL] O2:1752–17A2: 0..100 by 1, Space adds 5 and wraps above 100 by − 100.
                if (Up && _options.CrumblePercent < 100)
                    _options.CrumblePercent++;
                if (Down && _options.CrumblePercent > 0)
                    _options.CrumblePercent--;
                if (_space)
                    _options.CrumblePercent += 5;
                if (_options.CrumblePercent > 100)
                    _options.CrumblePercent -= 100;
                break;
            case 1:
                // [ORIGINAL] O2:17F3–1838: 0..5, Space (v + 1) mod 6.
                _options.Walls = (WallSetting)Step((int)_options.Walls, 5, 6);
                break;
            case 2:
                // [ORIGINAL] O2:1896–18D0: right sets, left clears, Space toggles.
                _options.TrailVisible = Toggle(_options.TrailVisible);
                break;
            case 3:
                // [ORIGINAL] O2:1930–1971: the shown value is "not Inviso".
                _options.InvisibleShot = !Toggle(!_options.InvisibleShot);
                break;
            case 4:
                // [ORIGINAL] O2:19DF–1A19.
                _options.TanksFall = Toggle(_options.TanksFall);
                break;
            case 5:
                // [ORIGINAL] O2:1A79–1AB3.
                _options.ComputersBuy = Toggle(_options.ComputersBuy);
                break;
            case 6:
                // [ORIGINAL] O2:1B13–1B58: 0..10, Space (v + 1) mod 11.
                _options.Background = Step(_options.Background, 10, 11);
                break;
            case 7:
                ChangeCycleDelay();
                break;
            case 8:
                // [ORIGINAL] O2:1C63–1CAA: 0..5, Space + 1, above 5 back to 0.
                var wind = (int)_options.Wind;
                if (Up && wind < 5)
                    wind++;
                if (Down && wind > 0)
                    wind--;
                if (_space)
                    wind++;
                if (wind > 5)
                    wind = 0;
                _options.Wind = (WindSetting)wind;
                break;
            default:
                // [ORIGINAL] O2:1D08–1D42.
                _options.TeamPlay = Toggle(_options.TeamPlay);
                break;
        }

        DrawValue(line, clear: true);
    }

    /// <summary>[ORIGINAL] O2:1BB6–1C1A: 0..250 by 1, Space sets 0 if above 0, else 1; the rotation gets the new speed.</summary>
    private void ChangeCycleDelay()
    {
        var delay = _menu.PaletteSpeed;
        if (Up && delay < 250)
            delay++;
        if (Down && delay > 0)
            delay--;
        if (_space)
            delay = delay > 0 ? 0 : 1;
        _menu.PaletteSpeed = delay;
    }

    /// <summary>Up + 1 to <paramref name="max"/>, down − 1 to 0, Space (v + 1) mod <paramref name="modulus"/> (Turbo Pascal mod).</summary>
    private int Step(int value, int max, int modulus)
    {
        if (Up && value < max)
            value++;
        if (Down && value > 0)
            value--;
        if (_space)
            value = (value + 1) % modulus;
        return value;
    }

    private bool Toggle(bool value)
    {
        if (Up)
            value = true;
        if (Down)
            value = false;
        if (_space)
            value = !value;
        return value;
    }

    private string ValueText(int line) => line switch
    {
        0 => _options.CrumblePercent.ToString(CultureInfo.InvariantCulture) + "%",
        1 => OptionsSettings.WallName(_options.Walls),
        2 => OptionsSettings.YesNo(_options.TrailVisible),
        3 => OptionsSettings.YesNo(!_options.InvisibleShot),
        4 => OptionsSettings.YesNo(_options.TanksFall),
        5 => OptionsSettings.YesNo(_options.ComputersBuy),
        6 => OptionsSettings.BackgroundName(_options.Background),
        7 => _menu.PaletteSpeed.ToString(CultureInfo.InvariantCulture),
        8 => OptionsSettings.WindName(_options.Wind),
        _ => OptionsSettings.YesNo(_options.TeamPlay),
    };

    private void DrawValue(int line, bool clear)
    {
        var y = LineTopY + LinePitch * line;
        if (clear)
        {
            _text.Color = 14;
            MenuGraphics.Bar(_menu.Screen, ClearLeft[line], y - 4, 225, ClearBottom[line], MenuGraphics.FrameFace);
        }

        _text.SetTextJustify(HorizontalJustify.Right, VerticalJustify.Center);
        _text.OutTextXY(_menu.Screen, ValueX, y, ValueText(line));
    }

    /// <summary>
    /// [ORIGINAL] O2:11CC–14D9: save (93, 45)–(227, 155) (GetImage into GetMem(3940)), button (95, 47)–(225, 153) in the
    /// menu colours, labels black left-aligned and values yellow right-aligned, each vertically centred at y = 52 + 10n;
    /// markers at line 0.
    /// [APPROX] The buffer of 3940 bytes is smaller than the image (135 × 111 pixels); the original overwrites the heap
    /// behind it. Here the whole area is restored when the screen is left, as PutImage would do with an intact heap.
    /// </summary>
    private void BuildScreen()
    {
        var screen = _menu.Screen;
        MenuGraphics.CopyRect(screen, _covered, SavedLeft, SavedTop, SavedRight, SavedBottom);
        MenuGraphics.Frame(screen, 95, 47, 225, 153, MenuGraphics.FrameFace, 15, MenuGraphics.FrameDark);

        _text.Color = 0;
        _text.SetTextJustify(HorizontalJustify.Left, VerticalJustify.Center);
        for (var line = 0; line < LineCount; line++)
            _text.OutTextXY(screen, LabelX, LineTopY + LinePitch * line, Labels[line]);

        _text.Color = 14;
        for (var line = 0; line < LineCount; line++)
            DrawValue(line, clear: false);

        StartRound();
    }
}
