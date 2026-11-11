using BombRedone.Core;
using BombRedone.Core.Setup;
using BombRedone.UI.Text;

namespace BombRedone.UI.Screens;

/// <summary>
/// Player selection of the original: O2:52DD (BOMB.OVR 0x058C5), called by O2:3D49 after the main menu ends with
/// START; with the big START button O2:07BA, the name boxes O2:4F0E, the control words O2:509C, the name entry O2:42E8
/// and the list of computer opponents O2:481B; docs/anforderungen/spielerauswahl.md.
/// </summary>
/// <remarks>
/// The screen keeps its own copy of video memory (<see cref="Screen"/>), starts from the save buffer of the main menu
/// (background and header) and restores markers from it, like the original. The palette rotation of the menu keeps
/// running. Busy waits become states; one update runs one pass of the active loop per key typed, at least one for the
/// mouse. The name entry and the opponent list are modes of this screen (they are nested loops in the original).
/// </remarks>
public sealed class PlayerSelectScreen : IScreen
{
    /// <summary>[ORIGINAL] O2:52E7–537F: box n has its upper left corner at x = 10 + 61 · ((n − 1) mod 5), y = 50 or 111.</summary>
    public const int BoxLeft = 10, BoxPitch = 61, BoxTopRow1 = 50, BoxTopRow2 = 111, BoxSize = 55;

    /// <summary>[ORIGINAL] O2:5849: START is chosen with the mouse inside 71 &lt; x &lt; 249, 171 &lt; y &lt; 197.</summary>
    public const int StartLeft = 71, StartTop = 171, StartRight = 248, StartBottom = 197;

    /// <summary>"Chosen entry" value of START ([bp-58h] = 10).</summary>
    public const int StartEntry = 10;

    /// <summary>[ORIGINAL] O2:42F3 / O2:46C1: GetImage and PutImage of (108, 83)–(212, 117) around the name entry.</summary>
    public const int NameLeft = 108, NameTop = 83, NameRight = 212, NameBottom = 117;

    /// <summary>[ORIGINAL] O2:4006/4153: characters of the name entry at x = 118 + 6 · (k − 1), y = 104.</summary>
    public const int NameFirstX = 118, NamePitch = 6, NameY = 104;

    /// <summary>[ORIGINAL] O2:485F / O2:4EF4: GetImage and PutImage of (114, 56)–(206, 144) around the opponent list.</summary>
    public const int ListLeft = 114, ListTop = 56, ListRight = 206, ListBottom = 144;

    /// <summary>[ORIGINAL] O2:48ED–4B94: entries centred at x = 160, y = 63 + 10 · k; markers at x = 119 and 201.</summary>
    public const int ListTextX = 160, ListTopY = 63, ListPitch = 10, ListMarkerLeftX = 119, ListMarkerRightX = 201;

    /// <summary>[ORIGINAL] O2:4D30–4DD6: row k of the list is hit for ListRowTop[k] &lt; y ≤ ListRowTop[k + 1], the last below 142.</summary>
    private static readonly int[] ListRowTop = [60, 69, 79, 89, 99, 109, 119, 129];

    /// <summary>Last entry of the opponent list (0 = Random … 7 = Wind Master).</summary>
    public const int LastListEntry = 7;

    /// <summary>
    /// [ORIGINAL] Words 0x60, 0x62, 0x64 of the player record set by O2:0216 (BOMB.OVR 0x00929–0x00A76): O10:01AC
    /// sets DAC entry 19 + n to (63 div r, 63 div g, 63 div b) (BOMB.OVR 0x1F2CF); 0x7FFF gives 0.
    /// </summary>
    public static readonly IReadOnlyList<(int R, int G, int B)> ColorDivisors =
    [
        (1, 0x7FFF, 0x7FFF), (0x7FFF, 1, 0x7FFF), (0x7FFF, 0x7FFF, 1), (1, 1, 0x7FFF), (0x7FFF, 1, 1),
        (1, 0x7FFF, 1), (1, 1, 1), (1, 2, 0x7FFF), (1, 0x7FFF, 2), (1, 2, 2),
    ];

    /// <summary>First DAC entry of the player colours: player n uses 19 + n (O2:4FB6).</summary>
    public const int PlayerColorBase = 19;

    // [ORIGINAL] Texts of O2:481B with their O2 offsets (BOMB.OVR 0x005E8 + offset): each entry is drawn in parts on
    // top of each other at the same point (OutText with centred text does not move the current point); the key letter
    // is yellow.
    private static readonly (int Offset, string Text, byte Color)[][] ListParts =
    [
        [(0x46DB, "Random (  ", 6), (0x46E6, "        ? ", 14), (0x46F1, "         )", 6)],
        [(0x46FC, "Mr.       ", 6), (0x4707, "    S     ", 14), (0x4712, "     tupid", 6)],
        [(0x471D, "L     ", 14), (0x4724, " obber", 6)],
        [(0x472B, "R       ", 14), (0x4734, " ifleman", 6)],
        [(0x473D, "Lob & Shoot (  ", 6), (0x474D, "             B ", 14), (0x475D, "              )", 6)],
        [(0x476D, "W           ", 14), (0x477A, " indless Wit", 6)],
        [(0x4787, "T      ", 14), (0x478F, " wanger", 6)],
        [(0x4797, "Wind       ", 6), (0x47A3, "     M     ", 14), (0x47AF, "      aster", 6)],
    ];

    /// <summary>Overlay O2 starts at this offset of BOMB.OVR (re-grundlagen §6).</summary>
    public const int OverlayO2FileOffset = 0x005E8;

    /// <summary>
    /// Texts this screen draws with their file offset in BOMB.OVR (length byte of the Pascal string), for checks against
    /// the original.
    /// </summary>
    public static IReadOnlyList<(int FileOffset, string Text)> OriginalTexts { get; } =
    [
        .. new (int Offset, string Text)[]
        {
            (0x07B4, "START"), (0x5084, "KEYBOARD"), (0x508D, "MOUSE"), (0x5093, "COMPUTER"), (0x425F, "NAME:"),
            (0x52D6, "Random"), (0x020E, "Player "),
        }.Select(t => (OverlayO2FileOffset + t.Offset, t.Text)),
        .. ListParts.SelectMany(e => e).Select(t => (OverlayO2FileOffset + t.Offset, t.Text)),
    ];

    private enum Mode
    {
        Select,
        Name,
        List,
    }

    private readonly MenuScreen _menu;
    private readonly PlayerRoster _roster;
    private readonly Func<int, int> _random;
    private readonly Func<GameSetup, IScreen?> _start;
    private readonly GraphText _text;
    private readonly Surface _covered = new();
    private readonly byte[] _playerDac = new byte[GameLimits.MaxPlayers * 3];
    private readonly Queue<ushort> _keys = new();
    private Mode _mode = Mode.Select;

    // Locals of O2:52DD: [bp-54] selection, [bp-56] previous, [bp-58] chosen or -1, [bp-5D] keyboard, [bp-5E] mouse
    // (both = computer), [bp-5F] name, [bp-63] done.
    private int _previous;
    private int _chosen = -1;
    private bool _keyboard, _mouse, _name;
    private bool _done;
    private int _actionPlayer;
    private PlayerControl _oldControl;
    private int _actionStep;

    // O2:42E8: name characters 1..13 (index 0 unused), length, cursor [bp-6], new cursor [bp-8], changed [bp-C],
    // finished [bp-B].
    private readonly char[] _nameChars = new char[PlayerSlot.MaxNameLength + 1];
    private int _nameLength, _cursor, _newCursor;
    private bool _changed, _nameDone, _cursorShown;

    // O2:481B: [bp-6] selection, [bp-8] previous, [bp-A] chosen or -1, [bp-D] done.
    private int _listSelection, _listPrevious, _listChosen = -1;
    private bool _listWaitingBeforeOpen, _listWaitingForRelease, _waitingBeforeStart;

    /// <param name="menu">The main menu that ended with START: save buffer, palette, fonts, settings, player count.</param>
    /// <param name="roster">The player records; names and controls are changed in place.</param>
    /// <param name="random">Turbo Pascal Random(n), used when START resolves "Random" computers.</param>
    /// <param name="start">Called with the setup when the selection ends; returns the next screen.</param>
    public PlayerSelectScreen(MenuScreen menu, PlayerRoster roster, Func<int, int> random, Func<GameSetup, IScreen?> start)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(start);
        _menu = menu;
        _roster = roster;
        _random = random;
        _start = start;
        _roster.Players = menu.Players;
        _text = new GraphText(menu.Fonts.Default);

        // [APPROX] The menu leaves through transitions (UCC8:0687 circles from the save buffer, O2:3E63–3EFB) that are
        // not reproduced; they restore the areas of menu panel and Load Game, so the selection starts on the buffer.
        menu.Saved.Pixels.CopyTo(Screen.Pixels, 0);
        DrawStartButton();
        DrawBoxes();
        DrawMarker();
    }

    /// <summary>Video memory as the original leaves it (palette indices).</summary>
    public Surface Screen { get; } = new();

    /// <summary>Selected player box, 0-based ([bp-54h]).</summary>
    public int Selection { get; private set; }

    /// <summary>True while the name entry O2:42E8 is open.</summary>
    public bool EnteringName => _mode == Mode.Name;

    /// <summary>True while the list of computer opponents O2:481B is open.</summary>
    public bool ChoosingOpponent => _mode == Mode.List;

    /// <summary>Selected entry of the opponent list while it is open.</summary>
    public int ListSelection => _listSelection;

    /// <summary>Box corners of player <paramref name="number"/> (1..10).</summary>
    public static (int X1, int Y1, int X2, int Y2) Box(int number)
    {
        if (number is < 1 or > GameLimits.MaxPlayers)
            throw new ArgumentOutOfRangeException(nameof(number));
        var x1 = BoxLeft + BoxPitch * ((number - 1) % 5);
        var y1 = number <= 5 ? BoxTopRow1 : BoxTopRow2;
        return (x1, y1, x1 + BoxSize, y1 + BoxSize);
    }

    /// <summary>DAC value (6 bit) of the colour of player <paramref name="number"/>.</summary>
    public static (byte R, byte G, byte B) PlayerColor(int number)
    {
        var (r, g, b) = ColorDivisors[number - 1];
        return ((byte)(63 / r), (byte)(63 / g), (byte)(63 / b));
    }

    public IScreen? Update(TimeSpan elapsed, ScreenInput input)
    {
        if (elapsed < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(elapsed), "Time cannot run backwards.");
        _menu.RunPaletteRotation(elapsed);
        foreach (var key in input.Keys)
            _keys.Enqueue(key);

        do
        {
            if (_waitingBeforeStart)
            {
                // [ORIGINAL] O2:5ACB–5AE3: wait until both buttons are released, then the wipe and the game follow.
                if (ButtonHeld(input))
                    return this;
                return _start(_roster.Start(_random));
            }

            if (Blocked(input))
                return this;
            var key = _keys.Count > 0 ? _keys.Dequeue() : (ushort?)null;
            switch (_mode)
            {
                case Mode.Select:
                    SelectPass(key, input);
                    break;
                case Mode.Name:
                    if (key is { } k)
                        NamePass(k);
                    break;
                default:
                    ListPass(key, input);
                    break;
            }

            if (Blocked(input))
                return this;
        }
        while (_keys.Count > 0 || _waitingBeforeStart && !ButtonHeld(input));

        return this;
    }

    public void Draw(Surface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (surface.Width != Screen.Width || surface.Height != Screen.Height)
            throw new ArgumentException("The player selection draws into a 320 x 200 surface only.", nameof(surface));
        Screen.Pixels.CopyTo(surface.Pixels, 0);
        Span<byte> dac = stackalloc byte[Formats.Palette.ByteLength];
        _menu.Dac.CopyTo(dac);
        _playerDac.CopyTo(dac[((PlayerColorBase + 1) * 3)..]);
        MenuGraphics.ApplyDac(dac, surface);
    }

    private bool ButtonHeld(ScreenInput input) =>
        _menu.Settings.MouseAvailable && (input.MouseHolds(MouseButtons.Left) || input.MouseHolds(MouseButtons.Right));

    /// <summary>Waits for the mouse release of the opponent list (before it opens and after a click).</summary>
    private bool Blocked(ScreenInput input)
    {
        if (_listWaitingBeforeOpen)
        {
            // [ORIGINAL] O2:4821–4839: the list opens only after both buttons are released.
            if (ButtonHeld(input))
                return true;
            _listWaitingBeforeOpen = false;
            OpenList();
        }

        if (_listWaitingForRelease)
        {
            // [ORIGINAL] O2:4DF5–4E0D.
            if (ButtonHeld(input))
                return true;
            _listWaitingForRelease = false;
            ListAfterMouse();
        }

        return false;
    }

    // ---------------------------------------------------------------- O2:52DD main loop

    /// <summary>One pass of the loop O2:551E–585C.</summary>
    private void SelectPass(ushort? key, ScreenInput input)
    {
        if (key is { } word)
            SelectKey(word);

        // [ORIGINAL] O2:5633–5849: mouse only while button 1 or 2 is held; x = Trunc(CX / 2), y = DX.
        if (ButtonHeld(input))
        {
            var x = input.MouseX / 2;
            var y = input.MouseY;
            for (var n = 1; n <= _roster.Players; n++)
            {
                var (x1, y1, x2, y2) = Box(n);
                var inColumn = x > x1 && x < x2;
                if (inColumn && y > y1 && y < y2)
                    Selection = n - 1;
                // The regions only set their flags (the keys K/M/C also clear the other one).
                if (x1 + 3 < x && y1 + 3 < y && x2 - 3 > x && y1 + 12 > y)
                    Choose(n - 1, name: true);
                if (inColumn && y1 + 15 < y && y1 + 25 > y)
                    _keyboard = Choose(n - 1);
                if (inColumn && y1 + 25 < y && y1 + 35 > y)
                    _mouse = Choose(n - 1);
                if (inColumn && y1 + 35 < y && y1 + 45 > y)
                    _keyboard = _mouse = Choose(n - 1);
            }

            if (x > StartLeft && x < StartRight + 1 && y > StartTop && y < StartBottom)
                _chosen = StartEntry;
        }

        // [ORIGINAL] O2:584E–585C: nothing to do without a new selection or a choice.
        if (_previous == Selection && _chosen < 0)
            return;

        // [ORIGINAL] O2:585F–589A: hide the mouse, erase the marker at the previous box.
        EraseMarker(_previous);
        if (_chosen is < 0 or > StartEntry)
        {
            DrawMarker();
            return;
        }

        _text.Color = 14;
        if (_chosen == StartEntry)
        {
            _done = true;
            FinishAction();
            return;
        }

        _actionPlayer = _chosen + 1;
        _actionStep = 0;
        ContinueAction();
    }

    /// <summary>Selection and choice := <paramref name="entry"/>; returns true for the flag the caller sets.</summary>
    private bool Choose(int entry, bool name = false)
    {
        Selection = entry;
        _chosen = entry;
        if (name)
            _name = true;
        return true;
    }

    private void ChooseControl(bool keyboard, bool mouse)
    {
        Choose(Selection);
        _keyboard = keyboard;
        _mouse = mouse;
    }

    /// <summary>
    /// [ORIGINAL] O2:552A–5633: UpCase(ReadKey); Enter/Space (set O2:5256) open the name, K/M/C choose the control,
    /// Esc, G, P, S (set O2:5276) choose START; extended keys from DS:3716: Left/Right move within the players with
    /// wrap-around, Up/Down move by five (wrapping between the rows, back by five above the player count).
    /// </summary>
    private void SelectKey(ushort word)
    {
        var character = UpCase((byte)(word & 0xFF));
        if (character is 13 or 32)
            Choose(Selection, name: true);
        if (character == (byte)'K')
            ChooseControl(keyboard: true, mouse: false);
        if (character == (byte)'M')
            ChooseControl(keyboard: false, mouse: true);
        if (character == (byte)'C')
            ChooseControl(keyboard: true, mouse: true);
        if (character is 27 or (byte)'G' or (byte)'P' or (byte)'S')
            _chosen = StartEntry;
        if (character != 0)
            return;

        var players = _roster.Players;
        var scan = (byte)(word >> 8);
        if (scan == 0x4B)
        {
            Selection--;
            if (Selection == -1)
                Selection = players - 1;
        }

        if (scan == 0x4D)
        {
            Selection++;
            if (Selection == players)
                Selection = 0;
        }

        if (scan == 0x48)
        {
            Selection -= 5;
            if (Selection < 0)
                Selection += 10;
            if (Selection >= players)
                Selection -= 5;
        }

        if (scan == 0x50)
        {
            Selection += 5;
            if (Selection > 9)
                Selection -= 10;
            if (Selection >= players)
                Selection -= 5;
        }
    }

    /// <summary>
    /// [ORIGINAL] O2:58DB–5A13, the action for player <see cref="_actionPlayer"/>; the steps resume after the name
    /// entry or the opponent list.
    /// </summary>
    private void ContinueAction()
    {
        var p = _actionPlayer;
        var slot = _roster[p];
        while (true)
        {
            switch (_actionStep++)
            {
                case 0:
                    // O2:58DB: name with a human control → name entry.
                    if (_name && slot.Control is PlayerControl.Keyboard or PlayerControl.Mouse)
                    {
                        OpenName(p);
                        return;
                    }

                    break;
                case 1:
                    // O2:590D: name of a computer → list of opponents.
                    if (_name && slot.Control == PlayerControl.Computer)
                    {
                        _listWaitingBeforeOpen = true;
                        return;
                    }

                    break;
                case 2:
                case 3:
                    // O2:5935 / O2:5981: keyboard or mouse; a former computer is asked for a name.
                    var wantKeyboard = _actionStep == 3;
                    if ((wantKeyboard ? _keyboard && !_mouse : _mouse && !_keyboard))
                    {
                        _oldControl = slot.Control;
                        slot.Control = wantKeyboard ? PlayerControl.Keyboard : PlayerControl.Mouse;
                        DrawControls(p);
                        if (_oldControl == PlayerControl.Computer)
                        {
                            OpenName(p);
                            return;
                        }
                    }

                    break;
                case 4:
                    // O2:59CD: computer.
                    if (_keyboard && _mouse)
                    {
                        slot.Control = PlayerControl.Computer;
                        DrawControls(p);
                        _listWaitingBeforeOpen = true;
                        return;
                    }

                    break;
                default:
                    FinishAction();
                    return;
            }
        }
    }

    /// <summary>Called when the name entry or the list closes: the name box is drawn again (O2:4F0E), then the rest.</summary>
    private void ResumeAction()
    {
        _mode = Mode.Select;
        DrawNameBox(_actionPlayer);
        ContinueAction();
    }

    /// <summary>O2:5A1E–5A29 and O2:54C4–551B: chosen := −1; unless done, clear the flags and draw the marker.</summary>
    private void FinishAction()
    {
        _chosen = -1;
        if (_done)
        {
            _waitingBeforeStart = true;
            return;
        }

        _keyboard = _mouse = _name = false;
        DrawMarker();
    }

    private void DrawMarker()
    {
        // [ORIGINAL] O2:54E0–5513: marker at the bottom centre of the selected box, then [bp-56] := selection.
        var (x1, _, x2, y2) = Box(Selection + 1);
        MenuGraphics.Marker(Screen, (x1 + x2) / 2, y2 - 4);
        _previous = Selection;
    }

    private void EraseMarker(int entry)
    {
        var (x1, _, x2, y2) = Box(entry + 1);
        MenuGraphics.EraseMarker(Screen, _menu.Saved, (x1 + x2) / 2, y2 - 4);
    }

    // ---------------------------------------------------------------- drawing of O2:52DD, O2:07BA, O2:4F0E, O2:509C

    /// <summary>
    /// [ORIGINAL] O2:07BA (BOMB.OVR 0x00DA2): button (71, 171)–(248, 197); EURO, SetUserCharSize(1, 1, 3, 10), centred;
    /// "START" in yellow (14) at (163 ± i, 182 ± i) for i = 1..5, then in light red (12) at (163, 182).
    /// </summary>
    private void DrawStartButton()
    {
        MenuGraphics.Frame(Screen, StartLeft, StartTop, StartRight, StartBottom, MenuGraphics.FrameFace, 15, MenuGraphics.FrameDark);
        _text.Color = 14;
        _text.SetTextStyle(_menu.Fonts.Euro, TextDirection.Horizontal, 1);
        _text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Center);
        _text.SetUserCharSize(1, 1, 3, 10);
        for (var i = 1; i <= 5; i++)
        {
            _text.OutTextXY(Screen, 163 + i, 182 + i, "START");
            _text.OutTextXY(Screen, 163 + i, 182 - i, "START");
            _text.OutTextXY(Screen, 163 - i, 182 + i, "START");
            _text.OutTextXY(Screen, 163 - i, 182 - i, "START");
        }

        _text.Color = 12;
        _text.OutTextXY(Screen, 163, 182, "START");
    }

    /// <summary>
    /// [ORIGINAL] O2:5381–54B3: SmallFont with SetUserCharSize(1, 1, 1, 1); per player a raised box (face 18, or 17 above
    /// the player count), the name box, the control words, the marker spot restored from the save buffer; boxes of
    /// players not taking part get a black plus there (UCC8:017D ellipse with radii 1, 1 and UCC8:0BB6 pixel).
    /// </summary>
    private void DrawBoxes()
    {
        _text.SetTextStyle(_menu.Fonts.Small, TextDirection.Horizontal, 1);
        _text.SetUserCharSize(1, 1, 1, 1);
        for (var n = 1; n <= GameLimits.MaxPlayers; n++)
        {
            var face = Face(n);
            var (x1, y1, x2, y2) = Box(n);
            MenuGraphics.Frame(Screen, x1, y1, x2, y2, face, 15, MenuGraphics.FrameDark);
            _text.Color = 5;
            _text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Top);
            DrawNameBox(n);
            DrawControls(n);
            var x = (x1 + x2) / 2;
            var y = y2 - 4;
            MenuGraphics.EraseMarker(Screen, _menu.Saved, x, y);
            if (face == MenuGraphics.FrameDark)
            {
                // [ORIGINAL] The ellipse of radii 1, 1 sets the four neighbours, the pixel the centre.
                Screen[x - 1, y] = 0;
                Screen[x + 1, y] = 0;
                Screen[x, y - 1] = 0;
                Screen[x, y + 1] = 0;
                Screen[x, y] = 0;
            }
        }
    }

    private byte Face(int number) => number > _roster.Players ? MenuGraphics.FrameDark : MenuGraphics.FrameFace;

    /// <summary>
    /// [ORIGINAL] O2:4F0E (BOMB.OVR 0x054F6): sunken box (x1 + 3, y1 + 3)–(x2 − 3, y1 + 12); DAC 19 + n := player colour
    /// (O10:01AC with 100 %); name in colour 19 + n (0 above the player count), centred at the top at ((x1 + x2) / 2,
    /// y1 + 1); SetUserCharSize(m, 100, 1, 1) with m = 100, 99, … until TextWidth ≤ 49.
    /// </summary>
    private void DrawNameBox(int number)
    {
        var face = Face(number);
        var (x1, y1, x2, _) = Box(number);
        MenuGraphics.Frame(Screen, x1 + 3, y1 + 3, x2 - 3, y1 + 12, face, MenuGraphics.FrameDark, 15);
        var (r, g, b) = PlayerColor(number);
        _playerDac[(number - 1) * 3] = r;
        _playerDac[(number - 1) * 3 + 1] = g;
        _playerDac[(number - 1) * 3 + 2] = b;
        _text.Color = (byte)(PlayerColorBase + number);
        if (face == MenuGraphics.FrameDark)
            _text.Color = 0;
        _text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Top);
        var mult = 100;
        _text.SetUserCharSize(mult, 100, 1, 1);
        var name = _roster[number].Name;
        while (_text.TextWidth(name) > 49)
        {
            mult--;
            _text.SetUserCharSize(mult, 100, 1, 1);
        }

        _text.OutTextXY(Screen, (x1 + x2) / 2, y1 + 1, name);
    }

    /// <summary>
    /// [ORIGINAL] O2:509C (BOMB.OVR 0x05684): players above the count get control 0; "KEYBOARD", "MOUSE", "COMPUTER"
    /// centred at ((x1 + x2) / 2, y1 + 19 / 29 / 39), the chosen one light blue (9), the others black; without a mouse
    /// (DS:E8B6 = 0) "MOUSE" is drawn with SetUserCharSize(1, 1, 1, 3).
    /// </summary>
    private void DrawControls(int number)
    {
        var slot = _roster[number];
        if (number > _roster.Players)
            slot.Control = PlayerControl.None;
        _text.SetUserCharSize(1, 1, 1, 1);
        _text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Center);
        var (x1, y1, x2, _) = Box(number);
        var x = (x1 + x2) / 2;
        _text.Color = slot.Control == PlayerControl.Keyboard ? (byte)9 : (byte)0;
        _text.OutTextXY(Screen, x, y1 + 19, "KEYBOARD");
        _text.Color = slot.Control == PlayerControl.Mouse ? (byte)9 : (byte)0;

        // [APPROX] DS:E8B6 (mouse found, O2:04F7) and DS:E94E (mouse used) are one setting here.
        if (!_menu.Settings.MouseAvailable)
            _text.SetUserCharSize(1, 1, 1, 3);
        _text.OutTextXY(Screen, x, y1 + 29, "MOUSE");
        _text.SetUserCharSize(1, 1, 1, 1);
        _text.Color = slot.Control == PlayerControl.Computer ? (byte)9 : (byte)0;
        _text.OutTextXY(Screen, x, y1 + 39, "COMPUTER");
    }

    // ---------------------------------------------------------------- O2:42E8 name entry

    /// <summary>
    /// [ORIGINAL] O2:42E8–4476 (BOMB.OVR 0x048D0): pad the name with spaces up to 13; save (108, 83)–(212, 117); raised
    /// box (110, 85)–(210, 115); "NAME:" magenta (5) left/top at (113, 85); sunken field (115, 100)–(205, 110); the
    /// characters black, left/centre, one by one at a pitch of 6; the length grows by one (the pad space) and the
    /// cursor stands there.
    /// </summary>
    private void OpenName(int number)
    {
        _mode = Mode.Name;
        var name = _roster[number].Name;
        Array.Fill(_nameChars, ' ');
        name.CopyTo(0, _nameChars, 1, name.Length);
        _nameLength = name.Length;

        MenuGraphics.CopyRect(Screen, _covered, NameLeft, NameTop, NameRight, NameBottom);
        MenuGraphics.Frame(Screen, 110, 85, 210, 115, MenuGraphics.FrameFace, 15, MenuGraphics.FrameDark);
        _text.SetTextJustify(HorizontalJustify.Left, VerticalJustify.Top);
        _text.SetUserCharSize(1, 1, 1, 1);
        _text.Color = 5;
        _text.OutTextXY(Screen, 113, 85, "NAME:");
        MenuGraphics.Frame(Screen, 115, 100, 205, 110, MenuGraphics.FrameFace, MenuGraphics.FrameDark, 15);
        _text.SetTextJustify(HorizontalJustify.Left, VerticalJustify.Center);
        _text.Color = 0;
        for (var k = 1; k <= _nameLength; k++)
            _text.OutTextXY(Screen, NameX(k), NameY, _nameChars[k].ToString());

        _nameLength++;
        _cursor = _nameLength;
        _nameDone = false;
        DrawCell(_cursor, cursor: true);
    }

    private static int NameX(int position) => NameFirstX + NamePitch * (position - 1);

    /// <summary>
    /// [ORIGINAL] O2:4006 / O2:4153: three nested rectangles in the face colour clear the cell (x − 1, 100)–(x + 4, 110),
    /// the character is drawn black; O2:4006 also copies (x − 1, 109)–(x + 4, 110) from the save buffer as the cursor.
    /// </summary>
    private void DrawCell(int position, bool cursor)
    {
        var x = NameX(position);
        MenuGraphics.Rectangle(Screen, x - 1, 100, x + 4, 110, MenuGraphics.FrameFace);
        MenuGraphics.Rectangle(Screen, x, 101, x + 3, 109, MenuGraphics.FrameFace);
        MenuGraphics.Rectangle(Screen, x + 1, 102, x + 2, 108, MenuGraphics.FrameFace);
        _text.Color = 0;
        _text.OutTextXY(Screen, x, NameY, _nameChars[position].ToString());
        if (cursor)
            MenuGraphics.CopyRect(_menu.Saved, Screen, x - 1, 109, x + 4, 110);
        _cursorShown = cursor;
    }

    /// <summary>
    /// [ORIGINAL] One key of the loop O2:447A–4666: Backspace (set O2:4265) clears the character before the cursor;
    /// Enter and Esc (O2:4285) finish; characters 32..125 (O2:42A5) are written at the cursor; extended: Home clears the
    /// name, Del the character at the cursor, End goes to the end, Left/Right move within the name.
    /// </summary>
    private void NamePass(ushort word)
    {
        _changed = false;
        var character = (byte)(word & 0xFF);
        if (character == 8 && _cursor > 1)
        {
            _newCursor = _cursor - 1;
            _nameChars[_newCursor] = ' ';
            _changed = true;
        }

        if (character is 13 or 27)
        {
            _nameDone = true;
            _changed = true;
        }

        if (character is >= 32 and <= 125)
        {
            _nameChars[_cursor] = (char)character;
            _changed = true;
            _newCursor = _cursor + 1;
            if (_nameLength < _newCursor)
                _nameLength++;
        }

        if (character == 0)
        {
            var scan = (byte)(word >> 8);
            if (scan == 0x47)
            {
                // [ORIGINAL] O2:4540–45A0: clear from the end, cell by cell; name := "  ", new cursor 1, but no change is
                // flagged, so nothing is redrawn until the next key.
                if (_nameLength >= 1)
                {
                    for (_cursor = _nameLength; ; _cursor--)
                    {
                        _nameChars[_cursor] = ' ';
                        DrawCell(_cursor, cursor: true);
                        DrawCell(_cursor, cursor: false);
                        if (_cursor == 1)
                            break;
                    }
                }

                _nameChars[1] = ' ';
                _nameChars[2] = ' ';
                _nameLength = 2;
                _newCursor = 1;
            }

            if (scan == 0x53)
            {
                _nameChars[_cursor] = ' ';
                _changed = true;
                _newCursor = _cursor;
            }

            if (scan == 0x4F)
            {
                _changed = true;
                _newCursor = _nameLength;
            }

            if (scan == 0x4B && _cursor > 1)
            {
                _newCursor = _cursor - 1;
                _changed = true;
            }

            if (scan == 0x4D && _nameLength > _cursor)
            {
                _newCursor = _cursor + 1;
                _changed = true;
            }
        }

        if (!_changed)
            return;

        // [ORIGINAL] O2:462E–4666.
        _newCursor = Math.Min(_newCursor, PlayerSlot.MaxNameLength);
        _nameLength = Math.Min(_nameLength, PlayerSlot.MaxNameLength);
        DrawCell(_cursor, cursor: false);
        _cursor = _newCursor;
        if (!_nameDone)
        {
            DrawCell(_cursor, cursor: true);
            return;
        }

        // [ORIGINAL] O2:4669–46D8: drop trailing spaces (set O2:42C8 = {0, 32}), PutImage, FreeMem.
        while (_nameLength > 0 && _nameChars[_nameLength] is ' ' or '\0')
            _nameLength--;
        _roster[_actionPlayer].Name = new string(_nameChars, 1, _nameLength);
        MenuGraphics.CopyRect(_covered, Screen, NameLeft, NameTop, NameRight, NameBottom);
        ResumeAction();
    }

    /// <summary>True while the cursor of the name entry is drawn (for tests).</summary>
    public bool CursorShown => _mode == Mode.Name && _cursorShown;

    /// <summary>Name as typed so far, without the cursor space (for tests).</summary>
    public string NameBeingEntered => new(_nameChars, 1, Math.Max(0, _nameLength));

    // ---------------------------------------------------------------- O2:481B opponent list

    /// <summary>
    /// [ORIGINAL] O2:483B–4B94 (BOMB.OVR 0x04E23): save (114, 56)–(206, 144); raised box (116, 58)–(204, 142); the
    /// selection starts at the entry 1..6 whose name the player has (entry 7 is not compared), else 0; the eight entries
    /// in brown (6) with the key letter in yellow (14), centred at (160, 63 + 10 k).
    /// </summary>
    private void OpenList()
    {
        _mode = Mode.List;
        MenuGraphics.CopyRect(Screen, _covered, ListLeft, ListTop, ListRight, ListBottom);
        MenuGraphics.Frame(Screen, 116, 58, 204, 142, MenuGraphics.FrameFace, 15, MenuGraphics.FrameDark);
        _listSelection = 0;
        var name = _roster[_actionPlayer].Name;
        for (var k = 1; k <= 6; k++)
        {
            if (name == ComputerOpponents.Names[k])
                _listSelection = k;
        }

        _text.Color = 6;
        _text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Center);
        _text.SetUserCharSize(1, 1, 1, 1);
        for (var k = 0; k <= LastListEntry; k++)
        {
            foreach (var (_, text, color) in ListParts[k])
            {
                _text.Color = color;
                _text.OutTextXY(Screen, ListTextX, ListTopY + ListPitch * k, text);
            }
        }

        _listChosen = -1;
        ListMarkers();
    }

    private void ListMarkers()
    {
        // [ORIGINAL] O2:4BBE–4BEC.
        var y = ListTopY + ListPitch * _listSelection;
        MenuGraphics.Marker(Screen, ListMarkerLeftX, y);
        MenuGraphics.Marker(Screen, ListMarkerRightX, y);
        _listPrevious = _listSelection;
    }

    /// <summary>
    /// [ORIGINAL] One pass of O2:4BEF–4E0D: UpCase(ReadKey); Esc and "?" choose Random, Enter/Space (O2:47BB) the
    /// selection, S L R B W T M the opponents 1..7; Up/Down move 0..7 without wrap-around. Mouse: inside 116 &lt; x &lt; 204
    /// the rows 60 &lt; y ≤ 69, …, 119 &lt; y ≤ 129, 129 &lt; y &lt; 142 choose 0..7, anything else Random; then wait
    /// for the release.
    /// </summary>
    private void ListPass(ushort? key, ScreenInput input)
    {
        if (key is { } word)
        {
            var character = UpCase((byte)(word & 0xFF));
            if (character == 27)
                _listChosen = 0;
            if (character is 13 or 32)
                _listChosen = _listSelection;
            var letter = Array.IndexOf("?SLRBWTM".ToCharArray(), (char)character);
            if (character != 0 && letter >= 0)
                _listChosen = letter;
            if (character == 0)
            {
                var scan = (byte)(word >> 8);
                if (scan == 0x48 && _listSelection > 0)
                    _listSelection--;
                if (scan == 0x50 && _listSelection < LastListEntry)
                    _listSelection++;
            }
        }

        if (ButtonHeld(input))
        {
            var x = input.MouseX / 2;
            var y = input.MouseY;
            _listChosen = 0;
            if (x > 116 && x < 204)
            {
                for (var k = 0; k <= LastListEntry; k++)
                {
                    if (y > ListRowTop[k] && (k < LastListEntry ? y <= ListRowTop[k + 1] : y < 142))
                        _listChosen = k;
                }
            }

            if (_listChosen >= 0)
                _listSelection = _listChosen;
            _listWaitingForRelease = true;
            return;
        }

        ListAfterMouse();
    }

    /// <summary>[ORIGINAL] O2:4E0F–4EDF: erase the markers with face bars, take the chosen name, close or go on.</summary>
    private void ListAfterMouse()
    {
        if (_listPrevious == _listSelection && _listChosen < 0)
            return;
        var y = ListTopY + ListPitch * _listPrevious;
        MenuGraphics.Bar(Screen, ListMarkerLeftX - 1, y - 1, ListMarkerLeftX + 1, y + 1, MenuGraphics.FrameFace);
        MenuGraphics.Bar(Screen, ListMarkerRightX - 1, y - 1, ListMarkerRightX + 1, y + 1, MenuGraphics.FrameFace);
        if (_listChosen is < 0 or > LastListEntry)
        {
            ListMarkers();
            return;
        }

        _text.Color = 14;
        _roster[_actionPlayer].Name = ComputerOpponents.Names[_listChosen];
        _listChosen = -1;

        // [ORIGINAL] O2:4EE4–4F03: PutImage(114, 56), FreeMem.
        MenuGraphics.CopyRect(_covered, Screen, ListLeft, ListTop, ListRight, ListBottom);
        ResumeAction();
    }

    /// <summary>Turbo Pascal UpCase: only a..z change.</summary>
    private static byte UpCase(byte c) => c is >= (byte)'a' and <= (byte)'z' ? (byte)(c - 32) : c;
}
