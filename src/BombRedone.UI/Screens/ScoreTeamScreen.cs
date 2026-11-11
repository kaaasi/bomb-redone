using System.Globalization;
using BombRedone.Core.Economy;
using BombRedone.UI.Text;

namespace BombRedone.UI.Screens;

/// <summary>
/// Status screen of the teams after the single status screen: O3:6696 (BOMB.OVR 0x0F122);
/// docs/anforderungen/wertung-einkauf.md §7.
/// </summary>
/// <remarks>
/// [ORIGINAL] O3:7981–79A2: shown after <see cref="ScoreScreen"/> when Team Play is on (DS:E8BC). The routine draws no
/// background of its own: it draws over what the single status screen left on the screen.
/// </remarks>
public sealed class ScoreTeamScreen : IScreen
{
    /// <summary>[ORIGINAL] O3:669D–66E3: half height 15 + 5 per team with members (slots 1..players).</summary>
    public static (int Top, int Bottom) Box(int teams) => (100 - (5 * teams + 15), 5 * teams + 15 + 100);

    /// <summary>[ORIGINAL] O3:7469: without buttons the screen stays 5000 ms (Crt.Delay 1388h).</summary>
    public static readonly TimeSpan AutoContinueDelay = TimeSpan.FromMilliseconds(5000);

    /// <summary>[ORIGINAL] O3:6EC1, 7078, 7140: rank, name, score and wins of every line in colour 14.</summary>
    public const byte LineColor = 14;

    private const byte Face = MenuGraphics.FrameFace, Dark = MenuGraphics.FrameDark, Light = 15;

    private readonly ShopFonts _fonts;
    private readonly TeamRanking _ranking;
    private readonly int _gamesPlayed, _gamesTotal;
    private readonly bool _buttons;
    private readonly Func<ScoreChoice, int, ScoreTeamScreen, IScreen?> _choose;
    private readonly byte[] _dac;
    private readonly GraphText _text;
    private readonly int _top, _bottom;
    private TimeSpan _waited;
    private bool _waitingForRelease;
    private int _pendingLine;

    /// <param name="fonts">Fonts from BOMB.EXE.</param>
    /// <param name="screen">What is on the screen before (in the original the background of the single status screen).</param>
    /// <param name="dac">6-bit DAC of that screen (768 bytes); kept as it is.</param>
    /// <param name="teams">Teams in any order (<see cref="TeamStandings.Rank"/> sorts them).</param>
    /// <param name="gamesPlayed">Games played so far (DS:47E2).</param>
    /// <param name="gamesTotal">Games of the battle (DS:E8A6).</param>
    /// <param name="buttons">
    /// Buttons and input (DS:4812 = 0); without them the screen continues by itself after 5 s. Unlike the single status
    /// screen the flag is not cleared after the last game (O3:5AEF restores it on exit, O3:6574).
    /// </param>
    /// <param name="choose">
    /// Called with the choice and, for statistics, the position (1-based) in <see cref="TeamRanking.Order"/>; returns the
    /// next screen. The statistics of a team (O3:2C8B) are not part of this package.
    /// </param>
    public ScoreTeamScreen(ShopFonts fonts, Surface screen, ReadOnlySpan<byte> dac, IEnumerable<Team> teams,
        int gamesPlayed, int gamesTotal, bool buttons, Func<ScoreChoice, int, ScoreTeamScreen, IScreen?> choose)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(choose);
        if (screen.Width != 320 || screen.Height != 200)
            throw new ArgumentException("The team status screen draws into a 320 x 200 surface only.", nameof(screen));
        if (dac.Length != 768)
            throw new ArgumentException("The DAC has 768 entries.", nameof(dac));
        _fonts = fonts;
        _gamesPlayed = gamesPlayed;
        _gamesTotal = gamesTotal;
        _buttons = buttons;
        _choose = choose;
        _dac = dac.ToArray();
        _text = new GraphText(fonts.Default);
        _ranking = TeamStandings.Rank(teams);
        (_top, _bottom) = Box(_ranking.CountedTeams);
        screen.Pixels.CopyTo(Screen.Pixels, 0);
        DrawTable();
    }

    /// <summary>Teams in screen order with their ranks and the sorted slots.</summary>
    public TeamRanking Ranking => _ranking;

    /// <summary>Video memory of the screen.</summary>
    public Surface Screen { get; } = new();

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
            // [ORIGINAL] O3:739E–73B6: the statistics open after both buttons are released.
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
            throw new ArgumentException("The team status screen draws into a 320 x 200 surface only.", nameof(surface));
        Screen.Pixels.CopyTo(surface.Pixels, 0);
        MenuGraphics.ApplyDac(_dac, surface);
    }

    /// <summary>
    /// [ORIGINAL] O3:72A3–730C: digits 1..9 and 0 (= 10) open the statistics of that sorted slot if it is not above the
    /// number of players (DS:47DA) – an empty slot as well; otherwise nothing happens. S/s (set O3:6676) saves and
    /// continues, every other key continues. There is no "Add" on this screen.
    /// </summary>
    private ScoreChoice? ReadKey(ushort key, out int line)
    {
        line = 0;
        var character = (char)(key & 0xFF);
        if (character is >= '0' and <= '9')
        {
            line = character == '0' ? 10 : character - '0';
            return line <= _ranking.Players ? ScoreChoice.Statistics : null;
        }

        return character is 'S' or 's' ? ScoreChoice.Save : ScoreChoice.Continue;
    }

    /// <summary>
    /// [ORIGINAL] O3:732E–744E: a team line (45 &lt; x &lt; 275, top + 12 + 10i ≤ y &lt; top + 22 + 10i, i = 1..teams)
    /// waits for the release and opens its statistics; Continue/Exit at 85..135 and Save at 185..235, each between
    /// bottom + 5 and bottom + 15.
    /// </summary>
    private IScreen? Click(int x, int y)
    {
        if (x > 45 && x < 275)
        {
            for (var i = 1; i <= _ranking.CountedTeams; i++)
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
        if (onButtons && x is >= 85 and <= 135)
            return _choose(ScoreChoice.Continue, 0, this);
        if (onButtons && x is >= 185 and <= 235)
            return _choose(ScoreChoice.Save, 0, this);
        return this;
    }

    /// <summary>[ORIGINAL] O3:6CD5–7283 (BOMB.OVR 0x0F761): boxes, title, column heads, lines and buttons.</summary>
    private void DrawTable()
    {
        MenuGraphics.Frame(Screen, 45, _top, 275, _bottom, Face, Light, Dark);
        MenuGraphics.Frame(Screen, 69, _top - 20, 251, _top - 5, Face, Light, Dark);

        // Title: SmallFont size 1, user size 1/1, colour 6, centred at (160, top − 13); texts O3:6631–6640.
        _text.SetTextStyle(_fonts.Small, TextDirection.Horizontal, 1);
        _text.SetUserCharSize(1, 1, 1, 1);
        _text.Color = 6;
        _text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Center);
        _text.OutTextXY(Screen, 160, _top - 13, ScoreScreen.Title(_gamesPlayed, _gamesTotal));

        // [ORIGINAL] O3:6DD9–6E0F calls O10[1] for every player record (DAC 19 + n); the colours are not used here and
        // the DAC of the previous screen already holds them.

        // Head "Team" (O3:664F) at (50, top + 5), colour 17, left/top; line (50..270, top + 16) through UCC8:09E4.
        _text.Color = Dark;
        _text.SetTextJustify(HorizontalJustify.Left, VerticalJustify.Top);
        MenuGraphics.Line(Screen, 50, _top + 16, 270, _top + 16, Dark);
        _text.OutTextXY(Screen, 50, _top + 5, "Team");
        foreach (var line in _ranking.Lines)
        {
            _text.Color = LineColor;
            var y = _top + 10 + 10 * line.Position;
            _text.OutTextXY(Screen, 50, y, ScoreScreen.RankText(line.Rank));
            _text.OutTextXY(Screen, 72, y, line.Team.Name);
        }

        // "Score" right-aligned at 270 with Str(score:10:0), "Wins" at 185 with Str(average:5:2).
        _text.SetTextJustify(HorizontalJustify.Right, VerticalJustify.Top);
        _text.Color = Dark;
        _text.OutTextXY(Screen, 270, _top + 5, "Score");
        foreach (var line in _ranking.Lines)
        {
            _text.Color = LineColor;
            _text.OutTextXY(Screen, 270, _top + 10 + 10 * line.Position, ScoreText(line.Score));
        }

        _text.Color = Dark;
        _text.OutTextXY(Screen, 185, _top + 5, "Wins");
        foreach (var line in _ranking.Lines)
        {
            _text.Color = LineColor;
            _text.OutTextXY(Screen, 185, _top + 10 + 10 * line.Position, WinsText(line.WinsSum, line.Team.Members.Count));
        }

        if (!_buttons)
            return;

        // [ORIGINAL] O3:71B2–7283: buttons in colour 0, text centred; "Exit" after the last game.
        _text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Center);
        Button(85, 135, 110, _gamesPlayed == _gamesTotal ? "Exit" : "Continue");
        Button(185, 235, 210, "Save");
    }

    private void Button(int x1, int x2, int textX, string label)
    {
        MenuGraphics.Frame(Screen, x1, _bottom + 5, x2, _bottom + 15, Face, Light, Dark);
        _text.Color = 0;
        _text.OutTextXY(Screen, textX, _bottom + 9, label);
    }

    /// <summary>[ORIGINAL] O3:709B–70AC: Str(score:10:0), padded on the left to ten characters.</summary>
    public static string ScoreText(long score) => score.ToString(CultureInfo.InvariantCulture).PadLeft(10);

    /// <summary>
    /// [ORIGINAL] O3:7163–7175: Str(average:5:2), padded on the left to five characters. [APPROX] Rounding of exact
    /// halves (e.g. 1/8 = 0.125) away from zero; the Real48 conversion of the runtime (12CE:1BDE) was not traced.
    /// </summary>
    public static string WinsText(int winsSum, int members)
    {
        var value = Math.Round((decimal)winsSum / Math.Max(1, members), 2, MidpointRounding.AwayFromZero);
        return value.ToString("0.00", CultureInfo.InvariantCulture).PadLeft(5);
    }
}
