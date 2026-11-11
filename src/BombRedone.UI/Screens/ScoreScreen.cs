using System.Globalization;
using BombRedone.Core.Economy;
using BombRedone.UI.Text;

namespace BombRedone.UI.Screens;

/// <summary>What the player chose on the status screen.</summary>
public enum ScoreChoice
{
    /// <summary>"Continue" / "Exit", or any key other than A, S and the digits.</summary>
    Continue,

    /// <summary>"Save" or S: continue and open the save menu (DS:4816 := 1).</summary>
    Save,

    /// <summary>"Add" or A: the dialog "# TO ADD:" (O3:5717); the status screen stays.</summary>
    Add,

    /// <summary>Click on a player line or digit 1..9, 0 = 10: statistics of that line (O3:2508); the status screen stays.</summary>
    Statistics,
}

/// <summary>A player on the status screen with the 6-bit colour of its name.</summary>
public sealed record ScoreLine(PlayerAccount Player, (int Red, int Green, int Blue) Color);

/// <summary>
/// Status screen between two games, single players: O3:5AEF (BOMB.OVR 0x0E57B); docs/anforderungen/wertung-einkauf.md §5.
/// </summary>
public sealed class ScoreScreen : IScreen
{
    /// <summary>[ORIGINAL] O3:5B0B–5B2B: half height 5 × players + 15 around y = 100.</summary>
    public static (int Top, int Bottom) Box(int players) => (100 - (5 * players + 15), 5 * players + 15 + 100);

    /// <summary>[ORIGINAL] O3:65C8: without buttons the screen stays 2000 ms (Crt.Delay).</summary>
    public static readonly TimeSpan AutoContinueDelay = TimeSpan.FromMilliseconds(2000);

    private const byte Face = MenuGraphics.FrameFace, Dark = MenuGraphics.FrameDark, Light = 15;
    private const int BackgroundFirstColor = 30, BackgroundColorCount = 30;

    private readonly ShopFonts _fonts;
    private readonly IReadOnlyList<Standing> _standings;
    private readonly int _gamesPlayed, _gamesTotal;
    private readonly bool _buttons;
    private readonly Func<ScoreChoice, int, ScoreScreen, IScreen?> _choose;
    private readonly byte[] _dac = new byte[768];
    private readonly GraphText _text;
    private readonly int _top, _bottom;
    private TimeSpan _waited;
    private bool _waitingForRelease;
    private int _pendingLine;

    /// <param name="fonts">Fonts from BOMB.EXE.</param>
    /// <param name="lines">Players in any order; the screen ranks them (<see cref="Core.Economy.Standings.Rank"/>).</param>
    /// <param name="gamesPlayed">Games played so far (DS:47E2).</param>
    /// <param name="gamesTotal">Games of the battle (DS:E8A6).</param>
    /// <param name="buttons">
    /// Buttons and input (DS:4812 = 0); without them the screen continues by itself after 2 s. The original clears the
    /// flag after the last game (O3:5AFD).
    /// </param>
    /// <param name="choose">Called with the choice and, for statistics, the line (1-based); returns the next screen.</param>
    public ScoreScreen(ShopFonts fonts, IReadOnlyList<ScoreLine> lines, int gamesPlayed, int gamesTotal, bool buttons,
        Func<ScoreChoice, int, ScoreScreen, IScreen?> choose)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(choose);
        _fonts = fonts;
        _gamesPlayed = gamesPlayed;
        _gamesTotal = gamesTotal;
        _buttons = buttons || gamesPlayed == gamesTotal;
        _choose = choose;
        _text = new GraphText(fonts.Default);
        _standings = Core.Economy.Standings.Rank(lines.Select(l => l.Player));
        (_top, _bottom) = Box(_standings.Count);

        MenuGraphics.DefaultColors.CopyTo(_dac);
        MenuGraphics.SetFrameColors(_dac);
        // [ORIGINAL] O3:5DA9 → O10[1] (player, 100): entry 19 + line gets the colour of the player at full strength.
        for (var i = 0; i < _standings.Count; i++)
        {
            var color = lines.First(l => ReferenceEquals(l.Player, _standings[i].Player)).Color;
            MenuGraphics.SetRgb(_dac, ShopScreen.PlayerColorBase + i + 1, color.Red, color.Green, color.Blue);
        }

        DrawBackground();
        // [ORIGINAL] O3:5C7F–5C91: the background is saved to DS:E5BC; the shop restores its lower part from there.
        Screen.Pixels.CopyTo(Background.Pixels, 0);
        DrawTable();
    }

    /// <summary>Players in screen order with their ranks.</summary>
    public IReadOnlyList<Standing> Standings => _standings;

    /// <summary>Video memory of the screen.</summary>
    public Surface Screen { get; } = new();

    /// <summary>The background without the table (buffer DS:E5BC), for the shop.</summary>
    public Surface Background { get; } = new();

    /// <summary>6-bit DAC of the screen.</summary>
    public ReadOnlySpan<byte> Dac => _dac;

    public IScreen? Update(TimeSpan elapsed, ScreenInput input)
    {
        if (!_buttons)
        {
            _waited += elapsed;
            return _waited >= AutoContinueDelay ? _choose(ScoreChoice.Continue, 0, this) : this;
        }

        if (_waitingForRelease)
        {
            // [ORIGINAL] O3:63E8–6400: statistics open after both buttons are released.
            if (input.MouseHolds(MouseButtons.Left) || input.MouseHolds(MouseButtons.Right))
                return this;
            _waitingForRelease = false;
            return _choose(ScoreChoice.Statistics, _pendingLine, this);
        }

        foreach (var key in input.Keys)
        {
            var choice = ReadKey(key, out var line);
            if (choice is { } c)
                return _choose(c, line, this);
        }

        if (input.MouseHolds(MouseButtons.Left) || input.MouseHolds(MouseButtons.Right))
            return Click(input.MouseX / 2, input.MouseY);
        return this;
    }

    public void Draw(Surface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (surface.Width != Screen.Width || surface.Height != Screen.Height)
            throw new ArgumentException("The status screen draws into a 320 x 200 surface only.", nameof(surface));
        Screen.Pixels.CopyTo(surface.Pixels, 0);
        MenuGraphics.ApplyDac(_dac, surface);
    }

    /// <summary>
    /// [ORIGINAL] O3:61F2–6357: digits 1..9 and 0 (= 10) open the statistics of that line if it exists, A (set O3:5ACF)
    /// adds games, S (set O3:5AAF) saves, every other key continues.
    /// </summary>
    private ScoreChoice? ReadKey(ushort key, out int line)
    {
        line = 0;
        var character = (char)(key & 0xFF);
        if (character is >= '0' and <= '9')
        {
            line = character == '0' ? 10 : character - '0';
            return line <= _standings.Count ? ScoreChoice.Statistics : null;
        }

        return character switch
        {
            'S' or 's' => ScoreChoice.Save,
            'A' or 'a' => ScoreChoice.Add,
            _ => ScoreChoice.Continue,
        };
    }

    /// <summary>
    /// [ORIGINAL] O3:6379–65AD: a player line (45 &lt; x &lt; 275, top + 12 + 10i ≤ y &lt; top + 22 + 10i) waits for the
    /// release and opens its statistics; the buttons Continue/Exit (135..185), Add (60..110) and Save (210..260) between
    /// bottom + 5 and bottom + 15.
    /// </summary>
    private IScreen? Click(int x, int y)
    {
        if (x > 45 && x < 275)
        {
            for (var i = 1; i <= _standings.Count; i++)
            {
                var lineTop = _top + 12 + 10 * i;
                if (lineTop <= y && lineTop + 10 > y)
                {
                    _pendingLine = i;
                    _waitingForRelease = true;
                    return this;
                }
            }
        }

        var onButtons = y >= _bottom + 5 && y <= _bottom + 15;
        if (onButtons && x is >= 135 and <= 185)
            return _choose(ScoreChoice.Continue, 0, this);
        if (onButtons && x is >= 60 and <= 110)
            return _choose(ScoreChoice.Add, 0, this);
        if (onButtons && x is >= 210 and <= 260)
            return _choose(ScoreChoice.Save, 0, this);
        return this;
    }

    /// <summary>
    /// [ORIGINAL] O2:3B96 with argument 1 (BOMB.OVR 0x0417E, called from O3:5C7A): DAC entries 30..59 get (i, 0, i);
    /// lines y = 0..199 in colour y mod 30 + 30 (UCC8:09E4). [APPROX] The palette rotation the routine starts
    /// (UC79:003D with Cycle Delay) is not run here.
    /// </summary>
    private void DrawBackground()
    {
        for (var i = BackgroundFirstColor; i < BackgroundFirstColor + BackgroundColorCount; i++)
            MenuGraphics.SetRgb(_dac, i, i, 0, i);
        for (var y = 0; y <= 199; y++)
            MenuGraphics.Line(Screen, 0, y, 319, y, (byte)(y % 30 + BackgroundFirstColor));
    }

    /// <summary>[ORIGINAL] O3:5C96–61CF (BOMB.OVR 0x0E722): boxes, title, column heads, lines and buttons.</summary>
    private void DrawTable()
    {
        MenuGraphics.Frame(Screen, 45, _top, 275, _bottom, Face, Light, Dark);
        MenuGraphics.Frame(Screen, 65, _top - 20, 255, _top - 5, Face, Light, Dark);

        // Title: SmallFont size 1, user size 1/1, colour 6, centred at (160, top − 13).
        _text.SetTextStyle(_fonts.Small, TextDirection.Horizontal, 1);
        _text.SetUserCharSize(1, 1, 1, 1);
        _text.Color = 6;
        _text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Center);
        _text.OutTextXY(Screen, 160, _top - 13, Title(_gamesPlayed, _gamesTotal));

        // Column "Player": colour 17, left/top; line under the heads (50..270, top + 16).
        _text.Color = Dark;
        _text.SetTextJustify(HorizontalJustify.Left, VerticalJustify.Top);
        MenuGraphics.Line(Screen, 50, _top + 16, 270, _top + 16, Dark);
        _text.OutTextXY(Screen, 50, _top + 5, "Player");
        for (var i = 1; i <= _standings.Count; i++)
        {
            var standing = _standings[i - 1];
            _text.Color = (byte)(ShopScreen.PlayerColorBase + i);
            var y = _top + 10 + 10 * i;
            _text.OutTextXY(Screen, 50, y, RankText(standing.Rank));
            _text.OutTextXY(Screen, 72, y, standing.Player.Name);
        }

        // Columns "Score" (x 270) and "Wins" (x 185), right-aligned.
        _text.SetTextJustify(HorizontalJustify.Right, VerticalJustify.Top);
        _text.Color = Dark;
        _text.OutTextXY(Screen, 270, _top + 5, "Score");
        for (var i = 1; i <= _standings.Count; i++)
        {
            _text.Color = (byte)(ShopScreen.PlayerColorBase + i);
            _text.OutTextXY(Screen, 270, _top + 10 + 10 * i, Number(_standings[i - 1].Player.Score));
        }

        _text.Color = Dark;
        _text.OutTextXY(Screen, 185, _top + 5, "Wins");
        for (var i = 1; i <= _standings.Count; i++)
        {
            _text.Color = (byte)(ShopScreen.PlayerColorBase + i);
            _text.OutTextXY(Screen, 185, _top + 10 + 10 * i, Number((short)_standings[i - 1].Player.Wins));
        }

        if (!_buttons)
            return;

        // [ORIGINAL] O3:60AF–61CF: buttons in colour 0, text centred.
        _text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Center);
        Button(60, 110, 85, "Add");
        Button(210, 260, 235, "Save");
        Button(135, 185, 160, _gamesPlayed == _gamesTotal ? "Exit" : "Continue");
    }

    private void Button(int x1, int x2, int textX, string label)
    {
        MenuGraphics.Frame(Screen, x1, _bottom + 5, x2, _bottom + 15, Face, Light, Dark);
        _text.Color = 0;
        _text.OutTextXY(Screen, textX, _bottom + 9, label);
    }

    /// <summary>
    /// [ORIGINAL] O3:5D17–5D8F: games played, " Games " (above 1) or " Game ", "Played out of ", total.
    /// </summary>
    public static string Title(int played, int total) =>
        Number(played) + (played > 1 ? " Games " : " Game ") + "Played out of " + Number(total);

    /// <summary>[ORIGINAL] O3:5E70–5E95: rank and "."; below 10 with a leading space.</summary>
    public static string RankText(int rank) => (rank < 10 ? " " : "") + Number(rank) + ".";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
