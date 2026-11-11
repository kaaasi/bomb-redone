using System.Globalization;
using BombRedone.Core;
using BombRedone.Core.Setup;
using BombRedone.Formats;
using BombRedone.UI.Text;

namespace BombRedone.UI.Screens;

/// <summary>What the player selection needs for team play.</summary>
/// <param name="Options">Game options; "Team Play" (DS:E8BC) decides whether the team setup follows and is switched off
/// when team play is cancelled.</param>
/// <param name="Tiny">TINY font of BOMB.EXE (font number DS:E91A), used by the message of cancelled team play.</param>
public sealed record PlayerSelectTeams(OptionsSettings Options, BgiFont Tiny);

/// <summary>
/// Team setup of the original: O2:2749 (BOMB.OVR 0x02D31), called by O2:3D49 after the player selection with "Team
/// Play" on; draws the tanks with U431:07BA; docs/anforderungen/spielerauswahl.md §7.
/// </summary>
/// <remarks>
/// Draws on top of the player selection as the original leaves video memory. Busy waits become states; one update runs
/// one pass of the loop per key typed, at least one for the mouse; Crt.Delay(40) after a mouse hit on a player row waits
/// for 40 ms of <see cref="Update"/> time before the pass goes on.
/// </remarks>
public sealed class PlayerSelectTeamScreen : IScreen
{
    /// <summary>[ORIGINAL] O2:2781: raised panel (60, 62)–(260, 182).</summary>
    public const int PanelLeft = 60, PanelTop = 62, PanelRight = 260, PanelBottom = 182;

    /// <summary>[ORIGINAL] O2:27E8 / O2:2B36: button "-- Done --" (130, 187)–(190, 197), chosen with the mouse inside it.</summary>
    public const int DoneLeft = 130, DoneTop = 187, DoneRight = 190, DoneBottom = 197;

    /// <summary>"Selection" value of the Done button ([bp-2] = 11).</summary>
    public const int DoneEntry = 11;

    /// <summary>[ORIGINAL] O2:2B25: Crt.Delay(40) after a mouse hit on a player row.</summary>
    public static readonly TimeSpan MouseDelay = TimeSpan.FromMilliseconds(40);

    /// <summary>[ORIGINAL] O2:2DE1: font number of the line "PRESS ANY KEY TO CONTINUE..." is DS:E91A (TINY).</summary>
    public const string MessageFont = BgiFontLibrary.Tiny;

    /// <summary>Texts of the team setup with their O2 offsets (BOMB.OVR 0x005E8 + offset, length byte).</summary>
    public static IReadOnlyList<(int FileOffset, string Text)> OriginalTexts { get; } =
    [
        .. new (int Offset, string Text)[]
        {
            (0x26BE, "Team Setup"), (0x26C9, "-- Done --"), (0x26D4, "Team # -->"), (0x26FF, "Team play canceled due"),
            (0x2716, "to extreme stupidity."), (0x272C, "PRESS ANY KEY TO CONTINUE..."),
        }.Select(t => (PlayerSelectScreen.OverlayO2FileOffset + t.Offset, t.Text)),
    ];

    private enum State
    {
        Setup,
        Delay,
        MessageRelease,
        MessageKey,
        Finished,
    }

    private readonly MenuScreen _menu;
    private readonly PlayerRoster _roster;
    private readonly GameSetup _setup;
    private readonly PlayerSelectTeams _teams;
    private readonly Func<int, int> _random;
    private readonly Func<GameSetup, IScreen?> _start;
    private readonly GraphText _text;
    private readonly Queue<ushort> _keys = new();
    private State _state = State.Setup;
    private TimeSpan _delayLeft;

    // Locals of O2:2749: [bp-2] selection, [bp-4] previous, [bp-5] left (team − 1, or Done), [bp-6] right (team + 1);
    // DS:4751 done.
    private int _previous;
    private bool _left, _right;
    private IScreen? _next;

    /// <param name="menu">Main menu: save buffer, palette, fonts, settings.</param>
    /// <param name="roster">Player records; the team numbers are changed in place and kept.</param>
    /// <param name="selection">Video memory as the player selection leaves it.</param>
    /// <param name="setup">Players as the player selection ended (Random already resolved).</param>
    /// <param name="teams">Options and font for team play.</param>
    /// <param name="random">Turbo Pascal Random(n), for the names of all-computer teams (O2:35CF).</param>
    /// <param name="start">Called with the final setup; returns the next screen.</param>
    public PlayerSelectTeamScreen(MenuScreen menu, PlayerRoster roster, Surface selection, GameSetup setup,
        PlayerSelectTeams teams, Func<int, int> random, Func<GameSetup, IScreen?> start)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(teams);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(start);
        _menu = menu;
        _roster = roster;
        _setup = setup;
        _teams = teams;
        _random = random;
        _start = start;
        _text = new GraphText(menu.Fonts.Default);
        selection.Pixels.CopyTo(Screen.Pixels, 0);
        DrawSetup();
        Selection = 1;
        DrawMarker();
    }

    /// <summary>Video memory as the original leaves it (palette indices).</summary>
    public Surface Screen { get; } = new();

    /// <summary>Selected player 1..count, or <see cref="DoneEntry"/>.</summary>
    public int Selection { get; private set; }

    /// <summary>True while the message of cancelled team play waits for a key or a button.</summary>
    public bool ShowsCancelMessage => _state is State.MessageKey;

    /// <summary>[ORIGINAL] O2:28F1–291F / O2:2C53–2C86: the tank of player p stands at (140 + 10 · team, 71 + 10 · p).</summary>
    public static (int X, int Y) TankPoint(int player, int team) => (140 + 10 * team, 71 + 10 * player);

    /// <summary>[ORIGINAL] O2:29E4: marker of the selection s at (55, 68 + 10 · s).</summary>
    public static (int X, int Y) MarkerPoint(int selection) => (55, 68 + 10 * selection);

    /// <summary>
    /// Tank of a player. [ORIGINAL] U431:07BA (BOMB.EXE 0x0616A) for a living player (record byte +23h): DAC 19 + n from
    /// the power (O10:014A), the body U431:05ED (BOMB.EXE 0x05F9D) at (x, y) – rows y: x−4..x+4; y−1: x−4..x−2, x,
    /// x+2..x+4; y−2 and y−3: x−3..x+3; y−4: x−1..x+1 – and the barrel UCC8:09E4 from (x, y) to
    /// (x + Trunc(8 · cos a) · w, y − Trunc(8 · sin a)) with the angle a (word +1Dh, degrees) and the word +1Fh.
    /// </summary>
    /// <remarks>
    /// [APPROX] Real48 is computed with double here. The setup only draws angle 90 (O2:0216 sets +1Dh = 90, +1Fh = 1):
    /// the barrel is the vertical line from y − 8 to y either way.
    /// </remarks>
    public static void DrawTank(Surface target, int x, int y, byte color, int angle = 90, int width = 1)
    {
        ArgumentNullException.ThrowIfNull(target);
        for (var dx = -4; dx <= 4; dx++)
        {
            Set(target, x + dx, y, color);
            if (dx is not (-1 or 1))
                Set(target, x + dx, y - 1, color);
            if (dx is >= -3 and <= 3)
            {
                Set(target, x + dx, y - 2, color);
                Set(target, x + dx, y - 3, color);
            }

            if (dx is >= -1 and <= 1)
                Set(target, x + dx, y - 4, color);
        }

        var radians = angle * Math.PI / 180;
        var x2 = x + (int)Math.Truncate(Math.Cos(radians) * 8) * width;
        var y2 = y - (int)Math.Truncate(Math.Sin(radians) * 8);
        MenuGraphics.Line(target, x, y, x2, y2, color);
    }

    private static void Set(Surface target, int x, int y, byte color) =>
        target.Pixels[y * target.Width + x] = color;

    public IScreen? Update(TimeSpan elapsed, ScreenInput input)
    {
        if (elapsed < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(elapsed), "Time cannot run backwards.");
        _menu.RunPaletteRotation(elapsed);
        foreach (var key in input.Keys)
            _keys.Enqueue(key);

        // A delay started in this update waits for later updates.
        var budget = _state == State.Delay ? elapsed : TimeSpan.Zero;
        while (true)
        {
            switch (_state)
            {
                case State.Finished:
                    return _next;
                case State.Delay:
                    _delayLeft -= budget;
                    budget = TimeSpan.Zero;
                    if (_delayLeft > TimeSpan.Zero)
                        return this;
                    _state = State.Setup;
                    AfterInput();
                    break;
                case State.MessageRelease:
                    // [ORIGINAL] O2:2D74–2D8C: wait until both buttons are released.
                    if (ButtonHeld(input))
                        return this;
                    DrawCancelMessage();
                    _state = State.MessageKey;
                    break;
                case State.MessageKey:
                    // [ORIGINAL] O2:2E54–2E85: wait for a key or a button, then empty the BIOS keyboard buffer.
                    if (_keys.Count == 0 && !ButtonHeld(input))
                        return this;
                    _keys.Clear();
                    _teams.Options.TeamPlay = false;
                    TeamPlay.Reset(_roster);
                    Finish(_start(_setup));
                    break;
                default:
                    var key = _keys.Count > 0 ? _keys.Dequeue() : (ushort?)null;
                    Pass(key, input);
                    if (_state == State.Setup && _keys.Count == 0)
                        return this;
                    break;
            }
        }
    }

    public void Draw(Surface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (surface.Width != Screen.Width || surface.Height != Screen.Height)
            throw new ArgumentException("The team setup draws into a 320 x 200 surface only.", nameof(surface));
        Screen.Pixels.CopyTo(surface.Pixels, 0);
        Span<byte> dac = stackalloc byte[Palette.ByteLength];
        PlayerDac(_menu, dac);
        MenuGraphics.ApplyDac(dac, surface);
    }

    /// <summary>
    /// Palette of the team screens: the menu palette, DAC 19 + n = colour of player n at 100 % (O2:2965–2978, O10:01AC)
    /// and DAC 19 := (63, 63, 63) (O2:29D0, routine DS:E8F6).
    /// </summary>
    internal static void PlayerDac(MenuScreen menu, Span<byte> dac) => TeamDac(menu.Dac, dac);

    /// <summary>
    /// Palette of the team screens from a given menu palette <paramref name="menuDac"/> (768 six-bit values): player
    /// colours in DAC 20..29 and DAC 19 := (63, 63, 63) (O2:29D0), see <see cref="PlayerDac"/>.
    /// </summary>
    public static void TeamDac(ReadOnlySpan<byte> menuDac, Span<byte> dac)
    {
        menuDac.CopyTo(dac);
        for (var n = 1; n <= GameLimits.MaxPlayers; n++)
        {
            var (r, g, b) = PlayerSelectScreen.PlayerColor(n);
            MenuGraphics.SetRgb(dac, PlayerSelectScreen.PlayerColorBase + n, r, g, b);
        }

        MenuGraphics.SetRgb(dac, MenuGraphics.FrameWhite, 63, 63, 63);
    }

    private bool ButtonHeld(ScreenInput input) =>
        _menu.Settings.MouseAvailable && (input.MouseHolds(MouseButtons.Left) || input.MouseHolds(MouseButtons.Right));

    /// <summary>
    /// [ORIGINAL] O2:2750–29C4: SmallFont, SetUserCharSize(1, 1, 1, 1); panel; "Team Setup" light green (10) centred at
    /// the bottom at (160, 55) on the button (130, 47)–(190, 57); "-- Done --" magenta (5) centred at the top at (160, 186)
    /// on (130, 187)–(190, 197); "Team # -->" yellow (14) left/top at (64, 62); team numbers t = 1..10 centred at the top
    /// at (140 + 10 t, 62), black from count + 1; the tanks in their team columns; the names left/top at (64, 62 + 10 p)
    /// in colour 19 + p (black above the count).
    /// </summary>
    private void DrawSetup()
    {
        _text.SetTextStyle(_menu.Fonts.Small, TextDirection.Horizontal, 1);
        _text.SetUserCharSize(1, 1, 1, 1);
        _text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Bottom);
        MenuGraphics.Frame(Screen, PanelLeft, PanelTop, PanelRight, PanelBottom, MenuGraphics.FrameFace, 15, MenuGraphics.FrameDark);
        MenuGraphics.Frame(Screen, 130, 47, 190, 57, MenuGraphics.FrameFace, 15, MenuGraphics.FrameDark);
        _text.Color = 10;
        _text.OutTextXY(Screen, 160, 55, "Team Setup");
        MenuGraphics.Frame(Screen, DoneLeft, DoneTop, DoneRight, DoneBottom, MenuGraphics.FrameFace, 15, MenuGraphics.FrameDark);
        _text.Color = 5;
        _text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Top);
        _text.OutTextXY(Screen, 160, 186, "-- Done --");
        _text.SetTextJustify(HorizontalJustify.Left, VerticalJustify.Top);
        _text.Color = 14;
        _text.OutTextXY(Screen, 64, 62, "Team # -->");
        _text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Top);
        var players = _roster.Players;
        for (var t = 1; t <= GameLimits.MaxPlayers; t++)
        {
            if (t > players)
                _text.Color = 0;
            _text.OutTextXY(Screen, 140 + 10 * t, 62, t.ToString(CultureInfo.InvariantCulture));
            for (var p = 1; p <= players; p++)
            {
                if (_roster[p].Team == t)
                    DrawPlayerTank(p);
            }
        }

        _text.SetTextJustify(HorizontalJustify.Left, VerticalJustify.Top);
        for (var p = 1; p <= GameLimits.MaxPlayers; p++)
        {
            _text.Color = p > players ? (byte)0 : (byte)(PlayerSelectScreen.PlayerColorBase + p);
            _text.OutTextXY(Screen, 64, 62 + 10 * p, _roster[p].Name);
        }
    }

    private void DrawPlayerTank(int player)
    {
        var (x, y) = TankPoint(player, _roster[player].Team);
        DrawTank(Screen, x, y, (byte)(PlayerSelectScreen.PlayerColorBase + player));
    }

    private void DrawMarker()
    {
        // [ORIGINAL] O2:29E4–29FA: marker, previous := selection.
        var (x, y) = MarkerPoint(Selection);
        MenuGraphics.Marker(Screen, x, y);
        _previous = Selection;
    }

    /// <summary>
    /// [ORIGINAL] One pass of O2:29FD–2B5C. Keys (UpCase(ReadKey)): Enter, Esc and D (set O2:26DF) choose Done;
    /// extended keys from DS:3716: Up/Down move within 1..count without wrap-around, Right/Left set the flags for team
    /// + 1 / − 1. Mouse (button 1 or 2 held, x = Trunc(CX / 2)): inside 60 &lt; x &lt; 260 the row 64 + 10 p ≤ y ≤ 71 + 10 p
    /// selects player p, button 1 sets "left", button 2 "right", then Crt.Delay(40); 130 &lt; x &lt; 190 and
    /// 187 &lt; y &lt; 197 choose Done. A held button repeats.
    /// </summary>
    private void Pass(ushort? key, ScreenInput input)
    {
        _left = _right = false;
        if (key is { } word)
        {
            var character = (byte)(word & 0xFF);
            if (character is >= (byte)'a' and <= (byte)'z')
                character -= 32;
            if (character is 13 or 27 or (byte)'D')
            {
                Selection = DoneEntry;
                _left = true;
            }

            if (character == 0)
            {
                var scan = (byte)(word >> 8);
                if (scan == 0x48 && Selection > 1)
                    Selection--;
                if (scan == 0x50 && Selection < _roster.Players)
                    Selection++;
                if (scan == 0x4D)
                    _right = true;
                if (scan == 0x4B)
                    _left = true;
            }
        }

        var delay = false;
        if (ButtonHeld(input))
        {
            var x = input.MouseX / 2;
            var y = input.MouseY;
            if (x > PanelLeft && x < PanelRight)
            {
                for (var p = 1; p <= _roster.Players; p++)
                {
                    if (y < 64 + 10 * p || y > 71 + 10 * p)
                        continue;
                    Selection = p;
                    if (_menu.Settings.MouseAvailable && input.MouseHolds(MouseButtons.Left))
                        _left = true;
                    if (_menu.Settings.MouseAvailable && input.MouseHolds(MouseButtons.Right))
                        _right = true;
                    delay = true;
                }
            }

            if (x > DoneLeft && x < DoneRight && y > DoneTop && y < DoneBottom)
            {
                Selection = DoneEntry;
                _left = true;
            }
        }

        if (delay)
        {
            _delayLeft = MouseDelay;
            _state = State.Delay;
            return;
        }

        AfterInput();
    }

    /// <summary>
    /// [ORIGINAL] O2:2B5F–2CA5: without a new selection or a flag nothing happens; otherwise the marker at the previous
    /// selection is restored from the save buffer; with a flag a player row moves its tank (the old one is cleared with
    /// a face bar (x − 5, y − 8)–(x + 5, y), "left" to team − 1 down to 1, "right" to team + 1 up to the count), Done ends.
    /// </summary>
    private void AfterInput()
    {
        if (_previous == Selection && !_left && !_right)
            return;
        var (mx, my) = MarkerPoint(_previous);
        MenuGraphics.EraseMarker(Screen, _menu.Saved, mx, my);
        var done = false;
        if (_left || _right)
        {
            if (Selection is >= 1 and <= GameLimits.MaxPlayers)
            {
                var slot = _roster[Selection];
                var (x, y) = TankPoint(Selection, slot.Team);
                MenuGraphics.Bar(Screen, x - 5, y, x + 5, y - 8, MenuGraphics.FrameFace);
                if (_left && slot.Team > 1)
                    slot.Team--;
                if (_right && slot.Team < _roster.Players)
                    slot.Team++;
                DrawPlayerTank(Selection);
            }
            else if (Selection == DoneEntry)
            {
                done = true;
            }
        }

        if (!done)
        {
            DrawMarker();
            return;
        }

        if (TeamPlay.IsValid(_roster))
        {
            Finish(new PlayerSelectTeamNamesScreen(_menu, _roster, _setup, _random, _start));
            return;
        }

        _state = State.MessageRelease;
    }

    /// <summary>
    /// [ORIGINAL] O2:2D8E–2E4F: button (90, 114)–(230, 142); centred at the top in brown (6) "Team play canceled due" at
    /// (160, 112) and "to extreme stupidity." at (160, 122) (SmallFont); TINY, SetUserCharSize(1, 1, 1, 1), red (4)
    /// "PRESS ANY KEY TO CONTINUE..." at (160, 129).
    /// </summary>
    private void DrawCancelMessage()
    {
        _text.Color = 6;
        MenuGraphics.Frame(Screen, 90, 114, 230, 142, MenuGraphics.FrameFace, 15, MenuGraphics.FrameDark);
        _text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Top);
        _text.OutTextXY(Screen, 160, 112, "Team play canceled due");
        _text.OutTextXY(Screen, 160, 122, "to extreme stupidity.");
        _text.SetTextStyle(_teams.Tiny, TextDirection.Horizontal, 1);
        _text.SetUserCharSize(1, 1, 1, 1);
        _text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Top);
        _text.Color = 4;
        _text.OutTextXY(Screen, 160, 129, "PRESS ANY KEY TO CONTINUE...");
    }

    private void Finish(IScreen? next)
    {
        _next = next;
        _state = State.Finished;
    }
}
